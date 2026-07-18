using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Core;
using RoyalSiege.Data;
using RoyalSiege.Juice;

namespace RoyalSiege.Units
{
    /// <summary>
    /// One enemy. Logic at 10 Hz (position on the tick), visuals interpolated per frame.
    /// Composition: Health + StatusController + AttackCycle + UnitAnimator.
    /// Movement is straight-line seek + soft SEPARATION steering (deterministic, no physics):
    /// neighbours inside each other's body radius push apart, so packs spread naturally and
    /// never overlap. Walk animation is driven by the ACTUAL velocity so feet never slide;
    /// rotation is smoothed in the view. Attack distance is measured to the structure's EDGE.
    /// </summary>
    public sealed class EnemyAgent : MonoBehaviour, ITickable, IEnemyTarget, IHealthReadout
    {
        private const float DeathDespawnSeconds = 1.5f;
        private const float RetargetHysteresis = 0.9f;  // switch only if the new target is >10% closer
        private const float SeparationSpring = 4f;       // push strength per unit of overlap
        private const float TurnSharpness = 8f;          // view rotation smoothing (1/s)
        // Structure-overlap resolve: search span (covers tower footprint + largest body) and
        // the fraction of attack range the standoff may consume — must stay < 1 so a clamped
        // unit is always still within reach of its target.
        private const float StructureSearchRadius = 4f;
        private const float StandoffReachFraction = 0.9f;

        private static readonly List<IStructureTarget> SplashBuffer = new();
        private static readonly List<IStructureTarget> StructureBuffer = new();

        private EnemyDefinitionSO _def;
        private EnemyRuntimeDeps _deps;
        private Health _health;
        private readonly StatusController _status = new();
        private AttackCycle _attack;
        private UnitAnimator _animator;
        private HitReaction _hitReaction;
        private EnemyLifecycleView _lifecycle;
        private EnemyThrowView _throwView;
        private AnimationEventRelay _throwEventRelay;
        private bool _impactPending;         // armed by AttackCycle, released by the anim event
        private float _impactPendingTimeout; // fallback: fire anyway if the event never arrives

        private IStructureTarget _target;
        private Vector3 _previousPosition;
        private Vector3 _logicPosition;
        private Vector3 _desiredForward = Vector3.forward;
        private bool _dead;
        private float _despawnTimer;

        private bool _isMoving;
        private float _hurtStaggerRemaining;
        private float _hurtCooldownRemaining;

        private float _slamTimer;
        private float _slamTelegraphRemaining;

        public int WaveIndex { get; private set; }
        public EnemyDefinitionSO Definition => _def;

        // ---- IEnemyTarget / IHealthReadout ----
        public bool IsAlive => !_dead;
        public Vector3 Position => _logicPosition;
        public float CurrentHp => _health?.Current ?? 0f;
        public float HpPct => _dead ? 0f : _health?.Pct ?? 0f;
        public bool IsBoss => _def != null && _def.isBoss;
        public float BodyRadius => _def != null ? _def.unitRadius : 0.45f;
        public void TakeDamage(float amount)
        {
            bool wasAlive = !_dead;
            _health?.TakeDamage(amount);
            // 18-Jul juice: every landed hit reports position+amount for the floating
            // damage numbers (killing blows styled bigger by the view).
            if (wasAlive && amount > 0f && _deps != null)
                _deps.Events.RaiseEnemyDamaged(_logicPosition, amount + _dotAccumulated, _dead);
            _dotAccumulated = 0f;
            if (_dead) return; // fatal hits skip the flash — death anim takes over
            _hitReaction?.Play();
            TryHurtStagger();
        }

        /// <summary>
        /// 18-Jul: Earthquake-style DoT ticks. Health drains normally but feedback is
        /// AGGREGATED — one damage number + one soft flash every ~0.9 s instead of a 10 Hz
        /// vibration (the old per-tick flinch made quaked enemies buzz for the full 4 s).
        /// No hurt-stagger from DoT at all — the slow is the readable effect.
        /// </summary>
        public void TakeDotDamage(float amount)
        {
            if (_dead || amount <= 0f) return;
            bool wasAlive = !_dead;
            _health?.TakeDamage(amount);
            _dotAccumulated += amount;
            _dotFlushTimer += 0f; // timer advances in Tick
            if (_dead)
            {
                // died to the DoT: flush the remainder as the killing-blow number
                if (wasAlive && _deps != null)
                    _deps.Events.RaiseEnemyDamaged(_logicPosition, _dotAccumulated, true);
                _dotAccumulated = 0f;
            }
        }

        private float _dotAccumulated;
        private float _dotFlushTimer;

        private void FlushDotFeedback(float dt)
        {
            if (_dotAccumulated <= 0f) { _dotFlushTimer = 0f; return; }
            _dotFlushTimer += dt;
            if (_dotFlushTimer < 0.9f) return;
            _dotFlushTimer = 0f;
            _deps.Events.RaiseEnemyDamaged(_logicPosition, _dotAccumulated, false);
            _hitReaction?.Play(); // one soft pulse per flush — not per tick
            _dotAccumulated = 0f;
        }

        /// <summary>
        /// Hit-stagger: a MOVING enemy briefly stops and plays the hurt flinch, then resumes.
        /// EVERY hit re-flinches (a hit landing mid-stagger restarts it) unless the per-enemy
        /// hurtCooldownSeconds says otherwise — the boss keeps a long cooldown so sustained
        /// fire can't flinch-lock it. Never fires while attacking or frozen/stunned.
        /// </summary>
        private void TryHurtStagger()
        {
            if (_def == null || _def.hurtStaggerSeconds <= 0f) return;
            if (!_isMoving && _hurtStaggerRemaining <= 0f) return; // moving, or already mid-flinch
            if (_status.IsBlocked) return;
            if (_hurtCooldownRemaining > 0f) return;

            _hurtStaggerRemaining = _def.hurtStaggerSeconds;
            _hurtCooldownRemaining = _def.hurtCooldownSeconds;
            _animator?.PlayHurt(_def.hurtStaggerSeconds);
        }
        public void ApplyFreeze(float seconds) => _status.ApplyFreeze(seconds);
        public void ApplyStun(float seconds) => _status.ApplyStun(seconds);
        public void ApplySlow(float strength, float seconds) => _status.ApplySlow(strength, seconds);

        /// <summary>
        /// v4 (Log/Fireball): planar shove scaled by the def's knockbackFactor (heavies 0.5,
        /// Ogre 0). Clamped to the map circle so a push can never shove a unit outside the
        /// playable area (where it would become untargetable).
        /// </summary>
        public void ApplyKnockback(Vector3 displacement)
        {
            if (_dead || _def == null || _def.knockbackFactor <= 0f) return;
            Vector3 pushed = _logicPosition + RangeMath.Flatten(displacement) * _def.knockbackFactor;
            Vector3 fromCenter = RangeMath.Flatten(pushed - _deps.MapCenter);
            float maxR = _def.engageRadiusFromCenter + 3f; // generous clamp; targeting map filter stays safe
            if (fromCenter.magnitude > maxR) pushed = _deps.MapCenter + fromCenter.normalized * maxR;
            _logicPosition = pushed;
        }

        public void Init(EnemyDefinitionSO def, Vector3 spawnPosition, int waveIndex, EnemyRuntimeDeps deps)
        {
            _def = def;
            _deps = deps;
            WaveIndex = waveIndex;

            _dead = false;
            _target = null;
            _status.Reset();
            _health = new Health(def.hp * deps.HpMultiplier);
            _health.Died += OnDied;
            _attack = new AttackCycle(def.attackRate, def.impactFraction, OnAttackImpact, OnAttackSwing);

            _logicPosition = spawnPosition;
            _previousPosition = spawnPosition;
            transform.position = spawnPosition;
            _desiredForward = RangeMath.PlanarDirection(spawnPosition, Vector3.zero);
            transform.rotation = Quaternion.LookRotation(_desiredForward);

            _slamTimer = def.slamInterval;
            _slamTelegraphRemaining = 0f;

            _isMoving = false;
            _hurtStaggerRemaining = 0f;
            _hurtCooldownRemaining = 0f;
            _dotAccumulated = 0f;
            _dotFlushTimer = 0f;

            if (_animator == null) _animator = GetComponent<UnitAnimator>();
            // Walk-anim speed = global GameConfig factor × this enemy's per-def scale.
            _animator?.ConfigureWalk(_deps.WalkAnimMultiplier * _def.walkAnimSpeedMultiplier);
            // Attack-anim speed = this enemy's per-def artistic factor (visual only).
            _animator?.ConfigureAttackAnim(_def.attackAnimSpeedMultiplier);
            // 18-Jul stylized look: outline BEFORE the lifecycle view exists — the lifecycle
            // caches sharedMaterials in ITS Awake and restores them on every ResetForSpawn,
            // so the outline slot must already be appended when that cache is taken.
            if (GetComponent<OutlineView>() == null) gameObject.AddComponent<OutlineView>();
            // Lifecycle view FIRST — HitReaction discovers it in Awake and routes its glow there.
            if (_lifecycle == null)
                _lifecycle = GetComponent<EnemyLifecycleView>() ?? gameObject.AddComponent<EnemyLifecycleView>();
            if (_hitReaction == null)
                _hitReaction = GetComponent<HitReaction>() ?? gameObject.AddComponent<HitReaction>();
            _hitReaction.Cancel();
            _lifecycle.ResetForSpawn();
            // Throwing enemies (Hellspawn): held-ammo visual in the hand. Enabled only when
            // this def actually fires a projectile — a melee def sharing the prefab stays empty.
            if (_throwView == null) _throwView = GetComponent<EnemyThrowView>();
            _throwView?.Init(_deps.Clock, _def.projectile != null);
            // Anim-event-released attacks (Hellspawns): the relay lives on the Animator's own
            // GameObject (Unity only delivers clip events there) and forwards the release
            // frame back to this agent. Plain assignment — pooled reuse can't double-subscribe.
            _impactPending = false;
            _throwEventRelay = null;
            if (_def.attackImpactOnAnimEvent)
            {
                var eventAnimator = GetComponentInChildren<Animator>(true);
                if (eventAnimator != null)
                {
                    _throwEventRelay = eventAnimator.GetComponent<AnimationEventRelay>()
                        ?? eventAnimator.gameObject.AddComponent<AnimationEventRelay>();
                    _throwEventRelay.ThrowReleased = OnThrowAnimEvent;
                }
            }
            _animator?.Rebind();
            // Deterministic walk-cycle phase from the spawn position — pack members animate
            // out of step with each other without introducing any RNG.
            float phase = Mathf.Abs(Mathf.Sin(spawnPosition.x * 12.9898f + spawnPosition.z * 78.233f));
            _animator?.SetWalkPhase(phase);

            _deps.Registry.Register(this);
            _deps.Ticker.Register(this);
        }

        public void Tick(float dt)
        {
            if (_dead)
            {
                _despawnTimer -= dt;
                if (_despawnTimer <= 0f) Despawn();
                return;
            }

            _status.Tick(dt);
            FlushDotFeedback(dt); // aggregated DoT number/flash (18-Jul)
            _lifecycle?.SetFrozen(_status.IsFrozen);
            if (_status.IsBlocked)
            {
                _previousPosition = _logicPosition;
                _isMoving = false;
                _animator?.SetMoving(false);
                _animator?.SetAttacking(false);
                return; // frozen/stunned: no movement, no attacks; still damageable
            }

            if (_hurtCooldownRemaining > 0f) _hurtCooldownRemaining -= dt;
            if (_hurtStaggerRemaining > 0f)
            {
                // Hit-stagger: hold position while the hurt flinch plays. The walk params
                // are deliberately left at their moving values — the Hurt state owns the
                // pose, and its exit crossfades straight back into the mid-stride walk.
                _hurtStaggerRemaining -= dt;
                _previousPosition = _logicPosition;
                _isMoving = false;
                _animator?.SetAttacking(false);
                return;
            }

            TickBossSlam(dt);
            AcquireTarget();

            _previousPosition = _logicPosition;

            if (_target == null)
            {
                _isMoving = false;
                _animator?.SetMoving(false);
                _animator?.SetAttacking(false);
                return;
            }

            float edgeDistance = RangeMath.PlanarDistance(_logicPosition, _target.Position) - _target.FootprintRadius;
            // 17-Jul rule: an enemy may only fight once it has actually ENTERED the territory
            // (inside engageRadiusFromCenter). Stops ranged units attacking from the outskirts.
            bool insideTerritory = RangeMath.IsInside(_deps.MapCenter, _logicPosition, _def.engageRadiusFromCenter);
            bool inRange = insideTerritory && edgeDistance <= _def.attackRange;
            _attack.Tick(dt, inRange);

            // Armed throw waiting for its release frame — if the animation event never comes
            // (culled/disabled animator), fire on the timeout so the attack is never lost.
            if (_impactPending)
            {
                _impactPendingTimeout -= dt;
                if (_impactPendingTimeout <= 0f)
                {
                    _impactPending = false;
                    ExecuteAttackImpact();
                }
            }

            Vector3 separation = ComputeSeparation();

            if (!inRange && !_attack.IsSwinging)
            {
                float speed = _def.moveSpeed * _status.MoveFactor; // v4: Earthquake slow
                Vector3 seek = RangeMath.PlanarDirection(_logicPosition, _target.Position) * speed;
                Vector3 velocity = seek + Vector3.ClampMagnitude(separation * SeparationSpring, speed);
                velocity = Vector3.ClampMagnitude(velocity, speed * 1.25f);

                _logicPosition += velocity * dt;
                _desiredForward = velocity.normalized;
                _isMoving = true;
                _animator?.SetMoving(true, velocity.magnitude);
                _animator?.SetAttacking(false);
            }
            else
            {
                // Attacking: hold position but still gently resolve overlaps so crowds
                // ring around the target instead of standing inside each other.
                Vector3 shuffle = Vector3.ClampMagnitude(separation * SeparationSpring * 0.5f, _def.moveSpeed * 0.4f);
                _logicPosition += shuffle * dt;
                _desiredForward = RangeMath.PlanarDirection(_logicPosition, _target.Position);
                _isMoving = false;
                _animator?.SetMoving(false);
                _animator?.SetAttacking(true);
            }

            ResolveStructureOverlap();
        }

        /// <summary>
        /// Hard rule: an enemy's centre never enters a structure's footprint. The 10 Hz seek
        /// step can overshoot into the tower and crowd separation shoves attackers straight
        /// through the mesh — this clamps them back onto a standoff ring. The ring sits at
        /// BodyRadius outside the footprint, capped just under the unit's attack reach so a
        /// fat unit (Ogre: body 0.85 > range 0.8) can still land its hits.
        /// </summary>
        private void ResolveStructureOverlap()
        {
            _deps.Registry.StructuresInRadius(_logicPosition, StructureSearchRadius, StructureBuffer);
            for (int i = 0; i < StructureBuffer.Count; i++)
            {
                var s = StructureBuffer[i];
                float standoff = s.FootprintRadius + Mathf.Min(BodyRadius, _def.attackRange * StandoffReachFraction);
                Vector3 center = RangeMath.Flatten(s.Position);
                Vector3 delta = RangeMath.Flatten(_logicPosition) - center;
                float distance = delta.magnitude;
                if (distance >= standoff) continue;

                Vector3 away = distance > 0.001f
                    ? delta / distance
                    : RangeMath.PlanarDirection(center, _previousPosition);
                if (away.sqrMagnitude < 0.001f) away = Vector3.forward; // fully degenerate: pick any stable side
                _logicPosition = center + away * standoff;
            }
        }

        private void Update()
        {
            if (_dead) return;
            transform.position = Vector3.Lerp(_previousPosition, _logicPosition, _deps.Clock.InterpolationAlpha);

            float viewDt = _deps.Clock.ScaledDeltaTime;
            if (_desiredForward.sqrMagnitude > 0.001f && viewDt > 0f)
            {
                var targetRot = Quaternion.LookRotation(_desiredForward);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                    1f - Mathf.Exp(-TurnSharpness * viewDt));
            }
            // Frozen/stunned units hold their pose — sells the ice block far better than idling.
            _animator?.SetPlaybackSpeed(_deps.Clock.IsPaused || _status.IsBlocked ? 0f : _deps.Clock.SpeedMultiplier);
        }

        /// <summary>Push away from overlapping neighbours (sum of overlap vectors).</summary>
        private Vector3 ComputeSeparation()
        {
            Vector3 push = Vector3.zero;
            var enemies = _deps.Registry.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var other = enemies[i];
                if (ReferenceEquals(other, this) || !other.IsAlive) continue;

                float minDistance = BodyRadius + other.BodyRadius;
                Vector3 delta = RangeMath.Flatten(_logicPosition - other.Position);
                float distance = delta.magnitude;
                if (distance >= minDistance) continue;

                // Coincident spawn safety: nudge along a deterministic tangent.
                Vector3 away = distance > 0.001f
                    ? delta / distance
                    : Vector3.Cross(Vector3.up, RangeMath.PlanarDirection(_logicPosition, Vector3.zero));
                push += away * (minDistance - distance);
            }
            return push;
        }

        private void AcquireTarget()
        {
            IStructureTarget best = _def.targetPriority == TargetPriority.BuildingsFirst
                ? _deps.Registry.ClosestBuilding(_logicPosition) ?? _deps.Registry.ClosestStructure(_logicPosition)
                : _deps.Registry.ClosestStructure(_logicPosition);

            if (_target == null || !_target.IsAlive)
            {
                _target = best;
                return;
            }

            if (best != null && !ReferenceEquals(best, _target))
            {
                float currentDist = RangeMath.PlanarDistance(_logicPosition, _target.Position);
                float bestDist = RangeMath.PlanarDistance(_logicPosition, best.Position);
                if (bestDist < currentDist * RetargetHysteresis) _target = best;
            }
        }

        private void OnAttackSwing() => _animator?.PlayAttack(_def.attackRate);

        private void OnAttackImpact()
        {
            // Anim-event mode: the 10 Hz cycle stays the authority on WHEN an attack is due,
            // but the launch itself waits for the clip's release frame (AnimEvent_Throw) so
            // the projectile leaves the hand exactly on the throw pose. The timeout fires the
            // hit anyway if the event never arrives (animator culled/disabled) — never drop DPS.
            if (_def.attackImpactOnAnimEvent && _throwEventRelay != null)
            {
                // A previous swing's armed throw that never got its release frame (event/arm
                // race, state crossfade swallowing the event) is fired NOW rather than being
                // silently overwritten — an armed attack must never be dropped (DPS guarantee).
                if (_impactPending) ExecuteAttackImpact();
                _impactPending = true;
                _impactPendingTimeout = _def.attackRate * 2f;
                return;
            }
            ExecuteAttackImpact();
        }

        /// <summary>Release frame of the throw clip (relayed AnimationEvent).</summary>
        private void OnThrowAnimEvent()
        {
            if (!_impactPending || _dead || _status.IsBlocked) return;
            _impactPending = false;
            ExecuteAttackImpact();
        }

        private void ExecuteAttackImpact()
        {
            if (_dead || _target == null || !_target.IsAlive) return;

            float damage = _def.damage * _deps.DamageMultiplier;
            if (_def.projectile != null)
            {
                // Launch from the held object in the hand (visual origin); the throw view then
                // hides it and respawns a fresh one. Subtract the settings' spawn-height offset
                // so the launcher re-adds it back to exactly the hand point.
                Vector3 from = _logicPosition;
                if (_throwView != null)
                {
                    Vector3 hand = _throwView.ThrowPoint;
                    from = new Vector3(hand.x, hand.y - _def.projectile.spawnHeightOffset, hand.z);
                    _throwView.OnThrow();
                }
                _deps.Launcher.Fire(from, _target, damage, _def.projectile, OnProjectileImpact);
            }
            else
            {
                _target.TakeDamage(damage);
                _deps.Events.RaiseEnemyAttackImpact(_def, _logicPosition); // weapon-hit SFX
            }
        }

        /// <summary>Mage-style splash: full damage to OTHER structures near the impact.</summary>
        private void OnProjectileImpact(Vector3 point, IDamageable primary)
        {
            _deps.Events.RaiseEnemyAttackImpact(_def, point); // weapon-hit SFX at the landing
            if (_def.splashRadius <= 0f) return;
            float damage = _def.damage * _deps.DamageMultiplier;
            _deps.Registry.StructuresInRadius(point, _def.splashRadius, SplashBuffer);
            for (int i = 0; i < SplashBuffer.Count; i++)
                if (!ReferenceEquals(SplashBuffer[i], primary))
                    SplashBuffer[i].TakeDamage(damage);
        }

        private void TickBossSlam(float dt)
        {
            if (!_def.isBoss || _def.slamInterval <= 0f) return;

            if (_slamTelegraphRemaining > 0f)
            {
                _slamTelegraphRemaining -= dt;
                if (_slamTelegraphRemaining <= 0f)
                {
                    _deps.Registry.StructuresInRadius(_logicPosition, _def.slamRadius, SplashBuffer);
                    float damage = _def.slamDamage * _deps.DamageMultiplier;
                    for (int i = 0; i < SplashBuffer.Count; i++)
                        SplashBuffer[i].TakeDamage(damage);
                    _deps.Events.RaiseBossSlammed(_logicPosition);
                }
                return;
            }

            _slamTimer -= dt;
            if (_slamTimer <= 0f)
            {
                _slamTimer = _def.slamInterval;
                _slamTelegraphRemaining = _def.slamTelegraphSeconds;
            }
        }

        private void OnDied()
        {
            if (_dead) return;
            _dead = true;
            _despawnTimer = DeathDespawnSeconds;
            _hitReaction?.Cancel();
            _lifecycle?.BeginDeath(DeathDespawnSeconds);

            // Unregister IMMEDIATELY: no targeting, no double bounty (GDD ruling).
            _deps.Registry.Unregister(this);
            _deps.Events.RaiseEnemyKilled(new EnemyKilledArgs(
                _def, WaveIndex, _def.bounty * _deps.BountyMultiplier, _logicPosition));
            // Update() stops driving playback speed once dead — restore it here so a unit
            // killed while frozen/stunned (speed parked at 0) still plays its death anim.
            _animator?.SetPlaybackSpeed(_deps.Clock.IsPaused ? 0f : _deps.Clock.SpeedMultiplier);
            _animator?.PlayDie();
        }

        private void Despawn()
        {
            _deps.Ticker.Unregister(this);
            _health.Died -= OnDied;
            _deps.Release(this);
        }
    }
}

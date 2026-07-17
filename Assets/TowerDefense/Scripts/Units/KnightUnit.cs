using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Core;
using RoyalSiege.Data;
using RoyalSiege.Juice;

namespace RoyalSiege.Units
{
    /// <summary>
    /// v4 Knights (13_PROGRESSION_V4 §5.3, 17-Jul user delta): GUARDS of the Royal Tower's
    /// circle. A knight targets enemies whose center is INSIDE the guard radius (= the white
    /// deployment circle, same reach as the tower) AND always retaliates against any enemy
    /// already within its own melee reach (18-Jul fix: an attacker poking the knight from
    /// just OUTSIDE the ring no longer gets a free hit). It never steps outside the circle to
    /// chase — retaliation is struck in place. Fights at melee edge-distance, body-blocks —
    /// enemies treat knights as targets because a knight registers as an IStructureTarget
    /// (IsBuilding = false, so the Ogre still prefers buildings). Never decays, persists
    /// across waves, dies only to damage. Logic on the 10 Hz tick, visuals interpolated.
    /// </summary>
    public sealed class KnightUnit : MonoBehaviour, ITickable, IStructureTarget, IHealthReadout
    {
        private const float RetargetHysteresis = 0.9f;
        private const float TurnSharpness = 10f;
        private const float SeparationSpring = 4f;
        private const float SpawnPopSeconds = 0.22f;
        private const float LungePunch = 0.28f;
        private const float DeathDespawnSeconds = 1.6f; // hold the corpse so the death anim plays before pooling

        private TroopCardSO _card;
        private KnightRuntimeDeps _deps;
        private Health _health;
        private AttackCycle _attack;
        private HitReaction _hitReaction;
        private UnitAnimator _animator;

        private IEnemyTarget _target;
        private Vector3 _logicPosition;
        private Vector3 _previousPosition;
        private Vector3 _desiredForward = Vector3.forward;
        private float _spawnPopT = 1f;
        private float _lungeT = 1f;
        private Vector3 _baseScale = Vector3.one;
        private bool _dead;
        private float _despawnTimer;

        public float HpPct => _health?.Pct ?? 0f;

        // ---- IStructureTarget ----
        public bool IsAlive => !_dead && _health != null && _health.IsAlive;
        public Vector3 Position => _logicPosition;
        public bool IsBuilding => false; // Ogre (BuildingsFirst) keeps preferring buildings/towers
        public bool BlocksPlacement => false; // mobile — never blocks building placement
        public float FootprintRadius => _card != null ? _card.unitRadius : 0.4f;
        public void TakeDamage(float amount)
        {
            _health?.TakeDamage(amount);
            if (!_dead) _hitReaction?.Play();
        }

        public void Init(TroopCardSO card, Vector3 spawnPosition, KnightRuntimeDeps deps)
        {
            _card = card;
            _deps = deps;
            _dead = false;
            _target = null;

            _health = new Health(card.hp);
            _health.Died += OnDied;
            _attack = new AttackCycle(card.attackRate, card.impactFraction, OnAttackImpact, OnAttackSwing);

            _logicPosition = spawnPosition;
            _previousPosition = spawnPosition;
            transform.position = spawnPosition;
            _desiredForward = RangeMath.PlanarDirection(deps.MapCenter, spawnPosition);
            transform.rotation = Quaternion.LookRotation(_desiredForward);
            _baseScale = transform.localScale;
            _spawnPopT = 0f;

            if (_hitReaction == null)
                _hitReaction = GetComponent<HitReaction>() ?? gameObject.AddComponent<HitReaction>();
            _hitReaction.Cancel();
            if (_animator == null) _animator = GetComponent<UnitAnimator>();
            _animator?.Rebind();
            _despawnTimer = 0f;

            _deps.Registry.Register(this);
            _deps.Ticker.Register(this);
        }

        public void Tick(float dt)
        {
            if (_dead)
            {
                // Dead: keep ticking only to hold the corpse while the death anim plays.
                _despawnTimer -= dt;
                if (_despawnTimer <= 0f) Despawn();
                return;
            }

            AcquireTarget();
            _previousPosition = _logicPosition;

            if (_target == null)
            {
                _attack.Tick(dt, false);
                _animator?.SetMoving(false);
            }
            else
            {
                float edgeDistance = RangeMath.PlanarDistance(_logicPosition, _target.Position) - _target.BodyRadius;
                bool inRange = edgeDistance <= _card.attackRange;
                _attack.Tick(dt, inRange);

                if (!inRange && !_attack.IsSwinging)
                {
                    Vector3 seekDir = SteerAroundStructures(
                        RangeMath.PlanarDirection(_logicPosition, _target.Position), _target.Position);
                    Vector3 seek = seekDir * _card.moveSpeed;
                    Vector3 velocity = Vector3.ClampMagnitude(seek + ComputeSeparation() * SeparationSpring, _card.moveSpeed * 1.2f);
                    _logicPosition += velocity * dt;
                    _desiredForward = velocity.sqrMagnitude > 0.001f ? velocity.normalized : _desiredForward;
                    _animator?.SetMoving(true, velocity.magnitude);
                }
                else
                {
                    _desiredForward = RangeMath.PlanarDirection(_logicPosition, _target.Position);
                    _animator?.SetMoving(false);
                }
            }

            // ALWAYS enforced — target or not (an idle knight can still be shoved/deployed badly):
            // hard leash to the guard circle, then solid-structure ejection.
            Vector3 fromCenter = RangeMath.Flatten(_logicPosition - _deps.MapCenter);
            if (fromCenter.magnitude > _deps.GuardRadius)
                _logicPosition = _deps.MapCenter + fromCenter.normalized * _deps.GuardRadius;

            ResolveStructureOverlap();
        }

        private static readonly System.Collections.Generic.List<IStructureTarget> StructureBuffer = new();

        /// <summary>
        /// Knights can't walk through the Royal Tower or buildings (same rule as enemies):
        /// after each movement tick, clamp the centre onto a standoff ring outside every
        /// solid structure's footprint. Fellow knights are skipped (separation handles them).
        /// </summary>
        private void ResolveStructureOverlap()
        {
            _deps.Registry.StructuresInRadius(_logicPosition, 3.5f, StructureBuffer);
            for (int i = 0; i < StructureBuffer.Count; i++)
            {
                var s = StructureBuffer[i];
                if (ReferenceEquals(s, this) || !s.BlocksPlacement) continue; // solid structures only
                float standoff = s.FootprintRadius + FootprintRadius;
                Vector3 center = RangeMath.Flatten(s.Position);
                Vector3 delta = RangeMath.Flatten(_logicPosition) - center;
                float distance = delta.magnitude;
                if (distance >= standoff) continue;

                Vector3 away = distance > 0.001f
                    ? delta / distance
                    : RangeMath.PlanarDirection(center, _previousPosition);
                if (away.sqrMagnitude < 0.001f) away = Vector3.forward;
                _logicPosition = center + away * standoff;
            }
        }

        /// <summary>
        /// Steer AROUND solid structures instead of grinding into them: if the straight line to
        /// the target passes through the Royal Tower or a building, slide along the tangent on
        /// the side that makes progress toward the target (deterministic geometry, no navmesh).
        /// Fixes knights pressing into the tower when their target sits on the far side (18-Jul).
        /// </summary>
        private Vector3 SteerAroundStructures(Vector3 desiredDir, Vector3 targetPos)
        {
            Vector3 toTarget = RangeMath.Flatten(targetPos - _logicPosition);
            float targetDist = toTarget.magnitude;
            if (targetDist < 0.001f) return desiredDir;
            Vector3 dirToTarget = toTarget / targetDist;

            _deps.Registry.StructuresInRadius(_logicPosition, 4f, StructureBuffer);
            for (int i = 0; i < StructureBuffer.Count; i++)
            {
                var s = StructureBuffer[i];
                if (ReferenceEquals(s, this) || !s.BlocksPlacement || !s.IsAlive) continue;

                Vector3 toObstacle = RangeMath.Flatten(s.Position - _logicPosition);
                float along = Vector3.Dot(toObstacle, dirToTarget);
                if (along <= 0f || along >= targetDist) continue;   // obstacle behind us, or target is nearer than it

                float clearance = s.FootprintRadius + FootprintRadius + 0.35f;
                Vector3 perp = toObstacle - dirToTarget * along;
                if (perp.magnitude >= clearance) continue;          // the straight path already clears the obstacle

                // Blocked: head along the tangent (perpendicular to the obstacle line) on the
                // side that still makes progress toward the target — the knight circles it.
                Vector3 n = toObstacle.sqrMagnitude > 1e-4f ? toObstacle.normalized : dirToTarget;
                Vector3 tangent = new Vector3(-n.z, 0f, n.x);
                if (Vector3.Dot(tangent, dirToTarget) < 0f) tangent = -tangent;
                return tangent;
            }
            return desiredDir;
        }

        /// <summary>
        /// Nearest valid enemy (see IsValidPrey): inside the guard circle, OR — new 18-Jul —
        /// any enemy already in melee reach, so a knight always hits back at an attacker poking
        /// it from just outside the ring. A target that is neither is dropped.
        /// </summary>
        private void AcquireTarget()
        {
            if (_target != null && !IsValidPrey(_target)) _target = null;

            if (_target != null && _target.IsAlive)
            {
                // Hysteresis: only switch when something is >10% closer.
                var best = ClosestEnemy();
                if (best != null && !ReferenceEquals(best, _target))
                {
                    float current = RangeMath.PlanarDistance(_logicPosition, _target.Position);
                    float candidate = RangeMath.PlanarDistance(_logicPosition, best.Position);
                    if (candidate < current * RetargetHysteresis) _target = best;
                }
                return;
            }
            _target = ClosestEnemy();
        }

        private IEnemyTarget ClosestEnemy()
        {
            IEnemyTarget best = null;
            float bestDist = float.MaxValue;
            var enemies = _deps.Registry.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (!IsValidPrey(e)) continue;
                float d = RangeMath.PlanarDistance(_logicPosition, e.Position);
                if (d < bestDist) { best = e; bestDist = d; }
            }
            return best;
        }

        /// <summary>
        /// Valid prey for a guarding knight: any live enemy INSIDE the guard circle (chased
        /// within the ring) OR any enemy already within the knight's melee reach — so an
        /// attacker just OUTSIDE the ring is hit back instead of poking the knight for free
        /// (18-Jul trap fix). Retaliation is struck in place; the leash is untouched, and
        /// because the knight's footprint (unitRadius) is smaller than its attackRange, an
        /// enemy pinned at its standoff is always within reach.
        /// </summary>
        private bool IsValidPrey(IEnemyTarget e)
        {
            if (e == null || !e.IsAlive) return false;
            if (RangeMath.IsInside(_deps.MapCenter, e.Position, _deps.GuardRadius)) return true;
            return RangeMath.PlanarDistance(_logicPosition, e.Position) - e.BodyRadius <= _card.attackRange;
        }

        /// <summary>Gentle push off fellow knights so a pair doesn't stack on one enemy.</summary>
        private Vector3 ComputeSeparation()
        {
            Vector3 push = Vector3.zero;
            var structures = _deps.Registry.Structures;
            for (int i = 0; i < structures.Count; i++)
            {
                if (structures[i] is not KnightUnit other || ReferenceEquals(other, this) || !other.IsAlive) continue;
                float minDistance = FootprintRadius + other.FootprintRadius;
                Vector3 delta = RangeMath.Flatten(_logicPosition - other.Position);
                float distance = delta.magnitude;
                if (distance >= minDistance) continue;
                push += (distance > 0.001f ? delta / distance : Vector3.right) * (minDistance - distance);
            }
            return push;
        }

        private void OnAttackSwing()
        {
            _lungeT = 0f;
            _animator?.PlayAttack(_card.attackRate);
        }

        private void OnAttackImpact()
        {
            if (_dead || _target == null || !_target.IsAlive) return;
            float edge = RangeMath.PlanarDistance(_logicPosition, _target.Position) - _target.BodyRadius;
            if (edge > _card.attackRange + 0.3f) return; // target slipped away mid-swing
            _target.TakeDamage(_card.damage);
            _deps.Events.RaiseKnightStruck(_target.Position);
        }

        private void Update()
        {
            // Visuals follow the game clock (pause/speed) — also drives the death anim while dead.
            _animator?.SetPlaybackSpeed(_deps.Clock.IsPaused ? 0f : _deps.Clock.SpeedMultiplier);
            if (_dead) return;
            Vector3 view = Vector3.Lerp(_previousPosition, _logicPosition, _deps.Clock.InterpolationAlpha);

            // Attack lunge: a quick forward punch that settles back — sells the hit without an animator.
            if (_lungeT < 1f)
            {
                _lungeT = Mathf.Min(1f, _lungeT + _deps.Clock.ScaledDeltaTime / 0.24f);
                float punch = Mathf.Sin(_lungeT * Mathf.PI) * LungePunch;
                view += _desiredForward * punch;
            }
            transform.position = view;

            // Spawn pop: 0 → overshoot → settle (ease-out-back).
            if (_spawnPopT < 1f)
            {
                _spawnPopT = Mathf.Min(1f, _spawnPopT + Time.deltaTime / SpawnPopSeconds);
                float e = EaseOutBack(_spawnPopT);
                transform.localScale = _baseScale * Mathf.Max(0.05f, e);
            }

            if (_desiredForward.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(_desiredForward),
                    1f - Mathf.Exp(-TurnSharpness * Time.deltaTime));
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private void OnDied()
        {
            if (_dead) return;
            _dead = true;
            _despawnTimer = DeathDespawnSeconds;
            // Drop out of targeting/guarding at once, but stay ticking so the death anim plays
            // before the pool reclaims the body (Despawn does the final unregister + release).
            _deps.Registry.Unregister(this);
            _hitReaction?.Cancel();
            _deps.Events.RaiseKnightDied(_logicPosition);
            _animator?.PlayDie();
        }

        private void Despawn()
        {
            _deps.Ticker.Unregister(this);
            _health.Died -= OnDied;
            _deps.Release(this);
        }
    }

    /// <summary>Constructor-injected services for knights (mirror of EnemyRuntimeDeps).</summary>
    public sealed class KnightRuntimeDeps
    {
        public ITargetRegistry Registry;
        public ITicker Ticker;
        public IClock Clock;
        public GameEvents Events;
        public Vector3 MapCenter;
        /// <summary>Knights guard THIS circle (= the white deployment ring / tower reach) — never leave it, never target outside it.</summary>
        public float GuardRadius = 5f;
        public System.Action<KnightUnit> Release;
    }
}

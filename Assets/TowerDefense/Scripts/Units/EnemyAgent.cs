using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Core;
using RoyalSiege.Data;

namespace RoyalSiege.Units
{
    /// <summary>
    /// One enemy. Logic at 10 Hz (position on the tick), visuals interpolated per frame.
    /// Composition: Health + StatusController + AttackCycle + UnitAnimator.
    /// Targeting: per-definition priority with 10% retarget hysteresis (GDD ruling).
    /// Attack distance is measured to the structure's EDGE (see 02_ARCHITECTURE).
    /// </summary>
    public sealed class EnemyAgent : MonoBehaviour, ITickable, IEnemyTarget, IHealthReadout
    {
        private const float DeathDespawnSeconds = 1.5f;
        private const float RetargetHysteresis = 0.9f; // switch only if the new target is >10% closer

        private static readonly List<IStructureTarget> SplashBuffer = new();

        private EnemyDefinitionSO _def;
        private EnemyRuntimeDeps _deps;
        private Health _health;
        private readonly StatusController _status = new();
        private AttackCycle _attack;
        private UnitAnimator _animator;

        private IStructureTarget _target;
        private Vector3 _previousPosition;
        private Vector3 _logicPosition;
        private bool _dead;
        private float _despawnTimer;

        private float _slamTimer;
        private float _slamTelegraphRemaining;

        public int WaveIndex { get; private set; }
        public EnemyDefinitionSO Definition => _def;

        // ---- IEnemyTarget ----
        public bool IsAlive => !_dead;
        public Vector3 Position => _logicPosition;
        public float CurrentHp => _health?.Current ?? 0f;
        public float HpPct => _dead ? 0f : _health?.Pct ?? 0f;
        public bool IsBoss => _def != null && _def.isBoss;
        public void TakeDamage(float amount) => _health?.TakeDamage(amount);
        public void ApplyFreeze(float seconds) => _status.ApplyFreeze(seconds);
        public void ApplyStun(float seconds) => _status.ApplyStun(seconds);

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

            _slamTimer = def.slamInterval;
            _slamTelegraphRemaining = 0f;

            if (_animator == null) _animator = GetComponent<UnitAnimator>();
            _animator?.Rebind();

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
            if (_status.IsBlocked)
            {
                _previousPosition = _logicPosition;
                _animator?.SetMoving(false);
                return; // frozen/stunned: no movement, no attacks; still damageable
            }

            TickBossSlam(dt);
            AcquireTarget();

            if (_target == null)
            {
                _previousPosition = _logicPosition;
                _animator?.SetMoving(false);
                return;
            }

            float edgeDistance = RangeMath.PlanarDistance(_logicPosition, _target.Position) - _target.FootprintRadius;
            bool inRange = edgeDistance <= _def.attackRange;
            _attack.Tick(dt, inRange);

            if (!inRange && !_attack.IsSwinging)
            {
                Vector3 direction = RangeMath.PlanarDirection(_logicPosition, _target.Position);
                _previousPosition = _logicPosition;
                _logicPosition += direction * (_def.moveSpeed * dt);
                transform.rotation = Quaternion.LookRotation(direction);
                _animator?.SetMoving(true, _def.moveSpeed);
            }
            else
            {
                _previousPosition = _logicPosition;
                _animator?.SetMoving(false);
                Vector3 face = RangeMath.PlanarDirection(_logicPosition, _target.Position);
                transform.rotation = Quaternion.LookRotation(face);
            }
        }

        private void Update()
        {
            if (_dead) return;
            transform.position = Vector3.Lerp(_previousPosition, _logicPosition, _deps.Clock.InterpolationAlpha);
            _animator?.SetPlaybackSpeed(_deps.Clock.IsPaused ? 0f : _deps.Clock.SpeedMultiplier);
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
            if (_dead || _target == null || !_target.IsAlive) return;

            float damage = _def.damage * _deps.DamageMultiplier;
            if (_def.projectile != null)
                _deps.Launcher.Fire(_logicPosition, _target, damage, _def.projectile, OnProjectileImpact);
            else
                _target.TakeDamage(damage);
        }

        /// <summary>Mage-style splash: full damage to OTHER structures near the impact.</summary>
        private void OnProjectileImpact(Vector3 point, IDamageable primary)
        {
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

            // Unregister IMMEDIATELY: no targeting, no double bounty (GDD ruling).
            _deps.Registry.Unregister(this);
            _deps.Events.RaiseEnemyKilled(new EnemyKilledArgs(
                _def, WaveIndex, _def.bounty * _deps.BountyMultiplier, _logicPosition));
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

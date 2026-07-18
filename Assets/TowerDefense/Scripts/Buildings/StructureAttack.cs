using System;
using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Core;
using RoyalSiege.Data;

namespace RoyalSiege.Buildings
{
    /// <summary>
    /// One complete attack unit (targeter + cycle + delivery), reused by buildings and by
    /// BOTH Royal Tower attacks. Delivery is projectile (via the shared launcher) or
    /// instant (Tesla) — decided purely by whether a ProjectileSettingsSO is assigned.
    /// Visual hooks: onSwing (windup started), onFire (shot actually left), firePoint
    /// (muzzle position override) — all optional, all view-only.
    /// </summary>
    public sealed class StructureAttack
    {
        private readonly RangeTargeter _targeter;
        private readonly AttackCycle _cycle;
        private readonly IProjectileLauncher _launcher;
        private readonly GameEvents _events;
        private readonly float _damage;
        private readonly float _range;
        private readonly ProjectileSettingsSO _projectile;
        private readonly Action _onFire;
        private readonly Func<Vector3> _firePoint;

        private Vector3 _position;

        public IEnemyTarget CurrentTarget => _targeter.Current;

        /// <param name="onSwing">Optional anim hook, called with the attack period at swing start.</param>
        /// <param name="onFire">Optional hook fired when a shot actually goes out (recoil, flashes).</param>
        /// <param name="firePoint">Optional world-space muzzle override for projectile spawns.</param>
        public StructureAttack(ITargetQuery query, IProjectileLauncher launcher, GameEvents events,
            float damage, float attackRate, float range, float impactFraction,
            ProjectileSettingsSO projectile, float retargetDelay = 0f, Action<float> onSwing = null,
            Action onFire = null, Func<Vector3> firePoint = null)
        {
            _launcher = launcher;
            _events = events;
            _damage = damage;
            _range = range;
            _projectile = projectile;
            _onFire = onFire;
            _firePoint = firePoint;
            _targeter = new RangeTargeter(query, range, retargetDelay);
            _cycle = new AttackCycle(attackRate, impactFraction, OnImpact,
                onSwing == null ? null : () => onSwing(attackRate));
        }

        public void Tick(float dt, Vector3 position)
        {
            _position = position;
            _targeter.Tick(dt, position);
            _cycle.Tick(dt, _targeter.HasTarget);
        }

        private void OnImpact()
        {
            var target = _targeter.Current;
            // Strict-range re-check at the moment of firing (target may have just left).
            if (target == null || !target.IsAlive || !RangeMath.IsInside(_position, target.Position, _range))
                return;

            Vector3 origin = _firePoint != null ? _firePoint() : _position;

            if (_projectile != null)
            {
                // Clamp the flight to this attack's radius: a target that leaves the ring
                // mid-flight can't pull the shot beyond it (QA 17-Jul DT-002).
                _launcher.Fire(origin, target, _damage, _projectile,
                    rangeOrigin: _position, maxRange: _range);
            }
            else
            {
                // A firePoint override IS the muzzle (Tesla coil top) — no extra height fudge.
                Vector3 zapFrom = _firePoint != null ? origin : origin + Vector3.up;
                _events.RaiseInstantShotFired(zapFrom, target);
                target.TakeDamage(_damage);
            }
            _onFire?.Invoke();
        }
    }
}

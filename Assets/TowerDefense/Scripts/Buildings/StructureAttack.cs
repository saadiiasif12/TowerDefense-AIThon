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

        private Vector3 _position;

        public IEnemyTarget CurrentTarget => _targeter.Current;

        /// <param name="onSwing">Optional anim hook, called with the attack period at swing start.</param>
        public StructureAttack(ITargetQuery query, IProjectileLauncher launcher, GameEvents events,
            float damage, float attackRate, float range, float impactFraction,
            ProjectileSettingsSO projectile, float retargetDelay = 0f, Action<float> onSwing = null)
        {
            _launcher = launcher;
            _events = events;
            _damage = damage;
            _range = range;
            _projectile = projectile;
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

            if (_projectile != null)
            {
                _launcher.Fire(_position, target, _damage, _projectile);
            }
            else
            {
                _events.RaiseInstantShotFired(_position + Vector3.up, target.Position + Vector3.up * 0.5f);
                target.TakeDamage(_damage);
            }
        }
    }
}

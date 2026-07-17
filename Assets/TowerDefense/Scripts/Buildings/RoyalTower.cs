using System;
using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Core;
using RoyalSiege.Data;

namespace RoyalSiege.Buildings
{
    /// <summary>
    /// The Royal Tower: Health + TWO StructureAttacks (king arrows + built-in cannon),
    /// both from GameConfigSO. Combined DPS (~93) must never solo waves — idle player
    /// dies at wave 4 (verified invariant).
    /// </summary>
    public sealed class RoyalTower : MonoBehaviour, ITickable, IStructureTarget, IHealthReadout
    {
        private GameConfigSO _config;
        private Health _health;
        private StructureAttack _kingAttack;
        private StructureAttack _cannonAttack;
        private GameEvents _events;

        public float HpPct => _health?.Pct ?? 0f;
        public float CurrentHp => _health?.Current ?? 0f;

        // ---- IStructureTarget ----
        public bool IsAlive => _health != null && _health.IsAlive;
        public Vector3 Position => transform.position;
        public bool IsBuilding => false;
        public float FootprintRadius => _config != null ? _config.towerFootprintRadius : 1.5f;
        public void TakeDamage(float amount) => _health?.TakeDamage(amount);

        public void Init(GameConfigSO config, ITargetRegistry registry, IProjectileLauncher launcher,
            GameEvents events, ITicker ticker, IClock clock)
        {
            _config = config;
            _events = events;

            _health = new Health(config.towerHp);
            _health.Damaged += (current, max) => _events.RaiseTowerDamaged(current, max);

            // The visible king on top mirrors the king attack (view-only).
            var kingView = GetComponentInChildren<KingView>();
            var king = config.kingAttack;
            _kingAttack = new StructureAttack(registry, launcher, events,
                king.damage, king.attackRate, king.range, king.impactFraction, king.projectile,
                onSwing: period => kingView?.OnSwing(period),
                // Bolt leaves from the king's casting hand, read at the exact fire moment
                // (impactFraction lands on the hand-extended release pose of the throw anim).
                firePoint: kingView == null ? (Func<Vector3>)null : () => kingView.CastPoint);
            kingView?.Init(clock, () => _kingAttack.CurrentTarget);

            _cannonAttack = CreateAttack(config.builtInCannon, registry, launcher, events);

            registry.Register(this);
            ticker.Register(this);
        }

        private static StructureAttack CreateAttack(AttackStats stats, ITargetRegistry registry,
            IProjectileLauncher launcher, GameEvents events)
        {
            return new StructureAttack(registry, launcher, events,
                stats.damage, stats.attackRate, stats.range, stats.impactFraction, stats.projectile);
        }

        public void Tick(float dt)
        {
            if (!IsAlive) return;
            _kingAttack.Tick(dt, transform.position);
            _cannonAttack.Tick(dt, transform.position);
        }
    }
}

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

        /// <summary>TEST hook: the test range silences the tower with this to isolate other
        /// systems (e.g. watch Tesla alone). Always true in the real game — nothing sets it.</summary>
        public bool AttacksEnabled { get; set; } = true;

        // ---- IStructureTarget ----
        public bool IsAlive => _health != null && _health.IsAlive;
        public Vector3 Position => transform.position;
        public bool IsBuilding => false;
        public bool BlocksPlacement => true;
        public float FootprintRadius => _config != null ? _config.towerFootprintRadius : 1.5f;
        public void TakeDamage(float amount) => _health?.TakeDamage(amount);

        private ITargetRegistry _registry;
        private IProjectileLauncher _launcher;
        private KingView _kingView;

        /// <summary>Current 1-based tower level (v4 §7).</summary>
        public int Level { get; private set; } = 1;

        public void Init(GameConfigSO config, ITargetRegistry registry, IProjectileLauncher launcher,
            GameEvents events, ITicker ticker, IClock clock)
        {
            _config = config;
            _events = events;
            _registry = registry;
            _launcher = launcher;

            _health = new Health(config.towerHp);
            _health.Damaged += OnHealthDamaged;

            // The visible king on top mirrors the king attack (view-only).
            _kingView = GetComponentInChildren<KingView>();
            BuildKingAttack(config.kingAttack.damage);
            _kingView?.Init(clock, () => _kingAttack.CurrentTarget);

            _cannonAttack = CreateAttack(config.builtInCannon, registry, launcher, events);

            registry.Register(this);
            ticker.Register(this);
        }

        private void OnHealthDamaged(float current, float max) => _events.RaiseTowerDamaged(current, max);

        private void BuildKingAttack(float damage)
        {
            var king = _config.kingAttack;
            _kingAttack = new StructureAttack(_registry, _launcher, _events,
                damage, king.attackRate, king.range, king.impactFraction, king.projectile,
                onSwing: period => _kingView?.OnSwing(period),
                // Bolt leaves from the king's casting hand, read at the exact fire moment
                // (impactFraction lands on the hand-extended release pose of the throw anim).
                firePoint: _kingView == null ? (Func<Vector3>)null : () => _kingView.CastPoint);
        }

        /// <summary>
        /// v4 checkpoint level-up (§7): new max HP with a FULL HEAL (the mercy component),
        /// king damage retuned, ranges untouched. Raises TowerLeveledUp for the visuals.
        /// </summary>
        public void ApplyLevel(int level, TowerLevelDef def)
        {
            Level = level;
            if (_health != null) _health.Damaged -= OnHealthDamaged;
            _health = new Health(def.hp);
            _health.Damaged += OnHealthDamaged;
            BuildKingAttack(def.kingDamage);
            _events.RaiseTowerDamaged(_health.Current, def.hp); // refresh HP bar to the new full
            _events.RaiseTowerLeveledUp(level);
        }

        private static StructureAttack CreateAttack(AttackStats stats, ITargetRegistry registry,
            IProjectileLauncher launcher, GameEvents events)
        {
            return new StructureAttack(registry, launcher, events,
                stats.damage, stats.attackRate, stats.range, stats.impactFraction, stats.projectile);
        }

        public void Tick(float dt)
        {
            if (!IsAlive || !AttacksEnabled) return;
            _kingAttack.Tick(dt, transform.position);
            _cannonAttack.Tick(dt, transform.position);
        }
    }
}

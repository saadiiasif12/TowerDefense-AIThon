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
            GameEvents events, ITicker ticker)
        {
            _config = config;
            _events = events;

            _health = new Health(config.towerHp);
            _health.Damaged += (current, max) => _events.RaiseTowerDamaged(current, max);

            _kingAttack = CreateAttack(config.kingAttack, registry, launcher, events);
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

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
        private Juice.StructureHitBlink _hitBlink;
        public void TakeDamage(float amount)
        {
            if (_health == null || !_health.IsAlive || amount <= 0f) return;
            _health.TakeDamage(amount);
            // 18-Jul ruling: structures show NO damage text — one juicy blink per hit
            // (lazily added so every scene incl. TestRange gets it without prefab edits).
            if (_hitBlink == null) _hitBlink = GetComponent<Juice.StructureHitBlink>()
                ?? gameObject.AddComponent<Juice.StructureHitBlink>();
            _hitBlink.Play();
        }

        private ITargetRegistry _registry;
        private IProjectileLauncher _launcher;
        private KingView _kingView;
        private MortarView _mortarView;
        private TowerLevelView _levelView;

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

            // 18-Jul delta: when a MortarView exists on the roof, IT fires the tower attack
            // (yaw-track/recoil/muzzle) and the King is static decoration — idle anim only,
            // fixed idle facing (target getter returns null so he never turns or throws).
            _kingView = GetComponentInChildren<KingView>();
            _mortarView = GetComponentInChildren<MortarView>();
            BuildKingAttack(config.kingAttack.damage);
            if (_mortarView != null)
            {
                _mortarView.Init(clock, () => _kingAttack.CurrentTarget);
                _kingView?.Init(clock, () => null); // static king: idle sway, no turning
            }
            else
            {
                _kingView?.Init(clock, () => _kingAttack.CurrentTarget);
            }

            // Level-up tower visuals (view-only): show the starting level instantly.
            _levelView = GetComponentInChildren<TowerLevelView>();
            _levelView?.Init(events, Level);

            _cannonAttack = CreateAttack(config.builtInCannon, registry, launcher, events);

            registry.Register(this);
            ticker.Register(this);
        }

        private void OnHealthDamaged(float current, float max) => _events.RaiseTowerDamaged(current, max);

        private void BuildKingAttack(float damage)
        {
            var king = _config.kingAttack;
            // 18-Jul user delta: the tower's reach IS the white circle. The attack range is
            // DERIVED from deploymentRadius (one knob: ring + placement + knight leash +
            // tower fire range all move together) — kingAttack.range is superseded/ignored.
            float range = _config.deploymentRadius;
            if (_mortarView != null)
            {
                // Mortar mode (18-Jul): shells leave the tube mouth; windup charges the tube,
                // the fire moment kicks the recoil + muzzle flash. King stays out of it.
                _kingAttack = new StructureAttack(_registry, _launcher, _events,
                    damage, king.attackRate, range, king.impactFraction, king.projectile,
                    onSwing: period => _mortarView.OnSwing(period),
                    onFire: () => _mortarView.OnFire(),
                    firePoint: () => _mortarView.FirePoint);
                return;
            }
            _kingAttack = new StructureAttack(_registry, _launcher, _events,
                damage, king.attackRate, range, king.impactFraction, king.projectile,
                onSwing: period => _kingView?.OnSwing(period),
                // Bolt leaves from the king's casting hand, read at the exact fire moment
                // (impactFraction lands on the hand-extended release pose of the throw anim).
                firePoint: _kingView == null ? (Func<Vector3>)null : () => _kingView.CastPoint);
        }

        private int _pendingVisualLevel = -1;

        /// <summary>
        /// v4 checkpoint level-up (§7): new max HP with a FULL HEAL (the mercy component),
        /// king damage retuned, ranges untouched. Raises TowerLeveledUp for the visuals.
        /// instantVisual=true (resume/retry scene load) snaps the model without the surge.
        /// deferVisual=true (screen-first level-up flow) applies the STATS now but HOLDS the
        /// sink/rise model swap until <see cref="PlayPendingLevelVisual"/> — so the level-up
        /// screen shows first and the tower animation plays only after Continue is tapped.
        /// </summary>
        public void ApplyLevel(int level, TowerLevelDef def, bool instantVisual = false, bool deferVisual = false)
        {
            Level = level;
            if (_health != null) _health.Damaged -= OnHealthDamaged;
            _health = new Health(def.hp);
            _health.Damaged += OnHealthDamaged;
            BuildKingAttack(def.kingDamage);
            _events.RaiseTowerDamaged(_health.Current, def.hp); // refresh HP bar to the new full
            if (deferVisual)
                _pendingVisualLevel = level;                    // hold the model swap for later
            else
                _levelView?.SetLevel(level, instantVisual);     // juicy seamless model swap (view-only)
            _events.RaiseTowerLeveledUp(level);
        }

        /// <summary>
        /// Screen-first flow: play the level-up model swap that <see cref="ApplyLevel"/> deferred.
        /// Returns TRUE if the sink/rise cinematic actually started (FALSE = no visual change,
        /// e.g. stage-complete or the last tower art — the caller may resume gameplay at once).
        /// </summary>
        public bool PlayPendingLevelVisual()
        {
            if (_pendingVisualLevel < 0 || _levelView == null) { _pendingVisualLevel = -1; return false; }
            int level = _pendingVisualLevel;
            _pendingVisualLevel = -1;
            return _levelView.SetLevel(level);
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

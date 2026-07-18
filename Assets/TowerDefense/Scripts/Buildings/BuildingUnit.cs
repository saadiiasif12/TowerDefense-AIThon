using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Core;
using RoyalSiege.Data;
using RoyalSiege.Units;

namespace RoyalSiege.Buildings
{
    /// <summary>
    /// A placed player building (Cannon / Tesla / X-Bow — all data-driven from BuildingCardSO).
    /// Composition: Health + StructureAttack + optional BuildingTurret (visual yaw/recoil/muzzle)
    /// + RangeRing + optional UnitAnimator. Not pooled: max 4 alive by rule.
    /// </summary>
    public sealed class BuildingUnit : MonoBehaviour, ITickable, IStructureTarget, IHealthReadout
    {
        private BuildingCardSO _card;
        private Health _health;
        private StructureAttack _attack;
        private ITargetRegistry _registry;
        private ITicker _ticker;
        private GameEvents _events;
        private UnitAnimator _animator;
        private BuildingTurret _turret;
        private RangeRing _ring;
        private float _decayPerSecond; // v4 lifetime: maxHP/lifetime, drains from placement
        private bool _tickingDecay;

        public BuildingCardSO Card => _card;
        public float HpPct => _health?.Pct ?? 0f;
        /// <summary>True when the killing blow was lifetime decay — view plays a crumble, not an explosion.</summary>
        public bool DiedOfDecay { get; private set; }

        // ---- IStructureTarget ----
        public bool IsAlive => _health != null && _health.IsAlive;
        public Vector3 Position => transform.position;
        public bool IsBuilding => true;
        public bool BlocksPlacement => true;
        public float FootprintRadius => _card != null ? _card.footprintRadius : 0.75f;
        private Juice.StructureHitBlink _hitBlink;
        public void TakeDamage(float amount)
        {
            if (_health == null || !_health.IsAlive || amount <= 0f) return;
            _health.TakeDamage(amount);
            // 18-Jul ruling: structures show NO damage text — one juicy blink per hit.
            // (Lifetime decay calls _health directly in Tick, so decay never blinks.)
            if (_hitBlink == null) _hitBlink = GetComponent<Juice.StructureHitBlink>()
                ?? gameObject.AddComponent<Juice.StructureHitBlink>();
            _hitBlink.Play();
        }

        public void Init(BuildingCardSO card, ITargetRegistry registry, IProjectileLauncher launcher,
            GameEvents events, ITicker ticker, IClock clock)
        {
            _card = card;
            _registry = registry;
            _events = events;
            _ticker = ticker;

            _health = new Health(card.hp);
            _health.Died += OnDied;
            // v4 building lifetime: the HP bar IS the lifetime bar (13_PROGRESSION_V4 §4).
            _decayPerSecond = card.lifetimeSeconds > 0f ? card.hp / card.lifetimeSeconds : 0f;
            DiedOfDecay = false;

            _animator = GetComponent<UnitAnimator>();
            _turret = GetComponent<BuildingTurret>();

            _attack = new StructureAttack(registry, launcher, events,
                card.damage, card.attackRate, card.range, card.impactFraction,
                card.projectile, card.retargetDelay,
                onSwing: period =>
                {
                    _animator?.PlayAttack(period);
                    _turret?.OnSwing();
                },
                onFire: () => _turret?.OnFire(),
                firePoint: () => _turret != null ? _turret.MuzzlePosition : transform.position);

            _turret?.Init(clock, () => _attack.CurrentTarget);

            // Range ring is configured but stays HIDDEN once placed — the drag ghost is the
            // range preview. (Otherwise it doubles up with the deployment ring on screen.)
            _ring = GetComponentInChildren<RangeRing>();
            if (_ring != null)
            {
                _ring.SetRadius(card.range);
                _ring.SetVisible(false);
            }

            registry.Register(this);
            ticker.Register(this);
        }

        public void Tick(float dt)
        {
            if (_decayPerSecond > 0f && IsAlive)
            {
                _tickingDecay = true;
                _health.TakeDamage(_decayPerSecond * dt);
                _tickingDecay = false;
                if (!IsAlive) return; // crumbled this tick
            }
            _attack.Tick(dt, transform.position);
        }

        private void OnDied()
        {
            DiedOfDecay = _tickingDecay;
            _registry.Unregister(this);
            _ticker.Unregister(this);
            _events.RaiseBuildingDestroyed(this);
            Destroy(gameObject);
        }
    }
}

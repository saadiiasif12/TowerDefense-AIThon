using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Core;
using RoyalSiege.Data;
using RoyalSiege.Units;

namespace RoyalSiege.Buildings
{
    /// <summary>
    /// A placed player building (Cannon / Tesla / X-Bow — all data-driven from BuildingCardSO).
    /// Composition: Health + StructureAttack + RangeRing + optional UnitAnimator.
    /// Not pooled: max 4 alive by rule, instantiate/destroy is fine.
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
        private RangeRing _ring;

        public BuildingCardSO Card => _card;
        public float HpPct => _health?.Pct ?? 0f;

        // ---- IStructureTarget ----
        public bool IsAlive => _health != null && _health.IsAlive;
        public Vector3 Position => transform.position;
        public bool IsBuilding => true;
        public float FootprintRadius => _card != null ? _card.footprintRadius : 0.75f;
        public void TakeDamage(float amount) => _health?.TakeDamage(amount);

        public void Init(BuildingCardSO card, ITargetRegistry registry, IProjectileLauncher launcher,
            GameEvents events, ITicker ticker)
        {
            _card = card;
            _registry = registry;
            _events = events;
            _ticker = ticker;

            _health = new Health(card.hp);
            _health.Died += OnDied;

            _animator = GetComponent<UnitAnimator>();
            _attack = new StructureAttack(registry, launcher, events,
                card.damage, card.attackRate, card.range, card.impactFraction,
                card.projectile, card.retargetDelay,
                _animator != null ? _animator.PlayAttack : (System.Action<float>)null);

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

        public void Tick(float dt) => _attack.Tick(dt, transform.position);

        private void OnDied()
        {
            _registry.Unregister(this);
            _ticker.Unregister(this);
            _events.RaiseBuildingDestroyed(this);
            Destroy(gameObject);
        }
    }
}

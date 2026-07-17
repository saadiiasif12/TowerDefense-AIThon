using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Core;
using RoyalSiege.Data;

namespace RoyalSiege.Juice
{
    /// <summary>
    /// Event → feedback map (Docs/11_MODULE_JUICE). Subscribes to GameEvents and plays
    /// pooled VFX; gameplay code never calls juice directly.
    /// </summary>
    public sealed class JuiceDirector : MonoBehaviour
    {
        [SerializeField] private GameContext _context;
        [SerializeField] private ParticleSystem _deathPuff;
        [SerializeField] private ParticleSystem _placementDust;
        [SerializeField] private ParticleSystem _spellBurst;

        private GameEvents _events;
        private IVfxSpawner _vfx;

        private void Start()
        {
            // Normal path: wired to the game's context. Test scenes call Init() manually instead.
            if (_events == null && _context != null && _context.Events != null)
                Init(_context.Events, _context.Vfx);
        }

        public void Init(GameEvents events, IVfxSpawner vfx)
        {
            _events = events;
            _vfx = vfx;
            _events.EnemyKilled += OnEnemyKilled;
            _events.BuildingPlaced += OnBuildingPlaced;
            _events.SpellCast += OnSpellCast;
        }

        private void OnDestroy()
        {
            if (_events == null) return;
            _events.EnemyKilled -= OnEnemyKilled;
            _events.BuildingPlaced -= OnBuildingPlaced;
            _events.SpellCast -= OnSpellCast;
        }

        private void OnEnemyKilled(EnemyKilledArgs args) =>
            _vfx.Spawn(_deathPuff, args.Position + Vector3.up * 0.6f);

        private void OnBuildingPlaced(IStructureTarget building) =>
            _vfx.Spawn(_placementDust, building.Position + Vector3.up * 0.1f,
                Quaternion.identity, 1.2f, new Color(0.7f, 0.6f, 0.45f));

        private void OnSpellCast(SpellCardSO card, Vector3 point)
        {
            // Tint + scale per spell identity; radius scales the burst to the true area.
            (Color tint, float scale) = card.id switch
            {
                "Fireball" => (new Color(1f, 0.55f, 0.15f), 1.6f),
                "Freeze" => (new Color(0.55f, 0.85f, 1f), 1.3f),
                "Lightning" => (new Color(1f, 1f, 0.75f), 1.4f),
                _ => (new Color(0.9f, 0.85f, 0.6f), 1f) // Arrows / default
            };
            _vfx.Spawn(_spellBurst, point + Vector3.up * 0.3f, Quaternion.identity,
                scale * Mathf.Max(1f, card.radius * 0.4f), tint);
        }
    }
}

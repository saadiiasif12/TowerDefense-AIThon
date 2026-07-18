using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Core;
using RoyalSiege.Data;

namespace RoyalSiege.Juice
{
    /// <summary>
    /// Event → feedback map (Docs/11_MODULE_JUICE). Subscribes to GameEvents and plays
    /// pooled VFX; gameplay code never calls juice directly. Spell choreography lives in
    /// SpellVfxDirector — this handles the generic lifecycle feedback.
    /// </summary>
    public sealed class JuiceDirector : MonoBehaviour
    {
        [SerializeField] private GameContext _context;
        [SerializeField] private ParticleSystem _deathPuff;
        [SerializeField] private ParticleSystem _placementDust;
        [SerializeField] private ParticleSystem _spellBurst; // legacy fallback, unused by the 4 shipped spells

        private GameEvents _events;
        private IVfxSpawner _vfx;
        private CameraShaker _shaker;

        private void Start()
        {
            // Normal path: wired to the game's context. Test scenes call Init() manually instead.
            if (_events == null && _context != null && _context.Events != null)
                Init(_context.Events, _context.Vfx, Camera.main != null ? Camera.main.GetComponent<CameraShaker>() : null);
        }

        public void Init(GameEvents events, IVfxSpawner vfx, CameraShaker shaker = null)
        {
            _events = events;
            _vfx = vfx;
            _shaker = shaker;
            _events.EnemyKilled += OnEnemyKilled;
            _events.EnemySpawned += OnEnemySpawned;
            _events.BuildingPlaced += OnBuildingPlaced;
            _events.BossSlammed += OnBossSlammed;
        }

        private void OnDestroy()
        {
            if (_events == null) return;
            _events.EnemyKilled -= OnEnemyKilled;
            _events.EnemySpawned -= OnEnemySpawned;
            _events.BuildingPlaced -= OnBuildingPlaced;
            _events.BossSlammed -= OnBossSlammed;
        }

        private void OnEnemyKilled(EnemyKilledArgs args)
        {
            // Per-enemy death burst (Skeleton bone shatter, …) with the generic puff as fallback.
            var vfx = args.Definition != null && args.Definition.deathVfx != null
                ? args.Definition.deathVfx
                : _deathPuff;
            _vfx.Spawn(vfx, args.Position + Vector3.up * 0.6f);
            // 18-Jul: the camera feels the big ones hit the ground — boss thump, and a
            // smaller thud for HEAVY units (Ogre-class, body radius ≥ 0.8) whose fall
            // animation is a full collapse. Fodder deaths stay shake-free.
            if (args.Definition != null)
            {
                if (args.Definition.isBoss) _shaker?.AddTrauma(0.45f);
                else if (args.Definition.unitRadius >= 0.8f) _shaker?.AddTrauma(0.3f);
            }
        }

        private void OnEnemySpawned(EnemyDefinitionSO def, Vector3 position) =>
            _vfx.Spawn(_placementDust, position + Vector3.up * 0.1f,
                Quaternion.identity, Mathf.Max(0.8f, def.unitRadius * 1.6f), new Color(0.55f, 0.5f, 0.42f));

        private void OnBuildingPlaced(IStructureTarget building)
        {
            _vfx.Spawn(_placementDust, building.Position + Vector3.up * 0.1f,
                Quaternion.identity, 1.2f, new Color(0.7f, 0.6f, 0.45f));
            _shaker?.AddTrauma(0.15f);
        }

        private void OnBossSlammed(Vector3 position)
        {
            _vfx.Spawn(_placementDust, position + Vector3.up * 0.1f,
                Quaternion.identity, 2.2f, new Color(0.5f, 0.42f, 0.3f));
            _shaker?.AddTrauma(0.55f);
        }
    }
}

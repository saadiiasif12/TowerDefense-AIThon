using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Buildings;
using RoyalSiege.Combat;
using RoyalSiege.Core;
using RoyalSiege.Data;
using RoyalSiege.Juice;
using RoyalSiege.Spells;
using RoyalSiege.Units;

namespace RoyalSiege.Testing
{
    /// <summary>
    /// TEST-ONLY composition root for the TestRange scene: minimal service stack
    /// (registry, launcher, VFX, events) with NO waves/economy/cards — spawn any
    /// building/enemy on demand and watch it animate, aim, fire, trail and splash.
    /// Buildings fire at the wandering TestDummyTarget; enemies attack the tower.
    /// Tune the SO assets / prefabs in the inspector, then Clear + respawn to apply.
    /// </summary>
    public sealed class TestRangeContext : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private TickSystem _tickSystem;
        [SerializeField] private TestDummyTarget _dummyTarget;
        [SerializeField] private RoyalTower _royalTower;
        [SerializeField] private GameConfigSO _gameConfig;
        [SerializeField] private Transform _projectileRoot;
        [SerializeField] private Transform _spawnRoot;

        [Header("What can be spawned (buttons are generated from these)")]
        public List<BuildingCardSO> buildingCards = new();
        public List<EnemyDefinitionSO> enemyDefinitions = new();
        [Tooltip("Spell cards get a 'Cast @ target' button each — casts on the dummy (artists: add/tune freely).")]
        public List<SpellCardSO> spellCards = new();
        [Tooltip("Loose VFX prefabs get a 'FX:' replay button each — plays at the FX anchor for eyeballing.")]
        public List<ParticleSystem> vfxGallery = new();
        [Tooltip("Where gallery VFX play. Defaults to (2, 0.5, -4) if empty.")]
        public Transform fxAnchor;

        public GameEvents Events { get; private set; }
        public IClock Clock => _tickSystem;
        public IVfxSpawner Vfx { get; private set; }
        public TestDummyTarget Dummy => _dummyTarget;

        private TargetRegistry _registry;
        private ProjectileLauncher _launcher;
        private EnemyFactory _enemyFactory;
        private EnemyRuntimeDeps _enemyDeps;
        private SpellCaster _spellCaster;
        private readonly List<BuildingUnit> _spawnedBuildings = new();
        private int _buildingSlot;
        private int _enemySlot;

        private void Awake()
        {
            Events = new GameEvents();
            _registry = new TargetRegistry();
            Vfx = new VfxSpawner(_projectileRoot);
            _launcher = new ProjectileLauncher(_projectileRoot, _tickSystem, Vfx);

            if (_dummyTarget != null) _registry.Register(_dummyTarget);

            _enemyDeps = new EnemyRuntimeDeps
            {
                MapCenter = Vector3.zero,
                Registry = _registry,
                Launcher = _launcher,
                Events = Events,
                Clock = _tickSystem,
                Ticker = _tickSystem,
                HpMultiplier = _gameConfig.enemyHpMultiplier,
                DamageMultiplier = _gameConfig.enemyDamageMultiplier,
                BountyMultiplier = 1f
            };
            _enemyFactory = new EnemyFactory(_spawnRoot, _enemyDeps);

            if (_royalTower != null)
                _royalTower.Init(_gameConfig, _registry, _launcher, Events, _tickSystem, _tickSystem);

            _spellCaster = new SpellCaster(_registry, _gameConfig, Vector3.zero, Events);
            _tickSystem.Register(_spellCaster);

            var shaker = Camera.main != null ? Camera.main.GetComponent<CameraShaker>() : null;
            GetComponent<ZapArcRenderer>()?.Init(Events, _tickSystem, Vfx);
            GetComponent<JuiceDirector>()?.Init(Events, Vfx, shaker);
            GetComponent<SpellVfxDirector>()?.Init(Events, Vfx, _tickSystem, shaker);
        }

        /// <summary>Cast a spell centered on the dummy (or map center when no dummy).</summary>
        public void CastSpell(SpellCardSO card)
        {
            if (card == null || _spellCaster == null) return;
            Vector3 point = _dummyTarget != null ? _dummyTarget.Position : Vector3.zero;
            _spellCaster.Cast(card, point);
        }

        /// <summary>Replay a loose VFX prefab at the FX anchor (artist eyeballing).</summary>
        public void PlayGalleryVfx(ParticleSystem prefab)
        {
            Vector3 point = fxAnchor != null ? fxAnchor.position : new Vector3(2f, 0.5f, -4f);
            Vfx.Spawn(prefab, point);
        }

        public void SpawnBuilding(BuildingCardSO card)
        {
            // ring of test pads around the center
            float angle = _buildingSlot * 60f * Mathf.Deg2Rad;
            var position = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 3.5f;
            _buildingSlot++;

            var go = Instantiate(card.buildingPrefab, position, Quaternion.identity, _spawnRoot);
            var unit = go.GetComponent<BuildingUnit>();
            if (unit == null) unit = go.AddComponent<BuildingUnit>();
            unit.Init(card, _registry, _launcher, Events, _tickSystem, _tickSystem);
            _spawnedBuildings.Add(unit);
            Events.RaiseBuildingPlaced(unit);
        }

        public void ClearBuildings()
        {
            foreach (var b in _spawnedBuildings)
                if (b != null) b.TakeDamage(float.MaxValue);
            _spawnedBuildings.Clear();
            _buildingSlot = 0;
        }

        public void SpawnEnemy(EnemyDefinitionSO definition)
        {
            float angle = (45f + _enemySlot * 90f) * Mathf.Deg2Rad;
            var position = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 10f;
            _enemySlot++;
            _enemyFactory.Spawn(definition, position, waveIndex: 0);
        }

        public void KillAllEnemies()
        {
            var enemies = new List<IEnemyTarget>(_registry.Enemies);
            foreach (var e in enemies)
                if (e is EnemyAgent agent) agent.TakeDamage(float.MaxValue);
        }

        public string TeslaQualityLabel
        {
            get { var zap = GetComponent<ZapArcRenderer>(); return zap != null ? zap.Quality.ToString() : "-"; }
        }

        public void CycleTeslaQuality()
        {
            var zap = GetComponent<ZapArcRenderer>();
            if (zap != null) zap.Quality = (TeslaVfxQuality)(((int)zap.Quality + 1) % 3);
        }

        public bool KingAttackEnabled => _royalTower != null && _royalTower.AttacksEnabled;
        public bool DummyWanderEnabled => _dummyTarget != null && _dummyTarget.Wander;
        public bool DummyMovementEnabled => _dummyTarget != null && _dummyTarget.MovementEnabled;
        public void ToggleKingAttack() { if (_royalTower != null) _royalTower.AttacksEnabled = !_royalTower.AttacksEnabled; }
        public void ToggleDummyWander() { if (_dummyTarget != null) _dummyTarget.Wander = !_dummyTarget.Wander; }
        public void ToggleDummyMovement() { if (_dummyTarget != null) _dummyTarget.MovementEnabled = !_dummyTarget.MovementEnabled; }
        public void ResetDummy() { if (_dummyTarget != null) _dummyTarget.ResetHp(); }
        public void SetGameSpeed(float speed) => _tickSystem.SpeedMultiplier = speed;
    }
}

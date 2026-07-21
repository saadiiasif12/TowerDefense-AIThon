using UnityEngine;
using RoyalSiege.Buildings;
using RoyalSiege.Cards;
using RoyalSiege.Combat;
using RoyalSiege.Data;
using RoyalSiege.Economy;
using RoyalSiege.Juice;
using RoyalSiege.Placement;
using RoyalSiege.Spells;
using RoyalSiege.Units;

namespace RoyalSiege.Core
{
    /// <summary>
    /// THE composition root — the only place that news up services and wires dependencies.
    /// No singletons, no service locator: everything downstream receives what it needs via
    /// constructors or Init(). UI reads the public getters, never the internals.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameContext : MonoBehaviour
    {
        [Header("Config (author from Docs/13_PROGRESSION_V4.md)")]
        [SerializeField] private GameConfigSO _gameConfig;
        [SerializeField] private EconomyConfigSO _economyConfig;
        [SerializeField] private CampaignSO _campaign;
        [SerializeField] private ProgressionConfigSO _progression;
        [Tooltip("0 = random per run. The deck shuffle is the game's ONLY RNG; set non-zero for deterministic tests.")]
        [SerializeField] private int _shuffleSeed;

        [Header("Scene")]
        [SerializeField] private TickSystem _tickSystem;
        [SerializeField] private RoyalTower _royalTower;
        [SerializeField] private OrbSpawner _orbSpawner;
        [SerializeField] private PlacementController _placement;
        [SerializeField] private Camera _gameCamera;
        [SerializeField] private Transform _projectileRoot;
        [SerializeField] private Transform _enemyRoot;
        [SerializeField] private Transform _buildingRoot;

        // Services (exposed read-only for UI/Juice)
        public GameEvents Events { get; private set; }
        public IClock Clock => _tickSystem;
        public IEnergyBank Energy { get; private set; }
        public DeckService Deck { get; private set; }
        public CardCooldowns Cooldowns { get; private set; }
        public ICardPlayService CardPlay { get; private set; }
        public ITargetQuery Targets { get; private set; }
        public WaveScheduler Waves { get; private set; }
        public IVfxSpawner Vfx { get; private set; }
        public GameConfigSO GameConfig => _gameConfig;
        public Vector3 MapCenter { get; private set; }

        private void Awake()
        {
            // Frame rate is owned by FrameRateSetter (panel-max, 120Hz where supported) —
            // it lives on GameSystems and in the Splash scene. Nothing hardcoded here.

            // Flatten: the tower model's pivot may sit above y=0, but all gameplay is planar.
            Vector3 center = RangeMath.Flatten(_royalTower.transform.position);
            MapCenter = center;

            // v4 journey: the profile IS the checkpoint (saved on checkpoints only). A scene
            // load with a profile = a retry/resume: tower at its level (full HP), earned
            // unlocks, next wave = checkpoint wave, empty field, 5 elixir, fresh shuffle.
            var profile = CampaignProfile.Load();

            Events = new GameEvents();
            // Map bounds on the registry: attacks may only target enemies whose center is
            // inside the map circle (QA 17-Jul DT-004 — spawn-formation units are off-limits).
            var registry = new TargetRegistry(center, _gameConfig.mapRadius);
            Targets = registry;

            Vfx = new VfxSpawner(_projectileRoot);
            var launcher = new ProjectileLauncher(_projectileRoot, _tickSystem, Vfx);
            Energy = new EnergyBank(_economyConfig, Events);

            // Deck = starter six + every unlock already earned (v4 §2).
            var deckCards = new System.Collections.Generic.List<CardDefinitionSO>(_progression.startingCards);
            foreach (var checkpoint in _progression.checkpoints)
                if (checkpoint.unlockCard != null && profile.unlockedCardIds.Contains(checkpoint.unlockCard.id))
                    deckCards.Add(checkpoint.unlockCard);
            Deck = new DeckService(deckCards, Events, _shuffleSeed);
            Cooldowns = new CardCooldowns();

            var knightDeps = new KnightRuntimeDeps
            {
                Registry = registry,
                Ticker = _tickSystem,
                Clock = _tickSystem,
                Events = Events,
                MapCenter = center,
                GuardRadius = _gameConfig.deploymentRadius, // knights guard the tower's white circle
                CombatStandoff = _gameConfig.knightCombatStandoff,
                Spacing = _gameConfig.knightSpacing
            };
            var knightFactory = new KnightFactory(_buildingRoot, knightDeps);

            CardPlay = new CardPlayService(Deck, Cooldowns, Energy, Events, knightFactory);

            var enemyDeps = new EnemyRuntimeDeps
            {
                MapCenter = center,
                Registry = registry,
                Launcher = launcher,
                Events = Events,
                Clock = _tickSystem,
                Ticker = _tickSystem,
                HpMultiplier = _gameConfig.enemyHpMultiplier,
                DamageMultiplier = _gameConfig.enemyDamageMultiplier,
                BountyMultiplier = _gameConfig.enemyBountyMultiplier,
                WalkAnimMultiplier = _gameConfig.enemyWalkAnimSpeedMultiplier,
                AttackStandoff = _gameConfig.enemyAttackStandoff,
                KnightStandoff = _gameConfig.knightCombatStandoff
            };
            var enemyFactory = new EnemyFactory(_enemyRoot, enemyDeps);
            var spawnPoints = new SpawnPointProvider(center, _gameConfig.mapRadius);
            Waves = new WaveScheduler(_campaign, enemyFactory, spawnPoints, Events, _orbSpawner, profile.nextWave);

            var buildingFactory = new BuildingFactory(_buildingRoot, registry, launcher, Events, _tickSystem, _tickSystem);
            var spellCaster = new SpellCaster(registry, _gameConfig, center, Events);
            var validator = new PlacementValidator(_gameConfig, registry, center);

            _royalTower.Init(_gameConfig, registry, launcher, Events, _tickSystem, _tickSystem);
            if (profile.towerLevel > 1 && profile.towerLevel <= _progression.towerLevels.Count)
                _royalTower.ApplyLevel(profile.towerLevel, _progression.towerLevels[profile.towerLevel - 1],
                    instantVisual: true); // resume shows the earned tower, no surge on load

            _orbSpawner.Init(Events, Energy, _economyConfig, _tickSystem);
            _placement.Init(_gameCamera, CardPlay, validator, buildingFactory, spellCaster, _gameConfig, center, knightFactory);

            var checkpoints = new CheckpointService(_progression, _campaign, _royalTower, Deck, _gameConfig, Events, _tickSystem, profile);
            var evaluator = new WinLoseEvaluator(_royalTower, Waves, registry, _gameConfig, Events, _tickSystem);

            _tickSystem.Register(Waves);
            _tickSystem.Register(Cooldowns);
            _tickSystem.Register(evaluator);
            _tickSystem.Register(spellCaster);              // delayed spells + zones resolve on the tick
            _tickSystem.Register((EnergyBank)Energy);        // v4 passive regen rides the sim clock

            // Initial UI push
            Events.RaiseEnergyChanged(Energy.Current, Energy.Max);
            Events.RaiseHandChanged();
        }
    }
}

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
        [Header("Config (author from Docs/01_DESIGN_CURRENT.md)")]
        [SerializeField] private GameConfigSO _gameConfig;
        [SerializeField] private EconomyConfigSO _economyConfig;
        [SerializeField] private DeckSO _deck;
        [SerializeField] private WaveTimelineSO _waveTimeline;
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

        private void Awake()
        {
            // Flatten: the tower model's pivot may sit above y=0, but all gameplay is planar.
            Vector3 center = RangeMath.Flatten(_royalTower.transform.position);

            Events = new GameEvents();
            var registry = new TargetRegistry();
            Targets = registry;

            Vfx = new VfxSpawner(_projectileRoot);
            var launcher = new ProjectileLauncher(_projectileRoot, _tickSystem, Vfx);
            Energy = new EnergyBank(_economyConfig, Events);
            Deck = new DeckService(_deck, Events, _shuffleSeed);
            Cooldowns = new CardCooldowns();
            CardPlay = new CardPlayService(Deck, Cooldowns, Energy, Events);

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
                BountyMultiplier = _gameConfig.enemyBountyMultiplier
            };
            var enemyFactory = new EnemyFactory(_enemyRoot, enemyDeps);
            var spawnPoints = new SpawnPointProvider(center, _gameConfig.mapRadius);
            Waves = new WaveScheduler(_waveTimeline, enemyFactory, spawnPoints, center, Events);

            var buildingFactory = new BuildingFactory(_buildingRoot, registry, launcher, Events, _tickSystem, _tickSystem);
            var spellCaster = new SpellCaster(registry, _gameConfig, center, Events);
            var validator = new PlacementValidator(_gameConfig, registry, center);

            _royalTower.Init(_gameConfig, registry, launcher, Events, _tickSystem, _tickSystem);
            _orbSpawner.Init(Events, Energy, _economyConfig, _tickSystem);
            _placement.Init(_gameCamera, CardPlay, validator, buildingFactory, spellCaster, _gameConfig);

            var evaluator = new WinLoseEvaluator(_royalTower, Waves, registry, _gameConfig, Events, _tickSystem);

            _tickSystem.Register(Waves);
            _tickSystem.Register(Cooldowns);
            _tickSystem.Register(evaluator);
            _tickSystem.Register(spellCaster); // sky-fall spells resolve on the tick (fallDelaySeconds)

            // Initial UI push
            Events.RaiseEnergyChanged(Energy.Current, Energy.Max);
            Events.RaiseHandChanged();
        }
    }
}

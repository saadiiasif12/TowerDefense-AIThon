using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Core;

namespace RoyalSiege.Juice
{
    /// <summary>
    /// Damage-number popups (view-only juice). Central owner of the pooled
    /// <see cref="DamagePopup"/>s: one Update ticks every active popup (no per-popup
    /// coroutines, no per-frame allocations, number strings cached) on WALL-CLOCK time so
    /// numbers finish floating while the sim pauses.
    ///
    /// Entry points:
    ///  • <c>DamagePopupManager.Instance.ShowDamage(amount, position, isCritical)</c> —
    ///    called from RoyalTower/BuildingUnit damage methods (null-safe: TestRange has none).
    ///  • GameEvents.EnemyDamaged — enemies stay event-driven (keeps the DoT aggregation
    ///    and killing-blow styling): killing blows render as criticals.
    ///
    /// "Random" spread/drift is HASHED off a hit counter — the project allows no RNG.
    /// </summary>
    public sealed class DamagePopupManager : MonoBehaviour
    {
        /// <summary>Scene-level view singleton (assigned in Awake). Gameplay callers must
        /// null-check — test scenes run without popups.</summary>
        public static DamagePopupManager Instance { get; private set; }

        [SerializeField] private GameContext _context;
        [SerializeField] private DamagePopupPool _pool;

        [Header("Timing")]
        [Tooltip("Total popup lifetime in seconds (spawn → released to pool).")]
        [SerializeField, Range(0.2f, 2f)] private float _duration = 0.7f;
        [Tooltip("Fraction of the lifetime spent punching up to Punch Scale.")]
        [SerializeField, Range(0.05f, 0.5f)] private float _punchTime = 0.16f;
        [Tooltip("Fraction of the lifetime by which the scale has settled back to 1.")]
        [SerializeField, Range(0.2f, 0.9f)] private float _settleTime = 0.42f;
        [Tooltip("Fraction of the lifetime where the fade-out starts.")]
        [SerializeField, Range(0.3f, 0.95f)] private float _fadeStart = 0.62f;

        [Header("Motion")]
        [Tooltip("World units the popup floats upward over its lifetime.")]
        [SerializeField] private float _riseDistance = 1.1f;
        [Tooltip("Max sideways drift in world units (hashed per hit, ±).")]
        [SerializeField] private float _driftRange = 0.35f;
        [Tooltip("Base world offset added to every reported hit position.")]
        [SerializeField] private Vector3 _worldOffset = new(0f, 1.4f, 0f);
        [Tooltip("Hashed random spawn jitter (world units, ±x / ±y).")]
        [SerializeField] private Vector2 _randomSpread = new(0.3f, 0.15f);
        [Tooltip("Popups spawning within this radius of a recent one are bumped upward so rapid hits stack instead of overlapping.")]
        [SerializeField] private float _stackRadius = 0.6f;
        [Tooltip("Upward bump per already-active popup inside Stack Radius.")]
        [SerializeField] private float _stackBump = 0.5f;

        [Header("Scale")]
        [Tooltip("Scale at spawn (fraction of the settled size).")]
        [SerializeField] private float _startScale = 0.6f;
        [Tooltip("Peak of the initial punch.")]
        [SerializeField] private float _punchScale = 1.2f;
        [Tooltip("Extra scale multiplier for criticals / killing blows.")]
        [SerializeField] private float _critScaleMul = 1.35f;

        [Header("Colors")]
        [Tooltip("Normal hit text (white per the reference).")]
        [SerializeField] private Color _textColor = Color.white;
        [Tooltip("Critical / killing-blow text (yellow-orange).")]
        [SerializeField] private Color _critTextColor = new(1f, 0.72f, 0.1f);
        [Tooltip("Rounded plate behind normal numbers (magenta/red).")]
        [SerializeField] private Color _backgroundColor = new(0.78f, 0.12f, 0.35f, 0.9f);
        [Tooltip("Plate behind criticals (deeper red).")]
        [SerializeField] private Color _critBackgroundColor = new(0.62f, 0.07f, 0.14f, 0.92f);

        // Read by DamagePopup.Tick — grouped accessors keep the popup free of duplicated fields.
        public float Duration => _duration;
        public float PunchTime => _punchTime;
        public float SettleTime => _settleTime;
        public float FadeStart => _fadeStart;
        public float RiseDistance => _riseDistance;
        public float StartScale => _startScale;
        public float PunchScale => _punchScale;

        private static readonly Dictionary<int, string> NumberCache = new();
        private readonly List<DamagePopup> _active = new();
        private GameEvents _events;
        private Camera _camera;
        private int _hitCounter;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            _camera = Camera.main;
            if (_pool == null) _pool = GetComponent<DamagePopupPool>();
            _pool?.Prewarm(this);

            if (_context == null) _context = FindFirstObjectByType<GameContext>();
            if (_context != null && _context.Events != null)
            {
                _events = _context.Events;
                _events.EnemyDamaged += OnEnemyDamaged;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_events != null) _events.EnemyDamaged -= OnEnemyDamaged;
        }

        // Enemies keep their event path: DoT ticks arrive pre-aggregated, kills read as crits.
        private void OnEnemyDamaged(Vector3 position, float amount, bool killingBlow)
            => ShowDamage(amount, position, killingBlow);

        /// <summary>
        /// Spawn one damage number at a world position (the serialized world offset and the
        /// hashed spread/anti-overlap bump are applied on top). Every hit gets its own popup.
        /// </summary>
        public void ShowDamage(float amount, Vector3 position, bool isCritical = false)
        {
            if (_pool == null) return;
            int value = Mathf.Max(1, Mathf.RoundToInt(amount));
            if (!NumberCache.TryGetValue(value, out string label))
            {
                label = value.ToString();
                NumberCache[value] = label;
            }

            // Hashed jitter (no RNG allowed in-project): spread + sideways drift per hit.
            _hitCounter++;
            float jx = (Hash01(_hitCounter * 7919) - 0.5f) * 2f * _randomSpread.x;
            float jy = Hash01(_hitCounter * 104729) * _randomSpread.y;
            float drift = (Hash01(_hitCounter * 1301081) - 0.5f) * 2f * _driftRange;

            Vector3 origin = position + _worldOffset + new Vector3(jx, jy, 0f);

            // Anti-overlap: young popups near this spot push the new number upward.
            int neighbours = 0;
            for (int i = 0; i < _active.Count; i++)
            {
                var other = _active[i];
                if (other.Age < 0.35f * _duration
                    && (other.Origin - origin).sqrMagnitude < _stackRadius * _stackRadius)
                    neighbours++;
            }
            origin += Vector3.up * (_stackBump * neighbours);

            var popup = _pool.Get();
            if (popup == null) // saturated: recycle the oldest active — never Instantiate
            {
                popup = _active[0];
                _active.RemoveAt(0);
            }

            popup.Show(label,
                isCritical ? _critTextColor : _textColor,
                isCritical ? _critBackgroundColor : _backgroundColor,
                isCritical ? _critScaleMul : 1f,
                origin, drift);
            _active.Add(popup);
        }

        private void Update()
        {
            if (_active.Count == 0) return;
            if (_camera == null) { _camera = Camera.main; if (_camera == null) return; }

            float dt = Time.deltaTime; // wall clock: popups finish even while the sim pauses
            Quaternion camRot = _camera.transform.rotation;
            Vector3 camRight = _camera.transform.right;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (!_active[i].Tick(dt, camRot, camRight))
                {
                    _pool.Release(_active[i]);
                    // swap-remove keeps this O(1); popup order only matters for oldest-first
                    // recycling, which stays approximately true.
                    _active[i] = _active[_active.Count - 1];
                    _active.RemoveAt(_active.Count - 1);
                }
            }
        }

        private static float Hash01(int n)
        {
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }
    }
}

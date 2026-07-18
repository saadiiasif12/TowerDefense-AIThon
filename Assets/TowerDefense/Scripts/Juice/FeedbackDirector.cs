using UnityEngine;
using RoyalSiege.Core;
using RoyalSiege.Data;
using Lofelt.NiceVibrations;

namespace RoyalSiege.Juice
{
    /// <summary>
    /// 18-Jul juice pass: ONE place that turns game events into SOUND + HAPTICS (+ the odd
    /// camera shake that isn't data-driven elsewhere). View-only, event-driven, pooled
    /// audio (8 round-robin 2D sources — no PlayClipAtPoint allocs), hashed pitch variation
    /// (zero RNG), per-event throttles so 10 simultaneous hits never stack into noise.
    /// Haptics via Feel's NiceVibrations — no-ops on desktop, buzzes on device.
    /// </summary>
    public sealed class FeedbackDirector : MonoBehaviour
    {
        [SerializeField] private GameContext _context;

        [Header("Combat")]
        [SerializeField] private AudioClip _enemyHit;        // small thock per landed hit (throttled)
        [SerializeField] private AudioClip[] _enemyDeaths;   // grunt variations (hashed pick)
        [SerializeField] private AudioClip _towerHit;        // tower taking damage (throttled)

        [Header("Cards & buildings")]
        [SerializeField] private AudioClip _cardPlayed;      // deploy pop
        [SerializeField] private AudioClip _buildingPlaced;  // heavy thud
        [SerializeField] private AudioClip _knightSpawn;

        [Header("Flow")]
        [SerializeField] private AudioClip _levelUp;         // checkpoint fanfare note
        [SerializeField] private AudioClip _victory;
        [SerializeField] private AudioClip _defeat;          // heavy thunder

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 0.8f;

        private const int Sources = 8;
        private AudioSource[] _pool;
        private int _next;
        private GameEvents _events;
        private int _hashCounter;
        private float _lastHitSound;    // realtime throttles
        private float _lastTowerSound;
        private float _lastHitHaptic;

        private void Start()
        {
            if (_context == null) _context = FindFirstObjectByType<GameContext>();
            if (_context == null || _context.Events == null) return;
            _events = _context.Events;

            _pool = new AudioSource[Sources];
            for (int i = 0; i < Sources; i++)
            {
                var go = new GameObject("Sfx" + i);
                go.transform.SetParent(transform, false);
                _pool[i] = go.AddComponent<AudioSource>();
                _pool[i].playOnAwake = false;
                _pool[i].spatialBlend = 0f; // 2D — the whole arena is on screen anyway
            }

            _events.EnemyDamaged += OnEnemyDamaged;
            _events.EnemyKilled += OnEnemyKilled;
            _events.TowerDamaged += OnTowerDamaged;
            _events.CardPlayed += OnCardPlayed;
            _events.BuildingPlaced += OnBuildingPlaced;
            _events.KnightSpawned += OnKnightSpawned;
            _events.CheckpointReached += OnCheckpoint;
            _events.MatchEnded += OnMatchEnded;
        }

        private void OnDestroy()
        {
            if (_events == null) return;
            _events.EnemyDamaged -= OnEnemyDamaged;
            _events.EnemyKilled -= OnEnemyKilled;
            _events.TowerDamaged -= OnTowerDamaged;
            _events.CardPlayed -= OnCardPlayed;
            _events.BuildingPlaced -= OnBuildingPlaced;
            _events.KnightSpawned -= OnKnightSpawned;
            _events.CheckpointReached -= OnCheckpoint;
            _events.MatchEnded -= OnMatchEnded;
        }

        // ---------------- handlers ----------------

        private void OnEnemyDamaged(Vector3 position, float amount, bool killingBlow)
        {
            if (killingBlow) return; // the death grunt covers it
            if (Time.realtimeSinceStartup - _lastHitSound < 0.09f) return; // anti-stack
            _lastHitSound = Time.realtimeSinceStartup;
            Play(_enemyHit, 0.35f, 0.92f, 1.1f);
        }

        private void OnEnemyKilled(EnemyKilledArgs args)
        {
            if (_enemyDeaths != null && _enemyDeaths.Length > 0)
                Play(_enemyDeaths[Mathf.Abs(Hash(_hashCounter)) % _enemyDeaths.Length], 0.5f, 0.9f, 1.12f);
            if (Time.realtimeSinceStartup - _lastHitHaptic > 0.15f)
            {
                _lastHitHaptic = Time.realtimeSinceStartup;
                HapticPatterns.PlayPreset(HapticPatterns.PresetType.LightImpact);
            }
        }

        private void OnTowerDamaged(float current, float max)
        {
            if (Time.realtimeSinceStartup - _lastTowerSound < 0.3f) return;
            _lastTowerSound = Time.realtimeSinceStartup;
            Play(_towerHit, 0.45f, 0.75f, 0.9f); // low pitch = heavy masonry
            HapticPatterns.PlayPreset(HapticPatterns.PresetType.LightImpact);
        }

        private void OnCardPlayed(CardDefinitionSO card)
        {
            Play(_cardPlayed, 0.6f, 1.0f, 1.15f);
            HapticPatterns.PlayPreset(HapticPatterns.PresetType.MediumImpact);
        }

        private void OnBuildingPlaced(Combat.IStructureTarget building)
        {
            Play(_buildingPlaced, 0.7f, 0.85f, 1.0f);
            HapticPatterns.PlayPreset(HapticPatterns.PresetType.HeavyImpact);
            CameraShaker.Main?.AddTrauma(0.18f);
        }

        private void OnKnightSpawned(Vector3 position)
        {
            Play(_knightSpawn, 0.5f, 1.05f, 1.25f);
        }

        private void OnCheckpoint(CheckpointReachedArgs args)
        {
            Play(_levelUp, 0.85f, 1f, 1f);
            HapticPatterns.PlayPreset(HapticPatterns.PresetType.Success);
        }

        private void OnMatchEnded(MatchResult result)
        {
            Play(result.Victory ? _victory : _defeat, 0.9f, 1f, 1f);
            HapticPatterns.PlayPreset(result.Victory
                ? HapticPatterns.PresetType.Success
                : HapticPatterns.PresetType.Failure);
            CameraShaker.Main?.AddTrauma(result.Victory ? 0.2f : 0.45f);
        }

        // ---------------- plumbing ----------------

        private void Play(AudioClip clip, float volume, float pitchMin, float pitchMax)
        {
            if (clip == null || _pool == null) return;
            var source = _pool[_next];
            _next = (_next + 1) % Sources;
            _hashCounter++;
            float pitch01 = (Hash(_hashCounter) & 0xFFFF) / 65535f;
            source.pitch = Mathf.Lerp(pitchMin, pitchMax, pitch01);
            source.PlayOneShot(clip, volume * _sfxVolume);
        }

        private static int Hash(int n)
        {
            n = (n << 13) ^ n;
            return n * (n * n * 15731 + 789221) + 1376312589;
        }
    }
}

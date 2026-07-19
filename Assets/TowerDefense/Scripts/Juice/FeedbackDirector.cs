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
        // 19-Jul user ruling: an enemy taking a hit plays ONLY its own per-enemy voice
        // (EnemyDefinitionSO.hurtSfx, the Sounds/new pack) — no generic thock, no shared
        // death grunts. An enemy without a clip is simply silent on hits.
        [SerializeField] private AudioClip _towerHit;        // tower taking damage (throttled)

        [Header("Cards & buildings")]
        [SerializeField] private AudioClip _cardPlayed;      // deploy pop
        [SerializeField] private AudioClip _buildingPlaced;  // heavy thud
        [SerializeField] private AudioClip _knightSpawn;
        [SerializeField] private AudioClip _knightHit;       // knight's sword lands (clash)

        [Header("Spells (played on cast/impact per card id)")]
        [SerializeField] private AudioClip _arrowRain;       // Arrows — volley falls (cast)
        [SerializeField] private AudioClip _fireballBlast;   // Fireball — meteor impact
        [SerializeField] private AudioClip _freeze;          // Frost-ball — nova at landing
        [SerializeField] private AudioClip _earthquake;      // Earthquake — zone opens
        [SerializeField] private AudioClip _logRoll;         // Log — landing + roll
        [SerializeField] private AudioClip _lightning;       // Lightning spell — sky storm

        [Header("Tesla (instant mini-lightning)")]
        [SerializeField] private AudioClip _teslaZap;        // Tesla shot = fire+impact in one crack

        [Header("Flow")]
        [SerializeField] private AudioClip _levelUp;         // checkpoint fanfare note
        [SerializeField] private AudioClip _victory;         // stage complete / campaign victory
        [SerializeField] private AudioClip _defeat;          // tower destroyed

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
        private float _lastWeaponSound;
        private float _lastKnightSound;
        private float _lastTeslaSound;

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

            _events.EnemyHurt += OnEnemyHurt;
            _events.EnemyKilled += OnEnemyKilled;
            _events.TowerDamaged += OnTowerDamaged;
            _events.CardPlayed += OnCardPlayed;
            _events.BuildingPlaced += OnBuildingPlaced;
            _events.KnightSpawned += OnKnightSpawned;
            _events.KnightStruck += OnKnightStruck;
            _events.EnemyAttackImpact += OnEnemyAttackImpact;
            _events.SpellCast += OnSpellCast;
            _events.SpellResolved += OnSpellResolved;
            _events.InstantShotFired += OnInstantShot;   // Tesla mini-lightning
            _events.CheckpointReached += OnCheckpoint;
            _events.MatchEnded += OnMatchEnded;
        }

        private void OnDestroy()
        {
            if (_events == null) return;
            _events.EnemyHurt -= OnEnemyHurt;
            _events.EnemyKilled -= OnEnemyKilled;
            _events.TowerDamaged -= OnTowerDamaged;
            _events.CardPlayed -= OnCardPlayed;
            _events.BuildingPlaced -= OnBuildingPlaced;
            _events.KnightSpawned -= OnKnightSpawned;
            _events.KnightStruck -= OnKnightStruck;
            _events.EnemyAttackImpact -= OnEnemyAttackImpact;
            _events.SpellCast -= OnSpellCast;
            _events.SpellResolved -= OnSpellResolved;
            _events.InstantShotFired -= OnInstantShot;
            _events.CheckpointReached -= OnCheckpoint;
            _events.MatchEnded -= OnMatchEnded;
        }

        // ---------------- handlers ----------------

        private void OnEnemyHurt(EnemyDefinitionSO def, Vector3 position)
        {
            // Only non-fatal hits raise this; the killing blow plays the same voice via
            // OnEnemyKilled. No fallback — the per-enemy voice is THE hit sound (19-Jul).
            // Per-enemy volume (hurtSfxVolume): 19-Jul user ruling — the non-skeleton voices
            // are too loud, so they ship at 0.5. Pitch band kept subtle (±3%).
            if (def == null || def.hurtSfx == null) return;
            if (Time.realtimeSinceStartup - _lastHitSound < 0.1f) return; // anti-stack across a crowd
            _lastHitSound = Time.realtimeSinceStartup;
            Play(def.hurtSfx, 0.5f * def.hurtSfxVolume, 0.97f, 1.03f);
        }

        private void OnEnemyKilled(EnemyKilledArgs args)
        {
            // Killing blow = the same per-enemy voice, pitched down so it reads as the
            // death groan (still the ONLY enemy-hit sound — Feel demo grunts retired).
            if (args.Definition != null && args.Definition.hurtSfx != null)
                Play(args.Definition.hurtSfx, 0.55f * args.Definition.hurtSfxVolume, 0.85f, 0.9f);
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

        private void OnKnightStruck(Vector3 position)
        {
            if (Time.realtimeSinceStartup - _lastKnightSound < 0.1f) return; // 4 knights can swing at once
            _lastKnightSound = Time.realtimeSinceStartup;
            Play(_knightHit, 0.45f, 0.95f, 1.1f);
        }

        private void OnEnemyAttackImpact(EnemyDefinitionSO def, Vector3 position)
        {
            if (def == null || def.attackHitSfx == null) return;
            if (Time.realtimeSinceStartup - _lastWeaponSound < 0.08f) return; // packs swing together
            _lastWeaponSound = Time.realtimeSinceStartup;
            Play(def.attackHitSfx, 0.5f, 0.95f, 1.08f);
        }

        private void OnSpellCast(SpellCardSO card, Vector3 point)
        {
            // Falling-volley sounds start at cast so the whistle rides the fall.
            if (card.id == "Arrows") Play(_arrowRain, 0.8f, 1f, 1.05f);
        }

        private void OnSpellResolved(SpellCardSO card, Vector3 point, System.Collections.Generic.IReadOnlyList<Vector3> hits)
        {
            switch (card.id)
            {
                case "Fireball": Play(_fireballBlast, 0.9f, 0.98f, 1.04f); break;
                case "Freeze": Play(_freeze, 0.8f, 0.98f, 1.04f); break;
                case "Earthquake": Play(_earthquake, 0.85f, 1f, 1f); break;
                case "Log": Play(_logRoll, 0.8f, 0.98f, 1.04f); break;
                case "Lightning": Play(_lightning, 1f, 0.95f, 1.02f); break; // big sky storm
            }
        }

        /// <summary>Tesla fires an instant bolt — one mini-lightning crack (fire + impact together).</summary>
        private void OnInstantShot(Vector3 from, Combat.IEnemyTarget target)
        {
            if (Time.realtimeSinceStartup - _lastTeslaSound < 0.06f) return; // fast fire rate
            _lastTeslaSound = Time.realtimeSinceStartup;
            Play(_teslaZap, 0.55f, 0.96f, 1.06f);
        }

        private void OnCheckpoint(CheckpointReachedArgs args)
        {
            // Stage clears get the big win sting; level-ups keep the fanfare note.
            Play(args.IsStageComplete ? _victory : _levelUp, 0.85f, 1f, 1f);
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

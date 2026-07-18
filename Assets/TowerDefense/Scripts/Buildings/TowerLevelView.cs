using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Core;
using RoyalSiege.Juice;

namespace RoyalSiege.Buildings
{
    /// <summary>
    /// View-only Royal Tower level visuals (never touches gameplay). Holds the 4 pre-placed,
    /// pre-normalised tower level models (index 0 = level 1). 18-Jul CINEMATIC sequence spec:
    /// LEVEL UP — king + mortar rig hide, the OLD tower sinks into the ground under a dust
    /// burst + camera shake, the NEW tower rises out of the ground under a second dust burst
    /// + shake, then king + mortar reappear with the king's Y re-aligned to the new roof —
    /// and only THEN the level-up screen shows (GameEvents.TowerTransitionCompleted gates it).
    /// DEFEAT — the tower goes up in a FIRE BLAST + heavy shake, the ruin crashes in under
    /// the explosion, then the fail screen shows (same completion gate). Runs on UNSCALED
    /// time so it plays while the checkpoint/defeat pause holds the game clock.
    /// </summary>
    public sealed class TowerLevelView : MonoBehaviour
    {
        [Tooltip("Tower models, index 0 = Level 1. Pre-placed children, normalised to the same height/base.")]
        [SerializeField] private Transform[] _levelModels;
        [Tooltip("Celebration flash/sparkle spawned when the NEW tower finishes rising.")]
        [SerializeField] private ParticleSystem _upgradeVfx;
        [Tooltip("Ground dust burst played when a tower sinks/rises (level-up elevator).")]
        [SerializeField] private ParticleSystem _dustVfx;
        [Tooltip("Fire explosion played on DEFEAT as the tower is destroyed.")]
        [SerializeField] private ParticleSystem _failBlastVfx;
        [Tooltip("Old placeholder/model transforms to hide once the level system takes over.")]
        [SerializeField] private Transform[] _hideOnInit;
        [Tooltip("Ruined tower shown on DEFEAT (tower HP 0), normalised like the level models.")]
        [SerializeField] private Transform _failModel;
        [Tooltip("Hidden when the tower is destroyed (e.g. the King on top).")]
        [SerializeField] private Transform[] _hideOnFail;
        [Tooltip("Hidden while the level-up elevator plays (king root + mortar rig), shown again after.")]
        [SerializeField] private Transform[] _hideDuringUpgrade;
        [Tooltip("Seconds for the old tower to sink into the ground.")]
        [SerializeField] private float _sinkSeconds = 1.0f;
        [Tooltip("Seconds for the new tower to rise out of the ground.")]
        [SerializeField] private float _riseSeconds = 1.1f;
        [Tooltip("Continuous camera rumble fed per second while a tower is moving (sink start → rise end). " +
                 "Must exceed CameraShaker's decay (1.4/s) or the shake dies out between the dust beats.")]
        [SerializeField] private float _moveRumblePerSecond = 1.45f;
        [Tooltip("Left-right shake damp during the tower cinematics (0..1): low = mostly vertical ground rumble.")]
        [Range(0f, 1f)] [SerializeField] private float _shakeHorizontalDamp = 0.25f;
        [Tooltip("Beat of stillness BEFORE the old tower starts sinking (and before the defeat blast).")]
        [SerializeField] private float _preDelaySeconds = 0.8f;
        [Tooltip("Beat AFTER the new tower (or ruin) has settled before the screen is allowed to show.")]
        [SerializeField] private float _postDelaySeconds = 0.8f;
        [Tooltip("Scale applied to the defeat fire blast so it covers the WHOLE tower.")]
        [SerializeField] private float _failBlastScale = 2.0f;
        // (_swapSeconds/_flashColor of the old scale-surge removed — superseded by the elevator)
        [Header("King placement (18 Jul — the 4 tower arts have different roof heights)")]
        [Tooltip("KingRoot — auto-repositioned so the king stands on the ACTIVE model's roof.")]
        [SerializeField] private Transform _kingRoot;
        [Tooltip("World-Y offset from the measured roof top to KingRoot (0 = the approved look on the old tower).")]
        [SerializeField] private float _kingHeightOffset;

        private Vector3[] _baseScales;
        private Vector3 _failBaseScale = Vector3.one;
        private int _currentLevel;      // 0 = not yet initialised
        private bool _failed;
        private CameraShaker _shaker;
        private Coroutine _running;
        private GameEvents _events;

        private void CacheScales()
        {
            if (_baseScales != null) return;
            _baseScales = new Vector3[_levelModels.Length];
            for (int i = 0; i < _levelModels.Length; i++)
                _baseScales[i] = _levelModels[i] != null ? _levelModels[i].localScale : Vector3.one;
            if (_failModel != null) _failBaseScale = _failModel.localScale;
        }

        /// <summary>Show a level instantly (no animation) — called once at match start; also
        /// subscribes to MatchEnded so the tower shows its RUIN on defeat (HP 0).</summary>
        public void Init(GameEvents events, int startLevel)
        {
            CacheScales();
            if (_hideOnInit != null)
                foreach (var h in _hideOnInit) if (h != null) h.gameObject.SetActive(false);
            if (_failModel != null) _failModel.gameObject.SetActive(false);
            _shaker = Camera.main != null ? Camera.main.GetComponent<CameraShaker>() : null;
            _currentLevel = Mathf.Clamp(startLevel, 1, _levelModels.Length);
            for (int i = 0; i < _levelModels.Length; i++)
                if (_levelModels[i] != null)
                {
                    _levelModels[i].localScale = _baseScales[i];
                    _levelModels[i].gameObject.SetActive(i == _currentLevel - 1);
                }

            AlignKing(_currentLevel);

            _events = events;
            if (_events != null) _events.MatchEnded += OnMatchEnded;
        }

        /// <summary>
        /// The tower arts have different roof heights — one fixed king height can't fit them
        /// all. Reads the level model's renderer bounds at its BASE scale and drops KingRoot's
        /// Y onto the roof (+ the artistic offset). X/Z stay exactly as authored on the prefab.
        /// </summary>
        private void AlignKing(int level)
        {
            if (_kingRoot == null) return;
            float top = RoofTopY(level);
            if (float.IsNaN(top)) return;
            var p = _kingRoot.position;
            _kingRoot.position = new Vector3(p.x, top + _kingHeightOffset, p.z);
        }

        /// <summary>World Y of the level model's highest point at its base scale (NaN if unknown).</summary>
        private float RoofTopY(int level)
        {
            var model = Get(level);
            if (model == null) return float.NaN;
            // Measure at base scale with the model briefly active (bounds need active renderers).
            Vector3 savedScale = model.localScale;
            bool savedActive = model.gameObject.activeSelf;
            model.localScale = Scale(level);
            if (!savedActive) model.gameObject.SetActive(true);
            float top = float.NaN;
            var renderers = model.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
                top = float.IsNaN(top) ? r.bounds.max.y : Mathf.Max(top, r.bounds.max.y);
            if (!savedActive) model.gameObject.SetActive(false);
            model.localScale = savedScale;
            return top;
        }

        private void OnDestroy()
        {
            if (_events != null) _events.MatchEnded -= OnMatchEnded;
            SetShakeBias(false); // never leave the shared shaker damped if a sequence is cut short
        }

        private void OnMatchEnded(MatchResult result)
        {
            if (!result.Victory) ShowFailed(); // tower HP 0 → ruin
        }

        /// <summary>Collapse the tower into its ruin (defeat). Hides levels + the King.</summary>
        public void ShowFailed()
        {
            CacheScales();
            if (_failed || _failModel == null) return;
            _failed = true;
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(Collapse());
        }

        /// <summary>
        /// 18-Jul defeat cinematic: the tower goes up in a FIRE BLAST + heavy camera shake;
        /// the ruin crashes in UNDER the explosion (the blast masks the swap), then
        /// TowerTransitionCompleted releases the fail screen.
        /// </summary>
        private IEnumerator Collapse()
        {
            _events?.RaiseTowerTransitionStarted();
            SetShakeBias(true); // mostly-vertical quake character, same as the level-up

            // Beat of stillness before the catastrophe — the death registers first.
            yield return WaitUnscaled(_preDelaySeconds);

            // Fire explosion engulfs the WHOLE tower. The blast prefab's systems use
            // scalingMode=Local (root scale is ignored), so the size boost is applied
            // per-system on THIS instance — the shared prefab (Fireball spell) is untouched.
            Vector3 blastPoint = transform.position + Vector3.up * 1.6f;
            if (_failBlastVfx != null)
            {
                var fx = Instantiate(_failBlastVfx, blastPoint, Quaternion.identity);
                float k = Mathf.Max(0.1f, _failBlastScale);
                foreach (var psys in fx.GetComponentsInChildren<ParticleSystem>(true))
                {
                    // Only the volumetric parts grow (fire core, smoke, shockwave, embers).
                    // The wispy highlight sheets and the ground scorch quad turn into big
                    // blocky shapes when enlarged — they stay at their authored size.
                    string n = psys.gameObject.name;
                    if (n.Contains("Highlights") || n.Contains("Scorch")) continue;
                    var m = psys.main;
                    m.startSizeMultiplier *= k;
                    m.startSpeedMultiplier *= 1f + (k - 1f) * 0.7f; // spread grows a bit less than size
                }
                fx.Play(true);
                Destroy(fx.gameObject, 5f);
            }
            _shaker?.AddTrauma(0.85f); // heavy blast

            // A beat inside the fireball before the swap — the explosion hides the cut.
            yield return WaitUnscaled(0.18f);

            foreach (var m in _levelModels) if (m != null) m.gameObject.SetActive(false);
            if (_hideOnFail != null)
                foreach (var h in _hideOnFail) if (h != null) h.gameObject.SetActive(false);

            _failModel.gameObject.SetActive(true);
            // Crash-in: overshoot big → squash-settle to base (ease-out-back on a downward hit).
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.5f;
                float e = EaseOutBack(Mathf.Clamp01(t));
                _failModel.localScale = _failBaseScale * Mathf.Lerp(1.25f, 1f, e);
                yield return null;
            }
            _failModel.localScale = _failBaseScale;

            // Let the fire and the ruin read before the fail screen covers them.
            yield return WaitUnscaled(_postDelaySeconds);

            SetShakeBias(false);
            _running = null;
            _events?.RaiseTowerTransitionCompleted(); // NOW the fail screen may show
        }

        /// <summary>Level changed — animate the seamless upgrade (or init instantly if fresh).
        /// Pass instant=true (resume/retry load) to snap without the surge animation.</summary>
        public void SetLevel(int level, bool instant = false)
        {
            CacheScales();
            if (_failed) return; // ruin is terminal — never resurrect a level model over it
            // LAST-TOWER RULE (18-Jul): the clamp maps any level beyond the art list onto the
            // final model, and the same-art early-return below then SKIPS the whole elevator
            // cinematic (no sink/rise, no dust, no delays — the screen shows immediately).
            // The animation only plays when there IS a next tower art ahead.
            level = Mathf.Clamp(level, 1, _levelModels.Length);
            if (_currentLevel == 0) { Init(_events, level); return; }
            if (level == _currentLevel) return;
            if (_running != null) StopCoroutine(_running);
            if (instant)
            {
                for (int i = 0; i < _levelModels.Length; i++)
                    if (_levelModels[i] != null)
                    {
                        _levelModels[i].localScale = _baseScales[i];
                        _levelModels[i].gameObject.SetActive(i == level - 1);
                    }
                AlignKing(level);
                _running = null;
            }
            else
            {
                _running = StartCoroutine(Upgrade(_currentLevel, level));
            }
            _currentLevel = level;
        }

        /// <summary>Ground dust burst at the tower base + camera shake (every sink/rise beat).</summary>
        private void DustAndShake(float trauma)
        {
            if (_dustVfx != null)
            {
                var fx = Instantiate(_dustVfx, transform.position + Vector3.up * 0.15f, Quaternion.identity);
                fx.Play(true);
                Destroy(fx.gameObject, 4f);
            }
            _shaker?.AddTrauma(trauma);
        }

        /// <summary>Unscaled-time wait usable inside the cinematics (game clock is paused).</summary>
        private static IEnumerator WaitUnscaled(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private void SetShakeBias(bool cinematic)
        {
            if (_shaker != null) _shaker.HorizontalDamp = cinematic ? _shakeHorizontalDamp : 1f;
        }

        /// <summary>
        /// 18-Jul level-up cinematic: king + mortar hide → OLD tower SINKS into the ground
        /// (dust + shake) → NEW tower RISES out of the ground (dust + shake) → king + mortar
        /// return with the king aligned to the new roof → TowerTransitionCompleted (the
        /// level-up screen waits for it).
        /// </summary>
        private IEnumerator Upgrade(int fromLevel, int toLevel)
        {
            _events?.RaiseTowerTransitionStarted();

            Transform oldM = Get(fromLevel), newM = Get(toLevel);

            // Measure the model heights first (world-space renderer bounds at base scale) so
            // each tower sinks/rises by exactly its own height + a safety margin.
            float baseY = transform.position.y;
            float oldDepth = Mathf.Max(2f, RoofTopY(fromLevel) - baseY) + 0.6f;
            float newDepth = Mathf.Max(2f, RoofTopY(toLevel) - baseY) + 0.6f;
            if (float.IsNaN(oldDepth)) oldDepth = 4f;
            if (float.IsNaN(newDepth)) newDepth = 4f;

            // The king and mortar rig vanish for the whole swap (they'd float mid-air).
            if (_hideDuringUpgrade != null)
                foreach (var h in _hideDuringUpgrade) if (h != null) h.gameObject.SetActive(false);

            // Ground-quake shake character: mostly vertical, only a little left-right.
            SetShakeBias(true);

            // Beat of stillness before the ground opens — sells the "something is happening".
            yield return WaitUnscaled(_preDelaySeconds);

            // ---- OLD TOWER SINKS ----
            // Camera rumbles CONTINUOUSLY from here until the rise completes: each moving
            // frame feeds a little trauma so the shake never decays out mid-sequence.
            DustAndShake(0.45f);
            if (oldM != null)
            {
                Vector3 home = oldM.localPosition;
                float t = 0f;
                while (t < 1f)
                {
                    float dt = Time.unscaledDeltaTime;
                    t += dt / Mathf.Max(0.05f, _sinkSeconds);
                    float e = Mathf.Clamp01(t);
                    e = e * e; // ease-in: starts slow, accelerates into the ground
                    oldM.localPosition = home + Vector3.down * (oldDepth * e);
                    _shaker?.AddTrauma(_moveRumblePerSecond * dt); // sustained ground rumble
                    yield return null;
                }
                oldM.gameObject.SetActive(false);
                oldM.localPosition = home; // restore the authored position for future swaps
            }

            // ---- NEW TOWER RISES ----
            DustAndShake(0.45f);
            if (newM != null)
            {
                Vector3 home = newM.localPosition;
                newM.localPosition = home + Vector3.down * newDepth;
                newM.gameObject.SetActive(true);
                float t = 0f;
                while (t < 1f)
                {
                    float dt = Time.unscaledDeltaTime;
                    t += dt / Mathf.Max(0.05f, _riseSeconds);
                    float e = Mathf.Clamp01(t);
                    e = 1f - (1f - e) * (1f - e); // ease-out: bursts up, settles at the top
                    newM.localPosition = home + Vector3.down * (newDepth * (1f - e));
                    _shaker?.AddTrauma(_moveRumblePerSecond * dt); // rumble carries through the rise
                    yield return null;
                }
                newM.localPosition = home;
            }

            // ---- KING + MORTAR RETURN on the finished tower ----
            if (_hideDuringUpgrade != null)
                foreach (var h in _hideDuringUpgrade) if (h != null) h.gameObject.SetActive(true);
            AlignKing(toLevel); // instant snap: king Y = the NEW tower's measured roof height

            // Celebration sparkle at the new rooftop.
            if (_upgradeVfx != null)
            {
                float top = RoofTopY(toLevel);
                Vector3 topPoint = float.IsNaN(top)
                    ? transform.position + Vector3.up * 2.4f
                    : new Vector3(transform.position.x, top, transform.position.z);
                var fx = Instantiate(_upgradeVfx, topPoint, Quaternion.identity);
                fx.Play();
                Destroy(fx.gameObject, 3f);
            }

            // Let the finished tower breathe before the screen covers it.
            yield return WaitUnscaled(_postDelaySeconds);

            SetShakeBias(false);
            _running = null;
            _events?.RaiseTowerTransitionCompleted(); // NOW the level-up screen may show
        }

        private Transform Get(int level) =>
            (level >= 1 && level <= _levelModels.Length) ? _levelModels[level - 1] : null;
        private Vector3 Scale(int level) =>
            (level >= 1 && level <= _baseScales.Length) ? _baseScales[level - 1] : Vector3.one;

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}

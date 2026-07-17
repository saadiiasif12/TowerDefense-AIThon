using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Core;
using RoyalSiege.Juice;

namespace RoyalSiege.Buildings
{
    /// <summary>
    /// View-only Royal Tower level visuals (never touches gameplay). Holds the 4 pre-placed,
    /// pre-normalised tower level models (index 0 = level 1); shows the current one and plays a
    /// juicy SEAMLESS "power surge" when the tower levels up: a light-flash masks the moment,
    /// the old model shrinks out while the new one scale-pops in (they overlap, so the tower is
    /// never empty), a burst VFX fires, and the camera shakes. Runs on UNSCALED time so it plays
    /// even while the checkpoint screen pauses the game clock.
    /// </summary>
    public sealed class TowerLevelView : MonoBehaviour
    {
        [Tooltip("Tower models, index 0 = Level 1. Pre-placed children, normalised to the same height/base.")]
        [SerializeField] private Transform[] _levelModels;
        [Tooltip("Optional burst spawned at the tower top on upgrade (sparkles/flash).")]
        [SerializeField] private ParticleSystem _upgradeVfx;
        [Tooltip("Old placeholder/model transforms to hide once the level system takes over.")]
        [SerializeField] private Transform[] _hideOnInit;
        [Tooltip("Ruined tower shown on DEFEAT (tower HP 0), normalised like the level models.")]
        [SerializeField] private Transform _failModel;
        [Tooltip("Hidden when the tower is destroyed (e.g. the King on top).")]
        [SerializeField] private Transform[] _hideOnFail;
        [SerializeField] private float _swapSeconds = 0.55f;
        [SerializeField] private Color _flashColor = new(1f, 0.92f, 0.55f); // golden

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

            _events = events;
            if (_events != null) _events.MatchEnded += OnMatchEnded;
        }

        private void OnDestroy()
        {
            if (_events != null) _events.MatchEnded -= OnMatchEnded;
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

        private IEnumerator Collapse()
        {
            foreach (var m in _levelModels) if (m != null) m.gameObject.SetActive(false);
            if (_hideOnFail != null)
                foreach (var h in _hideOnFail) if (h != null) h.gameObject.SetActive(false);

            Vector3 topPoint = transform.position + Vector3.up * 2.4f;
            if (_upgradeVfx != null)
            {
                var fx = Instantiate(_upgradeVfx, topPoint, Quaternion.identity);
                var main = fx.main; main.startColor = new Color(0.5f, 0.5f, 0.5f, 1f); // grey dust
                fx.Play();
                Destroy(fx.gameObject, 3f);
            }
            _shaker?.AddTrauma(0.8f); // heavy crash

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
            _running = null;
        }

        /// <summary>Level changed — animate the seamless upgrade (or init instantly if fresh).
        /// Pass instant=true (resume/retry load) to snap without the surge animation.</summary>
        public void SetLevel(int level, bool instant = false)
        {
            CacheScales();
            if (_failed) return; // ruin is terminal — never resurrect a level model over it
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
                _running = null;
            }
            else
            {
                _running = StartCoroutine(Upgrade(_currentLevel, level));
            }
            _currentLevel = level;
        }

        private IEnumerator Upgrade(int fromLevel, int toLevel)
        {
            Transform oldM = Get(fromLevel), newM = Get(toLevel);
            Vector3 oldBase = Scale(fromLevel), newBase = Scale(toLevel);

            Vector3 topPoint = transform.position + Vector3.up * 2.4f;
            if (_upgradeVfx != null)
            {
                var fx = Instantiate(_upgradeVfx, topPoint, Quaternion.identity);
                fx.Play();
                Destroy(fx.gameObject, 3f);
            }
            _shaker?.AddTrauma(0.55f);

            // Golden flash light that blooms then fades — masks the swap for a seamless read.
            var lightGo = new GameObject("TowerUpgradeFlash");
            lightGo.transform.position = topPoint;
            var flash = lightGo.AddComponent<Light>();
            flash.type = LightType.Point; flash.color = _flashColor; flash.range = 14f; flash.intensity = 0f;

            if (newM != null) { newM.localScale = newBase * 0.35f; newM.gameObject.SetActive(true); }

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.05f, _swapSeconds);
                float e = Mathf.Clamp01(t);
                // new model pops UP (ease-out-back), old model shrinks OUT — overlap = seamless.
                if (newM != null) newM.localScale = newBase * Mathf.Max(0.01f, EaseOutBack(e));
                if (oldM != null) oldM.localScale = oldBase * Mathf.Max(0.001f, 1f - Mathf.SmoothStep(0f, 1f, e));
                // flash: fast bloom, slow fade
                flash.intensity = 9f * Mathf.Sin(Mathf.Clamp01(e) * Mathf.PI);
                yield return null;
            }

            if (newM != null) newM.localScale = newBase;
            if (oldM != null) { oldM.localScale = oldBase; oldM.gameObject.SetActive(false); }
            Destroy(lightGo);
            _running = null;
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

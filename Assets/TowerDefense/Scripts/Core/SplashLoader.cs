using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoyalSiege.Core
{
    /// <summary>
    /// 18-Jul splash / loading scene: stretched background + title + PUBG/CoD-style loading
    /// bar with rotating dummy status lines, while the Game scene loads IN PARALLEL
    /// (LoadSceneAsync with delayed activation) — the heavy first-scene load hides behind
    /// the splash like any shipped title. The bar blends REAL load progress with a smooth
    /// time curve so it never stutters or teleports: it always reaches 100% exactly when
    /// the splash hands over. FrameRateSetter lives on the same object so the very first
    /// frames already run at the panel's max refresh rate.
    /// </summary>
    public sealed class SplashLoader : MonoBehaviour
    {
        [SerializeField] private string _gameSceneName = "Game";
        [Tooltip("The splash stays at least this long even if the load finishes instantly.")]
        [SerializeField] private float _minSplashSeconds = 4f;
        [Tooltip("Faded in over the first part of the splash.")]
        [SerializeField] private CanvasGroup _titleGroup;
        [SerializeField] private float _titleFadeSeconds = 0.6f;

        [Header("Loading bar")]
        [Tooltip("Filled-type Image; fillAmount is driven 0→1 across the splash.")]
        [SerializeField] private Image _barFill;
        [SerializeField] private Text _percentLabel;

        [Header("Rotating status lines (dummy, PUBG-style)")]
        [SerializeField] private Text _statusLabel;
        [SerializeField] private float _statusSwapSeconds = 0.7f;
        [SerializeField] private string[] _statusLines =
        {
            "Loading resources...",
            "Preparing battlefield...",
            "Rallying the knights...",
            "Charging the mortar...",
            "Sharpening arrows...",
            "Summoning enemies...",
            "Polishing the crown...",
            "Raising the walls...",
            "Brewing frost magic...",
            "Almost there...",
        };

        private void Start() => StartCoroutine(Run());

        private IEnumerator Run()
        {
            if (_titleGroup != null) _titleGroup.alpha = 0f;
            if (_barFill != null) _barFill.fillAmount = 0f;

            // Nothing competes with the load during a static splash — stream at full speed.
            var previousPriority = Application.backgroundLoadingPriority;
            Application.backgroundLoadingPriority = ThreadPriority.High;

            // Kick the Game scene load immediately — it streams while the splash shows.
            var load = SceneManager.LoadSceneAsync(_gameSceneName, LoadSceneMode.Single);
            load.allowSceneActivation = false;

            float elapsed = 0f;
            float shownProgress = 0f;
            float nextStatusAt = 0f;
            int statusIndex = -1;

            // progress parks at 0.9 until activation is allowed — that IS "ready".
            while (elapsed < _minSplashSeconds || load.progress < 0.9f)
            {
                elapsed += Time.unscaledDeltaTime;

                if (_titleGroup != null)
                    _titleGroup.alpha = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _titleFadeSeconds));

                // Bar: the slower of (time curve, real load) — smooth, monotonic, honest-ish.
                float timeCurve = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _minSplashSeconds));
                float real = Mathf.Clamp01(load.progress / 0.9f);
                shownProgress = Mathf.Max(shownProgress, Mathf.Min(timeCurve, real));
                if (_barFill != null) _barFill.fillAmount = shownProgress;
                if (_percentLabel != null) _percentLabel.text = Mathf.RoundToInt(shownProgress * 100f) + "%";

                // Rotating dummy status lines (sequential — reads like real steps).
                if (_statusLabel != null && elapsed >= nextStatusAt && _statusLines.Length > 0)
                {
                    nextStatusAt = elapsed + _statusSwapSeconds;
                    statusIndex = Mathf.Min(statusIndex + 1, _statusLines.Length - 1);
                    _statusLabel.text = _statusLines[statusIndex];
                }

                yield return null;
            }

            if (_titleGroup != null) _titleGroup.alpha = 1f;
            if (_barFill != null) _barFill.fillAmount = 1f;
            if (_percentLabel != null) _percentLabel.text = "100%";
            if (_statusLabel != null) _statusLabel.text = "Entering the arena!";
            yield return null; // one frame so 100% is actually visible

            Application.backgroundLoadingPriority = previousPriority; // back to normal for gameplay
            load.allowSceneActivation = true; // hand over — Awake/Start of Game run now
        }
    }
}

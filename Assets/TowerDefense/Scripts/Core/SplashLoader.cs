using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoyalSiege.Core
{
    /// <summary>
    /// 18-Jul splash / loading scene: a single stretched background + title, while the Game
    /// scene loads IN PARALLEL (LoadSceneAsync with delayed activation) — the game no longer
    /// hard-hitches on startup; the heavy first-scene load happens behind the splash like any
    /// shipped title. FrameRateSetter lives on the same object so the very first frames
    /// already run at the panel's max refresh rate. A minimum splash hold keeps the title
    /// readable on fast devices; a gentle title fade-in covers the wait on slow ones.
    /// </summary>
    public sealed class SplashLoader : MonoBehaviour
    {
        [SerializeField] private string _gameSceneName = "Game";
        [Tooltip("The splash stays at least this long even if the load finishes instantly.")]
        [SerializeField] private float _minSplashSeconds = 1.5f;
        [Tooltip("Faded in over the first part of the splash.")]
        [SerializeField] private CanvasGroup _titleGroup;
        [SerializeField] private float _titleFadeSeconds = 0.6f;

        private void Start() => StartCoroutine(Run());

        private IEnumerator Run()
        {
            if (_titleGroup != null) _titleGroup.alpha = 0f;

            // Kick the Game scene load immediately — it streams while the splash shows.
            var load = SceneManager.LoadSceneAsync(_gameSceneName, LoadSceneMode.Single);
            load.allowSceneActivation = false;

            float elapsed = 0f;
            // progress parks at 0.9 until activation is allowed — that IS "ready".
            while (elapsed < _minSplashSeconds || load.progress < 0.9f)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_titleGroup != null)
                    _titleGroup.alpha = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _titleFadeSeconds));
                yield return null;
            }

            if (_titleGroup != null) _titleGroup.alpha = 1f;
            load.allowSceneActivation = true; // hand over — Awake/Start of Game run now
        }
    }
}

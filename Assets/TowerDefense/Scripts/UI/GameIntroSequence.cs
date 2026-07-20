using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using RoyalSiege.Core;

namespace RoyalSiege.UI
{
    /// <summary>
    /// Juicy match-open cinematic (splash → game): the sim is held while the camera pushes in
    /// on the King then eases OUT to the play framing, the HUD header slides down + footer
    /// slides up, and the hand deals its cards in one by one. Fires <see cref="Completed"/>
    /// when done (the tutorial hooks it). All unscaled-time so it plays over the paused sim.
    /// View-only: it never touches gameplay beyond pausing/unpausing the clock.
    ///
    /// Refs resolve by name at runtime (the HUD is a prefab, so scene refs can't be baked):
    /// header = "TopBar", footer background = "BottomBanner", card row = "Slots".
    /// </summary>
    public sealed class GameIntroSequence : MonoBehaviour
    {
        [SerializeField] private GameContext _context;
        [SerializeField] private Camera _camera;

        [Header("Camera zoom")]
        [Tooltip("Orthographic size the intro starts at (close on the King). Authored size is captured as the target.")]
        [SerializeField] private float _closeOrthoSize = 5.5f;
        [SerializeField] private float _holdOnKingSeconds = 0.45f;
        [SerializeField] private float _zoomSeconds = 2.2f;

        [Header("HUD reveal")]
        [SerializeField] private float _headerSlideSeconds = 0.5f;
        [SerializeField] private float _footerSlideSeconds = 0.55f;
        [SerializeField] private float _cardDealStagger = 0.13f;
        [SerializeField] private float _cardPopSeconds = 0.3f;
        [Tooltip("Swipe/whoosh played as each card deals in.")]
        [SerializeField] private AudioClip _cardDealSwipe;
        [SerializeField, Range(0f, 1f)] private float _swipeVolume = 0.5f;

        private AudioSource _audio;

        /// <summary>Raised (once) when the whole intro finishes and the sim resumes.</summary>
        public event Action Completed;
        public bool IsComplete { get; private set; }

        private IClock _clock;
        private float _authoredOrtho;

        private RectTransform _header;   // TopBar
        private RectTransform _footer;   // BottomBanner
        private RectTransform[] _cards;  // the 4 hand slots — each deals in with a swipe (4 sounds total)
        private RectTransform[] _extras; // Next slot + energy bar — pop in silently
        private Vector2 _headerHome, _footerHome;

        private void Start()
        {
            if (_context == null) _context = FindFirstObjectByType<GameContext>();
            if (_camera == null) _camera = Camera.main;
            if (_context == null || _camera == null) { Finish(); return; }

            _clock = _context.Clock;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false; _audio.spatialBlend = 0f;
            ResolveHud();

            // Pause the whole sim (waves/decay/economy all hold behind the cinematic).
            _clock.IsPaused = true;

            // Camera: remember the play framing, snap to the close-on-King push-in.
            _authoredOrtho = _camera.orthographicSize;
            _camera.orthographicSize = _closeOrthoSize;

            // Hide the HUD off-screen so it can slide in.
            if (_header != null) { _headerHome = _header.anchoredPosition; _header.anchoredPosition = _headerHome + Vector2.up * (_header.rect.height + 40f); }
            if (_footer != null) { _footerHome = _footer.anchoredPosition; _footer.anchoredPosition = _footerHome + Vector2.down * (_footer.rect.height + 60f); }
            if (_cards != null) foreach (var c in _cards) if (c != null) c.localScale = Vector3.zero;
            if (_extras != null) foreach (var c in _extras) if (c != null) c.localScale = Vector3.zero;

            StartCoroutine(Run());
        }

        private void ResolveHud()
        {
            _header = FindRect("TopBar");
            _footer = FindRect("BottomBanner");
            // The 4 hand cards deal in with a swipe; Next slot + energy bar pop in silently.
            var cards = new System.Collections.Generic.List<RectTransform>();
            var slotsRoot = FindTransform("Slots");
            if (slotsRoot != null) foreach (Transform s in slotsRoot) cards.Add((RectTransform)s);
            _cards = cards.ToArray();

            var list = new System.Collections.Generic.List<RectTransform>();
            AddIf(list, "NextSlot");
            AddIf(list, "EnergyBar");
            _extras = list.ToArray();
        }

        private IEnumerator Run()
        {
            // Strict beat order (user spec): hold on King → zoom out → footer up → header down
            // → cards deal one by one → tutorial.
            yield return WaitUnscaled(_holdOnKingSeconds);
            yield return ZoomOut();
            yield return SlideIn(_footer, _footerHome, _footerSlideSeconds);
            yield return SlideIn(_header, _headerHome, _headerSlideSeconds);
            yield return DealCards();
            Finish();
        }

        private IEnumerator ZoomOut()
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, _zoomSeconds);
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f); // ease-out cubic
                _camera.orthographicSize = Mathf.Lerp(_closeOrthoSize, _authoredOrtho, e);
                yield return null;
            }
            _camera.orthographicSize = _authoredOrtho;
        }

        private IEnumerator SlideIn(RectTransform rt, Vector2 home, float seconds)
        {
            if (rt == null) yield break;
            Vector2 from = rt.anchoredPosition;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds);
                float e = EaseOutBack(Mathf.Clamp01(t));
                rt.anchoredPosition = Vector2.LerpUnclamped(from, home, e);
                yield return null;
            }
            rt.anchoredPosition = home;
        }

        private IEnumerator DealCards()
        {
            // Next slot + energy bar appear silently first (no swipe).
            if (_extras != null) foreach (var e in _extras) if (e != null) StartCoroutine(PopIn(e));

            if (_cards == null) yield break;
            for (int i = 0; i < _cards.Length; i++)
            {
                if (_cards[i] != null)
                {
                    StartCoroutine(PopIn(_cards[i]));
                    if (_cardDealSwipe != null && _audio != null) _audio.PlayOneShot(_cardDealSwipe, _swipeVolume); // one swipe per card = 4 total
                }
                yield return WaitUnscaled(_cardDealStagger);
            }
            yield return WaitUnscaled(_cardPopSeconds); // let the last card settle before the tutorial
        }

        private IEnumerator PopIn(RectTransform rt)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, _cardPopSeconds);
                rt.localScale = Vector3.one * EaseOutBack(Mathf.Clamp01(t));
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        private void Finish()
        {
            if (IsComplete) return;
            IsComplete = true;
            if (_camera != null) _camera.orthographicSize = _authoredOrtho > 0.01f ? _authoredOrtho : _camera.orthographicSize;
            if (_header != null) _header.anchoredPosition = _headerHome;
            if (_footer != null) _footer.anchoredPosition = _footerHome;
            if (_cards != null) foreach (var c in _cards) if (c != null) c.localScale = Vector3.one;
            if (_extras != null) foreach (var c in _extras) if (c != null) c.localScale = Vector3.one;
            if (_clock != null) _clock.IsPaused = false; // gameplay begins
            Completed?.Invoke();
        }

        // ---- helpers ----
        private static IEnumerator WaitUnscaled(float s) { float t = 0f; while (t < s) { t += Time.unscaledDeltaTime; yield return null; } }

        private void AddIf(System.Collections.Generic.List<RectTransform> list, string name)
        { var t = FindRect(name); if (t != null) list.Add(t); }

        private static RectTransform FindRect(string name) => FindTransform(name) as RectTransform;

        private static Transform FindTransform(string name)
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (t.name == name) return t;
            return null;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}

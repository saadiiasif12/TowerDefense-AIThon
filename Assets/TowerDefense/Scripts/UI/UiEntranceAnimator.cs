using System.Collections.Generic;
using UnityEngine;

namespace RoyalSiege.UI
{
    /// <summary>
    /// Staggered pop-in for overlay panels (checkpoint / match-end screens): every time the
    /// panel is enabled, each ACTIVE direct child scales up with an ease-out-back pop and
    /// fades in, one after another in hierarchy order. Runs on UNSCALED time — these screens
    /// show while the sim is paused. Zero per-frame allocations (entries pooled in a list);
    /// scales/alphas are restored on disable so an interrupted intro never leaves a child
    /// half-sized or invisible.
    /// </summary>
    public sealed class UiEntranceAnimator : MonoBehaviour
    {
        [SerializeField] private float _stagger = 0.08f;
        [SerializeField] private float _duration = 0.32f;
        [SerializeField] private float _startScale = 0.55f;

        private struct Entry
        {
            public RectTransform Rect;
            public CanvasGroup Fade;
            public Vector3 BaseScale;
            public float Delay;
        }

        private readonly List<Entry> _entries = new();
        private float _elapsed;
        private bool _playing;

        private void OnEnable()
        {
            _entries.Clear();
            _elapsed = 0f;
            _playing = true;

            int slot = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                if (!(child is RectTransform rect)) continue;

                var fade = child.GetComponent<CanvasGroup>();
                if (fade == null) fade = child.gameObject.AddComponent<CanvasGroup>();

                var entry = new Entry { Rect = rect, Fade = fade, BaseScale = rect.localScale, Delay = slot * _stagger };
                rect.localScale = entry.BaseScale * _startScale;
                fade.alpha = 0f;
                _entries.Add(entry);
                slot++;
            }
        }

        private void OnDisable()
        {
            // Never leave children mid-pop: an interrupted intro restores instantly.
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.Rect == null) continue;
                e.Rect.localScale = e.BaseScale;
                if (e.Fade != null) e.Fade.alpha = 1f;
            }
            _entries.Clear();
            _playing = false;
        }

        private void Update()
        {
            if (!_playing) return;
            _elapsed += Time.unscaledDeltaTime;

            bool allDone = true;
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.Rect == null) continue;
                float t = Mathf.Clamp01((_elapsed - e.Delay) / _duration);
                if (t < 1f) allDone = false;
                e.Rect.localScale = e.BaseScale * Mathf.LerpUnclamped(_startScale, 1f, EaseOutBack(t));
                e.Fade.alpha = Mathf.Clamp01(t * 2f); // fade lands in the first half of the pop
            }
            if (allDone) _playing = false; // idle after the intro — no per-frame cost
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}

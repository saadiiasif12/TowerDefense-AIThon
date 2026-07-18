using System;
using UnityEngine;
using UnityEngine.UI;

namespace RoyalSiege.UI
{
    /// <summary>
    /// The mini card that follows the finger during a drag (spec: zero-lag follow, scale
    /// driven by POSITION not time — 100% at the hand shrinking to 50% toward the board).
    /// 17-Jul UI pass: shows the real card art inside the blue Frame with a lightning cost
    /// badge. On a successful deploy it dissolves in ~120 ms; on cancel it tweens home.
    /// Built once at runtime by HandBarView; one instance is enough (single-touch rule).
    /// </summary>
    public sealed class CardDragProxy : MonoBehaviour
    {
        private const float DissolveSeconds = 0.12f;
        private const float ReturnSeconds = 0.18f;

        private RectTransform _rect;
        private RectTransform _canvasRect;
        private CanvasGroup _group;
        private Image _art;
        private Image _frame;
        private Image _gem;
        private Text _cost;

        private float _dissolveT = -1f;
        private float _returnT = -1f;
        private Vector2 _returnFrom;
        private Vector2 _returnTo;
        private Action _onReturned;

        // 18-Jul CR-style drag: fixed drag scale + fade toward the battlefield preview.
        private float _dragScale = 1.12f;
        private float _fadeTarget = 1f;
        private float _fadeSpeed = 12f;   // 1/duration, set per fade
        private float _fade = 1f;
        private Vector2 _velocity;         // SmoothDamp state (no per-frame alloc)
        private float _smoothTime;

        public static CardDragProxy Create(RectTransform canvasRect, Font font)
        {
            var go = new GameObject("CardDragProxy", typeof(RectTransform), typeof(CanvasGroup));
            var proxy = go.AddComponent<CardDragProxy>();
            proxy._canvasRect = canvasRect;
            proxy._rect = (RectTransform)go.transform;
            proxy._rect.SetParent(canvasRect, false);
            proxy._rect.sizeDelta = new Vector2(165f, 210f);
            proxy._group = go.GetComponent<CanvasGroup>();
            proxy._group.blocksRaycasts = false;
            proxy._group.interactable = false;

            // Art inside the frame window (same insets as CardSlotView).
            proxy._art = Sub(proxy._rect, "Art", new Vector2(0.075f, 0.225f), new Vector2(0.925f, 0.815f));
            proxy._frame = Sub(proxy._rect, "Frame", Vector2.zero, Vector2.one);

            var badge = new GameObject("Cost", typeof(RectTransform));
            var badgeRect = (RectTransform)badge.transform;
            badgeRect.SetParent(proxy._rect, false);
            badgeRect.anchorMin = new Vector2(0.5f, 0f); badgeRect.anchorMax = new Vector2(0.5f, 0f);
            badgeRect.sizeDelta = new Vector2(70f, 46f);
            badgeRect.anchoredPosition = new Vector2(0f, 20f);
            proxy._gem = Sub(badgeRect, "Gem", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            proxy._gem.rectTransform.sizeDelta = new Vector2(26f, 42f);
            proxy._gem.rectTransform.anchoredPosition = new Vector2(16f, 0f);
            proxy._cost = MakeText(badgeRect, font, 34);

            go.SetActive(false);
            return proxy;
        }

        private static Image Sub(RectTransform parent, string name, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = aMin; rect.anchorMax = aMax;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        private static Text MakeText(RectTransform parent, Font font, int size)
        {
            var go = new GameObject("Num", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(34f, 0f); rect.offsetMax = Vector2.zero;
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(1f, 0.85f, 0.2f);
            text.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        public void Show(Sprite art, Sprite frame, Sprite gem, string cost, Vector2 screenPosition,
            float dragScale = 1.12f, float smoothTime = 0f)
        {
            _dissolveT = -1f;
            _returnT = -1f;
            _fade = 1f; _fadeTarget = 1f;
            _dragScale = dragScale;
            _smoothTime = smoothTime;
            _velocity = Vector2.zero;
            _group.alpha = 1f;
            _art.sprite = art; _art.enabled = art != null; _art.color = Color.white;
            _frame.sprite = frame;
            _gem.sprite = gem;
            _cost.text = cost;
            gameObject.SetActive(true);
            _rect.anchoredPosition = ScreenToCanvas(screenPosition);
            _rect.localScale = Vector3.one * _dragScale;
        }

        /// <summary>
        /// Follow the (already finger-offset) screen position. Direct tracking, or a very
        /// small SmoothDamp when a smoothTime was configured — responsive, never floaty.
        /// </summary>
        public void Follow(Vector2 screenPosition)
        {
            if (_returnT >= 0f || _dissolveT >= 0f) return;
            Vector2 target = ScreenToCanvas(screenPosition);
            _rect.anchoredPosition = _smoothTime <= 0f
                ? target
                : Vector2.SmoothDamp(_rect.anchoredPosition, target, ref _velocity, _smoothTime,
                    float.PositiveInfinity, Time.unscaledDeltaTime);
            _rect.localScale = Vector3.one * (_dragScale * Mathf.Lerp(0.92f, 1f, _fade));
            _group.alpha = _fade;
        }

        /// <summary>Fade toward 0 when the pointer is over the battlefield (the world preview
        /// takes over) and back to 1 over the HUD. Duration from config.</summary>
        public void SetFieldFade(bool overField, float duration)
        {
            _fadeTarget = overField ? 0f : 1f;
            _fadeSpeed = duration > 0.001f ? 1f / duration : 1000f;
        }

        public void Dissolve() => _dissolveT = 0f;

        public void ReturnTo(Vector2 slotScreenPosition, Action onReturned)
        {
            _returnT = 0f;
            _returnFrom = _rect.anchoredPosition;
            _returnTo = ScreenToCanvas(slotScreenPosition);
            _onReturned = onReturned;
        }

        public void HideImmediate()
        {
            _dissolveT = -1f;
            _returnT = -1f;
            _onReturned = null;
            gameObject.SetActive(false);
        }

        private Vector2 ScreenToCanvas(Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPosition, null, out Vector2 local);
            return local;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Field fade integrates every frame (drag keeps calling Follow for pos/scale).
            if (_dissolveT < 0f && _returnT < 0f && !Mathf.Approximately(_fade, _fadeTarget))
                _fade = Mathf.MoveTowards(_fade, _fadeTarget, _fadeSpeed * dt);

            if (_dissolveT >= 0f)
            {
                _dissolveT += dt / DissolveSeconds;
                _group.alpha = 1f - _dissolveT * _dissolveT;
                if (_dissolveT >= 1f) HideImmediate();
                return;
            }

            if (_returnT >= 0f)
            {
                _returnT += dt / ReturnSeconds;
                float e = 1f - (1f - Mathf.Clamp01(_returnT)) * (1f - Mathf.Clamp01(_returnT));
                _rect.anchoredPosition = Vector2.Lerp(_returnFrom, _returnTo, e);
                _rect.localScale = Vector3.one * Mathf.Lerp(_rect.localScale.x, 1f, e);
                if (_returnT >= 1f)
                {
                    var done = _onReturned;
                    HideImmediate();
                    done?.Invoke();
                }
            }
        }
    }
}

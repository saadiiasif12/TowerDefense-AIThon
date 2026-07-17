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

        public void Show(Sprite art, Sprite frame, Sprite gem, string cost, Vector2 screenPosition)
        {
            _dissolveT = -1f;
            _returnT = -1f;
            _group.alpha = 1f;
            _art.sprite = art; _art.enabled = art != null; _art.color = Color.white;
            _frame.sprite = frame;
            _gem.sprite = gem;
            _cost.text = cost;
            gameObject.SetActive(true);
            Follow(screenPosition, 0f);
        }

        /// <summary>Zero-lag follow; scale/alpha are pure functions of position (the shrink01 input).</summary>
        public void Follow(Vector2 screenPosition, float shrink01)
        {
            if (_returnT >= 0f || _dissolveT >= 0f) return;
            _rect.anchoredPosition = ScreenToCanvas(screenPosition);
            float scale = Mathf.Lerp(1f, 0.5f, shrink01);
            _rect.localScale = Vector3.one * scale;
            _group.alpha = Mathf.Lerp(1f, 0.9f, shrink01);
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

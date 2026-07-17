using System;
using UnityEngine;
using UnityEngine.UI;

namespace RoyalSiege.UI
{
    /// <summary>
    /// The mini card that follows the finger during a drag (spec: zero-lag follow, scale
    /// driven by POSITION not time — 100% at the hand shrinking to 50% toward the board).
    /// On a successful deploy it dissolves in ~120 ms; on cancel it tweens back to its slot.
    /// Built once at runtime by HandBarView; one instance is enough (single-touch rule).
    /// </summary>
    public sealed class CardDragProxy : MonoBehaviour
    {
        private const float DissolveSeconds = 0.12f;
        private const float ReturnSeconds = 0.18f;

        private RectTransform _rect;
        private RectTransform _canvasRect;
        private CanvasGroup _group;
        private Image _background;
        private Text _name;
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
            proxy._rect.sizeDelta = new Vector2(190f, 240f);
            proxy._group = go.GetComponent<CanvasGroup>();
            proxy._group.blocksRaycasts = false;
            proxy._group.interactable = false;

            var borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
            var borderRect = (RectTransform)borderGo.transform;
            borderRect.SetParent(proxy._rect, false);
            borderRect.anchorMin = Vector2.zero; borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-6f, -6f); borderRect.offsetMax = new Vector2(6f, 6f);
            borderGo.GetComponent<Image>().raycastTarget = false;

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var bgRect = (RectTransform)bgGo.transform;
            bgRect.SetParent(proxy._rect, false);
            bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;
            proxy._background = bgGo.GetComponent<Image>();
            proxy._background.raycastTarget = false;

            proxy._name = CreateText(proxy._rect, font, 30, new Vector2(0f, 20f));
            proxy._cost = CreateText(proxy._rect, font, 40, new Vector2(0f, -78f));
            proxy._cost.color = new Color(1f, 0.9f, 0.3f);

            go.SetActive(false);
            return proxy;
        }

        private static Text CreateText(RectTransform parent, Font font, int size, Vector2 offset)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = offset;
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        public void Show(Color cardColor, string cardName, string cost, Vector2 screenPosition)
        {
            _dissolveT = -1f;
            _returnT = -1f;
            _group.alpha = 1f;
            _background.color = cardColor;
            _name.text = cardName;
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
                _group.alpha = 1f - _dissolveT * _dissolveT; // ease-in fade
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

using UnityEngine;
using UnityEngine.UI;

namespace RoyalSiege.UI
{
    /// <summary>
    /// The card's name spawned as floating text at the deploy point: drifts up ~70 px while
    /// shrinking, fading in the last third (spec §4). One pooled instance per concurrent play.
    /// </summary>
    public sealed class FloatingCardLabel : MonoBehaviour
    {
        private const float LifeSeconds = 1.7f;
        private const float RisePx = 70f;

        private RectTransform _rect;
        private RectTransform _canvasRect;
        private Text _text;
        private float _t = 2f;
        private Vector2 _start;

        public bool IsFree => _t >= 1f;

        public static FloatingCardLabel Create(RectTransform canvasRect, Font font)
        {
            var go = new GameObject("FloatingCardLabel", typeof(RectTransform), typeof(Text));
            var label = go.AddComponent<FloatingCardLabel>();
            label._canvasRect = canvasRect;
            label._rect = (RectTransform)go.transform;
            label._rect.SetParent(canvasRect, false);
            label._rect.sizeDelta = new Vector2(400f, 80f);
            label._text = go.GetComponent<Text>();
            label._text.font = font;
            label._text.fontSize = 44;
            label._text.fontStyle = FontStyle.Bold;
            label._text.alignment = TextAnchor.MiddleCenter;
            label._text.raycastTarget = false;
            go.SetActive(false);
            return label;
        }

        public void Play(string content, Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPosition, null, out _start);
            _text.text = content;
            _t = 0f;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_t >= 1f) { gameObject.SetActive(false); return; }
            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / LifeSeconds);

            float rise = 1f - (1f - _t) * (1f - _t); // fast ease-out drift
            _rect.anchoredPosition = _start + Vector2.up * (RisePx * rise);
            _rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.8f, _t);

            float alpha = _t < 0.66f ? 1f : 1f - (_t - 0.66f) / 0.34f;
            var c = _text.color;
            _text.color = new Color(c.r, c.g, c.b, alpha);
        }
    }
}

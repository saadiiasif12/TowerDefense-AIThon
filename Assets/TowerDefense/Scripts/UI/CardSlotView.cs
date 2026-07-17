using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RoyalSiege.Data;

namespace RoyalSiege.UI
{
    /// <summary>
    /// One hand slot, animated per the card-animation spec:
    ///  - idle is completely static — all motion is saved for interaction;
    ///  - select: border + glow on the SAME frame as the touch, ease-out-back pop to 1.08 with a lift;
    ///  - while dragged: the slot shows a recessed dark panel with a faint name watermark (slots never reflow);
    ///  - refill: 0.8 → 1.05 → 1.0 pop in under 100 ms;
    ///  - unaffordable: art desaturates, cost badge stays colored, tap gives a decaying shake.
    /// All card visuals live on a runtime "Lift" container so layout groups never fight the tweens.
    /// </summary>
    public sealed class CardSlotView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private Image _background;
        [SerializeField] private Text _nameLabel;
        [SerializeField] private Text _costLabel;
        [SerializeField] private Image _cooldownOverlay;
        [SerializeField] private CanvasGroup _group;

        private static readonly Color BuildingColor = new(0.85f, 0.7f, 0.45f);
        private static readonly Color SpellColor = new(0.55f, 0.65f, 0.95f);
        private static readonly Color RecessColor = new(0.1f, 0.1f, 0.13f, 0.92f);

        private const float SelectScale = 1.08f;
        private const float SelectLiftPx = 10f;
        private const float SelectInSeconds = 0.1f;
        private const float SelectOutSeconds = 0.16f;
        private const float RefillSeconds = 0.09f;
        private const float ShakeSeconds = 0.15f;
        private const float ShakePx = 4f;

        private int _slot = -1;
        private HandBarView _owner;
        private CardDefinitionSO _card;
        private Color _cardColor = Color.white;
        private bool _affordable = true;

        private RectTransform _lift;      // runtime container holding all card visuals
        private Image _border;
        private Image _recessPanel;
        private Text _watermark;

        private bool _selected;
        private float _selectT = 1f;
        private float _refillT = 1f;
        private float _shakeT = 1f;
        private bool _carried;
        private CardDefinitionSO _pendingCard;
        private bool _hasPendingCard;

        public bool IsCarried => _carried;
        public CardDefinitionSO Card => _card;
        public RectTransform Rect => (RectTransform)transform;

        public void Init(int slot, HandBarView owner)
        {
            _slot = slot;
            _owner = owner;
            EnsureRuntimeParts();
        }

        private void EnsureRuntimeParts()
        {
            if (_lift != null) return;

            var root = (RectTransform)transform;

            // Recessed empty-slot panel + watermark (revealed while the card is dragged).
            _recessPanel = CreateImage(root, "Recess", RecessColor);
            _recessPanel.rectTransform.SetSiblingIndex(0);
            _watermark = Object.Instantiate(_nameLabel, _recessPanel.rectTransform);
            var wmRect = _watermark.rectTransform;
            wmRect.anchorMin = Vector2.zero; wmRect.anchorMax = Vector2.one;
            wmRect.offsetMin = Vector2.zero; wmRect.offsetMax = Vector2.zero;
            _watermark.alignment = TextAnchor.MiddleCenter;
            _watermark.color = new Color(1f, 1f, 1f, 0.13f);
            _recessPanel.gameObject.SetActive(false);

            // Lift container: move the authored card visuals inside so pops/lifts never
            // fight the hand layout — the slot rect itself never moves (spec rule).
            var liftGo = new GameObject("Lift", typeof(RectTransform));
            _lift = (RectTransform)liftGo.transform;
            _lift.SetParent(root, false);
            _lift.anchorMin = Vector2.zero; _lift.anchorMax = Vector2.one;
            _lift.offsetMin = Vector2.zero; _lift.offsetMax = Vector2.zero;

            var toMove = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in root)
                if (child != _lift && child != _recessPanel.rectTransform) toMove.Add(child);
            foreach (var child in toMove) child.SetParent(_lift, true);

            // Selection border: behind the card content, slightly oversized.
            _border = CreateImage(_lift, "Border", Color.white);
            _border.rectTransform.SetSiblingIndex(0);
            _border.rectTransform.offsetMin = new Vector2(-6f, -6f);
            _border.rectTransform.offsetMax = new Vector2(6f, 6f);
            _border.gameObject.SetActive(false);
        }

        private static Image CreateImage(RectTransform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // ---------------- content ----------------

        public void SetCard(CardDefinitionSO card)
        {
            if (_carried) { _pendingCard = card; _hasPendingCard = true; return; }
            ApplyCard(card);
        }

        private void ApplyCard(CardDefinitionSO card)
        {
            _card = card;
            if (card == null)
            {
                _nameLabel.text = "";
                _costLabel.text = "";
                if (_watermark != null) _watermark.text = "";
                return;
            }
            _nameLabel.text = card.displayName;
            _costLabel.text = card.cost.ToString("0");
            _cardColor = card is BuildingCardSO ? BuildingColor : SpellColor;
            _background.color = _cardColor;
            if (_watermark != null) _watermark.text = card.displayName;
        }

        public void SetState(float cooldown01, bool affordable)
        {
            if (_cooldownOverlay != null) _cooldownOverlay.fillAmount = cooldown01;
            bool playable = affordable && cooldown01 <= 0f;
            if (playable == _affordable) return;
            _affordable = playable;

            // Desaturate the ART, keep the cost badge colored so the reason stays readable.
            if (_background != null)
                _background.color = playable ? _cardColor : Desaturate(_cardColor);
            if (_nameLabel != null)
                _nameLabel.color = playable ? Color.white : new Color(0.75f, 0.75f, 0.75f);
            if (_group != null) _group.alpha = 1f;
        }

        private static Color Desaturate(Color c)
        {
            float grey = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            return new Color(grey, grey, grey, c.a);
        }

        // ---------------- interaction states ----------------

        /// <summary>Border + pop, same frame as the touch.</summary>
        public void Select()
        {
            if (_border == null) EnsureRuntimeParts();
            _selected = true;
            _selectT = 0f;
            _border.gameObject.SetActive(true);
        }

        public void Deselect()
        {
            if (!_selected) return;
            _selected = false;
            _selectT = 0f;
            if (_border != null) _border.gameObject.SetActive(false);
        }

        /// <summary>The card left with the finger — reveal the recessed empty slot.</summary>
        public void SetCarried(bool carried)
        {
            if (_carried == carried) return;
            _carried = carried;
            _lift.gameObject.SetActive(!carried);
            _recessPanel.gameObject.SetActive(carried);

            if (!carried)
            {
                Deselect();
                if (_hasPendingCard)
                {
                    ApplyCard(_pendingCard);
                    _hasPendingCard = false;
                    PlayRefillPop();
                }
            }
        }

        public void PlayRefillPop() => _refillT = 0f;

        public void ShakeUnaffordable() => _shakeT = 0f;

        private void Update()
        {
            if (_lift == null) return;
            float dt = Time.unscaledDeltaTime;

            // Select pop / release.
            float selectScale;
            float lift;
            if (_selected)
            {
                _selectT = Mathf.Min(1f, _selectT + dt / SelectInSeconds);
                float e = EaseOutBack(_selectT);
                selectScale = Mathf.LerpUnclamped(1f, SelectScale, e);
                lift = Mathf.LerpUnclamped(0f, SelectLiftPx, e);
            }
            else
            {
                _selectT = Mathf.Min(1f, _selectT + dt / SelectOutSeconds);
                float e = 1f - (1f - _selectT) * (1f - _selectT); // ease-out
                selectScale = Mathf.Lerp(SelectScale, 1f, e);
                lift = Mathf.Lerp(SelectLiftPx, 0f, e);
            }

            // Refill pop: 0.8 → 1.05 → 1.0.
            float refillScale = 1f;
            if (_refillT < 1f)
            {
                _refillT = Mathf.Min(1f, _refillT + dt / RefillSeconds);
                refillScale = _refillT < 0.6f
                    ? Mathf.Lerp(0.8f, 1.05f, _refillT / 0.6f)
                    : Mathf.Lerp(1.05f, 1f, (_refillT - 0.6f) / 0.4f);
            }

            // Unaffordable shake: decaying horizontal oscillation.
            float shakeX = 0f;
            if (_shakeT < 1f)
            {
                _shakeT = Mathf.Min(1f, _shakeT + dt / ShakeSeconds);
                shakeX = Mathf.Sin(_shakeT * 24f) * ShakePx * (1f - _shakeT);
            }

            _lift.localScale = Vector3.one * (selectScale * refillScale);
            _lift.anchoredPosition = new Vector2(shakeX, lift);
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        // ---------------- pointer forwarding ----------------

        public void OnPointerDown(PointerEventData e) { if (_slot >= 0) _owner.OnSlotPointerDown(_slot, e.position); }
        public void OnDrag(PointerEventData e) { if (_slot >= 0) _owner.OnSlotDrag(_slot, e.position); }
        public void OnPointerUp(PointerEventData e) { if (_slot >= 0) _owner.OnSlotPointerUp(_slot, e.position); }
    }
}

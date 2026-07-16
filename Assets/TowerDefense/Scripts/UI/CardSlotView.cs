using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RoyalSiege.Data;

namespace RoyalSiege.UI
{
    /// <summary>
    /// One hand slot: name + cost + radial cooldown + affordability grey-out.
    /// Forwards pointer events to HandBarView, which drives the PlacementController.
    /// Greybox visuals — skinned on Day 3.
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

        private int _slot = -1;
        private HandBarView _owner;

        public void Init(int slot, HandBarView owner)
        {
            _slot = slot;
            _owner = owner;
        }

        public void SetCard(CardDefinitionSO card)
        {
            if (card == null)
            {
                _nameLabel.text = "";
                _costLabel.text = "";
                return;
            }
            _nameLabel.text = card.displayName;
            _costLabel.text = card.cost.ToString("0");
            _background.color = card is BuildingCardSO ? BuildingColor : SpellColor;
        }

        public void SetState(float cooldown01, bool affordable)
        {
            _cooldownOverlay.fillAmount = cooldown01;
            _group.alpha = affordable && cooldown01 <= 0f ? 1f : 0.55f;
        }

        public void OnPointerDown(PointerEventData e) { if (_slot >= 0) _owner.OnSlotPointerDown(_slot, e.position); }
        public void OnDrag(PointerEventData e) { if (_slot >= 0) _owner.OnSlotDrag(_slot, e.position); }
        public void OnPointerUp(PointerEventData e) { if (_slot >= 0) _owner.OnSlotPointerUp(_slot, e.position); }
    }
}

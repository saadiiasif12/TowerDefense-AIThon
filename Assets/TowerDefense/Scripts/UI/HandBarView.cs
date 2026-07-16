using UnityEngine;
using RoyalSiege.Core;
using RoyalSiege.Placement;

namespace RoyalSiege.UI
{
    /// <summary>
    /// The 4-card hand + NEXT preview. Reads DeckService/CardCooldowns/EnergyBank through
    /// GameContext, forwards slot drags to the PlacementController. Implements the 40 px
    /// drag threshold: the ghost/placement only starts once the pointer moved far enough,
    /// a plain tap-and-release does nothing (tooltip later).
    /// </summary>
    public sealed class HandBarView : MonoBehaviour
    {
        [SerializeField] private GameContext _context;
        [SerializeField] private PlacementController _placement;
        [SerializeField] private CardSlotView[] _slots = new CardSlotView[4];
        [SerializeField] private CardSlotView _nextSlot;

        private Vector2 _pressPosition;
        private int _pressedSlot = -1;
        private bool _dragStarted;

        private void Start()
        {
            for (int i = 0; i < _slots.Length; i++) _slots[i].Init(i, this);
            _context.Events.HandChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_context != null && _context.Events != null) _context.Events.HandChanged -= Refresh;
        }

        private void Refresh()
        {
            for (int i = 0; i < _slots.Length; i++) _slots[i].SetCard(_context.Deck.Hand[i]);
            _nextSlot.SetCard(_context.Deck.NextPreview);
        }

        private void Update()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                var card = _context.Deck.Hand[i];
                if (card == null) continue;
                _slots[i].SetState(_context.Cooldowns.Remaining01(card), _context.Energy.CanAfford(card.cost));
            }
            var next = _context.Deck.NextPreview;
            if (next != null) _nextSlot.SetState(_context.Cooldowns.Remaining01(next), true);
        }

        public void OnSlotPointerDown(int slot, Vector2 position)
        {
            _pressedSlot = slot;
            _pressPosition = position;
            _dragStarted = false;
        }

        public void OnSlotDrag(int slot, Vector2 position)
        {
            if (slot != _pressedSlot) return;

            if (!_dragStarted && Vector2.Distance(position, _pressPosition) >= _context.GameConfig.dragThresholdPx)
            {
                _dragStarted = true;
                _placement.BeginDrag(slot);
            }
            if (_dragStarted) _placement.UpdateDrag(position);
        }

        public void OnSlotPointerUp(int slot, Vector2 position)
        {
            if (_dragStarted) _placement.EndDrag(position);
            _pressedSlot = -1;
            _dragStarted = false;
        }
    }
}

using UnityEngine;
using RoyalSiege.Cards;
using RoyalSiege.Core;
using RoyalSiege.Data;
using RoyalSiege.Placement;

namespace RoyalSiege.UI
{
    /// <summary>
    /// The 4-card hand + NEXT preview, animated per the card-animation spec: fixed slots
    /// that never reflow, a zero-lag drag proxy that shrinks toward the board (position-
    /// driven), dissolve + floating name label on deploy, tween-back on cancel, refill pops
    /// on hand changes, and an unaffordable shake on dead taps. Implements the 40 px drag
    /// threshold: the ghost/placement only starts once the pointer moved far enough.
    /// </summary>
    public sealed class HandBarView : MonoBehaviour
    {
        [SerializeField] private GameContext _context;
        [SerializeField] private PlacementController _placement;
        [SerializeField] private CardSlotView[] _slots = new CardSlotView[4];
        [SerializeField] private CardSlotView _nextSlot;

        private static readonly Color BuildingColor = new(0.85f, 0.7f, 0.45f);
        private static readonly Color SpellColor = new(0.55f, 0.65f, 0.95f);

        private Vector2 _pressPosition;
        private int _pressedSlot = -1;
        private bool _dragStarted;
        private bool _playCommitted;

        private readonly CardDefinitionSO[] _lastHand = new CardDefinitionSO[DeckService.HandSize];
        private bool _firstRefresh = true;
        private CardDragProxy _proxy;
        private FloatingCardLabel _floatingLabel;

        private void Start()
        {
            for (int i = 0; i < _slots.Length; i++) _slots[i].Init(i, this);

            var canvas = GetComponentInParent<Canvas>();
            var canvasRect = (RectTransform)canvas.transform;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _proxy = CardDragProxy.Create(canvasRect, font);
            _floatingLabel = FloatingCardLabel.Create(canvasRect, font);

            _context.Events.HandChanged += Refresh;
            _context.Events.CardPlayed += OnCardPlayed;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_context == null || _context.Events == null) return;
            _context.Events.HandChanged -= Refresh;
            _context.Events.CardPlayed -= OnCardPlayed;
        }

        private void OnCardPlayed(CardDefinitionSO card)
        {
            if (_dragStarted) _playCommitted = true;
        }

        private void Refresh()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                var card = _context.Deck.Hand[i];
                bool changed = !ReferenceEquals(card, _lastHand[i]);
                _slots[i].SetCard(card);
                // Refill pop only on real changes — never on the initial deal, never while
                // the slot is carried (SetCarried(false) plays the pending pop instead).
                if (changed && !_firstRefresh && !_slots[i].IsCarried) _slots[i].PlayRefillPop();
                _lastHand[i] = card;
            }
            _nextSlot.SetCard(_context.Deck.NextPreview);
            _firstRefresh = false;
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

            // Zero-latency feedback, same frame as the touch (spec's #1 rule).
            if (_context.CardPlay.CanPlay(slot)) _slots[slot].Select();
            else _slots[slot].ShakeUnaffordable();
        }

        public void OnSlotDrag(int slot, Vector2 position)
        {
            if (slot != _pressedSlot) return;

            if (!_dragStarted && Vector2.Distance(position, _pressPosition) >= _context.GameConfig.dragThresholdPx)
            {
                bool wasDragging = _placement.IsDragging;
                _placement.BeginDrag(slot);
                if (!wasDragging && _placement.IsDragging)
                {
                    _dragStarted = true;
                    var card = _context.Deck.Hand[slot];
                    _slots[slot].SetCarried(true);
                    _proxy.Show(card is BuildingCardSO ? BuildingColor : SpellColor,
                        card.displayName, card.cost.ToString("0"), position);
                }
            }

            if (_dragStarted)
            {
                _placement.UpdateDrag(position);
                // Position-driven shrink: 100% at the hand → 50% approaching the board.
                float shrink = Mathf.Clamp01((position.y - _pressPosition.y) / (Screen.height * 0.33f));
                _proxy.Follow(position, shrink);
            }
        }

        public void OnSlotPointerUp(int slot, Vector2 position)
        {
            if (_dragStarted)
            {
                var played = _context.Deck.Hand[slot]; // capture BEFORE the hand cycles
                _playCommitted = false;
                _placement.EndDrag(position); // raises CardPlayed synchronously on success

                if (_playCommitted)
                {
                    _proxy.Dissolve();
                    if (played != null) _floatingLabel.Play(played.displayName, position);
                    _slots[slot].SetCarried(false); // applies pending card + refill pop
                }
                else
                {
                    // Invalid spot: the mini card flies home, then the slot restores (no pop).
                    var slotView = _slots[slot];
                    Vector2 slotScreen = RectTransformUtility.WorldToScreenPoint(null, slotView.Rect.position);
                    _proxy.ReturnTo(slotScreen, () => slotView.SetCarried(false));
                }
            }
            else if (_pressedSlot >= 0)
            {
                _slots[_pressedSlot].Deselect();
            }

            _pressedSlot = -1;
            _dragStarted = false;
        }
    }
}

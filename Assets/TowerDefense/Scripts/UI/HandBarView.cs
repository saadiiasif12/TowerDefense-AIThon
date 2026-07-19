using UnityEngine;
using RoyalSiege.Cards;
using RoyalSiege.Core;
using RoyalSiege.Data;
using RoyalSiege.Placement;

namespace RoyalSiege.UI
{
    /// <summary>
    /// The 4-card hand + NEXT preview. 18-Jul CR-style interaction pass (all values in
    /// CardInteractionAnimationConfig, all UI time UNSCALED):
    ///  · pickup: same-frame select + lift; proxy card floats a finger-offset above the touch
    ///    at dragScale with optional micro-smoothing,
    ///  · HUD ↔ battlefield handoff: crossing the hand panel's top edge fades the floating
    ///    card out and the world preview in (and back),
    ///  · release over the HUD = clean cancel (return tween + landing squash, nothing spent),
    ///  · release over the field = single authoritative commit via CardPlayService; the used
    ///    slot HOLDS EMPTY and the Next-Up card flies in after refillDelay (DeckCycleAnimator),
    ///  · pointer OWNERSHIP: only the finger that picked the card can move/release it,
    ///  · focus loss / match end cancel any live drag and finish cycle flights instantly.
    /// Gameplay stays authoritative: cost/cooldown at release (GDD), deck FIFO untouched.
    /// </summary>
    public sealed class HandBarView : MonoBehaviour
    {
        [SerializeField] private GameContext _context;
        [SerializeField] private PlacementController _placement;
        [SerializeField] private CardSlotView[] _slots = new CardSlotView[4];
        [SerializeField] private CardSlotView _nextSlot;
        [SerializeField] private CardInteractionAnimationConfig _animConfig;

        [Header("Drag-proxy sprites (card parts are preset on the slot prefabs)")]
        [SerializeField] private Sprite _frameSprite;      // Frame.png (card border)
        [SerializeField] private Sprite _gemSprite;        // icon_mana.png (lightning cost gem)

        private Vector2 _pressPosition;
        private int _pressedSlot = -1;
        private int _pressedPointerId = int.MinValue;      // multi-touch ownership
        private bool _dragStarted;
        private bool _playCommitted;
        private bool _matchOver;
        private bool _overField;
        private float _handTopScreenY;
        private int _cachedScreenWidth, _cachedScreenHeight;
        private int _lockedSlot = -1;   // tutorial input gate: -1 = free, else only this slot responds

        /// <summary>Tutorial gate: only <paramref name="slot"/> can be picked up (others ignored). -1 clears.</summary>
        public void LockToSlot(int slot) => _lockedSlot = slot;
        public void Unlock() => _lockedSlot = -1;

        private readonly CardDefinitionSO[] _lastHand = new CardDefinitionSO[DeckService.HandSize];
        private bool _firstRefresh = true;
        private Canvas _canvas;
        private CardDragProxy _proxy;
        private FloatingCardLabel _floatingLabel;
        private DeckCycleAnimator _deckCycle;

        private bool NextPreviewHeld => _deckCycle != null && _deckCycle.AnyActive;

        private void Start()
        {
            // HUD prefab can't serialize scene refs — resolve the GameContext at runtime.
            if (_context == null) _context = FindFirstObjectByType<GameContext>();
            for (int i = 0; i < _slots.Length; i++) _slots[i].Init(i, this, _animConfig);
            _nextSlot.Init(-1, this, _animConfig);

            _canvas = GetComponentInParent<Canvas>();
            var canvasRect = (RectTransform)_canvas.transform;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _proxy = CardDragProxy.Create(canvasRect);
            _floatingLabel = FloatingCardLabel.Create(canvasRect, font);
            _deckCycle = DeckCycleAnimator.Create(canvasRect, font, _animConfig,
                _frameSprite, _gemSprite, _slots, _nextSlot);

            _context.Events.HandChanged += Refresh;
            _context.Events.CardPlayed += OnCardPlayed;
            _context.Events.MatchEnded += OnMatchEnded;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_context == null || _context.Events == null) return;
            _context.Events.HandChanged -= Refresh;
            _context.Events.CardPlayed -= OnCardPlayed;
            _context.Events.MatchEnded -= OnMatchEnded;
        }

        /// <summary>Drag interrupted from outside (focus loss, match end): clean cancel.</summary>
        private void CancelActiveDrag()
        {
            if (_dragStarted)
            {
                _placement.CancelDrag();
                _proxy.HideImmediate();
                if (_pressedSlot >= 0) _slots[_pressedSlot].SetCarried(false);
            }
            else if (_pressedSlot >= 0)
            {
                _slots[_pressedSlot].Deselect();
            }
            _pressedSlot = -1;
            _pressedPointerId = int.MinValue;
            _dragStarted = false;
            _overField = false;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) CancelActiveDrag(); // spec M: focus loss mid-drag cancels cleanly
        }

        /// <summary>
        /// QA 17-Jul (DT-009): the battle is over — abort any live selection/drag so the
        /// victory/defeat panel takes the screen cleanly, and ignore hand input from now on.
        /// </summary>
        private void OnMatchEnded(MatchResult result)
        {
            _matchOver = true;
            CancelActiveDrag();
            _deckCycle?.CompleteAllInstantly(); // no flyers under the end panel
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
                // the slot is carried/held (the deck-cycle flight presents those).
                if (changed && !_firstRefresh && !_slots[i].IsCarried) _slots[i].PlayRefillPop();
                _lastHand[i] = card;
            }
            // While a cycle flight is airborne the ANIMATOR owns the Next-Up visual — it
            // advances the preview during the final 30% of the flight (spec J).
            if (!NextPreviewHeld && !_dragStarted)
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
            if (next != null && !NextPreviewHeld) _nextSlot.SetState(_context.Cooldowns.Remaining01(next), true);
        }

        /// <summary>Screen-space Y of the hand panel's top edge (battlefield begins above it).</summary>
        private float HandTopScreenY
        {
            get
            {
                if (_cachedScreenWidth != Screen.width || _cachedScreenHeight != Screen.height)
                {
                    _cachedScreenWidth = Screen.width;
                    _cachedScreenHeight = Screen.height;
                    float top = 0f;
                    for (int i = 0; i < _slots.Length; i++)
                    {
                        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, _slots[i].Rect.position);
                        float half = _slots[i].Rect.rect.height * 0.5f * _canvas.scaleFactor;
                        top = Mathf.Max(top, screen.y + half);
                    }
                    float margin = (_animConfig != null ? _animConfig.fieldBoundaryMarginPixels : 30f) * _canvas.scaleFactor;
                    _handTopScreenY = top + margin;
                }
                return _handTopScreenY;
            }
        }

        public void OnSlotPointerDown(int slot, Vector2 position, int pointerId)
        {
            if (_matchOver) return;
            if (_lockedSlot >= 0 && slot != _lockedSlot) return; // tutorial: only the taught card responds
            if (_pressedSlot >= 0) return; // another finger already owns a card
            _pressedSlot = slot;
            _pressedPointerId = pointerId;
            _pressPosition = position;
            _dragStarted = false;
            _overField = false;

            // Zero-latency feedback, same frame as the touch (spec's #1 rule).
            if (_context.CardPlay.CanPlay(slot)) _slots[slot].Select();
            else _slots[slot].ShakeUnaffordable();
        }

        public void OnSlotDrag(int slot, Vector2 position, int pointerId)
        {
            if (_matchOver || slot != _pressedSlot || pointerId != _pressedPointerId) return;

            if (!_dragStarted && Vector2.Distance(position, _pressPosition) >= _context.GameConfig.dragThresholdPx)
            {
                // QA 17-Jul (DT-010): an unaffordable/cooling card never enters the
                // placement state — it already shook on the tap. Cost is still re-validated
                // at release (GDD ruling) for the affordable card that started the drag.
                if (!_context.CardPlay.CanPlay(slot)) return;

                bool wasDragging = _placement.IsDragging;
                _placement.BeginDrag(slot);
                if (!wasDragging && _placement.IsDragging)
                {
                    _dragStarted = true;
                    // Clone the slot's live card visual BEFORE hiding it, so the floating card is
                    // pixel-identical to the tray card (art window / cost badge never reflow).
                    var lift = _slots[slot].LiftRect;
                    var cardSize = _slots[slot].CardSize;
                    _slots[slot].SetCarried(true);
                    float dragScale = _animConfig != null ? _animConfig.dragScale : 1.12f;
                    float smooth = _animConfig != null ? _animConfig.dragSmoothTime : 0f;
                    _proxy.Show(lift, cardSize, OffsetAboveFinger(position), dragScale, smooth);
                }
            }

            if (_dragStarted)
            {
                _placement.UpdateDrag(position);
                _proxy.Follow(OffsetAboveFinger(position));

                // HUD ↔ battlefield handoff: floating card and world preview crossfade.
                bool overField = position.y > HandTopScreenY;
                if (overField != _overField)
                {
                    _overField = overField;
                    float fade = _animConfig != null ? _animConfig.hudToFieldFadeDuration : 0.08f;
                    _proxy.SetFieldFade(overField, fade);
                    _placement.SetFieldHover(overField);
                }
            }
        }

        public void OnSlotPointerUp(int slot, Vector2 position, int pointerId)
        {
            if (_matchOver) return;
            if (slot != _pressedSlot || pointerId != _pressedPointerId) return;

            if (_dragStarted)
            {
                bool overField = position.y > HandTopScreenY;
                if (!overField)
                {
                    // Released back over the HUD / own slot: clean cancel — nothing spent,
                    // deck untouched (spec H).
                    _placement.CancelDrag();
                    ReturnProxyToSlot(slot);
                }
                else
                {
                    var played = _context.Deck.Hand[slot]; // capture BEFORE the hand cycles
                    _playCommitted = false;
                    _placement.EndDrag(position); // raises CardPlayed synchronously on success

                    if (_playCommitted)
                    {
                        _proxy.Dissolve();
                        if (played != null) _floatingLabel.Play(played.displayName, position);
                        // The used slot HOLDS EMPTY; the Next-Up card flies in after the
                        // delay. Deck already cycled — capture the authoritative results.
                        _slots[slot].ReleaseCarriedHoldEmpty();
                        _deckCycle.Enqueue(slot, _context.Deck.Hand[slot], _context.Deck.NextPreview);
                    }
                    else
                    {
                        ReturnProxyToSlot(slot);
                    }
                }
            }
            else if (_pressedSlot >= 0)
            {
                _slots[_pressedSlot].Deselect();
            }

            _pressedSlot = -1;
            _pressedPointerId = int.MinValue;
            _dragStarted = false;
            _overField = false;
        }

        /// <summary>Invalid spot / HUD release: the mini card flies home, slot restores + squash.</summary>
        private void ReturnProxyToSlot(int slot)
        {
            var slotView = _slots[slot];
            Vector2 slotScreen = RectTransformUtility.WorldToScreenPoint(null, slotView.Rect.position);
            _proxy.ReturnTo(slotScreen, () =>
            {
                slotView.SetCarried(false);
                slotView.PlayLandingSquash();
            });
        }

        /// <summary>The floating card rides a finger-offset above the touch (reference px → screen).</summary>
        private Vector2 OffsetAboveFinger(Vector2 screenPosition)
        {
            float offset = (_animConfig != null ? _animConfig.dragFingerOffsetPixels : 96f) * _canvas.scaleFactor;
            return screenPosition + Vector2.up * offset;
        }
    }
}

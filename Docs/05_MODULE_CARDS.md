# 05 · Module: Cards (RoyalSiege.Cards)

**Status: CODE COMPLETE (UI pending) — 16 Jul (see TASKS.md)**

## Responsibilities
- `DeckService` — shuffle once at match start (only RNG, seedable); ring buffer: hand[4] + queue[3] (7 cards). `Play(slot)` → card to queue back, queue front into that exact slot. Exposes `NextPreview` (queue front).
- `CardCooldowns` — per-card (not per-slot) cooldown timers, ticked at 10 Hz. A card can re-enter hand while cooling.
- `PlayCardValidator` — affordable (energy) ∧ off cooldown ∧ placement valid (delegates to Placement) → `PlayCardCommand` executes: spend energy, start cooldown, cycle, hand off to Placement/Spells.
- Emits `CardPlayed`, `HandChanged` events for UI.

## Rules (from design)
- All 7 cards live from t=0 — no unlocks. No evolutions (cut). Cost validated at RELEASE of the drag, not at drag start.
- Opening hand with no building is legal — cheap spell cycling is the intended answer.

## Acceptance
- Unit-style check: after 7 plays the hand is back to the original 4 in the same slots; playing while replacement is cooling shows greyed card with running radial.

## Notes / changes
- (log changes here)

# 09 · Module: Spells & Placement (RoyalSiege.Spells / RoyalSiege.Placement)

**Status: CODE COMPLETE (touch-drag via UI pending) — 16 Jul (see TASKS.md)**

## Spells (strategy pattern)
- `SpellCaster` — given a `SpellCardSO` + world point: gathers enemies whose center is inside radius AND inside the map circle (r=15), applies the card's `SpellEffectSO`.
- Effects:
  - `AreaDamageEffectSO` — Arrows (80/r4.0), Fireball (260/r2.5). Flat damage to all gathered.
  - `FreezeEffectSO` — 4 s move+attack stop (2 s vs boss); duration refreshes, never stacks; frozen enemies still damageable.
  - `LightningEffectSO` — 275 to the **3 highest-HP** gathered + 0.5 s stun.
- **No one-shot invariant:** no effect may take a full-HP enemy to 0 (validated in Data module tests).

## Placement (drag-to-play)
- Drag card past 40 px threshold → ghost appears 1.5 tiles above fingertip; single-touch lock.
- **Buildings:** ghost snaps to 0.5 grid; valid ⇔ inside deployment circle (r5.0) ∧ <4 buildings on field ∧ no overlap ∧ affordable — green/red tint. Release on valid → spend, spawn, cycle card. Release invalid → card returns, nothing spent.
- **Spells:** valid anywhere inside map circle; radius preview ring shows exactly the strict-range truth.
- Input: pointer events (Input System), mouse + touch unified.

## Acceptance
- Arrows on a goblin pack leaves them all at 10 HP; Fireball leaves a Mage at 30; Lightning picks the ogre over goblins; Freeze halts a wave 4 s and boss 2 s; can't place a 5th building or overlap; cancelled drag refunds nothing because nothing was spent.

## Notes / changes
- (log changes here)

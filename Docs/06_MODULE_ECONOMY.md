# 06 · Module: Economy (RoyalSiege.Economy)

**Status: CODE COMPLETE — 16 Jul (see TASKS.md)**

## Responsibilities
- `EnergyBank : IEnergyBank` — float energy, start 9, hard cap 10. `TrySpend(cost)`, `Credit(amount)` (clamped at cap, overflow WASTED). Emits `EnergyChanged`.
- `OrbSpawner` — on `EnemyKilled`: spawn pooled orb at corpse, fly to energy bar anchor over 0.55 s (curve), **credit on arrival** (this delay is the feel of the economy — never credit instantly).
- Wave-clear bonus: on `WaveCleared` → `Credit(+3)` unconditionally.
- **No passive regen. Ever.** If someone asks, point at GDD v2 §4.

## Edge cases (decided)
- Orb in flight while bar full → still flies, clamps on arrival.
- Orb in flight at match end → cosmetic, ignored for outcome.
- Enemy marked dead immediately at 0 HP → exactly one orb per enemy.

## Acceptance
- Idle over 60 s: energy unchanged. Kill goblin: +0.25 arrives ~0.55 s later. At 10.0, further orbs waste. Wave clear always +3.

## Notes / changes
- (log changes here)

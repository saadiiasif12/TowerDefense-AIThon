# Royal Siege — Config Reference (v2.0)

Every number in the game lives in the **CFG block at the top of `royal-siege.html`** (the embedded equivalent of the four original JSON files: `game`, `cards`, `enemies`, `waves`). Gameplay code contains zero hardcoded stats — balance iterations are still 30-second data edits.

## Config sections

| Section | Contains |
|---|---|
| `game` | Royal Tower stats, Energy collection economy, map/placement rules, global difficulty multipliers, win condition, UX constants |
| `cards` | All **8** player cards (4 buildings, 4 spells) with cost, cooldown, damage, range, HP, splash, and `evo` definitions |
| `enemies` | All 3 enemy types + boss, with Energy **drop** values (`bounty` field), plus AI targeting settings |
| `waves` | The full 12-wave timeline: absolute spawn times, compositions, spawn points |

## Units & conventions

- **Distances** in tiles, **times** in seconds, **damage/HP** in hit points, **Energy** as floats (UI renders a filling bar with 10 ticks).
- `projectileSpeed: -1` means instant hit (Tesla).
- `splashRadius` on a building (Bomb Tower) = full projectile damage to every enemy within that radius of impact.
- `spawnPoint` indices: 0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW, evenly spaced on the map edge (radius 15).
- Waves spawn at their `startTime` **unconditionally** — the anti-stall mechanism. Never gate waves on the previous wave being cleared.
- Spawn cadence: units in a group spawn **0.11 s** apart; in-wave `groupDelay` values run at **×0.7** in code.
- **Range checks are strict (v2):** tower and building attacks require enemy **center** distance ≤ `range`. Spells only affect enemies whose center is inside the map circle.

## Energy economy (v2 — collection, not regen)

- `regenPerSecond*` fields are **gone**. There is no passive Energy income.
- On death an enemy drops an orb worth its `bounty` (× `globalEnemyBountyMultiplier`); the orb flies for `energy.orbFlightSeconds` (0.55) and credits on arrival.
- Drops: Goblin **0.25** · Mage **0.5** · Ogre **1.0** · Boss **3.0**. *(The original spec named a "skeleton" for the 0.25 tier; this config has no skeleton, so the smallest drop maps to the smallest unit — the Goblin. To re-map, edit each enemy's `bounty`.)*
- `energy.starting` = **9**, `energy.max` = 10 (hard cap; orbs collected at cap are wasted).
- `energy.waveClearBonus` = **+3**, granted **unconditionally** on clearing a wave (the v1 "before next spawn" gate was removed).

## Card cycle & evolutions (v2)

- Deck = all 8 cards, available from t=0 (`unlockWave` no longer exists; the `unlockCards` arrays in `waves` are ignored).
- Match start: one shuffle → first 4 = hand, remaining 4 = queue. **This shuffle is the game's only RNG.**
- Ring buffer: playing a card pushes it to the back of the queue and pulls the queue front into the same hand slot. Per-card `cooldown` still applies wherever the card is.
- `evo` on a card = Evolution card: `{cycle, ...}` — after `cycle` plays (pips UI), the next play is evolved, then the counter resets.
  - Cannon `evo:{cycle:2, hpMult:1.5, dmgMult:1.3}` → 1050 HP / 110.5 dmg.
  - Arrows `evo:{cycle:2, damage:88, radius:5.5}`.

## How to tune

1. **First knob (v2): `difficulty.globalEnemyDamageMultiplier`** — currently **0.5**. It compensates for strict ranges + faster spawns + the leaner economy. Winning runs ending above ~85% tower HP → raise toward 0.6. Frequent wave-8/9 collapses → lower toward 0.45.
2. **Second knob: `difficulty.globalEnemyHpMultiplier`** (1.0). ⚠ Do **not** drop it below **0.89** or Arrows (80) starts one-shotting Goblins (90×mult), silently breaking the no-perfect-counter rule. Lightning (275) vs Mage (290) breaks even earlier — below ~0.95.
3. **Third knob:** wave `startTime` gaps and group counts in `waves` (more breather / fewer bodies = easier), or `energy.waveClearBonus` / `energy.starting` for economy-side relief.
4. **Per-card fixes:** if Bomb Tower trivializes swarms, lower its `splashRadius` to 1.2 before touching damage. If Mage sieges feel unanswerable, raise X-Bow's appeal by cutting its `cooldown`, not by re-extending building ranges.

## Key invariants — don't break these while tuning

- **No one-shots:** Arrows (80) < Goblin HP (90); Fireball (260) < Mage HP (290); Lightning (275) < Mage HP (290); Evo Arrows (88) < 90; Bomb Tower splash (80) < 90. An enemy dies only at 0 HP.
- **No passive regen** — all income is drops + wave-clear bonuses. If you re-add regen you must re-derate everything else.
- Deck must stay at **8 cards** (4-hand + 1 next + 3 hidden is a fixed layout).
- Tower total DPS (~93) alone must NOT clear waves (verified: idle player dies wave 4). If you buff the tower, re-check this.
- X-Bow range (9) + deployment radius (5) < map radius (15): nothing hits spawn points at frame 1.
- Freeze duration (4) < Freeze cooldown (14): freeze can never chain. Boss freeze stays 2 s.
- Strict range: keep `dist(center, center) <= range` checks — reverting to edge-based checks makes every range ring lie by ~1–2 tiles.
- Mark enemies dead immediately on 0 HP and fizzle in-flight projectiles at dead targets, or drops get double-counted.

## Verification harness (how v2 numbers were validated)

Two headless Node harnesses drive the real game code with a stubbed DOM:
- `harness.js` — a scripted player (4 buildings, cluster-aimed spells). v2 result: ~75% win rate across random opening hands, wins between 3% and 72% tower HP, losses at wave 9.
- `harness2.js` — invariant unit tests: idle-player loses (wave 4), all no-one-shot margins, orb drop → delayed credit → no regen over idle time, boss freeze 2 s, strict tower range (no damage at 7.4, damage at 6.5), 8-card ring buffer cycles back after 8 plays, evolution fires on the 3rd play with correct stats, bomb splash hits neighbors.

Re-run both after any tuning pass.

# 01 · Current Design — AUTHORITATIVE

Base: `Assets/TowerDefense/Documents/GDD_Royal_Siege_v2.md` (read it — it is excellent and detailed), **modified by the 16-July meeting notes** (notebook photo). This file records the deltas and the resulting final spec. When this file and the GDD disagree, THIS file wins.

> ⚠ `Assets/TowerDefense/Documents/*.json` are **v1** (passive-regen economy, wave unlocks) — outdated. Do not use them. Author ScriptableObjects from the tables below.

## Design deltas (17 July, verbal)

6. **No ranged enemies for now.** The "Mage" slot (Skeleton Knight model) is a MELEE unit: attackRange 0.6, no projectile, no splash. Its other stats (HP 290, dmg 90, rate 1.6, speed 1.2, bounty 0.5) are unchanged.
7. **Territory-engage rule (future-proofing):** any enemy may only ATTACK once it is within `engageRadiusFromCenter` (12) of the map center — if ranged enemies return later, they must enter the arena before throwing, never stand off at the outskirts.
8. Balance watch: wave 9 (the "Mage-siege exam") is now melee pressure instead — X-Bow's anti-siege role is reduced; revisit if X-Bow feels dead weight.
9. **Wave pacing compressed twice** (user: too much downtime). Current start times: **0/11/23/36/50/64/80/96/114/132/151/172** — boss at 2:52, run ≈3½–4 min. The wave table below shows the ORIGINAL v2 times; `Waves_Level1.asset` is authoritative for timing. HUD shows a live "NEXT IN Xs" countdown in the header.
   **How to tune yourself:** select `Assets/TowerDefense/Data/Waves_Level1.asset` → Inspector → `Waves` list → each element's **Start Time** is the absolute second that wave spawns (unconditionally). Per-group `Delay After Wave Start` staggers groups inside a wave; `Unit Spawn Interval` (0.11) is the gap between units in a group; `Group Delay Multiplier` (0.7) globally scales group delays. Edits apply on the NEXT play (the schedule is built at match start).

10. **Royal Tower fires ONE attack (17 Jul):** the built-in cannon is disabled (damage 0, range 0.01 in GameConfig) and its DPS folded into the king attack — **king: 93 dmg / 1.0 s / range 7** (total tower DPS ≈93, unchanged; idle-dies-wave-4 invariant preserved). Side effect: goblins (90 HP) die to one king hit instead of two — acceptable, tower attacks were never under the no-one-shot rule (spells only).

## Meeting deltas (16 July, notebook)

1. **Bomb Tower is CUT** ("Card naming: no bomb tower"). Deck is now **7 cards**.
2. **Buildings (3):** Tesla, Cannon, X-Bow
3. **Spells (4):** Lightning, Arrows, Freeze, Fireball
4. **No card upgrading system for now** → the v2 Evolution mechanic (Cannon/Arrows evo pips) is cut. Cards are flat.
5. **Range issue** → the v2 strict-range rule stands and must be implemented correctly from day one: attacks/spells only affect enemies whose **center** is inside the circle (`dist(center,center) <= range`, inclusive). The visual ring is exactly the truth.

## Consequences of the deltas (decided, tune later)

- **Deck 7 → hand 4 + NEXT preview 1 + 2 hidden in queue.** Ring buffer unchanged: play a card → to the back of the queue → queue front fills the used slot. Cycle returns after 7 plays.
- **Anti-swarm duty** moves fully to Arrows + Tesla + tower splash-less DPS. Watch wave 10 (16 goblins) in playtests; first relief knobs: Arrows cooldown 6→5, or wave-10 pack sizes.
- Without evolutions, late-game power curve is flatter → keep `globalEnemyDamageMultiplier = 0.5` as shipped and retune upward only if winners end >85% HP.

## Final card table (7 cards)

| Card | Type | Cost⚡ | Cooldown | Damage | Range/Radius | Rate | HP | Notes |
|---|---|---|---|---|---|---|---|---|
| Cannon | Building | 3 | 8 s | 85 | 5.5 | 1.0 s | 700 | Cheapest DPS workhorse (anchor of all balance) |
| Tesla | Building | 4 | 10 s | 110 | 5.0 | 1.1 s | 600 | Instant hit (no projectile), 0.2 s retarget |
| X-Bow | Building | 6 | 20 s | 30 | **9.0** | 0.25 s | 900 | 120 DPS extreme range; the Mage-siege answer |
| Arrows | Spell | 3 | 6 s | 80 area | 4.0 | instant | — | Leaves Goblins at 10 HP — buildings finish |
| Fireball | Spell | 4 | 9 s | 260 area | 2.5 | instant | — | Leaves Mage at 30 HP |
| Freeze | Spell | 4 | 14 s | 0 | 3.0 | 4 s (boss 2 s) | — | Stops move+attack; refreshes, never stacks |
| Lightning | Spell | 6 | 16 s | 275 ×3 | 3.5 | instant +0.5 s stun | — | Hits 3 highest-HP in radius |

**Placement:** buildings inside deployment circle (radius 5.0, snap 0.5, max 4 on field, no overlap, cost validated at release). Spells anywhere on map (radius 15). Green/red ghost preview, drag threshold 40 px, ghost 1.5 tiles above fingertip.

## Enemies (unchanged from GDD v2 §6)

| Enemy | HP | Speed | Damage* | Rate | Range | Priority | Drop⚡ |
|---|---|---|---|---|---|---|---|
| Goblin | 90 | 2.0 | 50 | 1.1 s | melee 0.5 | closest structure | 0.25 |
| Mage | 290 | 1.2 | 90 (0.8 splash) | 1.6 s | **5.0** | closest structure | 0.5 |
| Ogre | 950 | 0.8 | 160 | 1.5 s | melee 0.8 | **buildings first** | 1.0 |
| Ogre Warlord (boss) | 3200 | 0.7 | 200 | 1.6 s | melee 1.0 | buildings first | 3.0 |

\* × `globalEnemyDamageMultiplier` **0.5**. Boss ground-slam every 8 s: 100 dmg in 2.5 radius, telegraphed. Boss freeze duration 2 s.

## Economy (GDD v2 §4 — kill-collection)

- **No passive regen.** Start **9**, cap **10** (hard, overflow wasted, bar pulses gold at cap).
- Kill → orb flies to bar ~0.55 s, credited on ARRIVAL.
- **+3 unconditional wave-clear bonus.**
- Total run income ≈ 98⚡ vs ~90⚡ comfortable spend — the margin is the tension.

## Royal Tower (GDD v2 §5)

HP **4000**, no regen. King attack 50 dmg/1.0 s/range 7.0 (any) + built-in cannon 60 dmg/1.4 s/range 6.0 (ground). Combined ~93 DPS must NOT solo waves (idle player dies wave 4). Lose: HP 0. Win: all 12 waves spawned AND zero enemies alive → stars: ★ win, ★★ ≥50% HP, ★★★ ≥80% HP.

## Waves (GDD v2 §7 — 12 waves, ~7 min, absolute startTimes, unconditional spawn)

| # | t | Composition | Pattern |
|---|---|---|---|
| 1 | 0:00 | 5 Goblins | 1 pt (N) |
| 2 | 0:27 | 8 Goblins | 2 pts |
| 3 | 0:54 | 3 Mages | 1 pt |
| 4 | 1:24 | 6 Goblins + 2 Mages | 2 pts |
| 5 | 1:54 | 2 Ogres | 1 pt |
| 6 | 2:24 | 10 Goblins + 2 Mages | 3 pts staggered |
| 7 | 3:00 | 3 Ogres | N + S flanks |
| 8 | 3:36 | 12 Goblins + 3 Mages + 1 Ogre | 4 pts |
| 9 | 4:18 | 4 Mages + 2 Ogres | 2 pts |
| 10 | 5:00 | 16 Goblins (4×4) + 2 Mages | 4 pts |
| 11 | 5:42 | 4 Ogres + 5 Goblins | 3 pts, opposite flanks |
| 12 | 6:27 | Boss + 8 Goblins + 2 Mages (escorts +10–12 s) | Boss N, escorts E/W |

Spawn: 8 points on map edge (r=15), 0=N…7=NW clockwise. In-group cadence 0.11 s; group delays ×0.7.

## Edge-case rulings (all stand — GDD v2 §9)

Steering not A*; 10% retarget hysteresis; freeze refresh-not-stack; mark dead immediately (no double bounty); in-flight projectile fizzles if target dies; orb at cap = wasted; cooldown lives on the card not the slot; `dist <= range` inclusive; single-touch lock; auto-pause on background + 3-2-1 resume; victory only when spawn queue empty + wave 12 started + zero alive.

## Mock discrepancies (mocks are reference only — NOT spec)

`Assets/TowerDefense/MocksDirectionSoFar/` shows: gold-coin counter top bar, 3-card hand with DAMAGE/FIRE RATE/RANGE upgrade-style cards, wave 7/10, x5 speed. These contradict the GDD (Energy-only currency, 4-card hand of buildings/spells, 12 waves, 1×/2× speed). **Gameplay follows this doc**; visual style/layout direction (circular meadow arena, bottom card bar, magenta energy bar, top wave banner) comes from the mocks.

## Open questions (add here, don't invent)

- Second environment theme (requirement: ≥2 environments). Proposal: same arena logic, second biome skin (e.g., snow/lava) + optional obstacle layout variation. → decide with team.
- Game speed toggle: GDD says 1×/2×, mock shows x5 — ship 1×/2×, revisit if trivial.
- Skeleton Knight model is imported but the design has no Skeleton — use as Mage stand-in until a Mage model arrives, or as a skin variant? (currently: Mage stand-in, see 12_ASSET_STATUS)

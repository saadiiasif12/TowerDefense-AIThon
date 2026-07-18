# Game Design Document — "Royal Siege"
### Clash Royale × Tower Defense Hybrid
**Version 2.0 | Target session length: ~7–8 minutes | Platform: Mobile (portrait)**

> **v2.0 changelog (from v1.0):** kill-collection Energy economy replaces passive regen · 8-card deck with a 4-card rotating hand, NEXT preview, and deterministic ring-buffer cycle · Evolution cards (Cannon, Arrows) · new 8th card: Bomb Tower · no perfect counters — every spell damage value sits below the one-shot threshold · strict range enforcement for the Royal Tower and all player buildings · faster wave timeline and spawn cadence with larger wave compositions (101 enemies vs 79) · card wave-unlocks removed (full deck available from t=0).

---

## 1. Core Gameplay Loop

The player defends a central Royal Tower against timed waves of enemies approaching from the map edges. The loop is:

**See incoming wave → assess threat → spend Energy on a building or spell → enemies die and drop Energy orbs that fly into your bar → reinvest or bank → wave clears (+3⚡) → short breather → harder wave.**

Two tension drivers:
1. **Buildings are permanent investment (TD DNA); spells are instant tempo (CR DNA).** Over-invest in buildings and you have no spell answer to a swarm; only cast spells and you have no baseline defense.
2. **v2: kills are the *only* steady income.** There is no passive regen. Every Energy point in your bar was earned by a kill or a wave clear, so letting enemies die to your defenses efficiently — rather than leaking or overkilling — is the economy itself.

---

## 2. Combat System

- **Real-time**, no pausing. Enemies path in a straight line toward the Royal Tower, attacking any player building whose collision they touch (Ogres actively prefer buildings — §6).
- **Damage model:** flat damage, no armor types. Depth comes from range/speed/HP asymmetry.
- **Targeting:** all player buildings target the **closest enemy strictly inside their range circle** (center-to-center distance — v2 change, see below); retarget when the target dies or leaves range (10% hysteresis). Enemies target the **closest player structure** (Royal Tower counts).
- **Strict range rule (v2):** the Royal Tower and every player building may only fire at an enemy whose center is **inside the range circle**. No edge-radius grace. Spells only affect enemies **inside the territory** (the map circle, radius 15). What you see drawn as the range ring is exactly what can be hit.
- **Attack model:** buildings fire homing projectiles that always hit; a projectile fizzles if its target dies mid-flight (prevents double bounty). Tesla and Lightning are instant. **Bomb Tower shells splash** (v2): full damage to every enemy within 1.4 tiles of the impact.
- **Tick rate:** combat logic at 10 Hz, visuals interpolated at frame rate.
- **No perfect counters (v2):** no single spell cast can take any enemy from full HP to zero. An enemy dies only when accumulated damage brings its HP to 0.

---

## 3. Card System — Deck, Hand & Cycle (reworked in v2)

The v1 "all cards in one bar" layout is replaced by the full Clash Royale cycle:

- **Deck = 8 cards** (4 buildings + 4 spells). All 8 are in the deck from t=0 — wave unlocks are gone.
- **Hand = 4 playable cards** shown in the bottom bar (all four fit any ≥360 px phone width).
- **NEXT preview** on the left shows the card about to enter your hand; **3 hidden cards** sit fanned behind it.
- **Deterministic ring buffer:** at match start the deck is shuffled **once** — the first 4 drawn become your hand. *This shuffle is the only random element in the entire game.* From then on the cycle is pure FIFO: play a card → it goes to the back of the queue → the front of the queue slides into the exact hand slot you just used. After any 8 plays your hand has cycled back around.
- **Per-card cooldowns retained** from v1 (radial grey-out) — a card can rotate back into your hand while still recharging.
- Tap a card for its stat tooltip; drag past a 40 px threshold to play it.

### Evolution Cards (new in v2)

Two cards — **Cannon** and **Arrows** — are Evolution cards, marked by **2 diamond pips** above the card art. Each play fills a pip. When both pips are lit the card **glows gold**, and the **next (3rd) play comes out evolved**, after which the pips reset:

| Evolution | Effect of the evolved play |
|---|---|
| **Evo Cannon** | 1,050 HP (×1.5) and 110.5 damage (×1.3); gold ring on the field model |
| **Evo Arrows** | 88 damage in a **5.5-tile** radius (vs 80 in 4.0) — still below Goblin HP (90), preserving the no-one-shot rule |

Evolutions reward committing to a card through the cycle rather than fishing for one answer.

### Card Statistics Table

| Card | Cost | Cooldown | Damage | Range/Radius | Rate | HP | Notes / Strategic purpose |
|---|---|---|---|---|---|---|---|
| **Cannon** ◆evo (building) | 3 | 8 s | 85 | 5.5 tiles | 1.0 s | 700 | 85 DPS single-target workhorse; cheapest defense per Energy. Evo: 1050 HP / 110.5 dmg. |
| **Tesla** (building) | 4 | 10 s | 110 | 5.0 tiles | 1.1 s | 600 | 100 DPS, 0.2 s retarget (fastest). Instant hit, no projectile. |
| **Bomb Tower** (building, new) | 4 | 10 s | 80 (splash 1.4) | 4.5 tiles | 1.2 s | 800 | ~67 splash DPS. The anti-swarm building — one shell chips an entire goblin pack (80 < 90, no one-shot). Short reach; place it inside the flow. |
| **X-Bow** (building) | 6 | 20 s | 30 | **9.0 tiles** | 0.25 s | 900 | 120 DPS at extreme range. Expensive; losing it hurts. Also your answer to standoff Mages. |
| **Arrows** ◆evo (spell) | 3 | 6 s | 80 (area) | 4.0 radius | instant | — | Swarm softener: leaves Goblins at 10 HP for any follow-up hit. Evo: 88 dmg, 5.5 radius. |
| **Fireball** (spell) | 4 | 9 s | 260 (area) | 2.5 radius | instant | — | Leaves a Mage at 30 HP; deletes a quarter of an Ogre. The "medium threat" softener. |
| **Freeze** (spell) | 4 | 14 s | 0 | 3.0 radius | 4 s freeze (boss 2 s) | — | Stops movement *and* attacks; frozen enemies still take damage. Duration refreshes, never stacks. |
| **Lightning** (spell) | 6 | 16 s | 275 ×3 targets | 3.5 radius | instant + 0.5 s stun | — | Hits the **3 highest-HP** enemies in radius. The Ogre/boss chunker — no longer one-shots a Mage. |

**Placement rules (unchanged):** buildings only inside the deployment circle (radius 5.0, snap 0.5, max 4 on field, no overlap, cost validated at release); spells anywhere on the map; green/red ghost preview.

**Balance reasoning (v2):**
- **No enemy has a clean one-spell answer anymore.** Arrows 80 < Goblin 90; Fireball 260 < Mage 290; Lightning 275 < Mage 290. Spells *soften*, defenses *finish* — which also feeds the kill-collection economy through your buildings.
- Cost-per-DPS on buildings: Cannon 28.3, Tesla 25, Bomb Tower ~17 (vs groups), X-Bow 20.
- The random opening hand (the only RNG) creates run variety: an opener without Cannon plays very differently — cycle cheap spells to reach your buildings.

---

## 4. Resource / Economy Design — "Energy" (reworked in v2)

**Name: Energy** (⚡). v1's hybrid regen+bounty is replaced by a **pure kill-collection economy**: passive regen is deleted entirely.

When an enemy dies it **drops an Energy orb** that flies from the corpse into the Energy bar (~0.55 s flight; the Energy is credited on arrival, so income has a visible, satisfying delay).

| Parameter | Value | Reasoning |
|---|---|---|
| Maximum capacity | **10** | One-digit readability; hard cap — orbs collected at cap are wasted, so spend. |
| Starting amount | **9** | Nearly a full bar: funds an opening building + a spell before any income exists. Raised from 6 because there is no regen safety net. |
| Passive regen | **None (v2)** | Kills are the economy. Efficient defense literally pays. |
| Kill drop | Goblin **+0.25**, Mage **+0.5**, Ogre **+1.0**, Boss **+3.0** | Ascending with threat. (Config has no Skeleton; the smallest drop maps to the smallest unit, the Goblin.) |
| Wave-clear bonus | **+3 Energy, always granted** on clearing a wave | v1 gated it on clearing before the next spawn; with the faster v2 timeline waves overlap constantly, so the gate was removed. This is the guaranteed income floor. |
| Overflow | Hard cap; bar pulses gold at 10 to nudge spending. |
| Total match income | ≈ 53⚡ from drops + 36⚡ from clear bonuses + 9 start ≈ **98⚡** across ~7 minutes | Roughly 4 buildings + ~15 spell casts of budget. Rebuilding lost buildings eats the spell budget — protect them. |

**How Energy balance shifts:** early game is drop-limited (small goblin drops), mid game is bonus-carried, late game is ogre-funded (1⚡ each) but rebuild-taxed. The failure spiral is real and intended: leak a wave, earn less, defend the next with less — the +3 wave bonus is the ratchet that lets a good wave recover a bad one.

---

## 5. Royal Tower Design

| Stat | Value | Reasoning |
|---|---|---|
| HP | **4,000** | Long enough to react, short enough to punish neglect. No regen — damage is permanent within a match. |
| King attack (arrows) | 50 dmg / 1.0 s, range 7.0 | Baseline so the player is never fully helpless. |
| Built-in Cannon | 60 dmg / 1.4 s, range 6.0 (ground) | Combined tower DPS ≈ 93 — cannot solo a wave (verified: an idle player dies on wave 4). |
| **Strict range (v2)** | Both attacks fire **only when the enemy's center is inside the range circle** | v1 measured to the enemy's edge, effectively firing ~2 tiles beyond the drawn ring. Consequence: standoff Mages sieging buildings placed near the deployment edge can sit outside the tower's reach — X-Bow or Fireball is the intended answer. |

**Lose condition:** Royal Tower HP = 0 → defeat screen with wave reached + stats.
**Win condition:** survive all 12 waves and kill every remaining enemy → victory with stars: ★ win, ★★ above 50% tower HP, ★★★ above 80%.

---

## 6. Enemy Design

### Enemy Statistics Table

| Enemy | HP | Speed | Damage* | Attack rate | Range | Priority | Drop | Role |
|---|---|---|---|---|---|---|---|---|
| **Goblin** | 90 | 2.0 (fast) | 50 | 1.1 s | melee (0.5) | Closest structure | 0.25 ⚡ | Swarm. Survives Arrows at 10 HP — needs a finishing hit. Punishes empty defenses with speed. |
| **Mage** | 290 | 1.2 | 90 (0.8 splash) | 1.6 s | **5.0 tiles** | Closest structure | 0.5 ⚡ | Standoff threat; with v2 strict ranges it can outrange short-reach buildings and siege them. Fireball + one hit kills. |
| **Ogre** | 950 | 0.8 (slow) | 160 | 1.5 s | melee (0.8) | **Buildings first** | 1.0 ⚡ | Tank/wrecker; demolishes the nearest player building, then the tower. Best drop in the game — an ogre killed cleanly funds a Fireball. |
| **Ogre Warlord (Boss)** | 3,200 | 0.7 | 200 | 1.6 s | melee (1.0) | Buildings first | 3.0 ⚡ | Wave 12 finale. Ground-slam every 8 s: 100 dmg pulse in 2.5 radius (telegraphed). Freeze lasts only 2 s on it. |

\* All enemy damage is multiplied by `globalEnemyDamageMultiplier` = **0.5** in v2 — the compensation dial for strict ranges, faster spawns, and the leaner economy. Effective damage in play: Goblin 25, Mage 45, Ogre 80, Boss 100 (slam 50).

**Spawning:** 8 fixed spawn points on the map edge (radius 15), indices 0=N … 7=NW. **v2 cadence:** units within a group spawn 0.11 s apart (was 0.18) and in-wave group delays run at 70% of their config value — waves pour in noticeably faster.

---

## 7. Wave Design & Difficulty Progression (~7-minute run)

v2 compresses the timeline ~40% and raises every composition ~25–30% (101 enemies total vs 79). Waves spawn at their `startTime` unconditionally — overlap is the anti-stall mechanism. Victory requires every wave spawned **and every enemy dead** (a wave-11 straggler can outlive the boss).

| Wave | Time (m:ss) | Composition | Spawn pattern |
|---|---|---|---|
| 1 | 0:00 | 5 Goblins | 1 point (N) |
| 2 | 0:27 | 8 Goblins | 2 points |
| 3 | 0:54 | 3 Mages | 1 point |
| 4 | 1:24 | 6 Goblins + 2 Mages | 2 points |
| 5 | 1:54 | 2 Ogres | 1 point |
| 6 | 2:24 | 10 Goblins + 2 Mages | 3 points, staggered |
| 7 | 3:00 | 3 Ogres | opposite flanks (N + S) |
| 8 | 3:36 | 12 Goblins + 3 Mages + 1 Ogre | 4 points |
| 9 | 4:18 | 4 Mages + 2 Ogres | 2 points |
| 10 | 5:00 | 16 Goblins (4 packs of 4) + 2 Mages | 4 points |
| 11 | 5:42 | 4 Ogres + 5 Goblins | 3 points incl. simultaneous opposite flanks |
| 12 | 6:27 | **Boss** + 8 Goblins + 2 Mages (escorts +10–12 s) | Boss N, escorts E/W |

**Design beats:** wave 5's double-Ogre is the first real spell test, wave 7 is the two-front problem, wave 9 is the Mage-siege exam (they outrange your short buildings — X-Bow/Fireball check), wave 11 is the resource-pressure peak, wave 12 is spectacle. Deaths should cluster at waves 8–11.

**Measured difficulty (scripted-bot playtesting):** a competent scripted player wins ~75% of runs, with winning tower HP ranging from ~3% to ~72% — losses cluster at wave 9, exactly on curve. A passive player dies at wave 4.

---

## 8. Balancing Philosophy

1. **Anchor everything to the Cannon.** 3 Energy = 85 DPS + 700 HP is the exchange rate.
2. **Spells soften, structures finish (v2 replaces "one clean counter").** Every spell leaves its old counter-target alive by a margin (Goblin 10 HP, Mage 30 HP) so buildings and the tower convert softened waves into kills — and kills are income.
3. **Income ratio is the difficulty dial.** Total match income ≈ 98⚡ vs ≈ 90⚡ of comfortable spending. The margin *is* the tension.
4. **The v2 difficulty knob is `globalEnemyDamageMultiplier` (0.5).** Raise toward 0.6–0.7 to punish leaks harder; the HP multiplier is second choice because raising it inflates time-to-kill everywhere, and *lowering* it below ~0.89 silently re-creates the Arrows one-shot.
5. **Playtest metric:** if winning runs end at 40–70% tower HP, ship it.

---

## 9. Edge Cases & Solutions

All v1 rulings stand (steering not A*, 10% retarget hysteresis, freeze refresh-not-stack, boss freeze 2 s, mark-dead-immediately to prevent double drops, cost validated at release, ghost 1.5 tiles above the fingertip, single-touch lock, auto-pause on backgrounding with 3-2-1 resume, hard Energy cap). New/changed in v2:

- **Orb in flight when the bar is full:** the orb still flies; on arrival the credit clamps at 10 (overflow wasted). Do not queue orbs.
- **Orb in flight at match end:** cosmetic only — defeat/victory is decided by tower HP and enemy state, not pending income.
- **Playing a card while its replacement is on cooldown:** legal; the incoming card arrives greyed with its radial timer running. Cooldowns live on the card, not the slot.
- **Evolution charge persistence:** pips persist across the cycle (they belong to the card), reset only when the evolved play fires. Charge is not lost if the card sits in the queue.
- **First-hand contains no building:** possible (1-in-70 for zero buildings). Intended answer: cast a cheap spell to cycle. The 9⚡ start funds it.
- **Enemy exactly on the range ring:** `distance <= range` — the drawn circle is inclusive.
- **Mage sieging an edge building from outside all return fire:** legal and intended (see §5/§6) — the player's answer is X-Bow, Fireball, or letting the building go.
- **Victory with stragglers:** win fires only when the spawn queue is empty, wave 12 has started, and zero enemies are alive.

---

## 10. Scope — What Shipped

Single self-contained HTML file; all stats live in the embedded CFG block (mirrors the four config JSONs); 10 Hz logic / 60 fps interpolated rendering; canvas 2D; WebAudio synth SFX; pointer events (mouse + touch); 1×/2× speed, pause, mute; star rating and end-of-match stats.

**v3 hooks:** endless mode after wave 12, a Thief enemy that steals Energy on tower hits, more Evolution cards, per-run deck selection (choose 8 from a wider collection), meta-progression (+1 max Energy per account level).

# 13 · Royal Defense — Player Journey & Progression (Design v4.0)

**Supersedes** the v2 wave/economy/roster sections of `01_DESIGN_CURRENT.md` (waves, economy, card list, enemy list). Strict-range, determinism, SOLID and zero-hardcoded-stats rules all still apply. Received from design 17-Jul (evening); implemented same day.

## 0. Rulings / assumptions (approved in the brief)
1. Wave 44 = Stage 2 boss finale (2 Ogres + Hellspawn escorts).
2. Wave-40 tower level is **L5** (brief's "level 3" was a typo).
3. Elixir: +1 flat per kill (Ogre +5 — the one deviation). Fallback lever if snowballing: fodder pays +0.5.
4. **Tiered no-one-shot**: light tier (Sk1, Sk2, G1) may die to one AoE cast; heavy tier (G2, H1, H2, Ogre) never one-shot by a single cast (largest hit Lightning 300 < G2 320).
5. Checkpoint resume = full-HP tower at its level, 5 elixir, empty field (no buildings/knights), fresh shuffle of unlocked cards, next wave = checkpoint wave.
6. Clear-triggered waves: next wave 2.0 s after the last enemy dies AND its orb lands. No absolute timers. Elixir cap + building decay replace the timer as pressure.
7. Enemies target nearest **structure-or-knight** (10% hysteresis); Ogre prefers buildings/towers, ignores knights unless body-blocked.
8. Roster final: Bomb Tower cut, Freeze renamed **Frost-ball**, no evolutions. 10 cards: 3 buildings + 6 spells + 1 troop.

## 1. Journey
- **Stage 1** = waves 1–20 · **Stage 2** = waves 21–44 (global numbering).
- Every 10th wave from 20: Ogre wave (20, 30, 40) + the 44 finale (2 Ogres).
- **Checkpoints (5)**: after w4, w12, w24, w40 (level-up screens) + after w20 (stage complete). Tower death → retry from latest checkpoint.
- Stage header UI: progress bar fills across the stage (x/20 then x/24); wave counter shows global number.

## 2. Cards (10)
Starting six from wave 1: **Cannon, Log, Arrows, Earthquake, Frost-ball, Knights**. Unlocks in order at checkpoints 1–4: **Fireball → Tesla → X-Bow → Lightning**.

| # | Card | Type | Cost | CD | Damage | Range/Radius | HP | Lifetime |
|---|------|------|------|----|--------|--------------|----|----------|
| 1 | Cannon | Building | 3 | 8s | 90 / 1.0s | 5.5 | 700 | 35s |
| 2 | Log | Spell | 2 | 5s | 80 line | 9-tile roll, 1.6 wide, knockback 1.2 | — | — |
| 3 | Arrows | Spell | 3 | 6s | 90 area | r 4.0 | — | — |
| 4 | Earthquake | Spell | 3 | 9s | 40/s × 4s | r 3.0, 35% slow | — | — |
| 5 | Frost-ball | Spell | 4 | 14s | 0 | r 3.0, freeze 4s (Ogre 2s) | — | — |
| 6 | Knights | Troop | 5 | 12s | 75 / 1.1s each | melee, move 1.6 | 480 each | until killed |
| 7 | Fireball 🔓1 | Spell | 4 | 9s | 250 area + kb 0.6 | r 2.5 | — | — |
| 8 | Tesla 🔓2 | Building | 4 | 10s | 115 / 1.05s | 5.0 | 600 | 30s |
| 9 | X-Bow 🔓3 | Building | 6 | 20s | 35 / 0.25s | 9.0 | 900 | 40s |
| 10 | Lightning 🔓4 | Spell | 6 | 16s | 300 ×3 + 0.5s stun | r 3.5, 3 highest-HP | — | — |

Deck grows 6→7→8→9→10; hand 4 + Next Up; single opening shuffle; per-card cooldowns. Placement: buildings + Knights inside deployment circle (r5, snap 0.5); spells anywhere in territory. **17-Jul user delta: NO building count cap** (supersedes "max 4 on field" + QA DT-003 by-design ruling) - space, overlap, elixir and lifetime decay are the only limits (config maxPlayerBuildings 0 = unlimited).

## 3. Enemies (7)
| Enemy | HP | Speed | Dmg | Rate | Range | Bounty | Threat | Debut |
|---|---|---|---|---|---|---|---|---|
| Skeleton 1 | 55 | 2.2 | 30 | 1.0s | melee | +1 | 1 | w1 |
| Skeleton 2 | 110 | 1.8 | 45 | 1.1s | melee | +1 | 2 | w3 |
| Goblin 1 | 180 | 2.6 | 60 | 1.0s | melee | +1 | 3 | w6 |
| Goblin 2 (spear) | 320 | 1.6 | 70 | 1.4s | 4.5 ranged | +1 | 5 | w9 |
| Hellspawn 1 | 500 | 1.2 | 100 (0.8 splash) | 1.4s | melee | +1 | 8 | w13 |
| Hellspawn 2 | 800 | 1.0 | 90 (1.0 splash) | 1.6s | 5.0 ranged | +1 | 12 | w17 |
| Ogre (boss) | 3000 | 0.7 | 250 | 1.6s | melee | +5 | 30 | w20 |

Ogre: slam every 8s (120, r2.5, telegraphed), Frost-ball 2s only, prefers buildings, knockback-immune. Heavies take half knockback.

## 4. Building HP decay
Buildings drain `maxHP ÷ lifetime` per second from placement (Cannon 20/s, Tesla 20/s, X-Bow 22.5/s). Enemy damage stacks on top; no healing. Decayed death = crumble (no explosion). HP bar doubles as lifetime bar. No count cap (17-Jul delta).

## 5. Combat rulings
- **5.1** Tiered no-one-shot (see §0.4). Re-check the Lightning 300 < G2 320 line after ANY tuning.
- **5.2** Strict ranges unchanged.
- **5.3** Knights: 2 per cast; enemies treat knights as targets; Ogre ignores them unless blocked (slam hits them); never decay; persist across waves; max 4 active; card greys at 3+ alive; knight kills pay bounty normally. **17-Jul user delta (replaces "no leash"):** knights are GUARDS of the tower's white circle (guard radius = deploymentRadius = tower reach): they only target enemies whose center is inside the circle, never step outside it, and wait in place until a target comes under the radius. Live-verified: max knight distance 4.05 over a 10 s fight, zero chasing while all enemies were outside. **18-Jul fix (guard-ring trap):** a knight ALSO retaliates against any enemy already within its own melee reach even when that enemy is just OUTSIDE the ring — an attacker poking a ring-edge knight no longer gets a free hit. The strike is in place; the leash is unchanged, so distant out-of-circle enemies are still ignored. Rule = `IsValidPrey` (inside circle **OR** `edge ≤ attackRange`); safe because knight footprint (unitRadius) < attackRange, so an enemy pinned at its standoff is always within reach. Live-verified: out-of-circle adjacent attacker took 150 dmg (was 0), distant out-of-circle enemy ignored, knight stayed at 4.72 ≤ 5. **Also 18-Jul (path around the tower):** knights steer AROUND solid structures (Royal Tower / buildings) instead of grinding into the near side when their target is on the far side — deterministic tangential avoidance (`KnightUnit.SteerAroundStructures`, no navmesh). Live-verified: a knight at +Z reached a −Z target by arcing around the tower (never inside the standoff ring), landing 150 dmg. **Also 17-Jul:** knights cannot walk through the Royal Tower or buildings — clamped onto the standoff ring (footprint + knight radius) every sim tick, target or not (verified: forced-inside knight ejected to exactly 1.90; a 12 s fight never dipped below it).
- **5.4** Log rolls `rollDistance` (9 tiles) from the DROP POINT along a **FIXED forward direction** (`LogEffectSO.rollDirection`, default world +Z = "up the screen" for the pitched-down, zero-yaw camera), then vanishes; hits each ground enemy in its lane once, knockback 1.2 (interrupts wind-ups; heavies half; Ogre none); passes through buildings (only enemies are swept). Earthquake: 35% slow + DoT at 10 Hz; slow doesn't stack with itself; Frost-ball overrides.
  - **18-Jul user delta (supersedes the earlier "lane gate"):** the Log is placeable **anywhere** in the map circle — `LogEffectSO.HasTargets` returns `true` (no enemy required). It ALWAYS rolls the fixed direction regardless of where it's dropped (was radial: outward from map center through the drop point). `rollDirection` is the single source of truth read by BOTH the sim (`LogEffectSO.Apply`) and the visual (`V4CardVfx`), so the rolling-log animation can never drift from the damage lane. The generic spell-placement gate is still `SpellEffectSO.HasTargets` (virtual; default = "enemy inside `card.radius`", unchanged for the other 6 spells). Zero balance numbers touched.

- **5.5** Spawn spread (18-Jul): a wave's enemies fan out along the map-edge arc in staggered ranks, WIDELY spaced (centre spacing `max(2.0, unitRadius·4)`) with a small deterministic per-unit hash scatter (radial + angular, bounded so a jittered pair can't overlap), and each unit's spawn TIME is hash-jittered (0–1.4× `unitSpawnInterval`). Packs now arrive distant and a little irregular instead of bunched/lockstep. Zero-RNG (all hashed), so determinism is intact. Knobs: `SpawnPointProvider` spacing/scatter; `CampaignSO.unitSpawnInterval`.

## 6. Elixir economy (TIME-BASED — 18-Jul ruling)
**18-Jul user ruling:** kill-drop energy is OFF. The elixir bar fills ONLY from the time-based passive regen — enemy kills no longer drop energy orbs. Toggle back via `EconomyConfigSO.killDropsEnergy` (the old v4 hybrid: kills also paid bounty orbs).

| Param | Value |
|---|---|
| Cap | 10 (overflow wasted, bar pulses) |
| Start / checkpoint resume | 5 |
| Passive regen | 1 per 3.5 s, always on — **now the ONLY income source** |
| Kill bounty (orbs) | **OFF** (`killDropsEnergy = false`); orb path & bounty values kept for the toggle |
| Wave-clear bonus | REMOVED |

Implementation: `OrbSpawner.OnEnemyKilled` early-returns when `killDropsEnergy` is off (no orb spawn, nothing credited); `EnergyBank.Tick` passive regen is unchanged. `OrbsInFlight` stays 0, so the wave-clear gap no longer waits on orbs.

Tuning levers in order: `passiveRegenSeconds` (3.5 — the sole income knob now) → wave budgets → lifetimes ±5 s → tower growth ±3%. ⚠️ With kills off, 3.5 s/elixir is the only income — revisit the rate if the game feels starved.

## 7. Royal Tower levels
| L | Reached | HP | King (r7) | Cannon (r6) | ~DPS | Unlock |
|---|---|---|---|---|---|---|
| 1 | start | 4000 | 50/1.0s | 60/1.4s | 93 | starter six |
| 2 | w4 | 4600 | 58 | 68 | 107 | Fireball |
| 3 | w12 | 5300 | 66 | 77 | 121 | Tesla |
| 4 | w24 | 6100 | 75 | 87 | 137 | X-Bow |
| 5 | w40 | 7000 | 85 | 98 | 155 | Lightning |

Leveling fully heals. Ranges never change. Idle player must still die within ~4 waves of any checkpoint.
NOTE (implementation): current build has the cannon folded into the king (93 combined at L1) — v4 keeps that fold: king damage per level = combined DPS × 1.0s (93/107/121/137/155), built-in cannon stays disabled.
**Tower ARTS (18 Jul):** 4 real tower models (`RoyalTower_Full.prefab` → `TowerLevel1..4`, from `Environment/Tower/Model/Tower Levels/`). L1 = art 1 (default, active in the edit scene), L2 → art 2, L3 → art 3, L4 **and** L5 → art 4 (only 4 arts exist — `TowerLevelView` clamps; a 5th art later is one `_levelModels` entry). The King auto-aligns to the ACTIVE art's measured roof (`TowerLevelView.AlignKing` — roofs differ: 3.09/2.52/2.28/2.38 world Y) and glides onto the new roof during the upgrade surge. `TowerFail` ruin still shows on defeat.
**USER DELTA (17 Jul, post-implementation):** king range **7.0 → 5.0** — the Royal Tower may never hit outside the drawn white circle (= deploymentRadius); "the ring never lies" now applies to the tower itself. Placed buildings keep their own card ranges. Live-verified: 61 targeting samples in combat, worst target distance 4.71. ⚠️ This cuts tower coverage — the §10 idle-loss/balance targets must be re-tuned against r5.

## 8. Waves (44) — budgets & compositions
Stage 1: w1 5×Sk1 · w2 8×Sk1 · w3 6Sk1+3Sk2 · **w4█** 12Sk1+5Sk2 (2 flanks) · w5 7Sk2 · w6 4Sk1+2Sk2+3G1 · w7 6Sk1+5G1 (2f) · w8 10Sk1+3Sk2+3G1 (2f) · w9 4G1+3G2+2Sk2 (2f) · w10 8Sk2+3G2+2G1 (2f) · w11 5Sk1+6G1+3G2 (3f) · **w12█** 12Sk1+6Sk2+5G1+4G2 (3f) · w13 2H1+6Sk2 · w14 3H1+4G1 (2f) · w15 2H1+4G2+6Sk1 (2f) · w16 3H1+4G2 (2f) · w17 2H2+3H1+2G1 (2f) · w18 3H2+2H1+6Sk1 (3f) · w19 2H2+3H1+4G2+4G1 (4f) · **w20☠** Ogre + 8Sk1+4Sk2 escorts +8s (2f) → stage complete.
Stage 2: w21 3H1+4G2+5Sk2 (2f) · w22 2H2+2H1+5G1 (2f) · w23 2H2+3H1+4G2 (3f) · **w24█** 3H2+4H1+5G2+8Sk1 (4f) · w25 4H1+5G1+4Sk2 (2f) · w26 2H2+3H1+3G2 (3f) · w27 3H2+2H1+8Sk1+4G1 (3f) · w28 3H2+4H1+2G2 (3f) · w29 2H2+4H1+5G2+5Sk2 (4f) · **w30☠** Ogre+2H1+6Sk2 (2f) · w31 3H1+6G1+3G2 (3f) · w32 3H2+3H1+4G2 (3f) · w33 4H2+2H1+6Sk1+4G1 (4f) · w34 3H2+5H1+4G2 (3f) · w35 4H2+4H1+5G1+5Sk2 (4f) · w36 5H2+3H1+5G2 (4f) · w37 4H2+6H1+6Sk1 (4f) · w38 5H2+5H1+4G2+4G1 (4f) · w39 6H2+4H1+6G2 (4f) · **w40█☠** Ogre+4H2+3H1+6G2 (4f) → L5+Lightning · w41 4H2+5H1+5G1 (3f) · w42 5H2+5H1+5G2 (4f) · w43 6H2+6H1+8Sk1 (4f) · **w44☠☠** 2 Ogres (opposite flanks, 10 s apart)+3H2+6Sk2 (4f) → campaign complete.
Cadence: 0.11 s per unit in a group; flank groups stagger 2–3 s.

## 9. Fail / checkpoints / screens
- Checkpoint stores `{nextWave, towerLevel, unlockedCards, stage}` → `royalDefense.profile.v1` (saved on checkpoints only).
- Death/quit → retry from checkpoint (unlimited, resume per §0.5). Death on a spike wave = previous checkpoint (reward not earned).
- Level-up screen replaces the 2 s gap (next wave holds until dismissed): tower upgrade visual + "ROYAL TOWER LEVEL N" + card reveal (enters back of queue). Full sim pause during modals (decay too). Field/elixir carry through level-ups mid-run.
- Stage-complete (w20): stars (★ clear / ★★ ≥50% HP / ★★★ ≥80%), sets checkpoint, transition fully heals.
- Orbs in flight: gap waits for the last orb. Unlocked card enters queue at cooldown 0.

## 10. Verification targets
≥80% bot win w1–4; ~70% per segment; Ogre spikes 55–60%; idle dies within ~4 waves of any checkpoint at every level; campaign 35–50 min; segment 4–9 min.
Invariants: heavy no-one-shot line; idle-loss all 5 levels; knights cap 4; strict ranges; checkpoint resume state; Ogre freeze 2 s; decay = exact lifetime with no combat.

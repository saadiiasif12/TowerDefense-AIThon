# 07 · Module: Enemies & Waves (RoyalSiege.Units)

**Status: CODE COMPLETE — 16 Jul (see TASKS.md)**

## Responsibilities
- `WaveScheduler` — reads `WaveTimelineSO`; spawns each wave at its ABSOLUTE `startTime` unconditionally (overlap is the anti-stall design — never gate on previous wave cleared). In-group cadence 0.11 s; group delays ×0.7. Emits `WaveStarted`; `WaveCleared` when all members of a wave are dead. Exposes spawn-queue-empty for WinLoseEvaluator.
- `SpawnPoints` — 8 points on map edge circle (r=15), index 0=N clockwise … 7=NW.
- `EnemyAgent` — brain; picks target per `EnemyDefinitionSO.targetPriority`:
  - `ClosestStructure` (Goblin, Mage): nearest of {Royal Tower, player buildings}.
  - `BuildingsFirst` (Ogre, Boss): nearest player building if any exists, else tower.
  - Retarget with 10% hysteresis (switch only if new target is >10% closer or current died/left).
- `SteeringMover` — straight-line steering toward target (NOT A*), simple separation so packs don't stack. Ticked at 10 Hz; view interpolates.
- `MeleeAttacker` / `RangedAttacker` — attack when target center within `attackRange` (strict `RangeCheck`). Mage: ranged 5.0, 0.8 splash on impact. All damage × `globalEnemyDamageMultiplier` (0.5).
- `Health` + `BountyDropper` — at 0 HP mark dead IMMEDIATELY (unregister from targeting, no double bounty), emit `EnemyKilled(bounty)`, play die anim, release to pool after.
- `StatusEffects` — Freeze (stops move+attack, duration REFRESHES never stacks, boss 2 s) and Stun (0.5 s from Lightning). Frozen enemies still take damage.
- `BossSlam` — every 8 s: telegraph, then 100 dmg pulse radius 2.5 (× multiplier).

## Acceptance
- Wave 1 spawns 5 goblins at N at t=0; goblins walk to tower and hit it; ogre bypasses tower for a placed cannon; mage stops at range 5 and sieges; killing a full wave fires WaveCleared exactly once.

## Notes / changes
- **21 Jul (knight spacing):** knights merged into each other — separation ran only while seeking and only to body-contact. Now a gentle separation shuffle also runs while attacking/idle (mirrors the enemy crowd shuffle), and new `GameConfigSO.knightSpacing` (=0.5) → `KnightRuntimeDeps.Spacing` adds a real gap to the knight-knight `minDistance` (needed since the knight visual ≈2.5× its 0.4 gameplay radius). Knob: `GameConfig.asset` → "Knight Spacing".
- **21 Jul (knight melee gap):** knight↔enemy combat kept them body-to-body (meshes overlapped). New `GameConfigSO.knightCombatStandoff` (=0.3) → `EnemyRuntimeDeps.KnightStandoff` + `KnightRuntimeDeps.CombatStandoff`: the enemy-vs-knight clamp becomes `knightFootprint + enemyBody + gap` (crowd-safe), and the knight's reach is stretched to `max(attackRange, FootprintRadius + gap + 0.05)` (in-range check, impact guard, IsValidPrey) so it still lands hits across the gap. Distinct from the solid-structure gap; keep it ≤ Enemy Attack Standoff so enemies can still reach knights. Knob: `GameConfig.asset` → "Knight Combat Standoff".
- **21 Jul (standoff gap):** enemies were resting with their BODIES at/inside the tower footprint (the overlap clamp was `footprint + min(BodyRadius, attackRange*0.9)`), so they visually clipped into the tower. New `GameConfigSO.enemyAttackStandoff` (=0.4, wired via `EnemyRuntimeDeps.AttackStandoff`): the clamp is now `footprint + BodyRadius + standoff` (body keeps a uniform gap outside the footprint for every unit size) and the in-range reach is stretched to `max(attackRange, BodyRadius + standoff + 0.05)` so a clamped unit — Ogre/Warlord included — can still land hits. No DPS/balance change. **Applies ONLY to solid structures (tower + buildings, `BlocksPlacement`); against Knights (non-solid `IStructureTarget`) enemies press to body-to-body contact so the knight's short melee reach connects — the standoff was shoving enemies out of knight reach and knights couldn't fight (regression caught 21 Jul).** Tune the gap on `GameConfig.asset` → "Enemy Attack Standoff".
- **17 Jul:** Mage/Skeleton converted to MELEE (user decision — no ranged enemies for now): range 0.6, projectile/splash removed. New `engageRadiusFromCenter` (12) on EnemyDefinitionSO + check in EnemyAgent: enemies can only attack once inside the territory (guards future ranged units). Verified live: skeletons walk to contact; ogre melees at exactly footprint+0.8.
- **16 Jul (crowd polish):** `unitRadius` added to EnemyDefinitionSO (Goblin 0.4 / Mage 0.5 / Ogre 0.85 / Boss 1.1). Spawn formations fan out along the map-edge ARC in staggered quincunx ranks spaced by body radius. EnemyAgent got soft separation steering (overlap-spring, deterministic, no physics; gentle shuffle continues while attacking so crowds ring the target). Walk anim is driven by ACTUAL velocity vs the clip's root-motion speed (fallback 0.9 for the in-place shared clip) with damped params; walk phases de-synced per unit via a position hash; view rotation slerps (TurnSharpness 8). Controller transitions blended (attack→walk 0.3 over last 15%). Verified: 0 overlapping pairs in live crowd audits.

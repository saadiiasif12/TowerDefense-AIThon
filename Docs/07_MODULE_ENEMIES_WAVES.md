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
- (log changes here)

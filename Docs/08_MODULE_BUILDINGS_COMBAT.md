# 08 · Module: Buildings, Tower & Combat (RoyalSiege.Buildings / RoyalSiege.Combat)

**Status: CODE COMPLETE — 16 Jul (see TASKS.md)**

## Responsibilities
- Building composition (see 02): `RangeTargeter` + `AttackDriver` + `Health` + `RangeRing` visual.
- `RangeTargeter` — closest enemy with center STRICTLY inside range (`RangeCheck`, `<=` inclusive); retarget on death/leave with 10% hysteresis. Tesla retarget delay 0.2 s.
- `AttackDriver` variants:
  - `ProjectileAttacker` (Cannon, X-Bow, tower king/cannon) — fires pooled homing projectile; ALWAYS hits unless target dies mid-flight → fizzle (no damage, no bounty issues).
  - `InstantAttacker` (Tesla) — immediate damage + zap VFX hook.
- `RoyalTower` — same parts twice: king attack (50/1.0 s/7.0, any) + built-in cannon (60/1.4 s/6.0, ground). HP 4000, no regen. Emits `TowerDamaged`.
- `BuildingRegistry : ITargetQuery` — spatial source of truth: alive enemies, alive buildings, tower. Used by targeters, enemy agents, spells. Simple list scans are fine at this scale (≤4 buildings, ≤40 alive enemies).
- Max 4 player buildings on field; destroying frees the slot. Emits `BuildingPlaced/Destroyed`.

## Invariants
- Tower combined DPS (~93) must NOT solo waves — idle player dies at wave 4 (test this).
- Strict range everywhere — an enemy at distance 7.4 from tower takes zero king damage; at 6.5 both tower attacks apply (README harness case).

## Acceptance
- Cannon placed in a goblin lane kills the pack and outputs orbs; Tesla hits instantly; X-Bow shreds a Mage outside other ranges; tower alone loses on wave 4.

## Notes / changes
- **18 Jul — Tower level visuals + fail state (`TowerLevelView`, view-only).** Holds the 5 level models (index 0 = original tower = L1, the 4 fortress arts = L2–L5) + a fail/ruin model. `RoyalTower.ApplyLevel(level, def, instantVisual)` drives it: checkpoints play a seamless "power surge" (golden flash masking an overlapping shrink-out/scale-pop + burst VFX + camera shake, unscaled time); resume/retry loads snap instantly. On `MatchEnded` defeat (tower HP 0) it hides all levels + the King and crashes in the ruin. `SetLevel` is guarded after fail (ruin is terminal). Wired via `RoyalTower_Full.prefab` in `Game.unity` (the old `RoyalTower.prefab` had no view). King roof-fit: `AlignKing` sets KingRoot's **Y only** to each level model's measured roof top (X/Z authored, user ruling); glide lerps Y; King hidden on the ruin.
- **18 Jul — Damage popups hooked at the damage source.** `RoyalTower.TakeDamage` and `BuildingUnit.TakeDamage` guard-check alive/positive then call `Juice.DamagePopupManager.Instance?.ShowDamage(amount, transform.position + roofOffset, isCritical:false)` the moment damage lands (null-safe — TestRange has no manager). See 11_MODULE_JUICE for the popup system.

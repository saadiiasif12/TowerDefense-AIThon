# 02 · Architecture & Code Conventions

Unity **6000.3.6f1** · C# · main scene `Assets/TowerDefense/Scenes/Game.unity` · portrait mobile.

## Folder & namespace layout

```
Assets/TowerDefense/
  Scripts/
    Core/       RoyalSiege.Core       — GameContext, MatchStateMachine, TickSystem, GameEvents, GameSpeed
    Data/       RoyalSiege.Data       — all ScriptableObject definitions (no logic beyond validation)
    Cards/      RoyalSiege.Cards      — DeckService, hand ring buffer, cooldowns, card play flow
    Economy/    RoyalSiege.Economy    — EnergyBank, orb spawn/flight/credit
    Units/      RoyalSiege.Units      — Enemy agent, movement/steering, enemy targeting, spawner, WaveScheduler
    Buildings/  RoyalSiege.Buildings  — player buildings + Royal Tower composition parts
    Spells/     RoyalSiege.Spells     — ISpellEffect strategies
    Combat/     RoyalSiege.Combat     — Health, Targeting service, projectiles, damage application, freeze/stun status
    Placement/  RoyalSiege.Placement  — drag-from-hand, ghost preview, snap/validate/commit
    UI/         RoyalSiege.UI         — HUD, hand bar, energy bar, wave banner, end screens (event-driven, zero game logic)
    Juice/      RoyalSiege.Juice      — VFX hooks, camera shake, haptics, hit-stop
    Audio/      RoyalSiege.Audio      — SFX/music services
  Data/         ScriptableObject ASSETS (instances), mirrors Scripts/Data types
  Prefabs/      runtime prefabs (enemies, buildings, projectiles, orbs, vfx)
```

No asmdefs (hackathon speed). Namespaces are mandatory anyway.

## SOLID — how it concretely applies here

- **S:** a building is COMPOSED of small parts: `RangeTargeter` + `AttackDriver` (projectile or instant) + `Health` + `RangeRing`. The Royal Tower is the same parts twice (king + cannon) — no `Tower.cs` god class. Enemy = `EnemyAgent` (brain) + `SteeringMover` + `MeleeAttacker`/`RangedAttacker` + `Health` + `BountyDropper`.
- **O:** new spells = new `ISpellEffect` implementation + a SpellCardSO referencing it. New enemy = new EnemyDefinitionSO + prefab. No switch-on-type anywhere.
- **L:** `CardDefinitionSO` (abstract) → `BuildingCardSO`, `SpellCardSO`. Code that cycles/costs/cools cards only sees the base.
- **I:** small service interfaces: `IEnergyBank`, `IClock` (tick time, speed-scaled), `ITargetQuery` (spatial queries), `IHapticsService`, `ISfxService`. Consumers depend on the interface.
- **D:** one scene-scoped `GameContext` (composition root) constructs services and injects them into MonoBehaviours via `Init(...)` methods. **No static singletons, no `FindObjectOfType` in gameplay code.**

## Core runtime model

- **10 Hz logic tick** (`TickSystem` accumulates scaled deltaTime, fires `ITickable.Tick(dt=0.1)`), visuals interpolate per frame between last/next logic positions. Game speed (1×/2×) and pause scale the accumulator — never `Time.timeScale` for logic.
- **Determinism:** the ONLY `Random` call in the game is the single deck shuffle at match start (seedable for tests).
- **Events, not polling:** `GameEvents` (plain C# events on a context-owned object): `EnemyKilled(enemy, bounty)`, `EnergyChanged`, `WaveStarted/WaveCleared`, `CardPlayed`, `BuildingPlaced/Destroyed`, `TowerDamaged`, `MatchEnded(result, stars)`. UI/Juice/Audio subscribe; gameplay never references UI.
- **Match state machine:** `Boot → Playing → Paused(3-2-1 resume) → Victory | Defeat`.
- **Pooling:** enemies, projectiles, orbs, and VFX are pooled (`ObjectPool<T>`); 101 enemies + orb per kill make this non-optional.
- **Strict range check** lives in ONE place: `RangeCheck.IsInside(Vector3 a, Vector3 b, float range)` (center-to-center, `<=`). Everything (buildings, tower, spells, mage) calls it.

## ScriptableObject catalog (the balance surface)

| SO | Key fields |
|---|---|
| `GameConfigSO` | tower stats (2 attack blocks), map radii/snap/maxBuildings, difficulty multipliers, tick rate, UX constants, star thresholds |
| `EconomyConfigSO` | start/max energy, waveClearBonus, orbFlightSeconds |
| `CardDefinitionSO` (abstract) | id, displayName, icon, cost, cooldown |
| `BuildingCardSO` | prefab, hp, damage, range, attackRate, projectile (null = instant), footprint |
| `SpellCardSO` | radius, `SpellEffectSO` reference |
| `SpellEffectSO` (abstract, strategy) | `AreaDamageEffectSO`, `FreezeEffectSO` (duration, bossDuration), `LightningEffectSO` (damage, targetCount, stunSeconds) |
| `EnemyDefinitionSO` | prefab, hp, speed, damage, attackRate, attackRange, splash, targetPriority(ClosestStructure/BuildingsFirst), bounty, special (boss slam block) |
| `DeckSO` | ordered list of 7 CardDefinitionSO |
| `WaveTimelineSO` | list of `WaveEntry { startTime, groups: [{enemy, count, spawnPointIndex, groupDelay}] }` |

Author values from `Docs/01_DESIGN_CURRENT.md` tables ONLY.

## Locked implementation decisions (16 Jul)

- **No DI framework** — one `GameContext` composition root, constructor/`Init()` injection. **No singletons, no static state.** Static classes only for stateless pure math (`RangeMath`).
- **Interfaces everywhere consumers live:** `ITickable`, `ITicker`, `IClock`, `IEnergyBank`, `IDamageable`, `IEnemyTarget`, `IStructureTarget`, `ITargetQuery`/`ITargetRegistry`, `IProjectileLauncher`, `IEnemyFactory`, `IBuildingFactory`.
- **Patterns:** Strategy (`SpellEffectSO`), Factory + Pool (enemies, projectiles, orbs), Observer (`GameEvents`). Buildings are NOT pooled (max 4 alive, ever — instantiate/destroy is fine).
- **Shared Projectile module:** every shooter (Cannon, X-Bow, tower king, tower cannon, Mage) fires through `IProjectileLauncher` + `ProjectileSettingsSO` (speed, **arcHeight**, spawnHeightOffset, impactHeightOffset, prefab). Homing; fizzles if target dies mid-flight; splash handled by an `onImpact` callback from the shooter (keeps Projectile generic).
- **Animation sync:** reusable `AttackCycle` (plain class) drives every attacker: on swing → `UnitAnimator.PlayAttack(period)` scales the anim so one clip fits the attack period; damage/projectile fires at `attackImpactFraction` (per-unit config, default ~0.4) of the period. One generic AnimatorController over the shared GeneralAnimations; per-character swaps later via AnimatorOverrideController.
- **Enemy melee/ranged distance rule:** enemies measure to the structure's EDGE (`planarDist − structure.footprintRadius ≤ attackRange`) — otherwise melee range 0.5 could never reach the tower's 1.5 footprint center. Player attacks on enemies remain strict center-in-range (GDD v2).
- **No pathfinding, no physics separation:** straight-line steering + deterministic per-unit formation offsets assigned at spawn (keeps the zero-RNG guarantee).
- Pause/3-2-1 resume UI deferred to Day 2; `IClock.IsPaused` + speed already in core.

## Conventions

- C# naming: PascalCase types/methods, `_camelCase` private fields, `camelCase` locals. One public type per file.
- `[SerializeField] private` over public fields. `RequireComponent` where a part depends on another.
- No `Update()` gameplay logic — gameplay goes through `ITickable`; `Update()` only for view interpolation/input.
- Comments only for non-obvious constraints (e.g., "orb credits on arrival — see GDD §4").
- Keep UI code dumb: read events + services, write nothing to gameplay state except through explicit commands (`PlayCardCommand`).

# 11 · Module: Juice — VFX/SFX/Haptics/Audio (RoyalSiege.Juice / RoyalSiege.Audio)

**Status: VFX CORE LIVE — 17 Jul.** Implemented: `VfxSpawner` (pooled one-shots, tint/scale per spawn), `JuiceDirector` (EnemyKilled→puff, BuildingPlaced→dust, SpellCast→tinted burst per spell), `ZapArcRenderer` (Tesla jagged additive arcs + sparks via InstantShotFired), `BuildingTurret` (yaw tracking, anticipation→recoil kick, Tesla charge-squash/pop, ease-out-back placement pop-in), projectile juice (per-settings muzzle/impact VFX, impact tint, spin; metallic cannonball + Meshy Arrow bolt + purple emissive mage bolt, all with trails). Trajectory heights raised for tower clearance. Remaining for Day 3: SFX, haptics, camera shake, hit-stop, boss-slam telegraph decal, freeze tint on enemies, music.
Tuning knobs: BuildingTurret serialized fields per prefab; ProjectileSettingsSO juice block; `_yawModelOffsetDegrees` if a mesh's barrel axis isn't +Z.

**17 Jul additions:** X-Bow nocked-arrow cycle — `BuildingTurret._loadedAmmo` (the Arrow model on the bow): hidden the instant the shot fires ("becomes" the flying bolt), pops back with a 0.12 s ease-out-back on the next windup. X-Bow mesh fires along local −X → `_yawModelOffsetDegrees = 90` on its prefab (measured live: arrow long-axis vs firing line delta 0°). Muzzle moved to the nocked-arrow position so the projectile continues from it. If Cannon's barrel ever looks misaligned, apply the same live measurement (arrow/barrel world bounds vs pivot yaw) and set its offset.

## Juice map (event → response)

| Event | VFX | SFX | Haptic |
|---|---|---|---|
| Enemy killed | death puff + orb spawn | pop | light tick |
| Orb credited | bar flash +count | coin chime | — |
| Card drag start | ghost + range ring | pick | selection tick |
| Building placed | dust slam + ring pulse | thud | medium |
| Spell Arrows | arrow rain | whoosh-thunk | light |
| Spell Fireball | explosion + camera shake (small) | boom | medium |
| Spell Freeze | frost burst + tinted enemies | crystal | light |
| Spell Lightning | 3 bolts + flash + hit-stop (~60 ms) | thunder | heavy |
| Tesla fire | electric arc | zap | — |
| Tower damaged | hit flash + HP bar shake | impact | light (throttled) |
| Boss slam telegraph | ground ring decal grows | rumble build | — |
| Boss slam | shockwave + camera shake (big) | slam | heavy |
| Wave cleared | +3⚡ flourish | fanfare stinger | success pattern |
| Victory/Defeat | confetti / tower crumble | jingle | — |

## Implementation
- `JuiceDirector` subscribes to GameEvents, triggers pooled VFX prefabs + `ISfxService` + `IHapticsService`. Gameplay code never calls juice directly.
- Haptics: `Handheld.Vibrate` fallback; prefer fine-grained (e.g. Nice Vibrations-style or Android VibrationEffect / iOS CoreHaptics via simple wrapper). Throttle: max 1 haptic / 100 ms.
- Camera: single `CameraShaker` (amplitude-capped, additive sources).
- Audio: `SfxService` (pooled AudioSources, pitch jitter ±5%), `MusicService` (layered intensity: calm ↔ wave overlap count). AI-generate: music via Suno, SFX via ElevenLabs — log in AI Log.
- VFX: particle prefabs; AI-gen flipbooks/textures where useful.

## Acceptance
- Sound off / haptics off toggles work; no GC spikes from juice (pooled); a full run "feels" alive — every kill visibly pays.

## Notes / changes
- (log changes here)

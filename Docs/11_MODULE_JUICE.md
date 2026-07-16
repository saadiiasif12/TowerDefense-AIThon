# 11 · Module: Juice — VFX/SFX/Haptics/Audio (RoyalSiege.Juice / RoyalSiege.Audio)

**Status: NOT STARTED** · Day 3 focus, but hooks (events) exist from Day 1. Haptics + SFX + VFX are SCORED requirements.

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

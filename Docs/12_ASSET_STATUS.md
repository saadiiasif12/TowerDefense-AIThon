# 12 · Asset Status — real vs dummy

Update this whenever assets are imported/replaced. Models so far are Meshy AI generated (log in AI Log).

## Characters (imported)

| Design unit | Asset | Location | Status |
|---|---|---|---|
| Ogre | `Ogre.prefab` (Purple Ogre + Spiked War Club) | `Characters/Enemies/Enemy 1/Prefab/` | ✅ model imported |
| Goblin | `Goblin.prefab` (Hookhand Goblin + C-clamp handheld) | `Characters/Enemies/Enemy 2/Prefabs/` | ✅ model imported |
| Mage | — none — | — | ❌ **DUMMY** for now → use Skeleton Knight prefab as stand-in (decision in 01 open questions) |
| Ogre Warlord (boss) | — none — | — | ❌ **DUMMY** → scaled-up Ogre + tint until boss model arrives |
| Skeleton Knight | `Skeleton.prefab` (+ Greysteel Dagger sword) | `Characters/Enemies/Enemy 3/Prefab/` | ✅ imported, NOT in design — serving as Mage stand-in |

## Animations — ✅ WORKING (16 Jul)
- All 6 general clips are **humanoid**. Goblin + Skeleton FBXs converted Generic→Humanoid (avatars verified `isHuman`).
- Shared `Characters/GeneralAnimations/AC_Unit.controller`: Walk (motion = 1D "Locomotion" blend tree on `MoveBlend`: 0 = Walk@0.08× idle-sway stand-in, 1 = full walk; state speed = MoveSpeed param for exact foot matching) / Attack (speed=AttackSpeed param) / Die, AnyState triggers. Assigned to all 3 enemy prefabs. No dedicated Idle clip in the pack — sway is the stand-in until one arrives.
- Per-enemy AnimatorOverrideControllers (17 Jul) — each enemy prefab now runs its own AOC over `AC_Unit`: `Enemy 1/AOC_Ogre`, `Enemy 2/AOC_Goblin`, `Enemy 3/AOC_Skeleton`, `Enemy 4/AOC_DoubleHorn` (`.overrideController`). All four currently override Walk 1/Attack 1/Die 1 with **Enemy 2's mixamo clips** (`Enemy 2/Animations/Walk|Attack|Death.anim`, humanoid) as a stand-in — swap each AOC's 3 clip slots when per-character clips arrive, no code change (UnitAnimator auto-detects attack clip length via the override).
- Enemy 4 (Double Horn / Twin-Headed Orc) rig flipped Generic→Humanoid (`animationType: 3`, `avatarSetup: 1`) + Animator wired to AOC_DoubleHorn with root motion off — ⚠️ auto-mapped avatar not yet verified in editor.

## Buildings / Tower
| Unit | Status |
|---|---|
| Royal Tower | ✅ real model (`Assets/Environment/Tower/Prefab/Tower.prefab`, Tripo AI) as visual child, auto-scaled; placeholder cylinder hidden |
| Cannon / Tesla / X-Bow | ✅ real models (Meshy, `Environment/Tower/Small Towers/{Canon,Tesla,Mortar}.prefab`) nested as visual children of `Building_Cannon/Tesla/XBow`; placeholder primitives removed. Gameplay/stats/GUIDs unchanged. **Not yet play-verified** — tile scale/orientation may need in-editor tweak. Drag ghost still primitive |
| Projectiles / orbs | ❌ DUMMY (small spheres; orb = magenta) |

## Environment
- ✅ Ground: `Assets/Ground/Texture/grass pattern.png` tiled 9×9 on a 36×36 plane (wrap=repeat) — mock direction.
- **Requirement: ≥2 environments** — second biome undecided (01 open questions). Forest ring / edge dressing still missing vs mock.

## UI
- ✅ Greybox HUD live (hand bar + NEXT, energy bar, wave banner, end panel, world health bars). No art skin yet — AI-generate card frames/icons on Day 3 (Scenario/Ludo.ai). Mock `IMG-20260716-WA0006.jpg` remains the layout reference.

## Audio
- Nothing yet. Day 3: Suno (music), ElevenLabs (SFX).

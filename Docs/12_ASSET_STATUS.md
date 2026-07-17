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
- Shared `Characters/GeneralAnimations/AC_Unit.controller`: Walk (motion = 1D "Locomotion" blend tree on `MoveBlend`: 0 = Walk@0.08× idle-sway stand-in, 1 = full walk; state speed = MoveSpeed param for exact foot matching) / Attack (speed=AttackSpeed param) / Die, AnyState triggers. MoveSpeed/AttackSpeed default to 1 (0 froze the pose after Animator.Rebind on pool reuse); Die AnyState transition is ordered before Attack with interruption source = Source so death interrupts an in-progress attack crossfade (Attack entry 0.25 s, Die 0.3 s, Attack→Walk exit 0.3 s). Param damping runs per-frame in `UnitAnimator.Update` (damping from the 10 Hz tick starved the blend). Assigned to all 3 enemy prefabs. No dedicated Idle clip in the pack — sway is the stand-in until one arrives. Hurt state (trigger `Hurt`, speed=`HurtSpeed`): motion is `GeneralAnimations/Animations/Hurt.anim` — a DUPLICATE of Die 2 (no hurt clip in the pack); the state exits to Walk at 35% (only the opening flinch plays, speed-scaled by `UnitAnimator.PlayHurt` to fit the configured stagger). Replace per character by overriding the "Hurt" slot in each AOC when real hurt clips arrive. DONE for the Goblin: `AOC_Goblin` overrides Hurt → `Enemy 2/Animations/Hurt.anim` (1.4 s mixamo hit-react, humanoid). Ogre/Skeleton/DoubleHorn AOCs still fall back to the base Die-2-copy stand-in.
- Per-enemy AnimatorOverrideControllers (17 Jul) — each enemy prefab now runs its own AOC over `AC_Unit`: `Enemy 1/AOC_Ogre`, `Enemy 2/AOC_Goblin`, `Enemy 3/AOC_Skeleton`, `Enemy 4/AOC_DoubleHorn` (`.overrideController`). All four currently override Walk 1/Attack 1/Die 1 with **Enemy 2's mixamo clips** (`Enemy 2/Animations/Walk|Attack|Death.anim`, humanoid) as a stand-in — swap each AOC's 3 clip slots when per-character clips arrive, no code change (UnitAnimator auto-detects attack clip length via the override).
- Enemy 4 (Double Horn / Twin-Headed Orc) rig flipped Generic→Humanoid (`animationType: 3`, `avatarSetup: 1`) + Animator wired to AOC_DoubleHorn with root motion off — ⚠️ auto-mapped avatar not yet verified in editor.

## Buildings / Tower
| Unit | Status |
|---|---|
| Royal Tower | ✅ real model (Tripo AI, now under `Assets/TowerDefense/Environment/Tower/`) as visual child; placeholder cylinder hidden |
| King | ✅ Golden King (Meshy, `Characters/King/`) converted to Humanoid, standing on the tower top under scale-compensating `KingRoot`; `AC_King` controller (Idle=own walk clip @0.15 sway, Attack=general throw synced to attack rate); KingView rotates him to the king-attack's target. Fires `Projectile_KingMagic` (emissive orb, dual trail, sparkle wake) with `VFX_MagicFlash` cast + `VFX_MagicImpact` big splash |
| Cannon / Tesla / X-Bow | ✅ real models (Meshy, `Environment/Tower/Small Towers/{Canon,Tesla,Mortar}.prefab`) nested as visual children of `Building_Cannon/Tesla/XBow`; placeholder primitives removed. Gameplay/stats/GUIDs unchanged. **Not yet play-verified** — tile scale/orientation may need in-editor tweak. Drag ghost still primitive |
| Projectiles / orbs | ✅ Proj_CannonBall (metallic + smoke trail + spin), Proj_Bolt (Meshy Arrow art + tracer), Proj_MageBolt (purple emissive + trail). Orb still magenta sphere |
| VFX | ✅ `Prefabs/VFX/`: MuzzleFlash, Impact (tintable), Puff, Sparks, SpellBurst — all pooled via VfxSpawner. Tesla zap = ZapArcRenderer |

## Environment
- ✅ Ground: `Assets/Ground/Texture/grass pattern.png` tiled 9×9 on a 36×36 plane (wrap=repeat) — mock direction.
- **Requirement: ≥2 environments** — second biome undecided (01 open questions). Forest ring / edge dressing still missing vs mock.

## UI
- ✅ Greybox HUD live (hand bar + NEXT, energy bar, wave banner, end panel, world health bars). No art skin yet — AI-generate card frames/icons on Day 3 (Scenario/Ludo.ai). Mock `IMG-20260716-WA0006.jpg` remains the layout reference.

## Third-party FX
- ✅ `ThirdParty/MagicArsenal/` — dependency-closure subset (26 files, 3.7 MB) of the Arcane projectile/impact/muzzle triplet from the local Particles-Library project. Materials auto-converted Legacy-Additive → URP Particles Unlit. Pool-safe copies (lights + MagicLightFade stripped) live in `Prefabs/VFX/VFX_Arcane*`. Full library (6600+ prefabs, 30+ packs) at `D:\GameDevelopment\Assets\ParticlesLibrary\Particles-Library` + its `particles-skill.md` reference — pull more via the same closure-copy recipe (see AI Log).

## Audio
- ✅ First SFX: `arcaneimpact.wav` on the king's impact (VfxInstance now auto-plays an AudioSource if present). More Day 3: Suno (music), ElevenLabs (SFX).

## Overnight juice pass additions (17→18 Jul)
- ✅ New spell VFX prefabs in `Prefabs/VFX/`: `FX_ArrowRain` (15 Meshy-arrow skyfall volley), `FX_Meteor` (fire projectile + trail skyfall), `VFX_FireballBlast` (+fireimpact.wav), `VFX_LightningPillar` (+lightningimpact.wav), `VFX_FreezeNova` (+frostimpact02.wav), `VFX_GroundFrost` (lingering ice patch), `VFX_BoneBurst` (skeleton death bones, capsule mesh particles + `Mat_Bone`).
- ✅ MagicArsenal subset extended (+28 files: Fire/Lightning/FrostV2/GroundFrost sets) via `Tools/import-magic-arsenal.ps1` (reusable GUID closure copier); 9 more materials URP-converted. All library blasts shrunk to arena scale (they ship huge).
- ✅ `Shaders/RS_Dissolve.shader` — URP unlit UV-noise dissolve with ember edge (enemy deaths). Mobile-cheap, one pass.
- ✅ X-Bow: bow string LineRenderer (`Mat_BowString`) + limb-tip anchors in `Building_XBow`; Tesla: coil-top `Muzzle` in `Building_Tesla`.
- ✅ TestRange is now the artist sandbox: serialized `spellCards` + `vfxGallery` lists on TestRangeContext generate "Cast X" and "FX: Y" replay buttons at runtime — add/tune assets with zero code.
- Audio note: TestRange camera now has an AudioListener (spell SFX audible there).

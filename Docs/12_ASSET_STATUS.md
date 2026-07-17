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
- Shared `Characters/GeneralAnimations/AC_Unit.controller`: Walk (speed=MoveSpeed param) / Attack (speed=AttackSpeed param) / Die, AnyState triggers. Assigned to all 3 enemy prefabs.
- Per-character animations later → swap via AnimatorOverrideController, no code change (UnitAnimator auto-detects attack clip length).

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

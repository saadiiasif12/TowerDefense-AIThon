# AI LOG — Team 49 · GD Main · Tower Defence · "Royal Siege"

> Compulsory per hackathon rules (§5): every AI tool, the prompts/workflows behind key outputs, which outputs were adopted, and usage metrics where available. Maintained daily from Day 1. Append entries as work happens — never backfill from memory at the end.

**Toolchain:** Claude Code / Fable 5 (code, architecture, docs, Unity via MCP) · Meshy AI (3D models) · planned: Scenario/Ludo.ai (2D/UI), Mixamo/Cascadeur (anim), Suno (music), ElevenLabs (SFX), Runway (video).

Entry format: `| time | tool | task | prompt/workflow (summary) | output & adopted? | metrics/notes |`

---

## Day 1 — Thursday 16 July 2026

| Time | Tool | Task | Prompt / workflow | Output & adopted? | Notes |
|---|---|---|---|---|---|
| ~11:00 | Meshy AI | 3D enemy models | Text/image→3D generations: "Purple Ogre with Spiked club" (biped rig), "Hookhand Goblin" (biped), "Skeleton Knight" (biped), props (Spiked War Club, Greysteel Dagger, Rusty C-clamp) | ✅ Adopted — imported as Ogre/Goblin/Skeleton prefabs under `Assets/TowerDefense/Characters/` | Rigged biped outputs; textures included |
| earlier | Claude (chat) | GDD authoring & balance verification | Iterated Royal Siege GDD v1→v2; built HTML mechanics prototype (`royal-siege (1).html`) with embedded CFG; validated balance via two headless Node harnesses (scripted-player winrate ~75%, invariant tests) | ✅ Adopted as design source of truth (`Documents/GDD_Royal_Siege_v2.md`, `README_v2.md`) | Prototype is mechanics-only, not art |
| session | Claude Code (Fable 5) | Project analysis & full documentation scaffold | Fed: hackathon PDF, GDD v2, README v2, v1 JSONs, UI mocks, meeting-notes photo. Workflow: extract rules/requirements → reconcile GDD v2 with meeting deltas (cut Bomb Tower → 7 cards; cut evolutions; strict-range fix) → write CLAUDE.md + Docs/00–12 module docs + TASKS.md + this log | ✅ Adopted — `CLAUDE.md`, `Docs/*` (13 files), `AILog/AI_LOG.md` | Docs double as persistent AI context (memory-clear-safe) |

| session | Claude Code (Fable 5) | Full gameplay codebase — all core modules | Workflow: architecture constraints locked in docs first (SOLID, interfaces, factory/strategy/pool patterns, no singletons/statics, no DI framework, shared configurable projectile module, AttackCycle anim-damage sync) → generated 35 C# scripts across 9 namespaces (Core/Data/Combat/Units/Buildings/Economy/Cards/Spells/Placement) → compiled clean on first pass (0 errors, 0 warnings) | ✅ Adopted — `Assets/TowerDefense/Scripts/` | ~35 files; single-pass compile |
| session | Claude Code + Unity MCP | Data assets, prefabs, scene authoring | Executed editor C# through MCP: created all balance ScriptableObjects (7 cards, 4 enemies, 5 projectile settings, 4 spell effects, configs, deck, 12-wave timeline) with GDD v2 values; dummy prefabs (buildings/tower/orb/projectile/ghosts); wired Game.unity (arena, rings, camera, composition root) | ✅ Adopted — `Assets/TowerDefense/Data/`, `Prefabs/`, `Scenes/Game.unity` | Values traceable to Docs/01 tables |
| session | Claude Code + Unity MCP | Automated play-mode smoke test | Entered play mode via MCP and drove the game programmatically: verified wave spawning, tower kills, orb economy (cap + waste), deck ring-buffer cycling, real drag-flow X-Bow placement, 4× speed, defeat at wave 10 with auto-pause. 0 console errors across full run | ✅ Slice verified working | Found+fixed: tower pivot height leaking into planar math |

| session | Claude Code + Unity MCP | HUD, animations, grass, health bars | User feedback pass: built greybox HUD entirely via editor scripting (canvas, 4-slot hand bar + NEXT with radial cooldowns/cost pips/drag-to-place, energy bar with cap pulse, wave banner, end screen); converted Goblin/Skeleton rigs Generic→Humanoid and authored shared AnimatorController over the Meshy general clips (state speed bound to params so swings match attack rate); tiled `grass pattern.png` ground; swapped Tripo tower model in for the placeholder; world-space health bars on tower/buildings/enemies via IHealthReadout polling | ✅ Adopted — verified in play mode with screenshots: Walk/Attack anim states correct, mage sieges at exact strict range 5.0, orbs fly, HUD updates, 0 console errors | 6 new UI scripts + 5 gameplay edits, all editor work scripted (reproducible) |

<!-- Append new entries above this line, newest last within the day. Add "## Day 2 — Friday 17 July 2026" heading when Day 2 starts. -->

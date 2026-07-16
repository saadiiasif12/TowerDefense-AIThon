# CLAUDE.md — Royal Siege (Tower Defense · AI Gaming Hackathon 2026)

**Read this first.** If context/memory was cleared, read the docs in this order:
1. `Docs/00_PROJECT_OVERVIEW.md` — competition, deadlines, judging, deliverables
2. `Docs/01_DESIGN_CURRENT.md` — the FINAL design (GDD v2 + meeting deltas). Authoritative.
3. `Docs/02_ARCHITECTURE.md` — code architecture, SOLID conventions, folder layout
4. `Docs/TASKS.md` — current progress, what's done, what's next
5. Module docs `Docs/03..11_MODULE_*.md` — per-module detail
6. `Docs/12_ASSET_STATUS.md` — which assets are real vs dummy

## What this project is

**Royal Siege** — a Clash Royale × Tower Defense hybrid. Mobile portrait, Unity **6000.3.6f1**, built-in render pipeline scene at `Assets/TowerDefense/Scenes/Game.unity`. Defend a central Royal Tower on a circular arena against 12 timed waves; spend Energy (earned ONLY from kills + wave clears) on building cards and spell cards from a cycling hand.

Built for **AI GAMING HACKATHON 2026** (16–18 July 2026), Team 49 · GD Main · Tower Defence category. Full brief: `Docs/00_PROJECT_OVERVIEW.md`.

## Hard rules — never violate

1. **AI LOG IS COMPULSORY.** After every meaningful AI-assisted task, append an entry to `AILog/AI_LOG.md` (tool, prompt/workflow summary, what was adopted). Judges verify it. No exceptions.
2. **Keep docs current.** After completing a task that changes design, architecture, or status: update the relevant module doc + `Docs/TASKS.md`. These docs ARE the project memory.
3. **Zero hardcoded stats.** Every tunable number (damage, HP, cost, range, timings, wave comps, economy) lives in a ScriptableObject under `Assets/TowerDefense/Data/`. Gameplay code reads config; it never contains balance numbers.
4. **SOLID / clean code** — see `Docs/02_ARCHITECTURE.md` for the concrete conventions (namespaces, composition over inheritance, interfaces for services, no god classes, event-driven UI).
5. **Don't hallucinate design.** Everything gameplay-related is already specified in `Docs/01_DESIGN_CURRENT.md` and `Assets/TowerDefense/Documents/GDD_Royal_Siege_v2.md`. If something is genuinely unspecified, add it to the "Open Questions" list in `01_DESIGN_CURRENT.md` and ask — don't invent.
6. **Originality rule:** reference games (Kingdom Rush, Boom Castle, …) are direction only. No cloning — disqualification risk.
7. All work must be done during the 3 event days (16–18 July 2026).

## Key design facts (quick recall — details in 01_DESIGN_CURRENT.md)

- **7 cards** (meeting delta: Bomb Tower CUT): Buildings — Cannon, Tesla, X-Bow · Spells — Arrows, Fireball, Freeze, Lightning
- **No evolutions / card upgrading** (meeting delta: cut for now)
- **Energy economy:** no passive regen; kills drop orbs (Goblin 0.25 / Mage 0.5 / Ogre 1.0 / Boss 3.0), +3 per wave clear, start 9, cap 10
- **Strict range:** attack allowed only when enemy CENTER is inside the range circle (`dist <= range`). The drawn ring never lies.
- **No one-shots:** every spell leaves its counter-target alive (Arrows 80 < Goblin 90, Fireball 260 < Mage 290, Lightning 275 < Mage 290)
- **Deterministic:** one deck shuffle at match start is the ONLY RNG. Card cycle is a FIFO ring buffer. Combat logic ticks at 10 Hz, visuals interpolate.
- Enemies: Goblin, Mage, Ogre, Ogre Warlord (boss, wave 12). `globalEnemyDamageMultiplier = 0.5` is THE difficulty knob.
- 12 waves over ~7 min, waves spawn at absolute `startTime` unconditionally.

## Repo layout

```
CLAUDE.md                  ← you are here
Docs/                      ← all planning/status docs (project memory)
AILog/AI_LOG.md            ← compulsory AI usage log
Assets/TowerDefense/
  Documents/               ← original GDD v2, README v2, v1 JSONs (JSONs are OUTDATED v1 — do not trust; GDD v2 + Docs/01 win)
  MocksDirectionSoFar/     ← UI mocks (REFERENCE ONLY, not final art/UX)
  Scenes/Game.unity        ← main scene
  Characters/              ← imported enemy models (Meshy AI) + GeneralAnimations
  Scripts/                 ← all game code (namespaced RoyalSiege.*)
  Data/                    ← ScriptableObject assets (created as modules land)
Assets/Environment, Assets/Ground  ← environment art
D:\Users\GameDistrict\royal-siege (1).html  ← mechanics prototype (v2 logic reference; NOT art reference)
```

## Workflow

- Branch: work happens on `Saad/feature-Gameplay`; PRs to `main`.
- Unity access via UnityMCP tools (scene edits, prefabs, ScriptableObject assets, play-mode tests, console).
- Missing art/audio → use primitives/placeholder + log in `Docs/12_ASSET_STATUS.md`. General animations in `Characters/GeneralAnimations` are shared by ALL characters until per-character anims arrive.

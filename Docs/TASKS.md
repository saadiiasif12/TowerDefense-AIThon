# TASKS — living progress tracker

Legend: ⬜ todo · 🔄 in progress · ✅ done · Update after EVERY task. Keep newest notes at top of the log section.

## Day 1 (16 July) — CONTROLLER: playable vertical slice by end of day

- ✅ Read hackathon brief, GDD v2, mocks, meeting notes; set up all Docs/ + CLAUDE.md + AI Log
- ✅ M-Data: all SO classes + authored assets (7 cards, 4 enemies, 5 projectile settings, 4 effects, configs, deck, 12-wave timeline) in `Assets/TowerDefense/Data/`
- ✅ M-Core: GameContext (composition root), TickSystem (10 Hz + speed/pause), GameEvents, ObjectPool, RangeMath, WinLoseEvaluator
- ✅ Arena scene: circular ground r15, deployment ring r5, map edge ring, 8 spawn points, portrait camera, Royal Tower placeholder — `Scenes/Game.unity` wired & saved
- ✅ M-Enemies: EnemyAgent (priority targeting + hysteresis, melee/ranged, statuses, boss slam), pooled EnemyFactory, WaveScheduler (absolute times, formation offsets)
- ✅ M-Buildings/Combat: StructureAttack (targeter+cycle+delivery), shared Projectile module (arc/height configurable), RoyalTower (2 attacks), RangeRing
- ✅ M-Economy: EnergyBank (no regen, hard cap, waste event) + orb flight credit-on-arrival + unconditional wave bonus
- ✅ M-Cards: DeckService ring buffer (single-shuffle RNG), per-card cooldowns, CardPlayService (validate at release)
- ✅ M-Spells/Placement: SpellEffectSO strategies (AreaDamage/Freeze/Lightning), SpellCaster, PlacementValidator, ghosts, PlacementController + temp DebugInputDriver (keys 1–4 + mouse)
- ✅ Slice smoke test IN PLAY MODE: wave 1 goblins spawned→walked→died to tower; orbs credited (capped at 10 correctly); X-Bow placed via real drag flow (energy 10→4, hand cycled, NEXT correct)
- ✅ Full-run verification at 4× speed: ran to wave 10 with one X-Bow, tower died → MatchEnded(defeat) fired, clock auto-paused. 0 console errors/warnings entire run
- ⬜ Verify remaining acceptance cases: pure-idle dies ~wave 4, ogre buildings-first, mage stops at siege range, freeze/lightning/fireball behavior, victory + stars path
- ✅ Greybox HUD: hand bar (4 slots + NEXT, cost pips, radial cooldowns, affordability grey-out, 40 px drag threshold), energy bar (magenta, gold pulse at cap), wave banner + clear bonus flash, victory/defeat panel with stars. Screen Space Overlay canvas, 1080×1920 reference, EventSystem + InputSystemUIInputModule
- ✅ Animations: Goblin/Skeleton FBX rigs converted Generic→Humanoid (avatars verified human), shared `AC_Unit.controller` (Walk/Attack/Die; state speed driven by MoveSpeed/AttackSpeed params) assigned to all 3 enemy prefabs. UnitAnimator auto-reads attack clip length → swing/damage stay in sync; walk pose freezes when idle. Verified in play mode: Walk→Attack states, facing correct
- ✅ Grass ground per mocks: `grass pattern.png` (wrap=repeat) on a 36×36 plane, 9×9 tiling
- ✅ Real tower model (`Environment/Tower`) swapped in for the grey cylinder, auto-scaled to ~3.2 tiles
- ✅ World-space health bars: enemies + buildings (hide when full) + tower (always visible), billboarded, color lerps green→red, driven via `IHealthReadout` (UI polls gameplay; gameplay never references UI)

- ✅ Crowd/animation polish: arc-spread staggered spawn formations, per-unit body radii + separation steering (no overlaps — audited), velocity-matched walk anim (no foot-slide), de-synced walk phases, smooth turns, blended animator transitions

- ✅ Full-bleed ground: map-edge ring hidden (visual only — the r=15 gameplay boundary lives in config), ground plane 200×200 with density-preserving 50×50 tiling, camera clears to grass green — no skybox on any phone/tablet aspect

## Day 2 (17 July) — CONTENT

- ⬜ All 7 cards implemented (Tesla instant, X-Bow, Fireball, Freeze, Lightning + statuses)
- ⬜ All 4 enemies (Mage ranged+splash, Ogre buildings-first, Boss + slam) with priority/hysteresis rules
- ⬜ Full 12-wave timeline + WaveCleared/victory/stars/defeat + end screens
- ⬜ Full HUD per 10_MODULE_UI (NEXT preview, banners, pause 3-2-1, speed toggle)
- ⬜ Environment 1 final (meadow) + **Environment 2** (second biome — decide theme)
- ⬜ Character models hooked with shared animations; dummy → real swaps where assets arrive
- ⬜ Balance pass 1: full runs; check idle-dies-wave-4, winners at 40–70% tower HP
- ⬜ Mobile build on device; touch + performance check

## Day 3 (18 July) — POLISH & SUBMISSION

- ⬜ Juice map implemented (11_MODULE_JUICE): VFX, SFX, haptics, camera shake, hit-stop
- ⬜ Music (Suno) + SFX (ElevenLabs) integrated
- ⬜ UI skin pass (AI-generated card frames/icons)
- ⬜ Balance pass 2 + bugfix
- ⬜ 2–3 marketing creatives (AI)
- ⬜ 5-minute video (concept, gameplay, AI workflow)
- ⬜ AI Log final review + submission

## Change log (newest first)

- **16 Jul (PM)** — Entire gameplay core implemented (35 scripts, 9 modules), all data assets authored from design tables, Game.unity wired, play-mode smoke test passed (spawning→combat→economy→placement→defeat all working, 0 errors). Spawn-point picks for waves where the GDD didn't specify are authored in `Waves_Level1.asset` and can be retuned freely. Note: enemy models currently slide (no AnimatorController yet); Mage uses Skeleton model; Boss reuses Ogre model.

- **16 Jul** — Project docs scaffolded from PDF brief + GDD v2 + meeting notes. Design deltas locked: 7 cards (Bomb Tower cut), no evolutions, strict range confirmed. JSONs in Documents/ marked outdated (v1).

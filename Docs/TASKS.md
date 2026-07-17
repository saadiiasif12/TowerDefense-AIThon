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
- ✅ Animations: Goblin/Skeleton FBX rigs converted Generic→Humanoid (avatars verified human), shared `AC_Unit.controller` (Walk/Attack/Die; state speed driven by MoveSpeed/AttackSpeed params) assigned to all 3 enemy prefabs. UnitAnimator auto-reads attack clip length → swing/damage stay in sync. Verified in play mode: Walk→Attack states, facing correct
- ✅ Locomotion blend tree (17 Jul): Walk state motion replaced with 1D "Locomotion" blend tree on new `MoveBlend` param — 0 = Walk clip @0.08× (near-still idle sway; no Idle clip exists in the pack), 1 = full walk. Fixes the hard mid-stride freeze on stop; `MoveSpeed` state-speed param kept for exact foot-speed matching (parked at 1 while stopped so the sway plays). Attack entry crossfade 0.15→0.2 s. ⚠️ Not yet play-verified (edited outside the editor)
- ✅ Per-enemy AnimatorOverrideControllers (17 Jul): AOC_Ogre / AOC_Goblin / AOC_Skeleton / AOC_DoubleHorn created over `AC_Unit` and wired into each enemy prefab's Animator — all four override Walk/Attack/Die with Enemy 2's mixamo clips for now; per-character clips later = swap 3 slots per AOC. Enemy 4 rig flipped Generic→Humanoid + controller wired (had none). ⚠️ Not yet play-verified
- ✅ Anim sync fix (17 Jul): enemies looked static — `UnitAnimator` damped MoveBlend/MoveSpeed from the 10 Hz tick with frame-dt (~0.16 s of damping progress per real second, blend stuck near the 0.08× sway), and Rebind reset MoveSpeed/AttackSpeed to controller default **0** (fully frozen pose on pool reuse). Fixed: targets cached in SetMoving, damping integrated per-frame in UnitAnimator.Update; Rebind snaps MoveBlend=0/MoveSpeed=1; controller defaults MoveSpeed/AttackSpeed = 1. Transitions smoothed: Die AnyState transition ordered FIRST + interruption source Source on all three transitions (death now cuts into an in-progress attack crossfade), Attack entry 0.25 s, Die 0.3 s. Also: death anim now plays when a unit dies while frozen/stunned (playback speed restored in OnDied). ✅ Play-verified 17 Jul (waves 1–6 live: enemies march at full walk immediately, packs spread, combat clean, 0 console errors/warnings)
- ✅ Hurt/hit-stagger mechanic (17 Jul): a MOVING enemy that takes damage stops and plays a hurt flinch, then resumes walking. No hurt clip exists in the pack → new `Hurt.anim` = copy of Die 2 (opening backward flinch), Hurt state plays its first 35% speed-scaled to fit the stagger, then crossfades back to Walk (0.15 s in / 0.25 s out; walk params intentionally untouched during the stagger so the exit lands mid-stride). AnyState priority: Die → Hurt → Attack, interruptible. Gameplay side gated in EnemyAgent: only while moving (never attacking/frozen), per-enemy `hurtStaggerSeconds` + anti-stunlock `hurtCooldownSeconds` in EnemyDefinitionSO (0.35/2.5 all, boss 0.3/6 — boss under sustained fire must not perma-flinch). ✅ Ran clean in a live play session 17 Jul (waves 1–6, staggers firing under king fire, 0 warnings/errors); flinch READ not yet eyeballed frame-by-frame — swap the Hurt.anim slot per-AOC when real hurt clips arrive
- ✅ Per-hit hurt (17 Jul, user request): every hit on a WALKING enemy now replays the flinch — `hurtCooldownSeconds` 0 for Goblin/Mage/Ogre (boss kept 6 s anti-flinch-lock), mid-stagger hits restart the stagger, AnyState→Hurt allows self-retrigger. Deterministically verified in-editor (hit → Hurt, mid-flinch hit → Hurt>Hurt restart, post-recovery hit → Walk>Hurt). Attacking/frozen units still never flinch. ⚠️ Balance: massed towers can now near-lock a walking ogre — Day-3 balance pass should watch this (knob: per-enemy hurtCooldownSeconds)
- ✅ Animator param-guard + MCP session fix (17 Jul): AC_King spammed "Parameter 'Hash -1142910944' does not exist" — that hash is CRC32("MoveBlend"): the shared UnitAnimator's new per-frame damping drove MoveBlend/MoveSpeed on EVERY animator, but AC_King lacks MoveBlend (and Hurt). UnitAnimator now caches the controller's parameter set in Awake and gates every Set* call on it — safe on any rig. Play-verified live: 0 warnings across waves 1–6. Also fixed Unity MCP for future sessions: config only existed under the forward-slash project key in ~/.claude.json → added repo-root `.mcp.json` (UnityMCP → http://127.0.0.1:8080/mcp). NOTE: the hub server must be running — start from the MCP for Unity editor window, or `uvx --from mcpforunityserver mcp-for-unity --transport http --http-url http://127.0.0.1:8080`
- ✅ Grass ground per mocks: `grass pattern.png` (wrap=repeat) on a 36×36 plane, 9×9 tiling
- ✅ Real tower model (`Environment/Tower`) swapped in for the grey cylinder, auto-scaled to ~3.2 tiles
- ✅ World-space health bars: enemies + buildings (hide when full) + tower (always visible), billboarded, color lerps green→red, driven via `IHealthReadout` (UI polls gameplay; gameplay never references UI)

- ✅ Crowd/animation polish: arc-spread staggered spawn formations, per-unit body radii + separation steering (no overlaps — audited), velocity-matched walk anim (no foot-slide), de-synced walk phases, smooth turns, blended animator transitions

- ✅ Full-bleed ground: map-edge ring hidden (visual only — the r=15 gameplay boundary lives in config), ground plane 200×200 with density-preserving 50×50 tiling, camera clears to grass green — no skybox on any phone/tablet aspect

## Day 2 (17 July) — CONTENT

- ✅ Building attack juice: BuildingTurret (cannon Octagone yaw-tracks target 420°/s, Body anticipation+recoil; X-Bow fast swivel 720°/s + snappy recoil; Tesla charge-squash→discharge-pop; all buildings ease-out-back pop-in on placement)
- ✅ Projectile art + juice: metallic cannonball (spin+smoke trail), Meshy Arrow bolt (tracer), purple emissive mage bolt; per-settings muzzle flash + impact burst (tintable); trajectory heights/arcs raised so nothing clips the tower
- ✅ VFX pipeline: pooled VfxSpawner + JuiceDirector (death puffs, placement dust, per-spell tinted bursts) + Tesla ZapArcRenderer (animated jagged arcs + sparks). All verified live: pools show MuzzleFlash/Impact/Puff/SpellBurst/Sparks all spawning, turret yaws confirmed tracking, 0 console errors

- ⬜ All 7 cards implemented (Tesla instant, X-Bow, Fireball, Freeze, Lightning + statuses)
- ⬜ All 4 enemies (Mage ranged+splash, Ogre buildings-first, Boss + slam) with priority/hysteresis rules
- ⬜ Full 12-wave timeline + WaveCleared/victory/stars/defeat + end screens
- ⬜ Full HUD per 10_MODULE_UI (NEXT preview, banners, pause 3-2-1, speed toggle)
- ⬜ Environment 1 final (meadow) + **Environment 2** (second biome — decide theme)
- ⬜ Character models hooked with shared animations; dummy → real swaps where assets arrive
- ⬜ Balance pass 1: full runs; check idle-dies-wave-4, winners at 40–70% tower HP
- ⬜ Mobile build on device; touch + performance check

- ✅ **TestRange scene** (`Scenes/TestRange.unity`): isolated tuning sandbox — grass floor, full tower+king, wandering auto-heal dummy target with health bar, runtime button column (spawn each building/enemy, clear, kill all, wander toggle, HP reset, 0.25×/1×/3× speed for eyeballing trails), live Target DPS readout. `TestRangeContext` is a minimal composition root (no waves/economy) — spawnable lists are inspector-editable. Odin Inspector 3.1.14.2 imported for nicer inspectors
- ✅ King on the Royal Tower: Golden King (default scale) with AC_King (idle sway + speed-synced throw), turns to target, fires magic orb (dual trail + sparkle wake) with 5-layer arcane impact splash
- ✅ King spell "shooting star" pass: trail is one continuous ribbon (star head + distance-emitted dust tracing the path), pooled projectile FX hard-reset on launch + trail fades out at impact instead of cutting; new view-only `HitReaction` on all enemies/dummy (backward recoil that settles + glow flash on every hit); king casts from the actual RightHand bone (`KingView.CastPoint` → `firePoint`, spawnHeightOffset 0) with release timing verified against the throw clip (impactFraction 0.4 = hand-extended pose)

- ✅ **Overnight juice pass (17→18 Jul)** — see 11_MODULE_JUICE notes for full detail: sky-fall spell choreography (arrow volley / meteor+blast / freeze nova+tint+pose-hold / lightning pillars on actual victims, damage synced to landing via `fallDelaySeconds`), camera shake (trauma-based) + aspect-ratio fitter on both cameras, enemy spawn pop + death dissolve shader + skeleton bone-burst, Tesla coil-top forked thunder, X-Bow facing fix + live bow string, full card-animation spec implemented (select/drag-proxy/dissolve+name-float/cancel/refill/unaffordable-shake/recessed-slot), placement grid overlay synced with the grass checker, TestRange artist tools (cast-spell buttons, FX gallery replay buttons, serialized lists), first spell SFX wired (fire/lightning/frost impacts). All verified live, 0 errors.
- ⚠️ **Balance observation (18 Jul, from unattended run):** near-idle play (one fireball only) reached DEFEAT at wave 7/12 — pressure curve is alive but the wave-4 idle-death invariant should be re-verified by a human run on Day 3 (king 93 DPS folded from tower may have softened early waves).

- ✅ **QA round 1 — Coda bug sheet "Defence Tower-Bug Sheet-Build#(17-07-2026)"** (full detail: `QALogs/QA_Log_Build_17-07-2026.md`): 11 bugs triaged → 10 fixed, 1 confirmed by-design (DT-003 4-tower cap, GDD §3). Headline root cause: the spell ghost's range circle rendered at 60% of the true radius (0.6-scaled parent shrank the RangeRing) — behind all four "spell hits outside its radius" High bugs; RangeRing now scale-proof (world-space vertices). Also: rings clip to the map circle, player projectiles clamp flight+impact to their attack radius, tower/king targeting honours the map boundary (spawn-zone units untargetable), sky-fall damage re-checks the radius at landing, match-end cancels card selection + locks the hand + panel forced topmost, unaffordable cards can't enter placement, meteor drop straightened. Statuses + notes written back to the Coda sheet; design deltas in Docs/01.

## Day 3 (18 July) — POLISH & SUBMISSION

- ⬜ Juice map implemented (11_MODULE_JUICE): VFX, SFX, haptics, camera shake, hit-stop — **large part done overnight (VFX + shake + first SFX); remaining: haptics, hit-stop, music, full SFX set**
- ⬜ Music (Suno) + SFX (ElevenLabs) integrated
- ⬜ UI skin pass (AI-generated card frames/icons)
- ⬜ Balance pass 2 + bugfix
- ⬜ 2–3 marketing creatives (AI)
- ⬜ 5-minute video (concept, gameplay, AI workflow)
- ⬜ AI Log final review + submission

## Change log (newest first)

- **16 Jul (PM)** — Entire gameplay core implemented (35 scripts, 9 modules), all data assets authored from design tables, Game.unity wired, play-mode smoke test passed (spawning→combat→economy→placement→defeat all working, 0 errors). Spawn-point picks for waves where the GDD didn't specify are authored in `Waves_Level1.asset` and can be retuned freely. Note: enemy models currently slide (no AnimatorController yet); Mage uses Skeleton model; Boss reuses Ogre model.

- **16 Jul** — Project docs scaffolded from PDF brief + GDD v2 + meeting notes. Design deltas locked: 7 cards (Bomb Tower cut), no evolutions, strict range confirmed. JSONs in Documents/ marked outdated (v1).

# QA Log — Defence Tower Bug Sheet, Build 17-07-2026

**Source:** Coda / Superhuman Docs → `QA Tasks` → table *"Defence Tower-Bug Sheet-Build#(17-07-2026)"*
**QA round processed:** 17 July 2026 · **Fixed by:** Claude Code (Fable 5) session on branch `Saad/feature-Gameplay`
**Verification:** compile clean, play-mode checks in `Game.unity` (see per-bug notes), 0 console errors/warnings.

| ID | Severity | Status | Resolution |
|----|----------|--------|------------|
| DT-001 | Medium | ✅ Fixed | Range rings now clip to the map circle |
| DT-002 | Medium | ✅ Fixed | Projectile flight clamped to shooter's attack radius |
| DT-003 | High | ⚠️ By Design | Max 4 towers is an explicit GDD §3 rule — flagged to design, not changed |
| DT-004 | High | ✅ Fixed | Targeting ignores enemies whose center is outside the map circle |
| DT-005 | High | ✅ Fixed | Root cause: spell ghost ring drawn at 60% of true radius + resolve re-check |
| DT-006 | High | ✅ Fixed | Same root cause + resolve re-check |
| DT-007 | High | ✅ Fixed | Same root cause (Freeze is instant — ring fix alone covers it) |
| DT-008 | High | ✅ Fixed | Same root cause (Lightning is instant — ring fix alone covers it) |
| DT-009 | Medium | ✅ Fixed | Match end cancels selection/drag, locks hand input, panel forced topmost |
| DT-010 | Medium | ✅ Fixed | Unaffordable cards can no longer enter the placement state |
| DT-011 | Low | ✅ Fixed | Meteor lateral offset (-5,-2.5) → (-0.8,-0.4): near-vertical straight drop |

---

## DT-001 — Crossbow attack range extends outside the map

- **Root cause:** `RangeRing` always drew the full circle. Dragging a building/spell near the arena edge let the ghost's ring spill past the map boundary (X-Bow's 9-unit ring is the most visible case).
- **Fix:** `RangeRing.SetMapClip(center, radius)` — ring vertices outside the map circle are pulled onto the boundary, so the drawn shape becomes the exact intersection of range ∩ playable area. `GhostView.SetPosition` re-projects every drag frame; `PlacementController.Init` wires the map bounds (new `mapCenter` parameter from `GameContext`).
- **Files:** `RangeRing.cs`, `GhostView.cs`, `PlacementController.cs`, `GameContext.cs`
- **Verified:** play-mode script test — ghost at (13,0) with radius 4: max vertex distance from map center = 15.000 (≤ 15 ✓).

## DT-002 — King's projectile travels beyond its attack radius

- **Root cause:** projectiles home onto their target for the whole flight. Strict range is enforced at the moment of firing, but a target walking outward during flight dragged the projectile past the radius.
- **Fix:** `Projectile.Launch` takes optional `rangeOrigin`/`maxRange`; the homing end-point and impact position are clamped to that radius every frame. `StructureAttack` passes its position + range, so the king, cannon and X-Bow all inherit the guarantee. The hit itself still lands (deterministic "always hits unless target dies" ruling preserved) — the impact just resolves at the ring boundary. Enemy projectiles (Mage) are unclamped (unchanged behavior).
- **Files:** `Projectile.cs`, `ProjectileLauncher.cs` (interface + impl), `StructureAttack.cs`

## DT-003 — Cannot place more than 4 towers — **BY DESIGN**

- `Docs/01_DESIGN_CURRENT.md` line 45 (GDD §3): *"max 4 on field"*; `GameConfig.maxPlayerBuildings = 4`. This is a deliberate design constraint (economy/balance is tuned around ≤4 buildings — see `TargetRegistry` scan assumptions).
- **Action:** not changed. Marked *By Design* on the bug sheet. If design wants to lift the cap it's a one-field change in `GameConfig.asset` — but it needs a balance pass first.

## DT-004 — Crossbow targets enemies outside the playable map (mobile)

- **Root cause:** enemy targeting (`TargetRegistry.ClosestEnemyInRange` / `EnemiesInRadius`) never checked the map boundary. Spawn formations place enemies AT and BEYOND `mapRadius` (ranks step outward), and on mobile portrait the camera's extra vertical headroom makes that spawn zone visible — towers could acquire units that hadn't entered the arena yet. Spells already enforced the map circle (`SpellCaster`); buildings didn't.
- **Fix:** `TargetRegistry` now takes `(mapCenter, mapRadius)` and skips enemies whose center is outside the map circle in both enemy queries — symmetric with the spell rule. `TestRange` keeps the parameterless behavior (no filter, sandbox).
- **Files:** `TargetRegistry.cs`, `GameContext.cs`

## DT-005/006/007/008 — Fireball / Arrows / Freeze / Lightning hit enemies outside the radius

- **ROOT CAUSE (all four spells):** the scene's `Ghost_Spell` object has root scale **0.6** — the `RangeRing` child inherited it, so the drawn spell circle was **60% of the true radius** (Arrows: drawn 2.4 vs real 4.0). Every enemy between the drawn circle and the real radius was hit "outside the circle". Damage logic itself was always strict (center-in-circle). The building ghost has XZ scale 1, which is why building rings were never reported.
- **Fix 1 (root cause):** `RangeRing` now computes vertices in world space and maps them back through `transform.InverseTransformPoint`, so parent scale can no longer distort the drawn radius.
- **Fix 2 (hardening, DT-005/006):** Arrows/Fireball resolve `fallDelaySeconds` after cast with targets captured at cast — an enemy could walk OUT of the circle during the fall and still be hit. `SpellCaster.Resolve` now re-checks `RangeMath.IsInside(point, target, radius)` at landing: damage can only ever land inside the drawn circle. (Freeze/Lightning are instant — unaffected.)
- **Files:** `RangeRing.cs`, `SpellCaster.cs`
- **Verified:** play-mode script test — spell ghost `Show(4)`: drawn radius min/max = 4.000/4.000 (was 2.4).

## DT-009 — Selected card stays active when the Victory/Defeat panel appears

- **Root cause:** nothing listened to `MatchEnded` in the hand/placement UI — a selected or mid-drag card kept its state under the end panel.
- **Fix:** `HandBarView` subscribes to `MatchEnded`: cancels an active drag (placement ghost + drag proxy + carried slot), deselects a selected slot, and locks all hand input for the rest of the session. `MatchEndView` calls `SetAsLastSibling()` before showing so the panel always renders above every other UI element.
- **Files:** `HandBarView.cs`, `MatchEndView.cs`

## DT-010 — Cards can be selected without sufficient mana

- **Context:** GDD ruling says cost is validated at RELEASE of the drag. Tap feedback existed (desaturate + shake), but the drag threshold still let an unaffordable card enter the placement state (ghost shown, red).
- **Fix:** `HandBarView.OnSlotDrag` now requires `CardPlay.CanPlay(slot)` before starting placement — unaffordable/cooling cards never enter the placement state. Release-time validation is untouched (still the final gate for the card that legitimately started a drag). Documented as a QA delta in `Docs/01_DESIGN_CURRENT.md`.
- **Files:** `HandBarView.cs`

## DT-011 — Fireball VFX travels in a curved path

- **Root cause:** the meteor's `SkyfallEffect` path is a straight 3D lerp, but `FX_Meteor` was authored with `_lateralOffset (-5, -2.5)` against a 7-unit drop — a ~36°-slanted swoop that reads as a curved dive on screen (accelerating fall + perspective).
- **Fix:** `FX_Meteor.prefab` `_lateralOffset` → `(-0.8, -0.4)`: near-vertical, clearly straight drop onto the target point. (Prefab data change only — no code.)

---

## DT-012 — Energy orb flies to the wrong point, reads as a stray enemy projectile *(reported live by user, added to sheet post-hoc)*

- **Report:** an active `EnergyOrb(Clone)` was seen flying "towards the wrong target"; with no ranged enemies in the game it looked like an attack projectile that shouldn't exist.
- **Triage:** the orb itself is by design — it's the kill-bounty economy visual (enemy dies → magenta orb flies from the corpse → energy bar credits on arrival; energy ONLY comes from kills). Not an attack.
- **Root cause:** the orb's destination was a hardcoded world point (`EnergyBarAnchor` at (0, 0.5, −13)) that rendered at ~15% screen height — but the energy bar actually renders at ~2–3% (bottom edge). Orbs converged into the middle of the card hand instead of the bar, on every aspect ratio, which is exactly why it read as "flying at the wrong target".
- **Fix:** new `EnergyBarAnchor` component (RoyalSiege.UI) on the existing anchor object: every `LateUpdate` it pins the anchor to the world point that renders exactly under the energy bar's screen position at a fixed camera depth (21). Works on any aspect ratio / camera framing; `OrbSpawner`/`EnergyOrb` gameplay code untouched.
- **Files:** `EnergyBarAnchor.cs` (new), `Game.unity` (component wired: camera + EnergyBar rect)
- **Verified:** play mode, simulated kill at (4, 0, 6) — orb flew to viewport (0.504, 0.029) = exactly the bar's screen spot (0.500, 0.016 center), +0.25 energy credited on arrival, orb returned to pool, 0 console errors.

## Regression safety

- Combat determinism preserved: no RNG introduced; all fixes are strict-range enforcement, view-layer, or data.
- Balance data untouched except none — no damage/HP/cost/range numbers changed.
- `TestRange.unity` unaffected (parameterless `TargetRegistry`, rings without map clip behave as before).
- Play-mode smoke: enter play, ghost tests above, 0 console errors/warnings.

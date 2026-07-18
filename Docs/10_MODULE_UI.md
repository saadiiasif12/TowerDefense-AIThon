# 10 · Module: UI/HUD (RoyalSiege.UI)

**Status: GREYBOX COMPLETE — 16 Jul.** HandBarView/CardSlotView (drag threshold, radial cooldowns, cost pips, NEXT), EnergyBarView (cap pulse), WaveBannerView (+clear bonus flash), MatchEndView (stars), WorldHealthBar (billboarded, via `IHealthReadout`). Canvas built in `Game.unity` (1080×1920 ref). Remaining: pause/3-2-1 resume, speed toggle, tap tooltip, art skin (Day 3).

Layout direction from mocks (`MocksDirectionSoFar/`): top bar = pause · WAVE n/12 banner · speed toggle; bottom = card hand bar above a magenta Energy bar. Portrait, all four hand cards fit ≥360 px width.

## Components
- `HandBarView` — 4 card slots + NEXT preview (queue front) + fanned hidden stack; radial cooldown grey-out; cost pip; drag source. Cards greyed when unaffordable.
- `EnergyBarView` — 0–10 with tick marks; gold pulse at cap; orb arrival is where credit visually lands.
- `WaveBannerView` — "WAVE n/12" + 2 s breather banner between waves.
- `TopBarView` — pause button, speed toggle (1×/2×).
- `PauseOverlay` — resume with 3-2-1 countdown (auto-shown after app background).
- `EndScreenView` — victory (1–3 stars per HP thresholds) / defeat (wave reached), basic stats (kills, energy earned/spent, buildings lost).
- `TowerHpBar` — world-space above tower.
- Card stat tooltip on tap (no drag).

## Rules
- UI is event-driven and dumb: subscribes to GameEvents + reads services; issues only `PlayCardCommand`/pause/speed commands. Zero balance numbers in UI code.

## Acceptance
- Full match playable start→end screen with only HUD interactions; cooldown radial matches card timer; NEXT always shows the true queue front.

## Notes / changes
- (log changes here)

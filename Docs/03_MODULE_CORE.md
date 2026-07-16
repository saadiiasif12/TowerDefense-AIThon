# 03 · Module: Core (RoyalSiege.Core)

**Status: CODE COMPLETE — 16 Jul (see TASKS.md)**

## Responsibilities
- `GameContext` — composition root on a scene GameObject; creates/owns services (EnergyBank, DeckService, WaveScheduler, TargetQuery, GameEvents, Clock), injects into scene MonoBehaviours. Only place that news things up.
- `TickSystem` — 10 Hz fixed logic tick (accumulator over scaled delta), maintains `ITickable` registry. Exposes `LogicTimeNow`, interpolation alpha for views.
- `GameSpeedController` — 1× / 2×, pause. Scales the tick accumulator, NOT Time.timeScale.
- `MatchStateMachine` — Boot → Playing → Paused (3-2-1 resume overlay) → Victory/Defeat. Auto-pause on `OnApplicationPause`.
- `GameEvents` — plain C# events hub (see 02_ARCHITECTURE list).
- `WinLoseEvaluator` — defeat on tower HP 0; victory when spawn queue empty ∧ wave 12 started ∧ zero enemies alive; computes stars (≥50%/≥80% HP).
- `ObjectPool<T>` utility.
- `RangeCheck` static utility — THE single strict-range implementation.

## Acceptance
- Empty scene boots to Playing; tick counter advances at 10 Hz regardless of fps; pause/resume/2× work; win/lose fire correctly from fake signals.

## Notes / changes
- (log changes here)

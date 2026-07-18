using UnityEngine;
using RoyalSiege.Core;

namespace RoyalSiege.Juice
{
    /// <summary>
    /// Owns which arena environment is shown. Two paths drive it:
    ///   • AUTO (stage-driven): element i = stage i+1's root, wrapping when the campaign
    ///     loops. Applied on load (resume boots the saved biome), on WaveStarted, and
    ///     pre-swapped behind the stage-complete screen. It only acts when the STAGE index
    ///     actually CHANGES, so it never fights a manual pick every wave.
    ///   • MANUAL (HUD toggle): CycleManual() flips to the next environment immediately and
    ///     sticks until the next real stage change re-asserts that stage's environment.
    /// View-only — reads the scheduler / listens to events, never drives gameplay.
    /// </summary>
    public sealed class StageEnvironmentView : MonoBehaviour
    {
        [SerializeField] private GameContext _context;
        [Tooltip("Element i = environment root for stage i+1 (Arena, Arena_Stage2, …). Only one is active at a time.")]
        [SerializeField] private GameObject[] _stageEnvironments;

        private GameEvents _events;
        private int _active = -1;    // environment index currently shown
        private int _autoStage = -1; // last stage index the auto path acted on (guards manual override)

        /// <summary>Environment index currently displayed (0-based). -1 before Start.</summary>
        public int ActiveIndex => _active;
        public int EnvironmentCount => _stageEnvironments != null ? _stageEnvironments.Length : 0;

        /// <summary>Fires whenever the shown environment changes (manual toggle OR stage swap) with the new index — the HUD icon subscribes to stay in sync.</summary>
        public event System.Action<int> Changed;

        private void Start()
        {
            if (_context == null) _context = FindFirstObjectByType<GameContext>();
            if (_context == null || _context.Events == null || _context.Waves == null) return;
            _events = _context.Events;
            _events.WaveStarted += OnWaveStarted;
            _events.CheckpointReached += OnCheckpointReached;
            AutoApply(_context.Waves.PendingStageIndex); // resume/retry shows the saved stage from frame one
        }

        private void OnDestroy()
        {
            if (_events == null) return;
            _events.WaveStarted -= OnWaveStarted;
            _events.CheckpointReached -= OnCheckpointReached;
        }

        private void OnWaveStarted(int globalWave) => AutoApply(_context.Waves.StageIndexOf(globalWave));

        private void OnCheckpointReached(CheckpointReachedArgs args)
        {
            // Swap while the stage-complete modal covers the field — dismissing it reveals
            // the next stage's arena instead of popping mid-gap.
            if (args.IsStageComplete) AutoApply(_context.Waves.PendingStageIndex);
        }

        /// <summary>Stage-driven swap: acts only when the stage genuinely changes so a manual toggle isn't overridden every wave.</summary>
        private void AutoApply(int stageIndex)
        {
            if (_stageEnvironments == null || _stageEnvironments.Length == 0 || stageIndex < 0) return;
            if (stageIndex == _autoStage) return; // same stage — leave whatever's shown (incl. a manual pick)
            _autoStage = stageIndex;
            Show(stageIndex % _stageEnvironments.Length);
        }

        /// <summary>Player toggle: cycle to the next environment now. Sticky until the next stage change. Returns the new index.</summary>
        public int CycleManual()
        {
            if (_stageEnvironments == null || _stageEnvironments.Length == 0) return _active;
            Show((_active + 1) % _stageEnvironments.Length);
            return _active;
        }

        private void Show(int index)
        {
            if (_stageEnvironments == null || _stageEnvironments.Length == 0) return;
            index = ((index % _stageEnvironments.Length) + _stageEnvironments.Length) % _stageEnvironments.Length;
            if (index == _active) return;
            _active = index;
            for (int i = 0; i < _stageEnvironments.Length; i++)
                if (_stageEnvironments[i] != null) _stageEnvironments[i].SetActive(i == index);
            Changed?.Invoke(index);
        }
    }
}

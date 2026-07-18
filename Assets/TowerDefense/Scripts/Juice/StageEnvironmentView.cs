using UnityEngine;
using RoyalSiege.Core;

namespace RoyalSiege.Juice
{
    /// <summary>
    /// Shows the arena environment matching the CURRENT campaign stage: element i = stage
    /// i+1's root, wrapping when the campaign loops (stage 1 of loop 2 shows element 0
    /// again). View-only — reads the scheduler and listens to events, never drives gameplay.
    /// Applies immediately on load (checkpoint resume boots into the right biome) and
    /// pre-swaps behind the stage-complete screen so "Continue" reveals the new arena.
    /// </summary>
    public sealed class StageEnvironmentView : MonoBehaviour
    {
        [SerializeField] private GameContext _context;
        [Tooltip("Element i = environment root for stage i+1 (Arena, Arena_Stage2, …). Only one is active at a time.")]
        [SerializeField] private GameObject[] _stageEnvironments;

        private GameEvents _events;
        private int _active = -1;

        private void Start()
        {
            if (_context == null) _context = FindFirstObjectByType<GameContext>();
            if (_context == null || _context.Events == null || _context.Waves == null) return;
            _events = _context.Events;
            _events.WaveStarted += OnWaveStarted;
            _events.CheckpointReached += OnCheckpointReached;
            Apply(_context.Waves.PendingStageIndex); // resume/retry shows the saved stage from frame one
        }

        private void OnDestroy()
        {
            if (_events == null) return;
            _events.WaveStarted -= OnWaveStarted;
            _events.CheckpointReached -= OnCheckpointReached;
        }

        private void OnWaveStarted(int globalWave) => Apply(_context.Waves.StageIndexOf(globalWave));

        private void OnCheckpointReached(CheckpointReachedArgs args)
        {
            // Swap while the stage-complete modal covers the field — dismissing it reveals
            // the next stage's arena instead of popping mid-gap.
            if (args.IsStageComplete) Apply(_context.Waves.PendingStageIndex);
        }

        private void Apply(int stageIndex)
        {
            if (_stageEnvironments == null || _stageEnvironments.Length == 0 || stageIndex < 0) return;
            int index = stageIndex % _stageEnvironments.Length;
            if (index == _active) return;
            _active = index;
            for (int i = 0; i < _stageEnvironments.Length; i++)
                if (_stageEnvironments[i] != null) _stageEnvironments[i].SetActive(i == index);
        }
    }
}

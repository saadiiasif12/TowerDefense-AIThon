using System.Collections.Generic;
using UnityEngine;

namespace RoyalSiege.Core
{
    /// <summary>
    /// Fixed 10 Hz logic tick (GDD: combat logic at 10 Hz, visuals interpolated).
    /// Speed/pause scale the accumulator so determinism is preserved at any frame rate.
    /// </summary>
    public sealed class TickSystem : MonoBehaviour, IClock, ITicker
    {
        public const float TickInterval = 0.1f;

        private readonly List<ITickable> _tickables = new();
        private readonly List<ITickable> _pendingAdd = new();
        private readonly List<ITickable> _pendingRemove = new();
        private float _accumulator;

        public float LogicTime { get; private set; }
        public float SpeedMultiplier { get; set; } = 1f;
        public bool IsPaused { get; set; }
        public float ScaledDeltaTime => IsPaused ? 0f : Time.deltaTime * SpeedMultiplier;
        public float InterpolationAlpha => Mathf.Clamp01(_accumulator / TickInterval);

        public void Register(ITickable tickable) => _pendingAdd.Add(tickable);
        public void Unregister(ITickable tickable) => _pendingRemove.Add(tickable);

        private void Update()
        {
            if (IsPaused) return;

            _accumulator += Time.deltaTime * SpeedMultiplier;
            while (_accumulator >= TickInterval)
            {
                _accumulator -= TickInterval;
                LogicTime += TickInterval;
                ApplyPending();
                for (int i = 0; i < _tickables.Count; i++)
                    _tickables[i].Tick(TickInterval);
            }
        }

        private void ApplyPending()
        {
            for (int i = 0; i < _pendingRemove.Count; i++) _tickables.Remove(_pendingRemove[i]);
            _pendingRemove.Clear();
            for (int i = 0; i < _pendingAdd.Count; i++)
                if (!_tickables.Contains(_pendingAdd[i])) _tickables.Add(_pendingAdd[i]);
            _pendingAdd.Clear();
        }
    }
}

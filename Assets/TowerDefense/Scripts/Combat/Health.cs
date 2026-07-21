using System;
using UnityEngine;

namespace RoyalSiege.Combat
{
    /// <summary>
    /// Plain health model. Marks dead the instant HP hits 0 (GDD: mark-dead-immediately
    /// prevents double bounty); further damage is ignored.
    /// </summary>
    public sealed class Health
    {
        public float Max { get; }
        public float Current { get; private set; }
        public bool IsAlive => Current > 0f;
        public float Pct => Max <= 0f ? 0f : Current / Max;

        public event Action<float, float> Damaged; // current, max
        public event Action Died;

        public Health(float max)
        {
            Max = max;
            Current = max;
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            Current = Mathf.Max(0f, Current - amount);
            Damaged?.Invoke(Current, Max);
            if (Current <= 0f) Died?.Invoke();
        }

        /// <summary>
        /// Refill to full — the Royal Tower revive (fail screen). Works even from 0 HP (brings
        /// a dead structure back to life); fires Damaged so HP readouts snap to full.
        /// </summary>
        public void ResetToFull()
        {
            Current = Max;
            Damaged?.Invoke(Current, Max);
        }
    }
}

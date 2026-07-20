using UnityEngine;
using RoyalSiege.Core;
using RoyalSiege.Data;

namespace RoyalSiege.Economy
{
    public interface IEnergyBank
    {
        float Current { get; }
        float Max { get; }
        bool CanAfford(float cost);
        bool TrySpend(float cost);
        void Credit(float amount);
    }

    /// <summary>
    /// The elixir bar. v4 hybrid economy (13_PROGRESSION_V4 §6): kill bounties (orbs,
    /// credit on arrival) + a slow always-on passive regen (1 per 3.5 s — the
    /// anti-frustration floor, not the income engine). Hard cap: overflow is wasted
    /// (and reported, so UI can nudge spending). Regen ticks the 10 Hz sim clock.
    /// </summary>
    public sealed class EnergyBank : IEnergyBank, ITickable
    {
        private readonly GameEvents _events;
        private readonly float _regenSeconds;

        public float Current { get; private set; }
        public float Max { get; }

        public EnergyBank(EconomyConfigSO config, GameEvents events)
        {
            _events = events;
            Max = config.maxEnergy;
            Current = Mathf.Min(config.startingEnergy, Max);
            _regenSeconds = config.passiveRegenSeconds;
        }

        public void Tick(float dt)
        {
            if (_regenSeconds <= 0f || Current >= Max) return;
            // Accrue CONTINUOUSLY (fractional) rather than in whole-point jumps, so the bar can
            // show elixir filling smoothly over time (Clash-Royale style). Current stays the
            // authoritative live value; the bar polls it each frame for the sub-point fill.
            float before = Current;
            Current = Mathf.Min(Max, Current + dt / _regenSeconds);
            // Fire the discrete event only when the WHOLE-point count changes (or we top out) —
            // that's all the label / card-affordability need; the smooth motion is pure polling,
            // so we don't spam listeners at the 10 Hz sim rate.
            if (Mathf.FloorToInt(Current) != Mathf.FloorToInt(before) || Current >= Max)
                _events.RaiseEnergyChanged(Current, Max);
        }

        public bool CanAfford(float cost) => Current >= cost;

        public bool TrySpend(float cost)
        {
            if (!CanAfford(cost)) return false;
            Current -= cost;
            _events.RaiseEnergyChanged(Current, Max);
            return true;
        }

        public void Credit(float amount)
        {
            if (amount <= 0f) return;
            float wasted = Mathf.Max(0f, Current + amount - Max);
            Current = Mathf.Min(Max, Current + amount);
            _events.RaiseEnergyChanged(Current, Max);
            if (wasted > 0f) _events.RaiseEnergyWasted(wasted);
        }
    }
}

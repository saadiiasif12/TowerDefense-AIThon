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
        private float _regenTimer;

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
            if (_regenSeconds <= 0f || Current >= Max) { _regenTimer = 0f; return; }
            _regenTimer += dt;
            if (_regenTimer >= _regenSeconds)
            {
                _regenTimer -= _regenSeconds;
                Credit(1f);
            }
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

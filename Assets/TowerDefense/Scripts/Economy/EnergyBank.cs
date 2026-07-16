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
    /// The Energy bar. Kill-collection economy: credits come ONLY from orbs and wave
    /// bonuses — there is no passive regen anywhere in the codebase, by design (GDD v2 §4).
    /// Hard cap: overflow is wasted (and reported, so UI can nudge spending).
    /// </summary>
    public sealed class EnergyBank : IEnergyBank
    {
        private readonly GameEvents _events;

        public float Current { get; private set; }
        public float Max { get; }

        public EnergyBank(EconomyConfigSO config, GameEvents events)
        {
            _events = events;
            Max = config.maxEnergy;
            Current = Mathf.Min(config.startingEnergy, Max);
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

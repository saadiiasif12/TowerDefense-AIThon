using UnityEngine;

namespace RoyalSiege.Data
{
    /// <summary>v4 hybrid economy (13_PROGRESSION_V4 §6): kill bounties + slow passive regen.</summary>
    [CreateAssetMenu(menuName = "RoyalSiege/Economy Config", fileName = "EconomyConfig")]
    public sealed class EconomyConfigSO : ScriptableObject
    {
        [Tooltip("Also the checkpoint-resume amount (v4: 5).")]
        public float startingEnergy = 5f;
        public float maxEnergy = 10f;
        [Tooltip("v4: seconds per +1 passive elixir, always on. The anti-frustration floor, not the income engine. 0 = disabled.")]
        public float passiveRegenSeconds = 3.5f;
        [Tooltip("v4: REMOVED from the design (kills already pay per wave size). Keep 0.")]
        public float waveClearBonus = 0f;
        [Tooltip("Orb flight time; energy credits on ARRIVAL, not on kill.")]
        public float orbFlightSeconds = 0.55f;
    }
}

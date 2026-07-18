using UnityEngine;

namespace RoyalSiege.Data
{
    /// <summary>
    /// Elixir economy. 18-Jul ruling: kill-drop energy is OFF — the bar fills ONLY from the
    /// time-based passive regen (killDropsEnergy toggles the old v4 hybrid back on).
    /// </summary>
    [CreateAssetMenu(menuName = "RoyalSiege/Economy Config", fileName = "EconomyConfig")]
    public sealed class EconomyConfigSO : ScriptableObject
    {
        [Tooltip("Also the checkpoint-resume amount (v4: 5).")]
        public float startingEnergy = 5f;
        public float maxEnergy = 10f;
        [Tooltip("Seconds per +1 passive elixir, always on. 18-Jul: this is now the ONLY income " +
                 "source (kills don't drop energy). 0 = disabled.")]
        public float passiveRegenSeconds = 3.5f;
        [Tooltip("18-Jul ruling: OFF = enemy kills no longer drop energy orbs; the bar fills " +
                 "only from passiveRegenSeconds. ON = kills also pay bounty orbs (old v4 hybrid).")]
        public bool killDropsEnergy = false;
        [Tooltip("v4: REMOVED from the design (kills already pay per wave size). Keep 0.")]
        public float waveClearBonus = 0f;
        [Tooltip("Orb flight time; energy credits on ARRIVAL, not on kill. Unused while killDropsEnergy is off.")]
        public float orbFlightSeconds = 0.55f;
    }
}

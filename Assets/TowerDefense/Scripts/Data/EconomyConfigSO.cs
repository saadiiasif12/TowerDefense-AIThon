using UnityEngine;

namespace RoyalSiege.Data
{
    /// <summary>Kill-collection economy (GDD v2 §4). There is NO passive regen — by design.</summary>
    [CreateAssetMenu(menuName = "RoyalSiege/Economy Config", fileName = "EconomyConfig")]
    public sealed class EconomyConfigSO : ScriptableObject
    {
        public float startingEnergy = 9f;
        public float maxEnergy = 10f;
        [Tooltip("Granted UNCONDITIONALLY on clearing a wave (v2 removed the gate).")]
        public float waveClearBonus = 3f;
        [Tooltip("Orb flight time; energy credits on ARRIVAL, not on kill.")]
        public float orbFlightSeconds = 0.55f;
    }
}

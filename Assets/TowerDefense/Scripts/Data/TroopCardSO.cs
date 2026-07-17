using UnityEngine;

namespace RoyalSiege.Data
{
    /// <summary>
    /// v4 Knights: deploys N melee troops inside the deployment circle. They chase the
    /// nearest living enemy (no leash), tank hits (enemies target structure-or-knight),
    /// never decay, persist across waves. Card is unplayable when active + count would
    /// exceed maxActive.
    /// </summary>
    [CreateAssetMenu(menuName = "RoyalSiege/Cards/Troop Card", fileName = "Card_")]
    public sealed class TroopCardSO : CardDefinitionSO
    {
        [Header("Troop")]
        [Tooltip("Prefab with a KnightUnit component (added at runtime if missing).")]
        public GameObject troopPrefab;
        [Min(1)] public int countPerCast = 2;
        [Min(1)] public int maxActive = 4;

        [Header("Stats")]
        public float hp = 480f;
        public float damage = 75f;
        [Tooltip("Seconds per attack.")] public float attackRate = 1.1f;
        [Tooltip("Measured to the enemy's body EDGE (melee).")]
        public float attackRange = 0.5f;
        public float moveSpeed = 1.6f;
        [Range(0f, 1f)] public float impactFraction = 0.4f;
        [Tooltip("Personal-space radius (separation + enemies attack to this edge).")]
        public float unitRadius = 0.4f;
    }
}

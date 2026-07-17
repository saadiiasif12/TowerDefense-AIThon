using UnityEngine;

namespace RoyalSiege.Data
{
    [CreateAssetMenu(menuName = "RoyalSiege/Cards/Building Card", fileName = "Card_")]
    public sealed class BuildingCardSO : CardDefinitionSO
    {
        [Header("Building")]
        [Tooltip("Prefab with a BuildingUnit component (added at runtime if missing).")]
        public GameObject buildingPrefab;
        public float hp = 700f;
        public float footprintRadius = 0.75f;
        [Tooltip("v4 lifetime: max HP drains to zero over this many seconds even unattacked " +
                 "(decay = maxHP/lifetime per second; enemy damage stacks on top). 0 = no decay.")]
        public float lifetimeSeconds = 35f;

        [Header("Attack")]
        public float damage = 85f;
        [Tooltip("Seconds per shot.")] public float attackRate = 1f;
        public float range = 5.5f;
        [Range(0f, 1f)] public float impactFraction = 0.4f;
        [Tooltip("Delay before acquiring a new target after losing one (Tesla: 0.2).")]
        public float retargetDelay = 0f;
        [Tooltip("Null = instant hit (Tesla).")] public ProjectileSettingsSO projectile;
    }
}

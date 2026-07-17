using UnityEngine;
using UnityEngine.Serialization;

namespace RoyalSiege.Data
{
    public enum TargetPriority
    {
        ClosestStructure,   // Goblin, Mage
        BuildingsFirst      // Ogre, Boss — nearest player building if any exists, else the tower
    }

    [CreateAssetMenu(menuName = "RoyalSiege/Enemy Definition", fileName = "Enemy_")]
    public sealed class EnemyDefinitionSO : ScriptableObject
    {
        public string id;
        public string displayName;
        [Tooltip("Prefab with an EnemyAgent component (added at runtime if missing).")]
        public GameObject prefab;

        [Header("Stats (pre-multiplier — GameConfig difficulty multipliers apply at runtime)")]
        public float hp = 90f;
        public float moveSpeed = 2f;
        public float damage = 50f;
        [Tooltip("Seconds per attack.")] public float attackRate = 1.1f;
        [Tooltip("Measured to the structure's EDGE (see 02_ARCHITECTURE melee rule).")]
        public float attackRange = 0.5f;
        [Range(0f, 1f)] public float impactFraction = 0.4f;
        [Tooltip("Splash radius around the impact point, damaging other structures. 0 = none.")]
        public float splashRadius = 0f;
        [Tooltip("Null = melee. Set for ranged enemies.")]
        public ProjectileSettingsSO projectile;
        [Tooltip("An enemy may only ATTACK once it is within this distance of the map center — ranged enemies must enter the territory first instead of standing off at the outskirts (17-Jul rule). Melee enemies are unaffected in practice.")]
        public float engageRadiusFromCenter = 12f;

        [Header("Juice")]
        [Tooltip("Death burst played instead of the generic puff (e.g. bone shatter for the Skeleton). Optional.")]
        public ParticleSystem deathVfx;
        [Tooltip("Hit-stagger: a MOVING enemy stops and plays the hurt flinch for this long when damaged, then resumes. 0 = disabled. Attacking/frozen enemies never stagger.")]
        public float hurtStaggerSeconds = 0.35f;
        [Tooltip("Minimum seconds between staggers so rapid hits (Tesla, Arrows) can't stun-lock a unit into never advancing.")]
        public float hurtCooldownSeconds = 2.5f;
        [Tooltip("THIS enemy's own walk-animation speed multiplier (multiplies the global GameConfig " +
                 "one). 1 = feet synced to ground speed; <1 = calmer legs (e.g. the Skeleton reads " +
                 "frantic at full sync because it moves fast), >1 = busier.")]
        [FormerlySerializedAs("walkAnimSpeedScale")]
        [Range(0.25f, 4f)] public float walkAnimSpeedMultiplier = 1f;

        [Header("Behaviour")]
        [Tooltip("Personal-space radius for separation steering and formation spacing.")]
        public float unitRadius = 0.45f;
        public TargetPriority targetPriority = TargetPriority.ClosestStructure;
        [Tooltip("Energy orb value dropped on death.")]
        public float bounty = 0.25f;
        [Tooltip("v4: knockback multiplier — light 1, heavy 0.5, Ogre 0 (immune).")]
        [Range(0f, 1f)] public float knockbackFactor = 1f;
        [Tooltip("v4: visual scale applied to the model instance (type-2 variants read bigger).")]
        [Min(0.1f)] public float modelScale = 1f;

        [Header("Boss")]
        public bool isBoss;
        [Tooltip("Ground slam: pulse damaging all structures in radius. Interval 0 = disabled.")]
        public float slamInterval = 0f;
        public float slamDamage = 100f;
        public float slamRadius = 2.5f;
        public float slamTelegraphSeconds = 1f;
    }
}

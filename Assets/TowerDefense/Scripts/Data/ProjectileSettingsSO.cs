using UnityEngine;
using RoyalSiege.Combat;

namespace RoyalSiege.Data
{
    /// <summary>
    /// Shared projectile config — the ONE projectile module every shooter uses
    /// (Cannon, X-Bow, tower king/cannon, Mage). Tune arc/heights here per shooter.
    /// </summary>
    [CreateAssetMenu(menuName = "RoyalSiege/Projectile Settings", fileName = "Projectile_")]
    public sealed class ProjectileSettingsSO : ScriptableObject
    {
        [Tooltip("Visual prefab with a Projectile component. Pooled per settings asset.")]
        public Projectile prefab;

        [Min(0.5f)] public float speed = 10f;

        [Tooltip("Peak height of the flight parabola, in tiles. 0 = flat shot (e.g. X-Bow bolt).")]
        [Min(0f)] public float arcHeight = 1.5f;

        [Tooltip("Height above the shooter's origin where the projectile spawns (muzzle height).")]
        public float spawnHeightOffset = 1f;

        [Tooltip("Height above the target's origin where the projectile lands.")]
        public float impactHeightOffset = 0.5f;

        [Header("Juice")]
        [Tooltip("Played at the muzzle when this projectile fires. Optional.")]
        public ParticleSystem muzzleVfx;
        [Tooltip("Played at the impact point on a successful hit (not on fizzle). Optional.")]
        public ParticleSystem impactVfx;
        [Tooltip("Tint applied to the impact VFX (e.g. purple for mage bolts).")]
        public Color impactTint = Color.white;
        [Tooltip("Visual tumble around the flight axis (cannonballs). 0 = face velocity (bolts).")]
        public float spinDegreesPerSecond;
        [Tooltip("Camera trauma added when this projectile lands (0.1 tick / 0.35 thud / 0.6 blast). 0 = none.")]
        [Range(0f, 1f)] public float impactShake;
        [Tooltip("If true, hitting an enemy plays NO enemy hurt sound (e.g. the rapid-fire X-Bow — its " +
                 "stream of hits would spam the hurt voice). Damage, flash, numbers and impact VFX are unaffected.")]
        public bool silentHit;
    }
}

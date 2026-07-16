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
    }
}

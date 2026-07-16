using System;
using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Core;
using RoyalSiege.Data;
using RoyalSiege.Juice;

namespace RoyalSiege.Combat
{
    public interface IProjectileLauncher
    {
        void Fire(Vector3 from, IDamageable target, float damage,
            ProjectileSettingsSO settings, Action<Vector3, IDamageable> onImpact = null);
    }

    /// <summary>Factory + pool: one pool per ProjectileSettingsSO asset. Plays muzzle VFX.</summary>
    public sealed class ProjectileLauncher : IProjectileLauncher
    {
        private readonly Dictionary<ProjectileSettingsSO, ObjectPool<Projectile>> _pools = new();
        private readonly Transform _parent;
        private readonly IClock _clock;
        private readonly IVfxSpawner _vfx;

        public ProjectileLauncher(Transform parent, IClock clock, IVfxSpawner vfx)
        {
            _parent = parent;
            _clock = clock;
            _vfx = vfx;
        }

        public void Fire(Vector3 from, IDamageable target, float damage,
            ProjectileSettingsSO settings, Action<Vector3, IDamageable> onImpact = null)
        {
            if (settings == null || settings.prefab == null || target == null || !target.IsAlive)
                return;

            if (!_pools.TryGetValue(settings, out var pool))
            {
                pool = new ObjectPool<Projectile>(settings.prefab, _parent, prewarm: 4);
                _pools[settings] = pool;
            }

            if (settings.muzzleVfx != null)
            {
                Vector3 muzzlePos = from + Vector3.up * settings.spawnHeightOffset;
                Vector3 dir = RangeMath.PlanarDirection(from, target.Position);
                _vfx.Spawn(settings.muzzleVfx, muzzlePos, Quaternion.LookRotation(dir));
            }

            pool.Get().Launch(from, target, damage, settings, _clock, _vfx, pool.Release, onImpact);
        }
    }
}

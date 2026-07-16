using System;
using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Core;
using RoyalSiege.Data;

namespace RoyalSiege.Combat
{
    public interface IProjectileLauncher
    {
        void Fire(Vector3 from, IDamageable target, float damage,
            ProjectileSettingsSO settings, Action<Vector3, IDamageable> onImpact = null);
    }

    /// <summary>Factory + pool: one pool per ProjectileSettingsSO asset.</summary>
    public sealed class ProjectileLauncher : IProjectileLauncher
    {
        private readonly Dictionary<ProjectileSettingsSO, ObjectPool<Projectile>> _pools = new();
        private readonly Transform _parent;
        private readonly IClock _clock;

        public ProjectileLauncher(Transform parent, IClock clock)
        {
            _parent = parent;
            _clock = clock;
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

            pool.Get().Launch(from, target, damage, settings, _clock, pool.Release, onImpact);
        }
    }
}

using System;
using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Core;

namespace RoyalSiege.Units
{
    /// <summary>Everything an EnemyAgent needs, injected by the factory (no service lookups).</summary>
    public sealed class EnemyRuntimeDeps
    {
        public Vector3 MapCenter;
        public ITargetRegistry Registry;
        public IProjectileLauncher Launcher;
        public GameEvents Events;
        public IClock Clock;
        public ITicker Ticker;
        public float HpMultiplier = 1f;
        public float DamageMultiplier = 1f;
        public float BountyMultiplier = 1f;
        public Action<EnemyAgent> Release;
    }
}

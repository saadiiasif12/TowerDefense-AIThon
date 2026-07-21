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
        public float WalkAnimMultiplier = 1f; // global enemy walk-anim speed factor (GameConfig)
        public float AttackStandoff;          // gap enemies keep between body and a structure footprint (GameConfig)
        public float KnightStandoff;          // gap enemies keep from a knight they fight (GameConfig.knightCombatStandoff)
        public Action<EnemyAgent> Release;
    }
}

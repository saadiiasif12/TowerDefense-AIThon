using System;
using UnityEngine;

namespace RoyalSiege.Data
{
    [Serializable]
    public sealed class AttackStats
    {
        public float damage = 50f;
        [Tooltip("Seconds per attack.")] public float attackRate = 1f;
        public float range = 7f;
        [Tooltip("Fraction of the attack period at which the shot fires (anim sync).")]
        [Range(0f, 1f)] public float impactFraction = 0.4f;
        [Tooltip("Null = instant hit (no projectile).")] public ProjectileSettingsSO projectile;
    }

    /// <summary>Global tunables. Values authored ONLY from Docs/01_DESIGN_CURRENT.md.</summary>
    [CreateAssetMenu(menuName = "RoyalSiege/Game Config", fileName = "GameConfig")]
    public sealed class GameConfigSO : ScriptableObject
    {
        [Header("Royal Tower")]
        public float towerHp = 4000f;
        public float towerFootprintRadius = 1.5f;
        public AttackStats kingAttack;
        public AttackStats builtInCannon;

        [Header("Map / Placement")]
        public float mapRadius = 15f;
        public float deploymentRadius = 5f;
        public float placementSnap = 0.5f;
        [Tooltip("0 = UNLIMITED (17-Jul ruling) — space/overlap/elixir/decay are the real limits.")]
        public int maxPlayerBuildings = 0;

        [Header("Difficulty (THE tuning knobs — GDD §8)")]
        public float enemyHpMultiplier = 1f;
        [Tooltip("v2 ships at 0.5 — compensates strict ranges + faster spawns + lean economy.")]
        public float enemyDamageMultiplier = 0.5f;
        public float enemyBountyMultiplier = 1f;
        [Tooltip("GLOBAL enemy walk-animation speed factor. 1 = feet synced to ground speed; " +
                 "<1 = slower/calmer legs, >1 = faster. Tunes every enemy at once (per-enemy " +
                 "fine-tuning lives on each EnemyDefinition's walkAnimSpeedScale).")]
        [Range(0.25f, 2f)] public float enemyWalkAnimSpeedMultiplier = 1f;

        [Header("Stars")]
        [Range(0f, 1f)] public float twoStarHpPct = 0.5f;
        [Range(0f, 1f)] public float threeStarHpPct = 0.8f;

        [Header("UX")]
        public float dragThresholdPx = 40f;
        [Tooltip("Ghost hovers this many tiles above the fingertip (toward screen-up).")]
        public float ghostOffsetTiles = 1.5f;
    }
}

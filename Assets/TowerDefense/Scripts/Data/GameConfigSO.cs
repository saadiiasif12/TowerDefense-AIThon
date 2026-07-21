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
        [Tooltip("Extra gap (world units) an enemy keeps between its BODY and a structure's " +
                 "footprint edge when attacking — so melee enemies strike from a little distance " +
                 "instead of clipping into the tower/buildings. 0 = flush against the footprint. " +
                 "The attack reach auto-stretches to cover it, so every unit (fat ones included) " +
                 "can still land its hits from the standoff ring.")]
        public float enemyAttackStandoff = 0.4f;
        [Tooltip("Gap (world units) a Knight and the enemy it fights keep between their BODIES, so " +
                 "they trade blows from a little distance instead of clipping into each other. " +
                 "Enforced on the enemy side (crowd-safe) and matched by a stretched knight reach. " +
                 "Keep <= Enemy Attack Standoff so enemies can still reach knights.")]
        public float knightCombatStandoff = 0.3f;
        [Tooltip("Extra space (world units) knights keep between EACH OTHER via separation, on top " +
                 "of their body radii — so a group holds a visible gap instead of merging into one " +
                 "blob. Applied while moving AND while attacking/idle. 0 = bodies just touch.")]
        public float knightSpacing = 0.5f;
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

        [Header("Sky")]
        [Tooltip("Shared world-space origin for anything that falls from the sky (fireball " +
                 "meteor, arrow volley, future skyfall objects). One fixed point so every " +
                 "sky entrance reads consistent.")]
        public Vector3 skyPoint = new Vector3(1.19098997f, 36.4000015f, 3.86999989f);
    }
}

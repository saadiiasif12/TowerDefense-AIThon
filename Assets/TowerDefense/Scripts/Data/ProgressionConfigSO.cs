using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoyalSiege.Data
{
    [Serializable]
    public sealed class TowerLevelDef
    {
        public float hp = 4000f;
        [Tooltip("King attack damage at this level (rate/range never change — strict-range readability).")]
        public float kingDamage = 93f;
    }

    [Serializable]
    public sealed class CheckpointDef
    {
        [Tooltip("Checkpoint triggers when this GLOBAL wave is cleared.")]
        public int afterWave = 4;
        [Tooltip("Tower level reached at this checkpoint (1-based). 0 = no level change (stage-complete checkpoint).")]
        public int towerLevel;
        [Tooltip("Card revealed + added to the back of the queue. Null = none.")]
        public CardDefinitionSO unlockCard;
        [Tooltip("True for the w20 stage-complete screen (stars + 'Continue to Stage 2').")]
        public bool isStageComplete;
    }

    /// <summary>
    /// v4 journey config: tower levels, the five checkpoints, starting deck.
    /// (13_PROGRESSION_V4 §1/§7/§9). All tunables live here — zero hardcoded stats.
    /// </summary>
    [CreateAssetMenu(menuName = "RoyalSiege/Progression Config", fileName = "ProgressionConfig")]
    public sealed class ProgressionConfigSO : ScriptableObject
    {
        [Tooltip("Index 0 = Level 1. Leveling fully heals the tower.")]
        public List<TowerLevelDef> towerLevels = new();
        [Tooltip("In wave order. Level-up screens hold the next wave until dismissed.")]
        public List<CheckpointDef> checkpoints = new();
        [Tooltip("The starter six — unlocks are added on top in checkpoint order.")]
        public List<CardDefinitionSO> startingCards = new();
    }
}

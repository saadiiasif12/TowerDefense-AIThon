using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoyalSiege.Data
{
    [Serializable]
    public sealed class CampaignWave
    {
        [Tooltip("Editor label, e.g. 'w12 SPIKE ckpt-2'.")] public string label;
        public List<SpawnGroup> groups = new();
    }

    [Serializable]
    public sealed class CampaignStage
    {
        public string stageName = "Stage 1";
        public List<CampaignWave> waves = new();
    }

    /// <summary>
    /// v4 campaign: stages of CLEAR-TRIGGERED waves. The next wave spawns clearGapSeconds
    /// after the last enemy of the current wave dies AND its elixir orb lands. No absolute
    /// timers — elixir cap + building decay are the pressure now (13_PROGRESSION_V4 §1).
    /// SpawnGroup.delayAfterWaveStart = seconds after the WAVE begins (flank stagger 2–3 s).
    /// </summary>
    [CreateAssetMenu(menuName = "RoyalSiege/Campaign", fileName = "Campaign_")]
    public sealed class CampaignSO : ScriptableObject
    {
        [Tooltip("Seconds between units within one group.")]
        public float unitSpawnInterval = 0.11f;
        [Tooltip("Gap between wave clear (last enemy + last orb) and the next wave.")]
        public float clearGapSeconds = 2f;
        public List<CampaignStage> stages = new();

        public int TotalWaves
        {
            get { int n = 0; foreach (var s in stages) n += s.waves.Count; return n; }
        }

        /// <summary>Global 1-based wave number → (stageIndex, waveIndexInStage). False if out of range.</summary>
        public bool Locate(int globalWave, out int stageIndex, out int waveInStage)
        {
            int remaining = globalWave - 1;
            for (int s = 0; s < stages.Count; s++)
            {
                if (remaining < stages[s].waves.Count) { stageIndex = s; waveInStage = remaining; return true; }
                remaining -= stages[s].waves.Count;
            }
            stageIndex = -1; waveInStage = -1;
            return false;
        }
    }
}

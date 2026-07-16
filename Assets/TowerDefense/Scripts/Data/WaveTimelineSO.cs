using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoyalSiege.Data
{
    [Serializable]
    public sealed class SpawnGroup
    {
        public EnemyDefinitionSO enemy;
        [Min(1)] public int count = 1;
        [Tooltip("0=N, 1=NE, 2=E, 3=SE, 4=S, 5=SW, 6=W, 7=NW")]
        [Range(0, 7)] public int spawnPointIndex;
        [Tooltip("Seconds after the wave's startTime (scaled by groupDelayMultiplier).")]
        public float delayAfterWaveStart;
    }

    [Serializable]
    public sealed class WaveEntry
    {
        [Tooltip("ABSOLUTE match time. Waves spawn unconditionally — never gate on the previous wave.")]
        public float startTime;
        public List<SpawnGroup> groups = new();
    }

    [CreateAssetMenu(menuName = "RoyalSiege/Wave Timeline", fileName = "Waves_")]
    public sealed class WaveTimelineSO : ScriptableObject
    {
        [Tooltip("Seconds between units within one group (v2: 0.11).")]
        public float unitSpawnInterval = 0.11f;
        [Tooltip("Group delays run at this fraction of their authored value (v2: 0.7).")]
        public float groupDelayMultiplier = 0.7f;
        public List<WaveEntry> waves = new();
    }
}

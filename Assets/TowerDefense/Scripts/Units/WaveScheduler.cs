using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Core;
using RoyalSiege.Data;

namespace RoyalSiege.Units
{
    /// <summary>
    /// Drives the 12-wave timeline. Waves spawn at their ABSOLUTE startTime unconditionally
    /// (overlap is the anti-stall design). Tracks per-wave kill counts for WaveCleared and
    /// exposes AllSpawned/LastWaveStarted for the win check.
    /// </summary>
    public sealed class WaveScheduler : ITickable
    {
        private readonly struct SpawnEvent
        {
            public readonly float Time;
            public readonly EnemyDefinitionSO Enemy;
            public readonly int WaveIndex;
            public readonly int SpawnPointIndex;
            public readonly int UnitIndexInGroup;

            public SpawnEvent(float time, EnemyDefinitionSO enemy, int waveIndex, int spawnPointIndex, int unitIndexInGroup)
            {
                Time = time;
                Enemy = enemy;
                WaveIndex = waveIndex;
                SpawnPointIndex = spawnPointIndex;
                UnitIndexInGroup = unitIndexInGroup;
            }
        }

        private readonly WaveTimelineSO _timeline;
        private readonly IEnemyFactory _factory;
        private readonly SpawnPointProvider _spawnPoints;
        private readonly GameEvents _events;
        private readonly Vector3 _center;

        private readonly List<SpawnEvent> _spawnQueue = new();
        private readonly int[] _totalPerWave;
        private readonly int[] _spawnedPerWave;
        private readonly int[] _killedPerWave;
        private readonly bool[] _waveClearedRaised;

        private float _time;
        private int _nextSpawnIndex;
        private int _nextWaveToStart;

        public int WaveCount => _timeline.waves.Count;
        public bool AllSpawned => _nextSpawnIndex >= _spawnQueue.Count;
        public bool LastWaveStarted => _nextWaveToStart >= WaveCount;
        /// <summary>1-based number of the latest wave that has started (0 before wave 1).</summary>
        public int CurrentWaveNumber => _nextWaveToStart;

        public WaveScheduler(WaveTimelineSO timeline, IEnemyFactory factory,
            SpawnPointProvider spawnPoints, Vector3 center, GameEvents events)
        {
            _timeline = timeline;
            _factory = factory;
            _spawnPoints = spawnPoints;
            _center = center;
            _events = events;

            _totalPerWave = new int[WaveCount];
            _spawnedPerWave = new int[WaveCount];
            _killedPerWave = new int[WaveCount];
            _waveClearedRaised = new bool[WaveCount];

            BuildSpawnQueue();
            _events.EnemyKilled += OnEnemyKilled;
        }

        private void BuildSpawnQueue()
        {
            for (int w = 0; w < _timeline.waves.Count; w++)
            {
                var wave = _timeline.waves[w];
                foreach (var group in wave.groups)
                {
                    float groupStart = wave.startTime + group.delayAfterWaveStart * _timeline.groupDelayMultiplier;
                    for (int u = 0; u < group.count; u++)
                    {
                        float t = groupStart + u * _timeline.unitSpawnInterval;
                        _spawnQueue.Add(new SpawnEvent(t, group.enemy, w, group.spawnPointIndex, u));
                        _totalPerWave[w]++;
                    }
                }
            }
            _spawnQueue.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        public void Tick(float dt)
        {
            _time += dt;

            while (_nextWaveToStart < WaveCount && _timeline.waves[_nextWaveToStart].startTime <= _time)
            {
                _nextWaveToStart++;
                _events.RaiseWaveStarted(_nextWaveToStart);
            }

            while (_nextSpawnIndex < _spawnQueue.Count && _spawnQueue[_nextSpawnIndex].Time <= _time)
            {
                var evt = _spawnQueue[_nextSpawnIndex];
                _nextSpawnIndex++;
                Vector3 position = _spawnPoints.GetWithFormationOffset(evt.SpawnPointIndex, evt.UnitIndexInGroup, _center);
                _factory.Spawn(evt.Enemy, position, evt.WaveIndex);
                _spawnedPerWave[evt.WaveIndex]++;
            }
        }

        private void OnEnemyKilled(EnemyKilledArgs args)
        {
            int w = args.WaveIndex;
            if (w < 0 || w >= WaveCount) return;

            _killedPerWave[w]++;
            bool fullySpawned = _spawnedPerWave[w] >= _totalPerWave[w];
            if (fullySpawned && _killedPerWave[w] >= _totalPerWave[w] && !_waveClearedRaised[w])
            {
                _waveClearedRaised[w] = true;
                _events.RaiseWaveCleared(w + 1);
            }
        }
    }
}

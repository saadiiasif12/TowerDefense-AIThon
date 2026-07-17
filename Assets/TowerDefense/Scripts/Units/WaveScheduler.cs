using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Core;
using RoyalSiege.Data;
using RoyalSiege.Economy;

namespace RoyalSiege.Units
{
    /// <summary>
    /// v4 campaign driver (13_PROGRESSION_V4 §1): CLEAR-TRIGGERED waves — the next wave
    /// spawns clearGapSeconds after the last enemy of the current wave dies AND its elixir
    /// orb lands. No absolute timers (elixir cap + building decay are the pressure now).
    /// Checkpoint screens simply pause the sim clock, which freezes the gap — the next
    /// wave holds until the modal is dismissed, exactly per §9. Supports starting at any
    /// wave (checkpoint retry).
    /// </summary>
    public sealed class WaveScheduler : ITickable
    {
        private enum Phase { Gap, Fighting, Complete }

        private readonly struct SpawnEvent
        {
            public readonly float TimeIntoWave;
            public readonly EnemyDefinitionSO Enemy;
            public readonly int SpawnPointIndex;
            public readonly int UnitIndexInGroup;

            public SpawnEvent(float time, EnemyDefinitionSO enemy, int spawnPoint, int unitIndex)
            {
                TimeIntoWave = time;
                Enemy = enemy;
                SpawnPointIndex = spawnPoint;
                UnitIndexInGroup = unitIndex;
            }
        }

        private readonly CampaignSO _campaign;
        private readonly IEnemyFactory _factory;
        private readonly SpawnPointProvider _spawnPoints;
        private readonly GameEvents _events;
        private readonly OrbSpawner _orbs;

        private readonly List<SpawnEvent> _pending = new();
        private Phase _phase = Phase.Gap;
        private int _nextGlobalWave;   // 1-based wave that starts when the gap elapses
        private float _gapRemaining;
        private float _waveTime;
        private int _nextSpawnIndex;
        private int _totalThisWave;
        private int _spawnedThisWave;
        private int _killedThisWave;

        public int WaveCount => _campaign.TotalWaves;
        /// <summary>1-based number of the wave currently fighting (or last started).</summary>
        public int CurrentWaveNumber { get; private set; }
        public bool CampaignComplete => _phase == Phase.Complete;
        /// <summary>Gap countdown for UI; -1 while a wave is being fought.</summary>
        public float TimeToNextWave => _phase == Phase.Gap ? Mathf.Max(0f, _gapRemaining) : -1f;

        // Legacy API kept for WinLoseEvaluator wiring simplicity.
        public bool AllSpawned => CampaignComplete;
        public bool LastWaveStarted => CampaignComplete;

        public WaveScheduler(CampaignSO campaign, IEnemyFactory factory, SpawnPointProvider spawnPoints,
            GameEvents events, OrbSpawner orbs, int startWave = 1)
        {
            _campaign = campaign;
            _factory = factory;
            _spawnPoints = spawnPoints;
            _events = events;
            _orbs = orbs;

            _nextGlobalWave = Mathf.Clamp(startWave, 1, WaveCount);
            CurrentWaveNumber = _nextGlobalWave - 1;
            _gapRemaining = _campaign.clearGapSeconds;

            _events.EnemyKilled += OnEnemyKilled;
        }

        public void Tick(float dt)
        {
            switch (_phase)
            {
                case Phase.Gap:
                    _gapRemaining -= dt;
                    if (_gapRemaining <= 0f) StartWave();
                    break;

                case Phase.Fighting:
                    _waveTime += dt;
                    while (_nextSpawnIndex < _pending.Count && _pending[_nextSpawnIndex].TimeIntoWave <= _waveTime)
                    {
                        var evt = _pending[_nextSpawnIndex];
                        _nextSpawnIndex++;
                        Vector3 position = _spawnPoints.GetWithFormationOffset(
                            evt.SpawnPointIndex, evt.UnitIndexInGroup, evt.Enemy.unitRadius);
                        _factory.Spawn(evt.Enemy, position, CurrentWaveNumber - 1);
                        _spawnedThisWave++;
                    }

                    // Clear = everything spawned, everything dead, every orb landed (§0.6).
                    if (_spawnedThisWave >= _totalThisWave && _killedThisWave >= _totalThisWave
                        && (_orbs == null || _orbs.OrbsInFlight == 0))
                    {
                        _events.RaiseWaveCleared(CurrentWaveNumber);
                        if (_nextGlobalWave > WaveCount)
                        {
                            _phase = Phase.Complete;
                        }
                        else
                        {
                            _phase = Phase.Gap;
                            _gapRemaining = _campaign.clearGapSeconds;
                        }
                    }
                    break;
            }
        }

        private void StartWave()
        {
            if (!_campaign.Locate(_nextGlobalWave, out int stageIndex, out int waveInStage))
            {
                _phase = Phase.Complete;
                return;
            }

            var wave = _campaign.stages[stageIndex].waves[waveInStage];
            _pending.Clear();
            _nextSpawnIndex = 0;
            _waveTime = 0f;
            _totalThisWave = 0;
            _spawnedThisWave = 0;
            _killedThisWave = 0;

            foreach (var group in wave.groups)
            {
                for (int u = 0; u < group.count; u++)
                {
                    float t = group.delayAfterWaveStart + u * _campaign.unitSpawnInterval;
                    _pending.Add(new SpawnEvent(t, group.enemy, group.spawnPointIndex, u));
                    _totalThisWave++;
                }
            }
            _pending.Sort((a, b) => a.TimeIntoWave.CompareTo(b.TimeIntoWave));

            CurrentWaveNumber = _nextGlobalWave;
            _nextGlobalWave++;
            _phase = Phase.Fighting;
            _events.RaiseWaveStarted(CurrentWaveNumber);
            _events.RaiseWaveProgressChanged(stageIndex, waveInStage + 1, _campaign.stages[stageIndex].waves.Count);
        }

        private void OnEnemyKilled(EnemyKilledArgs args)
        {
            if (args.WaveIndex == CurrentWaveNumber - 1) _killedThisWave++;
        }
    }
}

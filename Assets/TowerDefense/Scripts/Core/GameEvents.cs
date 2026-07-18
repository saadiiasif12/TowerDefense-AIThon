using System;
using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Data;

namespace RoyalSiege.Core
{
    public readonly struct EnemyKilledArgs
    {
        public readonly EnemyDefinitionSO Definition;
        public readonly int WaveIndex;
        public readonly float Bounty;
        public readonly Vector3 Position;

        public EnemyKilledArgs(EnemyDefinitionSO definition, int waveIndex, float bounty, Vector3 position)
        {
            Definition = definition;
            WaveIndex = waveIndex;
            Bounty = bounty;
            Position = position;
        }
    }

    public readonly struct MatchResult
    {
        public readonly bool Victory;
        public readonly int Stars;
        public readonly int WaveReached;
        public readonly float TowerHpPct;

        public MatchResult(bool victory, int stars, int waveReached, float towerHpPct)
        {
            Victory = victory;
            Stars = stars;
            WaveReached = waveReached;
            TowerHpPct = towerHpPct;
        }
    }

    /// <summary>
    /// Observer hub. Gameplay raises; UI/Juice/Audio subscribe. Gameplay never references UI.
    /// Owned by GameContext — not static, not a singleton.
    /// </summary>
    public sealed class GameEvents
    {
        public event Action<EnemyKilledArgs> EnemyKilled;
        public event Action<float, float> EnergyChanged;                 // current, max
        public event Action<float> EnergyWasted;                          // overflow at cap
        public event Action<int> WaveStarted;                             // 1-based
        public event Action<int> WaveCleared;                             // 1-based
        public event Action<CardDefinitionSO> CardPlayed;
        public event Action HandChanged;
        public event Action<IStructureTarget> BuildingPlaced;
        public event Action<IStructureTarget> BuildingDestroyed;
        public event Action<float, float> TowerDamaged;                   // current, max
        public event Action<SpellCardSO, Vector3> SpellCast;              // cast moment (visuals start)
        public event Action<SpellCardSO, Vector3, IReadOnlyList<Vector3>> SpellResolved; // damage landed (+ struck positions)
        public event Action<EnemyDefinitionSO, Vector3> EnemySpawned;
        public event Action<Vector3> BossSlammed;
        public event Action<Vector3, IEnemyTarget> InstantShotFired;      // muzzle, victim (Tesla zap — target ref so VFX can track/attach)
        public event Action<MatchResult> MatchEnded;
        // ---- v4 journey ----
        public event Action<Vector3> KnightSpawned;                       // deploy flash
        public event Action<Vector3> KnightStruck;                        // a knight landed a hit (clash spark)
        public event Action<Vector3> KnightDied;
        public event Action<int, int, int> WaveProgressChanged;           // stageIndex(0-based), waveInStage(1-based), wavesInStage
        public event Action<CheckpointReachedArgs> CheckpointReached;     // level-up / stage-complete screen
        public event Action<int> TowerLeveledUp;                          // new 1-based level (visual upgrade)
        // 18-Jul tower cinematic: level-up elevator swap / defeat blast. UI screens (level-up,
        // match-end) HOLD until Completed so the tower animation always plays out first.
        public event Action TowerTransitionStarted;
        public event Action TowerTransitionCompleted;
        // ---- 18-Jul juice ----
        public event Action<Vector3, float, bool> EnemyDamaged;           // position, amount, wasKillingBlow (floating numbers)

        public void RaiseEnemyKilled(EnemyKilledArgs args) => EnemyKilled?.Invoke(args);
        public void RaiseEnergyChanged(float current, float max) => EnergyChanged?.Invoke(current, max);
        public void RaiseEnergyWasted(float amount) => EnergyWasted?.Invoke(amount);
        public void RaiseWaveStarted(int wave) => WaveStarted?.Invoke(wave);
        public void RaiseWaveCleared(int wave) => WaveCleared?.Invoke(wave);
        public void RaiseCardPlayed(CardDefinitionSO card) => CardPlayed?.Invoke(card);
        public void RaiseHandChanged() => HandChanged?.Invoke();
        public void RaiseBuildingPlaced(IStructureTarget b) => BuildingPlaced?.Invoke(b);
        public void RaiseBuildingDestroyed(IStructureTarget b) => BuildingDestroyed?.Invoke(b);
        public void RaiseTowerDamaged(float current, float max) => TowerDamaged?.Invoke(current, max);
        public void RaiseSpellCast(SpellCardSO card, Vector3 point) => SpellCast?.Invoke(card, point);
        public void RaiseSpellResolved(SpellCardSO card, Vector3 point, IReadOnlyList<Vector3> hits) => SpellResolved?.Invoke(card, point, hits);
        public void RaiseEnemySpawned(EnemyDefinitionSO def, Vector3 position) => EnemySpawned?.Invoke(def, position);
        public void RaiseBossSlammed(Vector3 position) => BossSlammed?.Invoke(position);
        public void RaiseInstantShotFired(Vector3 from, IEnemyTarget target) => InstantShotFired?.Invoke(from, target);
        public void RaiseMatchEnded(MatchResult result) => MatchEnded?.Invoke(result);
        public void RaiseKnightSpawned(Vector3 position) => KnightSpawned?.Invoke(position);
        public void RaiseKnightStruck(Vector3 position) => KnightStruck?.Invoke(position);
        public void RaiseKnightDied(Vector3 position) => KnightDied?.Invoke(position);
        public void RaiseWaveProgressChanged(int stage, int waveInStage, int wavesInStage) => WaveProgressChanged?.Invoke(stage, waveInStage, wavesInStage);
        public void RaiseCheckpointReached(CheckpointReachedArgs args) => CheckpointReached?.Invoke(args);
        public void RaiseTowerLeveledUp(int level) => TowerLeveledUp?.Invoke(level);
        public void RaiseTowerTransitionStarted() => TowerTransitionStarted?.Invoke();
        public void RaiseTowerTransitionCompleted() => TowerTransitionCompleted?.Invoke();
        public void RaiseEnemyDamaged(Vector3 position, float amount, bool killingBlow)
            => EnemyDamaged?.Invoke(position, amount, killingBlow);
    }

    /// <summary>What the checkpoint screen needs to show (v4 §9).</summary>
    public readonly struct CheckpointReachedArgs
    {
        public readonly int AfterWave;
        public readonly int TowerLevel;         // 0 = unchanged
        public readonly CardDefinitionSO Unlock; // null = none
        public readonly bool IsStageComplete;
        public readonly int Stars;               // stage-complete only
        public readonly Action Dismissed;        // UI calls this to resume the campaign

        public CheckpointReachedArgs(int afterWave, int towerLevel, CardDefinitionSO unlock,
            bool isStageComplete, int stars, Action dismissed)
        {
            AfterWave = afterWave;
            TowerLevel = towerLevel;
            Unlock = unlock;
            IsStageComplete = isStageComplete;
            Stars = stars;
            Dismissed = dismissed;
        }
    }
}

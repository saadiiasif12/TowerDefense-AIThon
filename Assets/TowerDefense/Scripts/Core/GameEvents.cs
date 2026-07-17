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
        public event Action<Vector3, Vector3> InstantShotFired;           // from, to (Tesla zap)
        public event Action<MatchResult> MatchEnded;

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
        public void RaiseInstantShotFired(Vector3 from, Vector3 to) => InstantShotFired?.Invoke(from, to);
        public void RaiseMatchEnded(MatchResult result) => MatchEnded?.Invoke(result);
    }
}

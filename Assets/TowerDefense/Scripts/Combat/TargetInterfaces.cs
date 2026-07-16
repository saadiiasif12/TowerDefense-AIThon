using System.Collections.Generic;
using UnityEngine;

namespace RoyalSiege.Combat
{
    /// <summary>Anything that can take damage. Dead targets ignore further damage.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        Vector3 Position { get; }
        void TakeDamage(float amount);
    }

    /// <summary>Read-only HP fraction, polled by world-space health bars (UI depends on this, never the reverse).</summary>
    public interface IHealthReadout
    {
        float HpPct { get; }
    }

    /// <summary>An enemy as seen by buildings, spells and projectiles.</summary>
    public interface IEnemyTarget : IDamageable
    {
        float CurrentHp { get; }
        bool IsBoss { get; }
        void ApplyFreeze(float seconds);
        void ApplyStun(float seconds);
    }

    /// <summary>A player structure (building or Royal Tower) as seen by enemies.</summary>
    public interface IStructureTarget : IDamageable
    {
        bool IsBuilding { get; }
        float FootprintRadius { get; }
    }

    /// <summary>Read-only spatial queries over everything alive. All distances planar (XZ).</summary>
    public interface ITargetQuery
    {
        IReadOnlyList<IEnemyTarget> Enemies { get; }
        IReadOnlyList<IStructureTarget> Structures { get; }
        int BuildingCount { get; }

        /// <summary>Closest enemy whose CENTER is strictly inside range (GDD strict-range rule).</summary>
        IEnemyTarget ClosestEnemyInRange(Vector3 position, float range);
        void EnemiesInRadius(Vector3 point, float radius, List<IEnemyTarget> results);
        IStructureTarget ClosestStructure(Vector3 position);
        /// <summary>Closest player building, or null if none on the field.</summary>
        IStructureTarget ClosestBuilding(Vector3 position);
        void StructuresInRadius(Vector3 point, float radius, List<IStructureTarget> results);
    }

    /// <summary>Registration side, used by units/structures on spawn and death.</summary>
    public interface ITargetRegistry : ITargetQuery
    {
        void Register(IEnemyTarget enemy);
        void Unregister(IEnemyTarget enemy);
        void Register(IStructureTarget structure);
        void Unregister(IStructureTarget structure);
    }
}

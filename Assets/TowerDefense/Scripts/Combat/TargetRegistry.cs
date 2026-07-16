using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Core;

namespace RoyalSiege.Combat
{
    /// <summary>
    /// Spatial source of truth. Plain list scans — at ≤4 buildings and ≤~40 alive enemies
    /// this beats any spatial structure in both speed and simplicity.
    /// </summary>
    public sealed class TargetRegistry : ITargetRegistry
    {
        private readonly List<IEnemyTarget> _enemies = new();
        private readonly List<IStructureTarget> _structures = new();

        public IReadOnlyList<IEnemyTarget> Enemies => _enemies;
        public IReadOnlyList<IStructureTarget> Structures => _structures;

        public int BuildingCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _structures.Count; i++)
                    if (_structures[i].IsBuilding && _structures[i].IsAlive) count++;
                return count;
            }
        }

        public void Register(IEnemyTarget enemy) { if (!_enemies.Contains(enemy)) _enemies.Add(enemy); }
        public void Unregister(IEnemyTarget enemy) => _enemies.Remove(enemy);
        public void Register(IStructureTarget structure) { if (!_structures.Contains(structure)) _structures.Add(structure); }
        public void Unregister(IStructureTarget structure) => _structures.Remove(structure);

        public IEnemyTarget ClosestEnemyInRange(Vector3 position, float range)
        {
            IEnemyTarget best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                if (!e.IsAlive) continue;
                float d = RangeMath.PlanarDistance(position, e.Position);
                if (d <= range && d < bestDist) { best = e; bestDist = d; }
            }
            return best;
        }

        public void EnemiesInRadius(Vector3 point, float radius, List<IEnemyTarget> results)
        {
            results.Clear();
            for (int i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                if (e.IsAlive && RangeMath.IsInside(point, e.Position, radius)) results.Add(e);
            }
        }

        public IStructureTarget ClosestStructure(Vector3 position) => ClosestOf(position, buildingsOnly: false);
        public IStructureTarget ClosestBuilding(Vector3 position) => ClosestOf(position, buildingsOnly: true);

        public void StructuresInRadius(Vector3 point, float radius, List<IStructureTarget> results)
        {
            results.Clear();
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (s.IsAlive && RangeMath.IsInside(point, s.Position, radius)) results.Add(s);
            }
        }

        private IStructureTarget ClosestOf(Vector3 position, bool buildingsOnly)
        {
            IStructureTarget best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _structures.Count; i++)
            {
                var s = _structures[i];
                if (!s.IsAlive || (buildingsOnly && !s.IsBuilding)) continue;
                float d = RangeMath.PlanarDistance(position, s.Position);
                if (d < bestDist) { best = s; bestDist = d; }
            }
            return best;
        }
    }
}

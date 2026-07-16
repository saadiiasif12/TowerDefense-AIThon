using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Combat;

namespace RoyalSiege.Data
{
    public readonly struct SpellContext
    {
        public readonly Vector3 Point;
        public readonly float Radius;
        /// <summary>Enemies already filtered: center inside radius AND inside the map circle.</summary>
        public readonly IReadOnlyList<IEnemyTarget> Targets;

        public SpellContext(Vector3 point, float radius, IReadOnlyList<IEnemyTarget> targets)
        {
            Point = point;
            Radius = radius;
            Targets = targets;
        }
    }

    /// <summary>Strategy base: new spells are new subclasses + an asset — no switch statements.</summary>
    public abstract class SpellEffectSO : ScriptableObject
    {
        public abstract void Apply(in SpellContext context);
    }
}

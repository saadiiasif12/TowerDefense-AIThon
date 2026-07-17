using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Combat;

namespace RoyalSiege.Data
{
    /// <summary>
    /// A spell that keeps simulating after the cast (Earthquake DoT zone, Log roll).
    /// Ticked by SpellCaster at the 10 Hz sim rate; return false when finished.
    /// </summary>
    public interface ISpellZone
    {
        bool Tick(float dt);
    }

    /// <summary>Services an effect may use beyond the captured target list (v4: zones).</summary>
    public interface ISpellRuntime
    {
        ITargetQuery Query { get; }
        Vector3 MapCenter { get; }
        void AddZone(ISpellZone zone);
    }

    public readonly struct SpellContext
    {
        public readonly Vector3 Point;
        public readonly float Radius;
        /// <summary>Enemies already filtered: center inside radius AND inside the map circle.</summary>
        public readonly IReadOnlyList<IEnemyTarget> Targets;
        /// <summary>Optional out-list: effects append the positions they actually struck (juice draws there).</summary>
        public readonly List<Vector3> HitReport;
        /// <summary>Zone/query services (v4). Null only in legacy tests.</summary>
        public readonly ISpellRuntime Runtime;

        public SpellContext(Vector3 point, float radius, IReadOnlyList<IEnemyTarget> targets,
            List<Vector3> hitReport = null, ISpellRuntime runtime = null)
        {
            Point = point;
            Radius = radius;
            Targets = targets;
            HitReport = hitReport;
            Runtime = runtime;
        }
    }

    /// <summary>Strategy base: new spells are new subclasses + an asset — no switch statements.</summary>
    public abstract class SpellEffectSO : ScriptableObject
    {
        public abstract void Apply(in SpellContext context);
    }
}

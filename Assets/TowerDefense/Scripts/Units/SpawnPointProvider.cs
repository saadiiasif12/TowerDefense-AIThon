using UnityEngine;

namespace RoyalSiege.Units
{
    /// <summary>8 evenly spaced points on the map edge. Index 0 = N (+Z), clockwise: 1=NE … 7=NW.</summary>
    public sealed class SpawnPointProvider
    {
        private readonly Vector3 _center;
        private readonly float _mapRadius;

        public SpawnPointProvider(Vector3 center, float mapRadius)
        {
            _center = center;
            _mapRadius = mapRadius;
        }

        public Vector3 Get(int index)
        {
            float angleRad = index * 45f * Mathf.Deg2Rad;
            return _center + new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad)) * _mapRadius;
        }

        /// <summary>
        /// Deterministic loose formation (zero-RNG guarantee): units fan out along the map
        /// edge ARC (columns) and in staggered ranks behind it (rows), WIDELY spaced, plus a
        /// per-unit hash scatter so a pack arrives as a spread, slightly irregular crowd
        /// instead of a tidy single-file stack (18-Jul: enemies felt too bunched/rigid).
        /// </summary>
        public Vector3 GetWithFormationOffset(int index, int unitIndexInGroup, float unitRadius)
        {
            int col = (unitIndexInGroup % 5) - 2;             // -2..2 across the arc
            int row = unitIndexInGroup / 5;                    // ranks behind the edge
            // Wider than before (was max(0.9, r*2.6)) so there is real air between enemies.
            float spacing = Mathf.Max(2.0f, unitRadius * 4.0f);

            // Deterministic per-unit scatter — breaks the rigid grid without any RNG. Kept small
            // relative to the spacing so a jittered pair can never end up overlapping.
            float radialJitter  = (Hash01((index * 73856093) ^ (unitIndexInGroup * 19349663)) - 0.5f) * spacing * 0.4f;
            float angularJitter = (Hash01((index * 83492791) ^ ((unitIndexInGroup + 7) * 12582917)) - 0.5f) * 0.4f;

            // Column spread as an angle so spacing stays constant on the circle.
            float arcDegreesPerColumn = spacing / _mapRadius * Mathf.Rad2Deg;
            float angleRad = (index * 45f + (col + angularJitter) * arcDegreesPerColumn) * Mathf.Deg2Rad;

            // Ranks step outward; alternate columns stagger half a step (quincunx) so no two
            // units share a lane toward the center, then the radial jitter loosens it further.
            float radius = _mapRadius + row * spacing + (Mathf.Abs(col) % 2) * spacing * 0.5f + radialJitter;

            return _center + new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad)) * radius;
        }

        /// <summary>Deterministic integer hash → [0,1). Keeps the zero-RNG guarantee for spawn scatter.</summary>
        private static float Hash01(int n)
        {
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }
    }
}

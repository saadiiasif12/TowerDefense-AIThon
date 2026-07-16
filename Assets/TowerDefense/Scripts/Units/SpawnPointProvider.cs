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
        /// edge ARC (columns) and in staggered ranks behind it (rows), spaced by their body
        /// radius, so a pack arrives as a spread crowd instead of a single-file stack.
        /// </summary>
        public Vector3 GetWithFormationOffset(int index, int unitIndexInGroup, float unitRadius)
        {
            int col = (unitIndexInGroup % 5) - 2;             // -2..2 across the arc
            int row = unitIndexInGroup / 5;                    // ranks behind the edge
            float spacing = Mathf.Max(0.9f, unitRadius * 2.6f);

            // Column spread as an angle so spacing stays constant on the circle.
            float arcDegreesPerColumn = spacing / _mapRadius * Mathf.Rad2Deg;
            float angleRad = (index * 45f + col * arcDegreesPerColumn) * Mathf.Deg2Rad;

            // Ranks step outward; alternate columns stagger half a step (quincunx) so no
            // two units share a lane toward the center.
            float radius = _mapRadius + row * spacing + (Mathf.Abs(col) % 2) * spacing * 0.5f;

            return _center + new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad)) * radius;
        }
    }
}

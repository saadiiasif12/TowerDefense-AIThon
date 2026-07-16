using UnityEngine;

namespace RoyalSiege.Units
{
    /// <summary>8 evenly spaced points on the map edge. Index 0 = N (+Z), clockwise: 1=NE … 7=NW.</summary>
    public sealed class SpawnPointProvider
    {
        private readonly Vector3[] _points = new Vector3[8];

        public SpawnPointProvider(Vector3 center, float mapRadius)
        {
            for (int i = 0; i < 8; i++)
            {
                float angleRad = i * 45f * Mathf.Deg2Rad;
                _points[i] = center + new Vector3(Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad)) * mapRadius;
            }
        }

        public Vector3 Get(int index) => _points[Mathf.Clamp(index, 0, 7)];

        /// <summary>
        /// Deterministic formation offset so pack members don't stack (zero-RNG guarantee).
        /// Offsets spread units perpendicular to the inward direction.
        /// </summary>
        public Vector3 GetWithFormationOffset(int index, int unitIndexInGroup, Vector3 center)
        {
            Vector3 basePoint = Get(index);
            Vector3 inward = (center - basePoint).normalized;
            Vector3 lateral = Vector3.Cross(Vector3.up, inward);
            float offset = ((unitIndexInGroup % 5) - 2) * 0.45f;
            float depth = (unitIndexInGroup / 5) * 0.6f;
            return basePoint + lateral * offset - inward * depth;
        }
    }
}

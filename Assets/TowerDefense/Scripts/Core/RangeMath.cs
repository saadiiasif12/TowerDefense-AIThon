using UnityEngine;

namespace RoyalSiege.Core
{
    /// <summary>
    /// THE single strict-range implementation (GDD v2 range rule). Stateless pure math —
    /// the only static class allowed in the project. All distances are planar (XZ).
    /// </summary>
    public static class RangeMath
    {
        /// <summary>Strict inclusive check: target CENTER inside the circle.</summary>
        public static bool IsInside(Vector3 a, Vector3 b, float range)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz <= range * range;
        }

        public static float PlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static Vector3 PlanarDirection(Vector3 from, Vector3 to)
        {
            var d = new Vector3(to.x - from.x, 0f, to.z - from.z);
            return d.sqrMagnitude < 0.0001f ? Vector3.forward : d.normalized;
        }

        public static Vector3 Flatten(Vector3 v) => new(v.x, 0f, v.z);
    }
}

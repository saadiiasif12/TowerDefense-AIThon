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

        // Golden-angle conjugate: successive unit indices land ~137.5° apart, so a group of
        // one enemy type fans out quasi-uniformly around the WHOLE tower — never a clustered
        // pack — while staying fully deterministic (the project's zero-RNG guarantee).
        private const float GoldenAngleFraction = 0.61803398875f;

        /// <summary>
        /// Full-ring scatter (18-Jul rule): each unit of a group spawns at its OWN bearing
        /// spread around the entire tower instead of clustering near one map-edge point, so a
        /// wave arrives from random positions all around rather than as a single pack. The
        /// spawn DISTANCE (map radius) is preserved — only the angle varies, plus a small
        /// radial jitter so two quasi-uniform neighbours never share an exact spot. Fully
        /// deterministic: <paramref name="groupSeed"/> rotates the whole ring per wave/group
        /// so no two waves march in from the same bearings.
        /// </summary>
        public Vector3 GetRingScatter(int groupSeed, int unitIndexInGroup, float unitRadius)
        {
            // Per-group rotation of the whole ring + golden-angle step per unit gives even
            // full-circle COVERAGE; a strong per-unit angular jitter (±~0.10 turns ≈ ±36°)
            // then breaks the too-regular spacing so gaps between enemies look irregular
            // (some cluster, some spread) — organic, not a metronome ring. Still zero-RNG.
            float angularJitter = (Hash01((groupSeed * 92083) ^ ((unitIndexInGroup + 11) * 51787)) - 0.5f) * 0.20f;
            float turns = Hash01(groupSeed * 374761393 + 668265263)
                          + unitIndexInGroup * GoldenAngleFraction
                          + angularJitter;
            float angleRad = (turns - Mathf.Floor(turns)) * 2f * Mathf.PI;

            // Small radial jitter breaks ties between golden-angle neighbours and adds depth,
            // kept modest so the intended spawn distance is essentially unchanged.
            float spacing = Mathf.Max(2.0f, unitRadius * 4.0f);
            float radialJitter = (Hash01((groupSeed * 40503) ^ ((unitIndexInGroup + 3) * 20903)) - 0.5f)
                                 * spacing * 0.5f;
            float radius = _mapRadius + radialJitter;

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

using UnityEngine;

namespace RoyalSiege.Buildings
{
    /// <summary>
    /// Draws the exact attack range as a flat circle. Because range checks are strict
    /// (center-in-circle), this ring never lies — a core v2 promise.
    /// QA 17-Jul fixes: points are computed in WORLD space and mapped back through the
    /// transform, so a scaled parent (Ghost_Spell is 0.6×) can no longer shrink the drawn
    /// circle below the true radius (DT-005..008). Optionally clips to the map circle so
    /// the ring never renders outside the playable area (DT-001).
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class RangeRing : MonoBehaviour
    {
        private const int Segments = 48;

        [SerializeField] private LineRenderer _line;
        [SerializeField] private float _height = 0.05f;

        private float _radius;
        private Vector3 _mapCenter;
        private float _mapRadius; // 0 = no clipping

        private void Awake()
        {
            if (_line == null) _line = GetComponent<LineRenderer>();
            _line.loop = true;
            _line.useWorldSpace = false;
            _line.positionCount = Segments;
        }

        public void SetRadius(float radius)
        {
            if (_line == null) Awake();
            _radius = radius;
            Rebuild();
        }

        /// <summary>Clip the ring to the map circle (0 radius = disabled).</summary>
        public void SetMapClip(Vector3 mapCenter, float mapRadius)
        {
            _mapCenter = mapCenter;
            _mapRadius = mapRadius;
        }

        /// <summary>Re-project after the ring moved (the ghost calls this every drag frame).</summary>
        public void Rebuild()
        {
            if (_line == null) return;
            Vector3 origin = transform.position;
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                var world = origin + new Vector3(Mathf.Sin(angle) * _radius, 0f, Mathf.Cos(angle) * _radius);

                if (_mapRadius > 0f)
                {
                    var offset = new Vector3(world.x - _mapCenter.x, 0f, world.z - _mapCenter.z);
                    float dist = offset.magnitude;
                    if (dist > _mapRadius)
                        world = new Vector3(_mapCenter.x, world.y, _mapCenter.z) + offset * (_mapRadius / dist);
                }

                world.y = origin.y + _height;
                _line.SetPosition(i, transform.InverseTransformPoint(world));
            }
        }

        public void SetVisible(bool visible) => _line.enabled = visible;
    }
}

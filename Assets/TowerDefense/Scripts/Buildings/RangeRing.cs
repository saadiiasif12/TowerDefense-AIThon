using UnityEngine;

namespace RoyalSiege.Buildings
{
    /// <summary>
    /// Draws the exact attack range as a flat circle. Because range checks are strict
    /// (center-in-circle), this ring never lies — a core v2 promise.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class RangeRing : MonoBehaviour
    {
        private const int Segments = 48;

        [SerializeField] private LineRenderer _line;
        [SerializeField] private float _height = 0.05f;

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
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                _line.SetPosition(i, new Vector3(Mathf.Sin(angle) * radius, _height, Mathf.Cos(angle) * radius));
            }
        }

        public void SetVisible(bool visible) => _line.enabled = visible;
    }
}

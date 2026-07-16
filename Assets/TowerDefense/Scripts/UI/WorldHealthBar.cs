using UnityEngine;
using RoyalSiege.Combat;

namespace RoyalSiege.UI
{
    /// <summary>
    /// Self-contained world-space health bar: finds an IHealthReadout on its object,
    /// builds its own quads, billboards to the camera, hides when full or dead.
    /// Gameplay code never references this — it only implements IHealthReadout.
    /// </summary>
    public sealed class WorldHealthBar : MonoBehaviour
    {
        [SerializeField] private float _width = 1.2f;
        [SerializeField] private float _thickness = 0.14f;
        [SerializeField] private float _yOffset = 2.3f;
        [SerializeField] private Color _fillColor = new(0.25f, 0.9f, 0.2f);
        [SerializeField] private Color _lowColor = new(0.95f, 0.25f, 0.15f);
        [SerializeField] private bool _hideWhenFull = true;

        private IHealthReadout _source;
        private Transform _root;
        private Transform _fill;
        private Renderer _fillRenderer;
        private Camera _camera;

        public void Configure(float width, float yOffset, bool hideWhenFull)
        {
            _width = width;
            _yOffset = yOffset;
            _hideWhenFull = hideWhenFull;
        }

        private void Start()
        {
            _source = GetComponentInParent<IHealthReadout>();
            _camera = Camera.main;
            Build();
        }

        private void Build()
        {
            var shader = Shader.Find("Sprites/Default");

            _root = new GameObject("HealthBar").transform;
            _root.SetParent(transform, false);

            Transform MakeQuad(string name, Color color, float z)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = name;
                Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(_root, false);
                quad.transform.localPosition = new Vector3(0f, 0f, z);
                var r = quad.GetComponent<Renderer>();
                r.sharedMaterial = new Material(shader) { color = color };
                return quad.transform;
            }

            var bg = MakeQuad("BG", new Color(0f, 0f, 0f, 0.65f), 0.001f);
            bg.localScale = new Vector3(_width, _thickness, 1f);
            _fill = MakeQuad("Fill", _fillColor, 0f);
            _fillRenderer = _fill.GetComponent<Renderer>();
        }

        private void LateUpdate()
        {
            if (_source == null || _root == null) return;

            float pct = Mathf.Clamp01(_source.HpPct);
            bool visible = pct > 0f && (!_hideWhenFull || pct < 0.999f);
            _root.gameObject.SetActive(visible);
            if (!visible) return;

            // Anchor above the OWNER'S WORLD position (not local) and face the camera.
            _root.position = transform.position + Vector3.up * _yOffset;
            if (_camera != null) _root.rotation = _camera.transform.rotation;

            _fill.localScale = new Vector3(_width * pct, _thickness, 1f);
            _fill.localPosition = new Vector3(-_width * (1f - pct) * 0.5f, 0f, 0f);
            _fillRenderer.material.color = Color.Lerp(_lowColor, _fillColor, pct);
        }
    }
}

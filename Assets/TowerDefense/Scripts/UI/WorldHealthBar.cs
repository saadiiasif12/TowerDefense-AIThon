using UnityEngine;
using RoyalSiege.Combat;

namespace RoyalSiege.UI
{
    /// <summary>
    /// Self-contained world-space health bar: finds an IHealthReadout on its object,
    /// billboards to the camera, hides when full or dead. 18-Jul: the VISUALS are a PREFAB
    /// (Prefabs/UI/HealthBar + per-category variants — Enemy/Ally/Building/Tower) so UI can
    /// preset size, sprites, border and colors per unit type in the editor. This component
    /// only instantiates the assigned bar prefab and drives fill/visibility/billboard:
    ///  · full-HP color = the AUTHORED color of the prefab's "Fill" sprite,
    ///  · low-HP color  = serialized here (lerped toward as HP drops),
    ///  · bar width     = measured from the prefab's "BG" child (authored, not hardcoded).
    /// Falls back to the legacy runtime quads when no prefab is assigned (test scenes).
    /// Gameplay code never references this — it only implements IHealthReadout.
    /// </summary>
    public sealed class WorldHealthBar : MonoBehaviour
    {
        [Tooltip("Bar visuals prefab (BG + Fill children). Category variants live in Prefabs/UI.")]
        [SerializeField] private GameObject _barPrefab;
        [Tooltip("World height of the bar above this object's pivot (per-unit placement).")]
        [SerializeField] private float _yOffset = 2.3f;
        [Tooltip("Color the fill lerps toward as HP approaches zero.")]
        [SerializeField] private Color _lowColor = new(0.95f, 0.25f, 0.15f);
        [SerializeField] private bool _hideWhenFull = true;
        [Tooltip("Render the bar at a consistent world size regardless of this unit's transform scale " +
                 "(e.g. the 2.5× Knight gets the same small bar as a 1× enemy). Leave off for enemies/tower.")]
        [SerializeField] private bool _counterParentScale;

        [Header("Legacy fallback (used only when no prefab is assigned)")]
        [SerializeField] private float _width = 1.2f;
        [SerializeField] private float _thickness = 0.14f;
        [SerializeField] private Color _fillColor = new(0.25f, 0.9f, 0.2f);

        private IHealthReadout _source;
        private Transform _root;
        private Transform _fill;
        private SpriteRenderer _fillSprite;    // prefab path (per-renderer color, no material instancing)
        private Renderer _fillRenderer;        // legacy quad path
        private Color _fullColor;
        private float _barWidth;
        private Camera _camera;

        /// <summary>Legacy runtime configuration (kept for API compat; width scales the bar).</summary>
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
            if (_barPrefab != null) BuildFromPrefab();
            else BuildLegacyQuads();
        }

        private void BuildFromPrefab()
        {
            var instance = Instantiate(_barPrefab, transform, false);
            instance.name = "HealthBar";
            _root = instance.transform;

            var bg = _root.Find("BG");
            _fill = _root.Find("Fill");
            _fillSprite = _fill != null ? _fill.GetComponent<SpriteRenderer>() : null;
            _fullColor = _fillSprite != null ? _fillSprite.color : _fillColor; // authored full-HP color
            // Full-fill width = the artist-authored Fill width (a 1×1 Square scaled to fit the
            // frame), NOT bg.localScale.x — that was 1.0 while the BG SPRITE is 1.2 wide, so the
            // green never reached the frame edges. The authored Fill (1.13) fills it properly.
            _barWidth = _fill != null ? Mathf.Abs(_fill.localScale.x)
                      : bg != null ? bg.localScale.x : _width;

            // Consistent world size across unit scales (Knight root is 2.5× → same bar as enemies).
            if (_counterParentScale)
            {
                float s = transform.lossyScale.x;
                if (s > 0.0001f) _root.localScale /= s;
            }
        }

        private void BuildLegacyQuads()
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
            _fullColor = _fillColor;
            _barWidth = _width;
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

            // Fill drains from the right (left-anchored) and lerps toward the low color.
            var fillScale = _fill.localScale;
            _fill.localScale = new Vector3(_barWidth * pct, fillScale.y, fillScale.z);
            _fill.localPosition = new Vector3(-_barWidth * (1f - pct) * 0.5f, _fill.localPosition.y, _fill.localPosition.z);
            var color = Color.Lerp(_lowColor, _fullColor, pct);
            if (_fillSprite != null) _fillSprite.color = color;
            else if (_fillRenderer != null) _fillRenderer.material.color = color;
        }
    }
}

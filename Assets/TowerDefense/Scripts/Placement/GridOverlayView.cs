using UnityEngine;

namespace RoyalSiege.Placement
{
    /// <summary>
    /// Soft additive grid over the deployment circle, shown only while dragging a building
    /// card, so the snap grid is readable exactly when it matters. Everything is generated
    /// at Init: one quad, one procedurally drawn texture (cell = placementSnap, circular
    /// fade baked in) on an additive material — a single cheap draw call, mobile-safe.
    /// </summary>
    public sealed class GridOverlayView : MonoBehaviour
    {
        [SerializeField] private Material _additiveMaterialTemplate;
        [SerializeField] private Color _lineColor = Color.white; // additive over bright grass needs full white to read
        [Range(0f, 1f)] [SerializeField] private float _lineStrength = 0.9f;

        private GameObject _quad;

        public void Init(float deploymentRadius, float cellSize)
        {
            if (_quad != null) return;

            const int texSize = 512;
            var texture = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float worldSize = deploymentRadius * 2f;
            float pixelsPerCell = texSize / (worldSize / cellSize);
            var pixels = new Color32[texSize * texSize];
            float half = texSize * 0.5f;

            for (int y = 0; y < texSize; y++)
            for (int x = 0; x < texSize; x++)
            {
                // Distance to the nearest grid line, in pixels (grid is centered).
                float gx = Mathf.Abs(Mathf.Repeat(x - half + pixelsPerCell * 0.5f, pixelsPerCell) - pixelsPerCell * 0.5f);
                float gy = Mathf.Abs(Mathf.Repeat(y - half + pixelsPerCell * 0.5f, pixelsPerCell) - pixelsPerCell * 0.5f);
                float line = 1f - Mathf.Clamp01(Mathf.Min(gx, gy) / 2.4f);

                // Radial fade to zero at the circle edge.
                float r = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                float mask = Mathf.Clamp01((1f - r) * 4f);

                float v = line * mask * _lineStrength;
                pixels[y * texSize + x] = new Color(_lineColor.r * v, _lineColor.g * v, _lineColor.b * v, v);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quad.name = "GridOverlay";
            Destroy(_quad.GetComponent<Collider>());
            _quad.transform.SetParent(transform, false);
            _quad.transform.SetPositionAndRotation(new Vector3(0f, 0.03f, 0f), Quaternion.Euler(90f, 0f, 0f));
            _quad.transform.localScale = new Vector3(worldSize, worldSize, 1f);

            var renderer = _quad.GetComponent<MeshRenderer>();
            var material = _additiveMaterialTemplate != null
                ? new Material(_additiveMaterialTemplate)
                : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _quad.SetActive(false);
        }

        public void Show() => _quad?.SetActive(true);
        public void Hide() => _quad?.SetActive(false);
    }
}

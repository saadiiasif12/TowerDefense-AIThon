using UnityEngine;

namespace RoyalSiege.Placement
{
    /// <summary>
    /// Filled snap TILES over the deployment circle, shown only while dragging a building
    /// card, so the snap grid is readable exactly when it matters. Everything is generated
    /// at Init: one quad, one procedurally drawn texture (cell = placementSnap, solid cells
    /// split by thin transparent seams + soft checker, circular fade baked in) on a
    /// transparent material — a single cheap draw call, mobile-safe. Tiles are offset so
    /// their CENTERS sit on snap points: a snapped building lands mid-tile, not on a corner.
    /// </summary>
    public sealed class GridOverlayView : MonoBehaviour
    {
        [SerializeField] private Material _additiveMaterialTemplate;
        [SerializeField] private Color _lineColor = Color.white; // tile tint — white reads best over bright grass
        [Range(0f, 1f)] [SerializeField] private float _lineStrength = 0.9f; // master opacity multiplier

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

            // Solid tiles need far less alpha than hairlines — full strength would white out the arena.
            const float fillOpacity = 0.24f;
            const float gutterPixels = 1f; // transparent seam between tiles

            for (int y = 0; y < texSize; y++)
            for (int x = 0; x < texSize; x++)
            {
                // Tile-local coords, offset half a cell so tile CENTERS sit on snap points.
                float lx = Mathf.Repeat(x - half + pixelsPerCell * 0.5f, pixelsPerCell);
                float ly = Mathf.Repeat(y - half + pixelsPerCell * 0.5f, pixelsPerCell);

                // Solid fill up to a thin gutter at the tile border (1.5 px anti-alias ramp).
                float border = Mathf.Min(Mathf.Min(lx, pixelsPerCell - lx), Mathf.Min(ly, pixelsPerCell - ly));
                float fill = Mathf.Clamp01((border - gutterPixels) / 1.5f);

                // Subtle checker keeps tiles readable where the thin gutters mip away.
                int cx = Mathf.FloorToInt((x - half + pixelsPerCell * 0.5f) / pixelsPerCell);
                int cy = Mathf.FloorToInt((y - half + pixelsPerCell * 0.5f) / pixelsPerCell);
                float checker = ((cx + cy) & 1) == 0 ? 1f : 0.62f;

                // Radial fade to zero at the circle edge.
                float r = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                float mask = Mathf.Clamp01((1f - r) * 4f);

                float v = fill * checker * mask * _lineStrength * fillOpacity;
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

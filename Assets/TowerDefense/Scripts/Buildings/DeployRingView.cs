using UnityEngine;
using RoyalSiege.Data;

namespace RoyalSiege.Buildings
{
    /// <summary>
    /// The dashed white deployment circle (18-Jul art direction: dotted patches, opaque mesh —
    /// transparent LineRenderers don't render under this URP setup). Self-building and
    /// self-positioning:
    ///   · RADIUS  = GameConfig.deploymentRadius — THE single knob (gameplay validator,
    ///     knight guard leash and this ring all read the same field).
    ///   · CENTER  = follows the Royal Tower's XZ every frame (gameplay's map center is
    ///     derived from the tower too, so the ring can never lie).
    ///   · HEIGHT  = tower Y + _heightOffset, so lifting the tower lifts the ring with it.
    /// Runs in edit mode (ExecuteAlways) so the scene view always shows the true circle;
    /// the mesh is generated in-memory (DontSave) — nothing baked, nothing to go stale.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class DeployRingView : MonoBehaviour
    {
        [Tooltip("Radius source — deploymentRadius is the single knob for ring + gameplay.")]
        [SerializeField] private GameConfigSO _config;
        [Tooltip("The Royal Tower — the ring follows its XZ and rides its height.")]
        [SerializeField] private Transform _tower;

        [Header("Dash look")]
        [SerializeField, Min(8)] private int _dashCount = 40;
        [SerializeField, Min(0.05f)] private float _dashLength = 0.42f;
        [SerializeField, Min(0.01f)] private float _dashWidth = 0.18f;
        [Tooltip("Ring Y relative to the tower's Y (tower pivot sits above the ground).")]
        [SerializeField] private float _heightOffset = -1.74f;

        private Mesh _mesh;
        private float _builtRadius = -1f;
        private int _builtCount;
        private float _builtLength, _builtWidth;

        // 18-Jul drag highlight: the circle rests slightly dimmed and brightens to pure
        // white while a card is being dragged (MPB — the shared material stays untouched).
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color RestColor = new(0.82f, 0.82f, 0.82f, 1f);
        private MaterialPropertyBlock _mpb;
        private float _highlight;          // 0 rest → 1 dragging
        private float _highlightTarget;

        /// <summary>Brighten while a card is held (called by PlacementController).</summary>
        public void SetDragHighlight(bool on) => _highlightTarget = on ? 1f : 0f;

        private float Radius => _config != null ? _config.deploymentRadius : 5f;

        private void OnEnable()
        {
            // The arena is a PREFAB (18-Jul): a prefab can't serialize a scene ref, so find
            // the tower at runtime/editor when the slot is empty (same pattern as the HUD).
            if (_tower == null)
            {
                var tower = FindFirstObjectByType<RoyalTower>();
                if (tower != null) _tower = tower.transform;
            }
            Rebuild();
        }

        private void OnDisable()
        {
            if (_mesh != null) { DestroyMesh(); }
        }

        private void LateUpdate()
        {
            // Follow the tower (XZ + relative height). Cheap: one transform write.
            if (_tower != null)
                transform.position = new Vector3(_tower.position.x, _tower.position.y + _heightOffset, _tower.position.z);
            transform.rotation = Quaternion.identity; // never inherit the tower's yaw

            if (!Mathf.Approximately(_builtRadius, Radius) || _builtCount != _dashCount ||
                !Mathf.Approximately(_builtLength, _dashLength) || !Mathf.Approximately(_builtWidth, _dashWidth))
                Rebuild();

            // Drag highlight (runtime only; MPB keeps the shared material clean).
            if (Application.isPlaying && !Mathf.Approximately(_highlight, _highlightTarget))
            {
                _highlight = Mathf.MoveTowards(_highlight, _highlightTarget, Time.unscaledDeltaTime * 8f);
                _mpb ??= new MaterialPropertyBlock();
                var mr = GetComponent<MeshRenderer>();
                mr.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, Color.Lerp(RestColor, Color.white, _highlight));
                mr.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>Regenerate the dash quads for the current radius/tuning (in-memory only).</summary>
        public void Rebuild()
        {
            float radius = Radius;
            int n = _dashCount;
            var verts = new Vector3[n * 4];
            var tris = new int[n * 6];
            float halfW = _dashWidth * 0.5f;

            for (int i = 0; i < n; i++)
            {
                float th = i * Mathf.PI * 2f / n;
                var c = new Vector3(Mathf.Sin(th) * radius, 0f, Mathf.Cos(th) * radius);
                var tan = new Vector3(Mathf.Cos(th), 0f, -Mathf.Sin(th));
                var rad = new Vector3(Mathf.Sin(th), 0f, Mathf.Cos(th));
                int vi = i * 4;
                verts[vi] = c - tan * (_dashLength * 0.5f) - rad * halfW;
                verts[vi + 1] = c + tan * (_dashLength * 0.5f) - rad * halfW;
                verts[vi + 2] = c + tan * (_dashLength * 0.5f) + rad * halfW;
                verts[vi + 3] = c - tan * (_dashLength * 0.5f) + rad * halfW;
                int ti = i * 6;
                tris[ti] = vi; tris[ti + 1] = vi + 1; tris[ti + 2] = vi + 2;
                tris[ti + 3] = vi; tris[ti + 4] = vi + 2; tris[ti + 5] = vi + 3;
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "DeployRingDash (runtime)" };
                _mesh.hideFlags = HideFlags.DontSave;
            }
            _mesh.Clear();
            _mesh.vertices = verts;
            _mesh.triangles = tris;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;

            _builtRadius = radius; _builtCount = n; _builtLength = _dashLength; _builtWidth = _dashWidth;
        }

        private void DestroyMesh()
        {
            if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
            _mesh = null;
        }
    }
}

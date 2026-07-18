using TMPro;
using UnityEngine;

namespace RoyalSiege.Juice
{
    /// <summary>
    /// One pooled damage-number popup: 3D TextMeshPro digits over a rounded SpriteRenderer
    /// plate (no world-space Canvas, no rebuilds). Animation is ticked centrally by
    /// <see cref="DamagePopupManager"/> (single Update, zero coroutines/allocations):
    /// spawn at startScale → sharp punch → smooth settle → rise + tiny lateral drift →
    /// text AND plate fade near the end. Always billboards to the camera.
    /// The text material is instanced ONCE per pooled popup (outline/shadow alpha can't be
    /// faded through vertex color) — never per frame.
    /// </summary>
    public sealed class DamagePopup : MonoBehaviour
    {
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int UnderlayColorId = Shader.PropertyToID("_UnderlayColor");

        [SerializeField] private TextMeshPro _text;
        [SerializeField] private SpriteRenderer _background;
        [Tooltip("World padding added around the text when sizing the background plate.")]
        [SerializeField] private Vector2 _backgroundPadding = new(0.28f, 0.12f);

        private DamagePopupManager _manager;
        private Material _textMaterial;      // per-popup instance, created once at pool build
        private Color _outlineBase;
        private Color _underlayBase;
        private Color _faceColor;
        private Color _backgroundBase;
        private Vector3 _origin;
        private float _driftX;
        private float _scaleMul = 1f;
        private float _age;

        /// <summary>Pool-build-time setup (called once, before first Show).</summary>
        public void Init(DamagePopupManager manager)
        {
            _manager = manager;
            _textMaterial = _text.fontMaterial; // instances the material ONCE for this popup
            _outlineBase = _textMaterial.GetColor(OutlineColorId);
            _underlayBase = _textMaterial.HasProperty(UnderlayColorId)
                ? _textMaterial.GetColor(UnderlayColorId) : new Color(0f, 0f, 0f, 0.5f);
        }

        /// <summary>Arm and show this popup. Label/colors are pre-resolved by the manager.</summary>
        public void Show(string label, Color faceColor, Color backgroundColor, float scaleMul,
            Vector3 origin, float driftX)
        {
            _faceColor = faceColor;
            _backgroundBase = backgroundColor;
            _origin = origin;
            _driftX = driftX;
            _scaleMul = scaleMul;
            _age = 0f;

            transform.position = origin;
            transform.localScale = Vector3.one * (_manager.StartScale * scaleMul);
            gameObject.SetActive(true); // BEFORE measuring — inactive TMP reports garbage bounds

            _text.text = label;
            _text.color = faceColor;
            // Preferred values are reliable without a full mesh rebuild; clamp as a safety net.
            Vector2 textSize = _text.GetPreferredValues(label);
            _background.size = new Vector2(
                Mathf.Min(textSize.x, 4f) + _backgroundPadding.x,
                Mathf.Min(textSize.y, 2f) + _backgroundPadding.y);
            _background.color = backgroundColor;
        }

        /// <summary>Spawn position this popup is anchored to (anti-overlap checks).</summary>
        public Vector3 Origin => _origin;
        public float Age => _age;

        /// <summary>
        /// Advance the animation. Returns false when finished (manager releases to the pool).
        /// Runs on wall-clock dt so numbers keep floating while the sim pauses.
        /// </summary>
        public bool Tick(float dt, Quaternion cameraRotation, Vector3 cameraRight)
        {
            _age += dt;
            float duration = Mathf.Max(0.05f, _manager.Duration);
            float t = _age / duration;
            if (t >= 1f) { gameObject.SetActive(false); return false; }

            // --- Scale: sharp punch (ease-out-back) then smooth settle to 1 ---
            float scale;
            float punchEnd = _manager.PunchTime, settleEnd = _manager.SettleTime;
            if (t < punchEnd)
                scale = Mathf.LerpUnclamped(_manager.StartScale, _manager.PunchScale, EaseOutBack(t / punchEnd));
            else if (t < settleEnd)
                scale = Mathf.Lerp(_manager.PunchScale, 1f, Mathf.SmoothStep(0f, 1f, (t - punchEnd) / (settleEnd - punchEnd)));
            else
                scale = 1f;
            transform.localScale = Vector3.one * (scale * _scaleMul);

            // --- Position: smooth ease-out rise + tiny lateral drift ---
            float rise = 1f - (1f - t) * (1f - t);
            transform.position = _origin
                + Vector3.up * (_manager.RiseDistance * rise)
                + cameraRight * (_driftX * t);
            transform.rotation = cameraRotation; // always face the camera

            // --- Fade text (face + outline + shadow) AND plate over the tail ---
            float fadeStart = _manager.FadeStart;
            float alpha = t <= fadeStart ? 1f : 1f - (t - fadeStart) / Mathf.Max(0.01f, 1f - fadeStart);
            var face = _faceColor; face.a *= alpha;
            _text.color = face;
            var outline = _outlineBase; outline.a *= alpha;
            _textMaterial.SetColor(OutlineColorId, outline);
            if (_textMaterial.HasProperty(UnderlayColorId))
            {
                var underlay = _underlayBase; underlay.a *= alpha;
                _textMaterial.SetColor(UnderlayColorId, underlay);
            }
            var bg = _backgroundBase; bg.a *= alpha;
            _background.color = bg;

            return true;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}

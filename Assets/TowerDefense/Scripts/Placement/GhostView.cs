using System;
using UnityEngine;
using RoyalSiege.Buildings;
using RoyalSiege.Data;

namespace RoyalSiege.Placement
{
    /// <summary>
    /// Green/red placement preview with an exact range ring. 18-Jul card-interaction polish
    /// (config-driven, unscaled time, zero alloc per frame):
    ///  · scale/alpha IN animation when the preview first appears over the battlefield,
    ///  · gentle valid pulse (0.98↔1.02), one small shake when first turning invalid,
    ///  · card name floating above the ghost (billboarded TextMesh, built once),
    ///  · commit confirm — freeze, 1→1.06→1 pulse, fade out — then Hide,
    ///  · master fade so the proxy card and the preview can crossfade at the HUD boundary.
    /// </summary>
    public sealed class GhostView : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer[] _tintTargets;
        [SerializeField] private RangeRing _rangeRing;
        [SerializeField] private Color _validColor = new(0.2f, 1f, 0.3f, 0.6f);
        [SerializeField] private Color _invalidColor = new(1f, 0.25f, 0.2f, 0.6f);
        [SerializeField] private CardInteractionAnimationConfig _config;

        private MaterialPropertyBlock _block;
        private Vector3 _baseScale = Vector3.one;
        private bool _baseScaleCached;

        private bool _isValid = true;
        private float _showT = 1f;        // in-animation progress
        private float _fade = 1f;         // master visibility (HUD boundary crossfade)
        private float _fadeTarget = 1f;
        private float _shakeT = 1f;       // invalid-enter shake
        private float _pulseTime;
        private Vector3 _position;

        // commit confirm sequence
        private float _commitT = -1f;
        private Action _onCommitDone;

        private TextMesh _label;
        private Transform _labelRoot;
        private Camera _camera;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            CacheBaseScale();
        }

        private void CacheBaseScale()
        {
            if (_baseScaleCached) return;
            _baseScale = transform.localScale;
            _baseScaleCached = true;
        }

        public void Show(float rangeRadius) => Show(rangeRadius, null);

        public void Show(float rangeRadius, string cardName)
        {
            CacheBaseScale();
            gameObject.SetActive(true);
            _rangeRing?.SetRadius(rangeRadius);
            _showT = 0f;
            _fade = 0f;
            _fadeTarget = 0f;   // stays invisible until the pointer crosses into the field
            _shakeT = 1f;
            _commitT = -1f;
            _pulseTime = 0f;
            EnsureLabel();
            _label.text = cardName ?? "";
            _labelRoot.gameObject.SetActive(!string.IsNullOrEmpty(cardName));
            ApplyVisuals();
        }

        public void Hide()
        {
            _commitT = -1f;
            _onCommitDone = null;
            gameObject.SetActive(false);
        }

        /// <summary>The drawn ring never leaves the playable circle (QA 17-Jul DT-001).</summary>
        public void SetMapClip(Vector3 mapCenter, float mapRadius) =>
            _rangeRing?.SetMapClip(mapCenter, mapRadius);

        public void SetPosition(Vector3 position)
        {
            _position = position;
            transform.position = position;
            _rangeRing?.Rebuild(); // re-clip against the map circle at the new spot
        }

        public void SetValid(bool valid)
        {
            if (_isValid && !valid && _fade > 0.5f)
                _shakeT = 0f; // one small shake when ENTERING invalid — not every frame
            _isValid = valid;
        }

        /// <summary>Preview visibility follows the HUD/battlefield boundary (proxy crossfade).</summary>
        public void SetFieldVisible(bool visible) => _fadeTarget = visible ? 1f : 0f;

        /// <summary>Valid release: freeze → confirm pulse → fade → Hide(). Safe to call once.</summary>
        public void PlayCommitConfirm(Action onDone = null)
        {
            if (_commitT >= 0f) return;
            _commitT = 0f;
            _onCommitDone = onDone;
        }

        private void Update()
        {
            if (_config == null) return;
            float dt = Time.unscaledDeltaTime;

            if (_commitT >= 0f)
            {
                _commitT += dt;
                float hold = _config.commitHoldDuration;
                float confirm = _config.commitConfirmDuration;
                float fadeOut = _config.previewFadeOutDuration;
                if (_commitT <= hold)
                {
                    // freeze at final pose
                }
                else if (_commitT <= hold + confirm)
                {
                    float t = (_commitT - hold) / confirm;
                    float pulse = 1f + (_config.commitConfirmScale - 1f) * Mathf.Sin(t * Mathf.PI);
                    transform.localScale = _baseScale * pulse;
                }
                else if (_commitT <= hold + confirm + fadeOut)
                {
                    float t = (_commitT - hold - confirm) / fadeOut;
                    _fade = 1f - t;
                    transform.localScale = _baseScale;
                    ApplyVisuals();
                }
                else
                {
                    var done = _onCommitDone;
                    Hide();
                    transform.localScale = _baseScale;
                    done?.Invoke();
                }
                return;
            }

            // in-animation + boundary fade
            if (_showT < 1f) _showT = Mathf.Min(1f, _showT + dt / Mathf.Max(0.01f, _config.previewInDuration));
            _fade = Mathf.MoveTowards(_fade, _fadeTarget,
                dt / Mathf.Max(0.01f, _config.previewInDuration));

            // valid pulse (paused while invalid so red reads steady)
            float pulseScale = 1f;
            if (_isValid && _fade > 0.5f)
            {
                _pulseTime += dt;
                float s = Mathf.Sin(_pulseTime / Mathf.Max(0.05f, _config.validPulsePeriod) * Mathf.PI * 2f) * 0.5f + 0.5f;
                pulseScale = Mathf.Lerp(_config.validPulseScaleMin, _config.validPulseScaleMax, s);
            }

            // invalid-enter shake (world-space X wobble, decaying)
            Vector3 shakeOffset = Vector3.zero;
            if (_shakeT < 1f)
            {
                _shakeT = Mathf.Min(1f, _shakeT + dt / Mathf.Max(0.01f, _config.invalidEnterShakeDuration));
                shakeOffset = Vector3.right *
                    (Mathf.Sin(_shakeT * 26f) * _config.invalidEnterShakePixels * (1f - _shakeT));
            }

            float inScale = Mathf.LerpUnclamped(_config.previewStartScale, 1f, EaseOutBack(_showT));
            transform.localScale = _baseScale * (inScale * pulseScale);
            transform.position = _position + shakeOffset;

            ApplyVisuals();
        }

        /// <summary>Tint + alpha in one MPB pass; label billboards to the camera.</summary>
        private void ApplyVisuals()
        {
            var color = _isValid ? _validColor : _invalidColor;
            color.a *= _fade * (_showT < 1f ? Mathf.Clamp01(_showT * 2f) : 1f);
            for (int i = 0; i < _tintTargets.Length; i++)
            {
                _tintTargets[i].GetPropertyBlock(_block);
                _block.SetColor(ColorId, color);
                _block.SetColor(BaseColorId, color);
                _tintTargets[i].SetPropertyBlock(_block);
            }

            if (_labelRoot != null && _labelRoot.gameObject.activeSelf)
            {
                if (_camera == null) _camera = Camera.main;
                if (_camera != null) _labelRoot.rotation = _camera.transform.rotation;
                var c = _label.color; c.a = _fade; _label.color = c;
            }
        }

        /// <summary>Card name above the preview — one billboarded TextMesh, built lazily.</summary>
        private void EnsureLabel()
        {
            if (_label != null) return;
            var go = new GameObject("GhostLabel", typeof(TextMesh));
            _labelRoot = go.transform;
            _labelRoot.SetParent(transform, false);
            _labelRoot.localPosition = Vector3.up * 2.2f;
            _label = go.GetComponent<TextMesh>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 42;
            _label.fontStyle = FontStyle.Bold;
            _label.characterSize = 0.1f;
            _label.anchor = TextAnchor.LowerCenter;
            _label.alignment = TextAlignment.Center;
            _label.color = Color.white;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = _label.font.material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}

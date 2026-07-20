using UnityEngine;

namespace RoyalSiege.Juice
{
    /// <summary>
    /// Trauma-based camera shake (Perlin offset + a touch of roll). The shake is applied in
    /// LateUpdate and removed again first thing next frame, so the base camera pose is never
    /// polluted and it composes with any other camera motion. Trauma is squared — small hits
    /// barely register, big blasts thump — and hard-capped for comfort. Zero cost when idle.
    /// </summary>
    public sealed class CameraShaker : MonoBehaviour
    {
        [SerializeField] private float _maxOffset = 0.28f;
        [SerializeField] private float _maxRollDegrees = 1.5f;
        [SerializeField] private float _frequency = 17f;
        [SerializeField] private float _decayPerSecond = 1.4f;

        private float _trauma;
        private Vector3 _appliedOffset;
        private float _appliedRoll;

        /// <summary>
        /// 18-Jul: scales the LEFT-RIGHT component (X offset + roll) of the shake, 0..1.
        /// The tower level-up/fail cinematics set this low (~0.25) so the quake reads as a
        /// vertical ground rumble with only a little sideways randomness, then restore to 1.
        /// </summary>
        public float HorizontalDamp { get; set; } = 1f;

        private static CameraShaker _main;
        /// <summary>The main camera's shaker (lazy, survives scene reloads). Null-safe to call.</summary>
        public static CameraShaker Main
        {
            get
            {
                if (_main == null && Camera.main != null) _main = Camera.main.GetComponent<CameraShaker>();
                return _main;
            }
        }

        private void OnDestroy() { if (_main == this) _main = null; }

        /// <summary>0.1 ≈ tiny tick, 0.35 ≈ solid thud, 0.6 ≈ big blast. Clamped to 1.</summary>
        public void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        private void LateUpdate()
        {
            // Undo last frame's shake first — the base pose stays authoritative.
            if (_appliedRoll != 0f) transform.localRotation *= Quaternion.Euler(0f, 0f, -_appliedRoll);
            if (_appliedOffset != Vector3.zero) transform.localPosition -= _appliedOffset;
            _appliedOffset = Vector3.zero;
            _appliedRoll = 0f;

            if (_trauma <= 0f) return;
            _trauma = Mathf.Max(0f, _trauma - _decayPerSecond * Time.deltaTime);

            float strength = _trauma * _trauma;
            float t = Time.time * _frequency;
            float hDamp = Mathf.Clamp01(HorizontalDamp);
            var offset = new Vector3(
                (Mathf.PerlinNoise(t, 0.3f) * 2f - 1f) * hDamp,
                Mathf.PerlinNoise(0.7f, t) * 2f - 1f,
                0f) * (_maxOffset * strength);
            float roll = (Mathf.PerlinNoise(t, 9.1f) * 2f - 1f) * (_maxRollDegrees * strength * hDamp);

            transform.localPosition += offset;
            transform.localRotation *= Quaternion.Euler(0f, 0f, roll);
            _appliedOffset = offset;
            _appliedRoll = roll;
        }
    }
}

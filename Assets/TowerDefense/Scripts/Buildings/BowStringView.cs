using UnityEngine;

namespace RoyalSiege.Buildings
{
    /// <summary>
    /// Draws the X-Bow's string as a 3-point LineRenderer between the two limb tips, with
    /// the middle point pulled back while an arrow is nocked and snapping forward on release.
    /// Fully self-contained: reads the nocked arrow's active state, no turret wiring needed.
    /// This GameObject's -Z must point along the draw direction (its +Z = firing direction).
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class BowStringView : MonoBehaviour
    {
        [SerializeField] private Transform _limbTipA;
        [SerializeField] private Transform _limbTipB;
        [Tooltip("The nocked-arrow model — string is drawn while this is active.")]
        [SerializeField] private GameObject _loadedAmmo;
        [SerializeField] private float _restBulge = 0.03f;
        [SerializeField] private float _drawnPull = 0.24f;
        [SerializeField] private float _snapSharpness = 22f;

        private LineRenderer _line;
        private float _pull;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.positionCount = 3;
            _line.useWorldSpace = true;
            _pull = _restBulge;
        }

        private void LateUpdate()
        {
            if (_limbTipA == null || _limbTipB == null) return;

            bool drawn = _loadedAmmo != null && _loadedAmmo.activeInHierarchy;
            float target = drawn ? _drawnPull : _restBulge;
            // Draw slowly, snap forward fast — the asymmetry sells the shot.
            float sharpness = target > _pull ? _snapSharpness * 0.35f : _snapSharpness;
            _pull = Mathf.Lerp(_pull, target, 1f - Mathf.Exp(-sharpness * Time.deltaTime));

            Vector3 a = _limbTipA.position;
            Vector3 b = _limbTipB.position;
            Vector3 mid = (a + b) * 0.5f - transform.forward * _pull;
            _line.SetPosition(0, a);
            _line.SetPosition(1, mid);
            _line.SetPosition(2, b);
        }
    }
}

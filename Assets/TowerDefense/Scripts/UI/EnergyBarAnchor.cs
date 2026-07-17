using UnityEngine;

namespace RoyalSiege.UI
{
    /// <summary>
    /// Pins this world transform to the point that renders exactly under a UI element
    /// (the energy bar), at a fixed camera depth — energy orbs fly to this transform.
    /// QA 17-Jul: the anchor was a hardcoded world point that only lined up with the bar
    /// on one framing; orbs converged onto the card hand and read as a stray enemy
    /// projectile. Tracking the bar's actual screen position works on every aspect ratio.
    /// </summary>
    public sealed class EnergyBarAnchor : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [Tooltip("The energy bar element the orbs should visually fly into.")]
        [SerializeField] private RectTransform _uiTarget;
        [Tooltip("Distance from the camera the anchor sits at (controls the orb's on-screen size near arrival).")]
        [SerializeField] private float _cameraDepth = 21f;

        private void LateUpdate()
        {
            if (_camera == null || _uiTarget == null) return;
            // Overlay canvas → camera-less screen point of the bar's center.
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, _uiTarget.position);
            transform.position = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, _cameraDepth));
        }
    }
}

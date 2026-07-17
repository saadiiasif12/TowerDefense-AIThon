using UnityEngine;

namespace RoyalSiege.Core
{
    /// <summary>
    /// Keeps the framed WORLD WIDTH constant across aspect ratios: the vertical FOV is
    /// recomputed from a reference portrait tuning, so on a wider or narrower phone the
    /// arena never crops horizontally — the view gains/loses vertical headroom instead.
    /// Attach to the gameplay camera; captures its authored FOV as the reference.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraAspectFitter : MonoBehaviour
    {
        [Tooltip("Aspect (w/h) the scene camera was tuned at — default 1080×1920 portrait.")]
        [SerializeField] private float _referenceAspect = 1080f / 1920f;
        [Tooltip("Vertical FOV at the reference aspect. 0 = capture the camera's authored FOV on Awake.")]
        [SerializeField] private float _referenceFov;

        private Camera _camera;
        private float _lastAspect;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_referenceFov <= 0f) _referenceFov = _camera.fieldOfView;
            Apply();
        }

        private void Update()
        {
            if (!Mathf.Approximately(_camera.aspect, _lastAspect)) Apply();
        }

        private void Apply()
        {
            _lastAspect = _camera.aspect;
            // Hold the reference horizontal FOV constant; solve vertical FOV for this aspect.
            float hFov = 2f * Mathf.Atan(Mathf.Tan(_referenceFov * Mathf.Deg2Rad * 0.5f) * _referenceAspect);
            float vFov = 2f * Mathf.Atan(Mathf.Tan(hFov * 0.5f) / _camera.aspect);
            _camera.fieldOfView = vFov * Mathf.Rad2Deg;
        }
    }
}

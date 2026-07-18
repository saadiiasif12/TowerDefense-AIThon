using UnityEngine;

namespace RoyalSiege.Juice
{
    /// <summary>
    /// View-only hit feedback: on Play() the unit kicks backward a little, settles back,
    /// and glows once (overbright tint pulse on all renderers via MaterialPropertyBlock).
    /// The recoil is applied in LateUpdate ON TOP of whatever the owner wrote this frame —
    /// owners MUST rewrite transform.position every frame while alive (EnemyAgent and
    /// TestDummyTarget both do), so the offset never accumulates into gameplay position.
    /// </summary>
    public sealed class HitReaction : MonoBehaviour
    {
        private const float DurationSeconds = 0.22f;
        private const float RecoilDistance = 0.16f;
        private static readonly Color GlowColor = new(1.2f, 1.3f, 1.55f); // cold star-blue; kept under the bloom threshold so flashes never blow out
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private float _remaining;
        private EnemyLifecycleView _tintSink; // when present, owns the renderers — glow composes with freeze tint there

        private void Awake()
        {
            _tintSink = GetComponent<EnemyLifecycleView>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
        }

        public void Play() => _remaining = DurationSeconds;

        /// <summary>Stop immediately and restore materials (owner died / returned to pool).</summary>
        public void Cancel()
        {
            _remaining = 0f;
            if (_tintSink != null) _tintSink.SetHitPulse(0f);
            else if (_renderers != null) ClearTint();
        }

        private void LateUpdate()
        {
            if (_remaining <= 0f) return;
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
            {
                if (_tintSink != null) _tintSink.SetHitPulse(0f);
                else ClearTint();
                return;
            }

            // 0 → 1 → 0 pulse: kick backward, then spring back to rest.
            float t = 1f - _remaining / DurationSeconds;
            float pulse = Mathf.Sin(t * Mathf.PI);
            transform.position -= transform.forward * (RecoilDistance * pulse);

            if (_tintSink != null)
            {
                _tintSink.SetHitPulse(pulse);
                return;
            }
            var tint = Color.Lerp(Color.white, GlowColor, pulse);
            _block.SetColor(ColorId, tint);
            _block.SetColor(BaseColorId, tint);
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].SetPropertyBlock(_block);
        }

        private void ClearTint()
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].SetPropertyBlock(null);
        }
    }
}

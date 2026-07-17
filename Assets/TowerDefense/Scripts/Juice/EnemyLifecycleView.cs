using UnityEngine;

namespace RoyalSiege.Juice
{
    /// <summary>
    /// View-only enemy lifecycle polish, auto-added to every enemy by EnemyAgent:
    ///  - spawn: ease-out-back scale pop from zero (pooled respawn safe).
    ///  - freeze: icy tint while the unit is frozen.
    ///  - hit glow: HitReaction routes its pulse here so freeze + hit tints COMPOSE on one
    ///    MaterialPropertyBlock instead of fighting over the renderers.
    ///  - death: once the death anim has read, swaps materials to RoyalSiege/Dissolve and
    ///    burns the model away right before the pooled despawn.
    /// Renderers are cached in Awake — before WorldHealthBar builds its quads in Start —
    /// so the health bar is never tinted or dissolved.
    /// </summary>
    public sealed class EnemyLifecycleView : MonoBehaviour
    {
        private const float SpawnPopSeconds = 0.32f;
        private const float DissolveSeconds = 0.7f;
        private static readonly Color FreezeTint = new(0.55f, 0.78f, 1.25f);
        private static readonly Color GlowColor = new(1.45f, 1.6f, 2f);
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

        private static Shader _dissolveShader;

        private Renderer[] _renderers;
        private Material[][] _originalMaterials;
        private Material[] _dissolveMaterials; // one per renderer, lazily built from its main texture
        private MaterialPropertyBlock _block;
        private Vector3 _baseScale;

        private float _spawnT = 1f;
        private float _hitPulse;
        private bool _frozen;
        private bool _tintApplied;
        private float _dissolveDelay = -1f;
        private float _dissolveT = -1f;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _originalMaterials = new Material[_renderers.Length][];
            for (int i = 0; i < _renderers.Length; i++)
                _originalMaterials[i] = _renderers[i].sharedMaterials;
            _block = new MaterialPropertyBlock();
            _baseScale = transform.localScale;
            if (_dissolveShader == null) _dissolveShader = Shader.Find("RoyalSiege/Dissolve");
        }

        /// <summary>Pooled (re)spawn: restore materials, clear states, pop the model in.</summary>
        public void ResetForSpawn()
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].sharedMaterials = _originalMaterials[i];
            _dissolveDelay = -1f;
            _dissolveT = -1f;
            _hitPulse = 0f;
            _frozen = false;
            ClearTint();
            _spawnT = 0f;
            transform.localScale = _baseScale * 0.01f;
        }

        /// <summary>Death started: wait for the death anim to read, then dissolve out.</summary>
        public void BeginDeath(float despawnSeconds)
        {
            _frozen = false;
            _hitPulse = 0f;
            ClearTint();
            _dissolveDelay = Mathf.Max(0f, despawnSeconds - DissolveSeconds - 0.05f);
        }

        public void SetFrozen(bool frozen) => _frozen = frozen;

        /// <summary>Driven by HitReaction each frame (0→1→0 pulse).</summary>
        public void SetHitPulse(float pulse) => _hitPulse = pulse;

        private void Update()
        {
            if (_spawnT < 1f)
            {
                _spawnT = Mathf.Min(1f, _spawnT + Time.deltaTime / SpawnPopSeconds);
                transform.localScale = _baseScale * Mathf.Max(0.01f, EaseOutBack(_spawnT));
            }

            if (_dissolveDelay >= 0f)
            {
                _dissolveDelay -= Time.deltaTime;
                if (_dissolveDelay < 0f)
                {
                    SwapToDissolveMaterials();
                    _dissolveT = 0f;
                }
            }
            else if (_dissolveT >= 0f && _dissolveT < 1f)
            {
                _dissolveT = Mathf.Min(1f, _dissolveT + Time.deltaTime / DissolveSeconds);
                for (int i = 0; i < _dissolveMaterials.Length; i++)
                    if (_dissolveMaterials[i] != null) _dissolveMaterials[i].SetFloat(DissolveId, _dissolveT);
            }
        }

        private void LateUpdate()
        {
            if (_dissolveT >= 0f) return; // dying: tint channel is done

            bool wantsTint = _frozen || _hitPulse > 0f;
            if (!wantsTint)
            {
                if (_tintApplied) ClearTint();
                return;
            }

            Color tint = _frozen ? FreezeTint : Color.white;
            if (_hitPulse > 0f) tint = Color.Lerp(tint, GlowColor, _hitPulse);
            _block.SetColor(ColorId, tint);
            _block.SetColor(BaseColorId, tint);
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].SetPropertyBlock(_block);
            _tintApplied = true;
        }

        private void SwapToDissolveMaterials()
        {
            if (_dissolveShader == null) { _dissolveT = 2f; return; } // shader missing: skip gracefully

            if (_dissolveMaterials == null)
            {
                _dissolveMaterials = new Material[_renderers.Length];
                for (int i = 0; i < _renderers.Length; i++)
                {
                    var source = _renderers[i] != null ? _renderers[i].sharedMaterial : null;
                    if (source == null) continue;
                    var mat = new Material(_dissolveShader);
                    var tex = source.HasProperty(BaseMapId) ? source.GetTexture(BaseMapId)
                            : source.HasProperty(MainTexId) ? source.GetTexture(MainTexId) : null;
                    if (tex != null) mat.SetTexture(BaseMapId, tex);
                    if (source.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, source.GetColor(BaseColorId));
                    _dissolveMaterials[i] = mat;
                }
            }

            ClearTint();
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null || _dissolveMaterials[i] == null) continue;
                var mats = _renderers[i].sharedMaterials;
                for (int m = 0; m < mats.Length; m++) mats[m] = _dissolveMaterials[i];
                _renderers[i].sharedMaterials = mats;
                _dissolveMaterials[i].SetFloat(DissolveId, 0f);
            }
        }

        private void ClearTint()
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].SetPropertyBlock(null);
            _tintApplied = false;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private void OnDestroy()
        {
            if (_dissolveMaterials == null) return;
            foreach (var mat in _dissolveMaterials)
                if (mat != null) Destroy(mat);
        }
    }
}

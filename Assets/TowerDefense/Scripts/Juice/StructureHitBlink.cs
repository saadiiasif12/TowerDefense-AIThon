using System.Collections.Generic;
using UnityEngine;

namespace RoyalSiege.Juice
{
    /// <summary>
    /// 18-Jul ruling: structures (Royal Tower, buildings) show NO damage text — they BLINK
    /// once per landed hit instead. One bright flash that decays fast (~0.15 s), applied via
    /// MaterialPropertyBlock across the child mesh renderers (shared materials untouched;
    /// the RS_Outline extra slot has no _BaseColor so outlines stay black). A tiny minimum
    /// interval keeps massed fire from strobing. Zero allocation after the first hit;
    /// the property block is CLEARED at the end so materials return to their exact look.
    /// Added lazily by RoyalTower/BuildingUnit — works in every scene, prefab edits optional.
    /// </summary>
    public sealed class StructureHitBlink : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Peak flash tint (slightly over-bright white reads as a hit on any albedo).")]
        [SerializeField] private Color _flashColor = new(1.9f, 1.75f, 1.6f);
        [SerializeField] private float _blinkSeconds = 0.15f;
        [Tooltip("Hits landing faster than this share one blink (anti-strobe under massed fire).")]
        [SerializeField] private float _minInterval = 0.1f;

        private readonly List<Renderer> _renderers = new();
        private readonly List<Color> _baseColors = new(); // each material's OWN authored tint
        private MaterialPropertyBlock _block;
        private float _blinkT = 1f;      // >=1 = idle
        private float _lastBlinkTime = -10f;
        private bool _cleared = true;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                _renderers.Add(r);
                // decay target = the material's own base color, so tinted art returns exactly
                var m = r.sharedMaterial;
                _baseColors.Add(m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId)
                    : m != null && m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white);
            }
        }

        /// <summary>One juicy blink (rate-limited). Call on every landed hit.</summary>
        public void Play()
        {
            if (Time.unscaledTime - _lastBlinkTime < _minInterval) return;
            _lastBlinkTime = Time.unscaledTime;
            _blinkT = 0f;
            _cleared = false;
        }

        private void Update()
        {
            if (_blinkT >= 1f)
            {
                if (_cleared) return;
                // restore the exact material look by clearing the property block once
                _block.Clear();
                for (int i = 0; i < _renderers.Count; i++)
                    if (_renderers[i] != null) _renderers[i].SetPropertyBlock(_block);
                _cleared = true;
                return;
            }

            _blinkT = Mathf.Min(1f, _blinkT + Time.unscaledDeltaTime / Mathf.Max(0.02f, _blinkSeconds));
            // sharp attack, smooth decay — reads as a snap
            float w = 1f - _blinkT;
            w *= w;
            for (int i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] == null) continue;
                var tint = Color.Lerp(_baseColors[i], _flashColor, w); // decays to the art's own tint
                _block.Clear();
                _block.SetColor(ColorId, tint);
                _block.SetColor(BaseColorId, tint);
                _renderers[i].SetPropertyBlock(_block);
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace RoyalSiege.Juice
{
    /// <summary>
    /// 18-Jul stylized look: clear dark outline on every character/building. Implementation:
    /// the shared RS_Outline material (inverted hull) is APPENDED as an extra material slot on
    /// each renderer — Unity re-renders the last submesh with it, so a single-submesh model is
    /// drawn twice with ONE skinning pass. No copy renderers, no post-processing: mobile-cheap.
    /// Idempotent (safe on pooled respawns) and shared-material based (no per-instance mats).
    /// </summary>
    public sealed class OutlineView : MonoBehaviour
    {
        private static Material _outlineMaterial;
        private static readonly List<Material> Buffer = new();

        private bool _applied;

        /// <summary>The one shared outline material (lazy; loaded from the shader).</summary>
        public static Material SharedMaterial
        {
            get
            {
                if (_outlineMaterial == null)
                {
                    var shader = Shader.Find("RoyalSiege/Outline");
                    if (shader != null) _outlineMaterial = new Material(shader) { name = "Mat_Outline (shared)" };
                }
                return _outlineMaterial;
            }
        }

        private void Awake() => Apply();

        /// <summary>Append the outline slot to every child renderer (skips particles/trails/lines).</summary>
        public void Apply()
        {
            if (_applied) return;
            var outline = SharedMaterial;
            if (outline == null) return;

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                    continue;

                renderer.GetSharedMaterials(Buffer);
                if (Buffer.Contains(outline)) continue; // pooled reuse — already outlined
                Buffer.Add(outline);
                renderer.sharedMaterials = Buffer.ToArray();
            }
            _applied = true;
        }
    }
}

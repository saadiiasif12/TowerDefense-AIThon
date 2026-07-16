using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Core;

namespace RoyalSiege.Juice
{
    /// <summary>
    /// Draws Tesla's instant hits as short-lived jagged lightning arcs (pooled LineRenderers,
    /// re-jittered every frame) + a spark burst at the victim. Listens to
    /// GameEvents.InstantShotFired — zero coupling from gameplay to this class.
    /// </summary>
    public sealed class ZapArcRenderer : MonoBehaviour
    {
        private const int PointCount = 10;
        private const float ArcLifetime = 0.14f;
        private const float JitterAmplitude = 0.3f;

        [SerializeField] private GameContext _context;
        [SerializeField] private Material _arcMaterial;
        [SerializeField] private ParticleSystem _impactSparks;
        [SerializeField] private Color _arcColor = new(0.55f, 0.9f, 1f);

        private sealed class Arc
        {
            public LineRenderer Line;
            public Vector3 From;
            public Vector3 To;
            public float Life;
        }

        private readonly List<Arc> _arcs = new();

        private void Start() => _context.Events.InstantShotFired += OnZap;

        private void OnDestroy()
        {
            if (_context != null && _context.Events != null) _context.Events.InstantShotFired -= OnZap;
        }

        private void OnZap(Vector3 from, Vector3 to)
        {
            Arc arc = null;
            for (int i = 0; i < _arcs.Count; i++)
                if (_arcs[i].Life <= 0f) { arc = _arcs[i]; break; }
            if (arc == null)
            {
                arc = new Arc { Line = CreateLine() };
                _arcs.Add(arc);
            }

            arc.From = from;
            arc.To = to;
            arc.Life = ArcLifetime;
            arc.Line.enabled = true;

            _context.Vfx.Spawn(_impactSparks, to, Quaternion.identity, 1f, _arcColor);
        }

        private void Update()
        {
            float dt = _context.Clock.ScaledDeltaTime;
            for (int i = 0; i < _arcs.Count; i++)
            {
                var arc = _arcs[i];
                if (arc.Life <= 0f) continue;

                arc.Life -= dt;
                if (arc.Life <= 0f)
                {
                    arc.Line.enabled = false;
                    continue;
                }

                float fade = arc.Life / ArcLifetime;
                arc.Line.widthMultiplier = 0.18f * fade;
                Rejitter(arc, i);
            }
        }

        private void Rejitter(Arc arc, int seed)
        {
            Vector3 dir = (arc.To - arc.From).normalized;
            Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;

            for (int p = 0; p < PointCount; p++)
            {
                float u = p / (float)(PointCount - 1);
                Vector3 basePoint = Vector3.Lerp(arc.From, arc.To, u);
                float envelope = Mathf.Sin(u * Mathf.PI); // pinned at both ends
                float n1 = Mathf.Sin(Time.time * 83f + p * 12.9898f + seed * 7f);
                float n2 = Mathf.Sin(Time.time * 61f + p * 78.233f + seed * 3f);
                basePoint += side * (n1 * JitterAmplitude * envelope) + Vector3.up * (n2 * JitterAmplitude * 0.5f * envelope);
                arc.Line.SetPosition(p, basePoint);
            }
        }

        private LineRenderer CreateLine()
        {
            var go = new GameObject("ZapArc");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = PointCount;
            lr.material = _arcMaterial;
            lr.startColor = lr.endColor = _arcColor;
            lr.numCapVertices = 2;
            lr.enabled = false;
            return lr;
        }
    }
}

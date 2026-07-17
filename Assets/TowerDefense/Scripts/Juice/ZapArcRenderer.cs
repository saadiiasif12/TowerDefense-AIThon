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
            public float MaxLife = ArcLifetime;
            public float Width = 1f;
        }

        private readonly List<Arc> _arcs = new();
        private GameEvents _events;
        private IClock _clock;
        private IVfxSpawner _vfx;

        private void Start()
        {
            // Normal path: wired to the game's context. Test scenes call Init() manually instead.
            if (_events == null && _context != null && _context.Events != null)
                Init(_context.Events, _context.Clock, _context.Vfx);
        }

        public void Init(GameEvents events, IClock clock, IVfxSpawner vfx)
        {
            _events = events;
            _clock = clock;
            _vfx = vfx;
            _events.InstantShotFired += OnZap;
        }

        private void OnDestroy()
        {
            if (_events != null) _events.InstantShotFired -= OnZap;
        }

        private void OnZap(Vector3 from, Vector3 to)
        {
            // Main bolt + two shorter forks branching off partway — reads as real thunder
            // instead of a single wobbling line. Fork geometry is hashed from the endpoints
            // (deterministic, no RNG).
            SpawnArc(from, to, ArcLifetime, 1f);

            Vector3 dir = to - from;
            Vector3 side = Vector3.Cross(dir.normalized, Vector3.up);
            float h = Mathf.Sin(to.x * 12.9898f + to.z * 78.233f);
            Vector3 branchRoot1 = Vector3.Lerp(from, to, 0.45f);
            Vector3 branchRoot2 = Vector3.Lerp(from, to, 0.7f);
            SpawnArc(branchRoot1, branchRoot1 + dir * 0.3f + side * (0.9f * h) + Vector3.down * 0.4f, ArcLifetime * 0.7f, 0.45f);
            SpawnArc(branchRoot2, branchRoot2 + dir * 0.22f - side * (0.7f * h) + Vector3.down * 0.6f, ArcLifetime * 0.55f, 0.35f);

            _vfx.Spawn(_impactSparks, to, Quaternion.identity, 1f, _arcColor);
        }

        private void SpawnArc(Vector3 from, Vector3 to, float life, float width)
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
            arc.Life = life;
            arc.MaxLife = life;
            arc.Width = width;
            arc.Line.enabled = true;
        }

        private void Update()
        {
            if (_clock == null) return;
            float dt = _clock.ScaledDeltaTime;
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

                float fade = arc.Life / arc.MaxLife;
                arc.Line.widthMultiplier = 0.18f * fade * arc.Width;
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

using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Core;

namespace RoyalSiege.Juice
{
    public enum TeslaVfxQuality { Low, Medium, High }

    /// <summary>
    /// Archero-style Tesla discharge. Each zap is a pooled 3-layer beam (white-hot core +
    /// saturated electric-blue body + soft cyan glow — three LineRenderers sharing ONE
    /// reused point array), with hashed branch offshoots, per-frame width/brightness
    /// flicker, a jag shape that re-rolls at a controlled frequency, a muzzle flash at the
    /// coil, and a per-ENEMY shared TeslaTargetGlow (contact flash, crawling arcs, sparks)
    /// that follows the victim and scales with how many Teslas are hitting it.
    /// Listens to GameEvents.InstantShotFired — zero coupling from gameplay to this class.
    /// Zero per-frame allocations after warm-up; everything pooled; quality tiers scale
    /// segments/branches/arcs/sparks/refresh-rate without touching gameplay.
    /// </summary>
    public sealed class ZapArcRenderer : MonoBehaviour
    {
        // ---- quality table (Low / Medium / High) ----
        private static readonly int[] QSegments = { 7, 10, 14 };
        private static readonly int[] QBranches = { 1, 2, 3 };
        private static readonly float[] QRefreshHz = { 20f, 30f, 45f };
        private static readonly int[] QCrawlArcs = { 1, 2, 3 };
        private static readonly float[] QSparkScale = { 0.6f, 0.8f, 1f };

        public const int MaxSegments = 14;
        private const int MaxBranches = 3;
        private const int BranchPointCount = 4;
        private const float BeamLifeSeconds = 0.32f;
        private const float JagAmplitude = 0.36f;
        private const float ContactHeight = 0.55f;

        [SerializeField] private GameContext _context;
        [SerializeField] private Material _arcMaterial;        // legacy fallback if the layer materials are missing
        [SerializeField] private ParticleSystem _impactSparks; // first-connection burst
        [SerializeField] private Color _arcColor = new(0.55f, 0.9f, 1f);

        [Header("Tesla discharge layers")]
        [SerializeField] private Material _coreMaterial;
        [SerializeField] private Material _bodyMaterial;
        [SerializeField] private Material _glowMaterial;
        [SerializeField] private ParticleSystem _muzzleFlash;
        [Tooltip("Low/Medium/High — scales segments, branches, target arcs, sparks and jag refresh rate. No gameplay impact.")]
        [SerializeField] private TeslaVfxQuality _quality = TeslaVfxQuality.High;

        public TeslaVfxQuality Quality { get => _quality; set => _quality = value; }

        private sealed class Beam
        {
            public GameObject Root;
            public LineRenderer Core;
            public LineRenderer Body;
            public LineRenderer Glow;
            public LineRenderer[] Branches;
            public Vector3[] Points;             // shared by all three layers, sized MaxSegments
            public Vector3 From;
            public IEnemyTarget Target;
            public Vector3 LastEnd;
            public Vector3 ContactOffset;
            public float Life;
            public float RefreshTimer;
            public int JagPhase;                 // bumps at each refresh → new deterministic shape
            public int Seed;
        }

        private readonly List<Beam> _beams = new();
        private readonly Dictionary<IEnemyTarget, TeslaTargetGlow> _glows = new();
        private readonly Stack<TeslaTargetGlow> _glowPool = new();
        private static readonly List<IEnemyTarget> FinishedGlowKeys = new();
        private static readonly Vector3[] BranchScratch = new Vector3[BranchPointCount];

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

        // ---------------- zap entry ----------------

        private void OnZap(Vector3 from, IEnemyTarget target)
        {
            if (target == null) return;
            int q = (int)_quality;

            Beam beam = null;
            for (int i = 0; i < _beams.Count; i++)
                if (_beams[i].Life <= 0f) { beam = _beams[i]; break; }
            beam ??= CreateBeam();

            // Stable per-Tesla contact point: hash the muzzle position into an angle around
            // the victim's body, so three Teslas on one enemy stay individually readable.
            float angle = Mathf.Abs(Mathf.Sin(from.x * 12.9898f + from.z * 78.233f)) * Mathf.PI * 2f;
            float radius = Mathf.Max(0.25f, target.BodyRadius * 0.8f);
            beam.ContactOffset = new Vector3(Mathf.Cos(angle) * radius, ContactHeight, Mathf.Sin(angle) * radius);

            beam.From = from;
            beam.Target = target;
            beam.LastEnd = target.Position + beam.ContactOffset;
            beam.Life = BeamLifeSeconds;
            beam.RefreshTimer = 0f;
            beam.Seed = Mathf.Abs((int)(from.x * 131f + from.z * 517f)) % 977;
            beam.Root.SetActive(true);
            for (int b = 0; b < beam.Branches.Length; b++)
                beam.Branches[b].enabled = b < QBranches[q];

            // Initialize shape + widths NOW — a pooled beam must be full-strength on its very
            // first (brightest) frame, not wear last life's faded-out widths until Update.
            RebuildBeam(beam, q);
            beam.Core.widthMultiplier = 0.055f;
            beam.Body.widthMultiplier = 0.17f;
            beam.Glow.widthMultiplier = 0.52f;
            for (int b = 0; b < beam.Branches.Length; b++)
                if (beam.Branches[b].enabled) beam.Branches[b].widthMultiplier = 0.07f;

            // Source flash at the coil, aimed at the victim.
            if (_muzzleFlash != null)
            {
                Vector3 dir = beam.LastEnd - from;
                _vfx.Spawn(_muzzleFlash, from, dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) : Quaternion.identity);
            }

            // ONE shared impact per enemy; extra connections only raise its intensity.
            if (!_glows.TryGetValue(target, out var glow))
            {
                glow = _glowPool.Count > 0 ? _glowPool.Pop() : TeslaTargetGlow.Create(transform, _bodyMaterial, _glowMaterial);
                glow.Attach(target, QSparkScale[q], QCrawlArcs[q]);
                _glows[target] = glow;
                // Stronger burst when the first beam connects.
                if (_impactSparks != null) _vfx.Spawn(_impactSparks, beam.LastEnd, Quaternion.identity, 1.4f, _arcColor);
            }
            glow.NotifyZap();
        }

        private Beam CreateBeam()
        {
            var beam = new Beam
            {
                Root = new GameObject("TeslaBeam"),
                Points = new Vector3[MaxSegments],
                Branches = new LineRenderer[MaxBranches]
            };
            beam.Root.transform.SetParent(transform, false);

            beam.Glow = MakeLine(beam.Root.transform, "Glow", _glowMaterial != null ? _glowMaterial : _arcMaterial, 4);
            beam.Body = MakeLine(beam.Root.transform, "Body", _bodyMaterial != null ? _bodyMaterial : _arcMaterial, 2);
            beam.Core = MakeLine(beam.Root.transform, "Core", _coreMaterial != null ? _coreMaterial : _arcMaterial, 2);
            for (int b = 0; b < MaxBranches; b++)
            {
                beam.Branches[b] = MakeLine(beam.Root.transform, "Branch" + b, _bodyMaterial != null ? _bodyMaterial : _arcMaterial, 1);
                beam.Branches[b].positionCount = BranchPointCount;
            }

            beam.Root.SetActive(false);
            _beams.Add(beam);
            return beam;
        }

        private static LineRenderer MakeLine(Transform parent, string name, Material material, int capVerts)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = material;
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCapVertices = capVerts;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.startColor = lr.endColor = Color.white; // color lives in the (HDR) materials
            return lr;
        }

        // ---------------- per-frame ----------------

        private void Update()
        {
            if (_clock == null) return;
            float dt = _clock.ScaledDeltaTime;
            float time = Time.time;
            int q = (int)_quality;

            for (int i = 0; i < _beams.Count; i++)
            {
                var beam = _beams[i];
                if (beam.Life <= 0f) continue;

                beam.Life -= dt;
                if (beam.Life <= 0f)
                {
                    beam.Root.SetActive(false);
                    beam.Target = null;
                    continue;
                }

                // Follow the victim while it lives; freeze at the last point if it died.
                if (beam.Target != null && beam.Target.IsAlive)
                    beam.LastEnd = beam.Target.Position + beam.ContactOffset;

                // Jag shape re-rolls at the quality-controlled rate; endpoints track every frame.
                beam.RefreshTimer -= dt;
                if (beam.RefreshTimer <= 0f)
                {
                    beam.RefreshTimer += 1f / QRefreshHz[q];
                    beam.JagPhase++;
                }
                RebuildBeam(beam, q);

                // Fast width/brightness flicker + fade-out over the beam's life.
                float fade = Mathf.Clamp01(beam.Life / BeamLifeSeconds * 2.2f); // holds bright, drops at the end
                float flick = 0.72f + 0.56f * Hash01(beam.Seed * 3 + (int)(time * 47f));
                beam.Core.widthMultiplier = 0.055f * flick * fade;
                beam.Body.widthMultiplier = 0.17f * flick * fade;
                beam.Glow.widthMultiplier = 0.52f * (0.8f + 0.35f * flick) * fade;
                for (int b = 0; b < beam.Branches.Length; b++)
                    if (beam.Branches[b].enabled)
                        beam.Branches[b].widthMultiplier = 0.07f * flick * fade;
            }

            TickGlows(dt);
        }

        private void TickGlows(float dt)
        {
            if (_glows.Count == 0) return;

            // Connection count per glow = beams currently attached to that enemy.
            foreach (var pair in _glows) pair.Value.FrameConnections = 0;
            for (int i = 0; i < _beams.Count; i++)
            {
                var beam = _beams[i];
                if (beam.Life > 0f && beam.Target != null && _glows.TryGetValue(beam.Target, out var g))
                    g.FrameConnections++;
            }

            FinishedGlowKeys.Clear();
            foreach (var pair in _glows)
            {
                pair.Value.TickFrame(dt);
                if (pair.Value.IsFinished) FinishedGlowKeys.Add(pair.Key);
            }
            for (int i = 0; i < FinishedGlowKeys.Count; i++)
            {
                var glow = _glows[FinishedGlowKeys[i]];
                _glows.Remove(FinishedGlowKeys[i]);
                glow.Detach();
                _glowPool.Push(glow);
            }
        }

        private void RebuildBeam(Beam beam, int q)
        {
            int segments = QSegments[q];
            Vector3 dir = beam.LastEnd - beam.From;
            float length = dir.magnitude;
            if (length < 0.05f) return;
            dir /= length;
            Vector3 perp1 = Vector3.Cross(dir, Vector3.up);
            if (perp1.sqrMagnitude < 0.01f) perp1 = Vector3.right; else perp1.Normalize();
            Vector3 perp2 = Vector3.Cross(dir, perp1);

            int kinkIndex = 1 + (beam.Seed + beam.JagPhase) % (segments - 2); // one extra-sharp elbow
            for (int p = 0; p < segments; p++)
            {
                float u = p / (float)(segments - 1);
                Vector3 point = Vector3.LerpUnclamped(beam.From, beam.LastEnd, u);
                if (p > 0 && p < segments - 1)
                {
                    float envelope = Mathf.Sin(u * Mathf.PI);
                    float amp = JagAmplitude * (p == kinkIndex ? 1.7f : 1f) * envelope;
                    float n1 = Hash01(beam.Seed + p * 7 + beam.JagPhase * 131) * 2f - 1f;
                    float n2 = Hash01(beam.Seed + p * 13 + beam.JagPhase * 71) * 2f - 1f;
                    // two octaves: big kinks + fine sizzle
                    float f1 = Hash01(beam.Seed + p * 29 + beam.JagPhase * 17) * 2f - 1f;
                    point += perp1 * (n1 * amp + f1 * amp * 0.35f) + perp2 * (n2 * amp * 0.6f);
                }
                beam.Points[p] = point;
            }

            beam.Core.positionCount = segments;
            beam.Body.positionCount = segments;
            beam.Glow.positionCount = segments;
            beam.Core.SetPositions(beam.Points);
            beam.Body.SetPositions(beam.Points);
            beam.Glow.SetPositions(beam.Points);

            // Short unstable offshoots rooted partway down the bolt.
            for (int b = 0; b < beam.Branches.Length; b++)
            {
                var branch = beam.Branches[b];
                if (!branch.enabled) continue;

                int rootIndex = 1 + (beam.Seed + b * 3 + beam.JagPhase / 2) % (segments - 3);
                Vector3 root = beam.Points[rootIndex];
                float side = (Hash01(beam.Seed + b * 97 + beam.JagPhase * 5) > 0.5f) ? 1f : -1f;
                Vector3 branchDir = (perp1 * side + dir * 0.35f + Vector3.down * 0.45f).normalized;
                float branchLength = 0.45f + 0.45f * Hash01(beam.Seed + b * 41 + beam.JagPhase * 11);

                for (int p = 0; p < BranchPointCount; p++)
                {
                    float u = p / (float)(BranchPointCount - 1);
                    Vector3 point = root + branchDir * (branchLength * u);
                    if (p > 0 && p < BranchPointCount - 1)
                    {
                        float n = Hash01(beam.Seed + b * 53 + p * 19 + beam.JagPhase * 13) * 2f - 1f;
                        point += perp2 * (n * 0.14f) + perp1 * (n * 0.1f * side);
                    }
                    BranchScratch[p] = point;
                }
                branch.SetPositions(BranchScratch);
            }
        }

        private static float Hash01(int n)
        {
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }
    }
}

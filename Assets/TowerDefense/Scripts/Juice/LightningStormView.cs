using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using RoyalSiege.Core;
using RoyalSiege.Data;

namespace RoyalSiege.Juice
{
    /// <summary>
    /// The Lightning spell as a MIGHTY storm (user brief 17-Jul): on cast, night falls for a
    /// beat — the sun/ambient dim to a cold blue and a multiply vignette (the provided
    /// highlight-white sprite) closes in on the arena; at resolve, Tesla-style jagged
    /// 3-layer bolts (same core/body/glow materials as the Tesla discharge, ~1.7× wider)
    /// crash DOWN FROM THE SKY onto each actual victim with a bigger Tesla-like impact
    /// (contact flash, outward sparks, crawling ground arcs) plus rising smoke, a white
    /// screen flash and a heavier camera shake; then dawn fades back in.
    /// Purely view-layer: listens to SpellCast/SpellResolved, zero gameplay coupling,
    /// zero RNG (hash-driven like ZapArcRenderer), pooled after warm-up.
    /// </summary>
    public sealed class LightningStormView : MonoBehaviour
    {
        private const int Segments = 14;
        private const int BranchCount = 2;
        private const int BranchPoints = 4;
        private const float BoltLife = 0.45f;
        private const float JagAmplitude = 0.55f;
        private const float RefreshHz = 30f;
        private const float NightInSeconds = 0.14f;
        private const float NightOutSeconds = 0.55f;
        private const float StrikeHoldSeconds = 0.18f;
        private const float CastFailsafeSeconds = 1.5f;
        private const int RingArcs = 2;
        private const int RingPoints = 8;
        private const float RingLife = 0.5f;

        [SerializeField] private GameContext _context;

        [Header("Bolt layers (same materials as the Tesla discharge)")]
        [SerializeField] private Material _coreMaterial;
        [SerializeField] private Material _bodyMaterial;
        [SerializeField] private Material _glowMaterial;

        [Header("Storm splash (existing HUD element)")]
        [Tooltip("Full-screen Image inside the HUD canvas with a PRESET color — the storm only " +
                 "lerps its alpha 0 → 57/255 → 0. Scene lighting is never touched (user ruling 17-Jul).")]
        [SerializeField] private Image _thunderSplash;

        private const float SplashPeakAlpha = 57f / 255f;

        [Header("Impact")]
        [SerializeField] private Material _smokeMaterial;
        [SerializeField] private float _shakeTrauma = 0.5f;
        [Tooltip("Small ground crack under each struck victim — reuses the quake's crack decal (18-Jul user ask).")]
        [SerializeField] private float _crackRadius = 0.8f;
        [SerializeField] private float _crackSeconds = 0.8f;
        private V4CardVfx _cardVfx;

        // ---- runtime ----
        private sealed class Bolt
        {
            public GameObject Root;
            public LineRenderer Core, Body, Glow;
            public LineRenderer[] Branches;
            public Vector3[] Points;
            public Vector3 From, To;
            public float Life, RefreshTimer;
            public int Seed, JagPhase;
        }

        private sealed class GroundRing
        {
            public GameObject Root;
            public LineRenderer[] Arcs;
            public Vector3 Center;
            public float Life, Timer;
            public int Phase, Seed;
        }

        private readonly List<Bolt> _bolts = new();
        private readonly List<GroundRing> _rings = new();
        private static readonly Vector3[] BranchScratch = new Vector3[BranchPoints];
        private static readonly Vector3[] RingScratch = new Vector3[RingPoints];

        private GameEvents _events;
        private IClock _clock;
        private CameraShaker _shaker;
        private Camera _camera;

        private ParticleSystem _flashPs, _sparksPs, _smokePs;
        private Image _flashImage;
        private Volume _stormVolume;
        private Color _splashColor = Color.white;

        private float _night01;
        private bool _nightRising;
        private float _holdRemaining = -1f;
        private float _failsafe;
        private float _screenFlash;

        private static Texture2D _softDot;

        private void Start()
        {
            if (_events == null && _context != null && _context.Events != null)
                Init(_context.Events, _context.Clock,
                    Camera.main != null ? Camera.main.GetComponent<CameraShaker>() : null);
        }

        public void Init(GameEvents events, IClock clock, CameraShaker shaker)
        {
            _events = events;
            _clock = clock;
            _shaker = shaker;
            _camera = Camera.main;
            _cardVfx = FindFirstObjectByType<V4CardVfx>(); // quake-crack stamps under strikes (null-safe)

            if (_thunderSplash != null)
            {
                _splashColor = _thunderSplash.color; // preset by design — we only drive alpha
                _thunderSplash.color = new Color(_splashColor.r, _splashColor.g, _splashColor.b, 0f);
                _thunderSplash.raycastTarget = false;
                _thunderSplash.gameObject.SetActive(false);
            }

            BuildOverlay();
            BuildImpactSystems();
            BuildStormVolume();

            _events.SpellCast += OnSpellCast;
            _events.SpellResolved += OnSpellResolved;
        }

        private void OnDestroy()
        {
            if (_events != null)
            {
                _events.SpellCast -= OnSpellCast;
                _events.SpellResolved -= OnSpellResolved;
            }
        }

        // ---------------- choreography ----------------

        private void OnSpellCast(SpellCardSO card, Vector3 point)
        {
            if (card.id != "Lightning") return;
            _nightRising = true;
            _holdRemaining = -1f;
            _failsafe = CastFailsafeSeconds;
        }

        private void OnSpellResolved(SpellCardSO card, Vector3 point, IReadOnlyList<Vector3> hits)
        {
            if (card.id != "Lightning") return;

            if (hits != null && hits.Count > 0)
                for (int i = 0; i < hits.Count; i++) Strike(hits[i]);
            else
                Strike(point); // whiffed cast still cracks

            _screenFlash = 0.55f;
            _holdRemaining = StrikeHoldSeconds;
            _shaker?.AddTrauma(_shakeTrauma);
        }

        private void Strike(Vector3 ground)
        {
            Vector3 contact = ground + Vector3.up * 0.4f;

            // Scorched ground: the quake's crack texture, stamped small under the victim.
            if (_cardVfx != null) _cardVfx.StampCrack(ground, _crackRadius, _crackSeconds);

            // Sky origin = the shared GameConfig.skyPoint (18-Jul ruling: ALL sky spells
            // enter from ONE fixed world point — supersedes the 17-Jul top-screen-edge
            // projection). Bolts to different victims still fan and jag individually.
            // Fallback (no GameContext, e.g. a bare harness scene): the old off-screen
            // viewport origin with hashed drift.
            var cfg = _context != null ? _context.GameConfig : null;
            Vector3 sky;
            if (cfg != null)
            {
                sky = cfg.skyPoint;
            }
            else
            {
                float drift = (Mathf.Abs(Mathf.Sin(ground.x * 12.9898f + ground.z * 78.233f)) - 0.5f) * 0.16f;
                if (_camera != null)
                {
                    Vector3 vp = _camera.WorldToViewportPoint(contact);
                    float vx = Mathf.Clamp(vp.x + drift, 0.05f, 0.95f);
                    sky = _camera.ViewportToWorldPoint(new Vector3(vx, 1.18f, vp.z));
                }
                else
                {
                    sky = contact + new Vector3(drift * 10f, 24f, 0f);
                }
            }

            Bolt bolt = null;
            for (int i = 0; i < _bolts.Count; i++)
                if (_bolts[i].Life <= 0f) { bolt = _bolts[i]; break; }
            bolt ??= CreateBolt();

            bolt.From = sky;
            bolt.To = contact;
            bolt.Life = BoltLife;
            bolt.RefreshTimer = 0f;
            bolt.Seed = Mathf.Abs((int)(ground.x * 131f + ground.z * 517f)) % 977;
            bolt.JagPhase = 0;
            bolt.Root.SetActive(true);
            RebuildBolt(bolt);
            ApplyBoltWidths(bolt, 1f);

            // Tesla-style impact, one size up: contact flash + spark burst + crawling ring + smoke.
            var emit = new ParticleSystem.EmitParams { position = contact };
            _flashPs.Emit(emit, 1);
            for (int i = 0; i < 16; i++)
            {
                float a = Hash01(bolt.Seed + i * 17) * Mathf.PI * 2f;
                float pitch = Hash01(bolt.Seed + i * 31) * 0.9f;
                var dir = new Vector3(Mathf.Cos(a) * (1f - pitch * 0.5f), 0.35f + pitch, Mathf.Sin(a) * (1f - pitch * 0.5f)).normalized;
                emit.velocity = dir * (3f + Hash01(bolt.Seed + i * 53) * 3.5f);
                emit.position = contact + dir * 0.15f;
                _sparksPs.Emit(emit, 1);
            }
            for (int i = 0; i < 9; i++)
            {
                float a = Hash01(bolt.Seed + 977 + i * 13) * Mathf.PI * 2f;
                float r = 0.15f + Hash01(bolt.Seed + i * 71) * 0.45f;
                emit.position = ground + new Vector3(Mathf.Cos(a) * r, 0.15f, Mathf.Sin(a) * r);
                emit.velocity = new Vector3(Mathf.Cos(a) * 0.25f, 0.7f + Hash01(bolt.Seed + i * 29) * 0.7f, Mathf.Sin(a) * 0.25f);
                _smokePs.Emit(emit, 1);
            }

            GroundRing ring = null;
            for (int i = 0; i < _rings.Count; i++)
                if (_rings[i].Life <= 0f) { ring = _rings[i]; break; }
            ring ??= CreateRing();
            ring.Center = ground + Vector3.up * 0.12f;
            ring.Life = RingLife;
            ring.Timer = 0f;
            ring.Phase = 0;
            ring.Seed = bolt.Seed;
            ring.Root.SetActive(true);
        }

        // ---------------- per-frame ----------------

        private void Update()
        {
            if (_clock == null) return;
            float dt = _clock.ScaledDeltaTime;
            float time = Time.time;

            // Night envelope: rises fast on cast, holds through the strike, then dawn.
            if (_nightRising)
            {
                _night01 = Mathf.Min(1f, _night01 + dt / NightInSeconds);
                _failsafe -= dt;
                if (_holdRemaining >= 0f)
                {
                    _holdRemaining -= dt;
                    if (_holdRemaining < 0f) _nightRising = false;
                }
                else if (_failsafe <= 0f) _nightRising = false; // resolve never arrived
            }
            else if (_night01 > 0f)
            {
                _night01 = Mathf.Max(0f, _night01 - dt / NightOutSeconds);
            }
            ApplyNight(_night01);

            // White thunder-crack flash.
            if (_screenFlash > 0f)
            {
                _screenFlash = Mathf.Max(0f, _screenFlash - dt * 4.5f);
                _flashImage.color = new Color(1f, 1f, 1f, _screenFlash);
                _flashImage.enabled = _screenFlash > 0.003f;
            }

            for (int i = 0; i < _bolts.Count; i++)
            {
                var bolt = _bolts[i];
                if (bolt.Life <= 0f) continue;
                bolt.Life -= dt;
                if (bolt.Life <= 0f) { bolt.Root.SetActive(false); continue; }

                bolt.RefreshTimer -= dt;
                if (bolt.RefreshTimer <= 0f)
                {
                    bolt.RefreshTimer += 1f / RefreshHz;
                    bolt.JagPhase++;
                    RebuildBolt(bolt);
                }

                float fade = Mathf.Clamp01(bolt.Life / BoltLife * 2.2f);
                float flick = 0.72f + 0.56f * Hash01(bolt.Seed * 3 + (int)(time * 47f));
                ApplyBoltWidths(bolt, flick * fade);
            }

            for (int i = 0; i < _rings.Count; i++)
            {
                var ring = _rings[i];
                if (ring.Life <= 0f) continue;
                ring.Life -= dt;
                if (ring.Life <= 0f) { ring.Root.SetActive(false); continue; }
                TickRing(ring, dt);
            }
        }

        private void ApplyNight(float night)
        {
            // User ruling 17-Jul: never touch the scene lighting or darken the screen.
            // The whole storm mood is the ThunderSplash HUD image (preset color) pulsing
            // its alpha 0 → 57/255 → 0 with the night envelope.
            if (_thunderSplash != null)
            {
                bool on = night > 0.003f;
                if (_thunderSplash.gameObject.activeSelf != on) _thunderSplash.gameObject.SetActive(on);
                if (on) _thunderSplash.color = new Color(_splashColor.r, _splashColor.g, _splashColor.b, night * SplashPeakAlpha);
            }

            // Weight-driven bloom boost only (no darkening) — bolts flare harder during the storm.
            if (_stormVolume != null) _stormVolume.weight = night;
        }

        /// <summary>
        /// Storm post-processing as a runtime global Volume (weight = night01): ONLY a bloom
        /// boost so the HDR bolts flare harder — no exposure/vignette (nothing darkens).
        /// Uber-pass effect, no extra render passes, mobile-safe; runtime profile instance so
        /// the authored PostFX assets are never modified.
        /// </summary>
        private void BuildStormVolume()
        {
            var go = new GameObject("StormPostFX");
            go.transform.SetParent(transform, false);
            _stormVolume = go.AddComponent<Volume>();
            _stormVolume.isGlobal = true;
            _stormVolume.priority = 50f;
            _stormVolume.weight = 0f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>();
            bloom.intensity.Override(2.2f); // bolts flare during the storm
            _stormVolume.profile = profile;
        }

        // ---------------- construction ----------------

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        private void BuildOverlay()
        {
            var canvasGo = new GameObject("LightningStormOverlay", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60; // above the HUD — the thunder-crack flash covers everything

            _flashImage = MakeOverlayImage(canvas.transform, "ThunderFlash");
            _flashImage.color = new Color(1f, 1f, 1f, 0f);
            _flashImage.enabled = false;
        }

        private static Image MakeOverlayImage(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private void BuildImpactSystems()
        {
            var soft = SoftDot;

            _flashPs = MakeSystem("StrikeFlash", _glowMaterial, soft);
            var flashMain = _flashPs.main;
            flashMain.startLifetime = 0.16f;
            flashMain.startSpeed = 0f;
            flashMain.startSize = 2.6f;                       // bigger than the Tesla contact flash
            flashMain.startColor = new Color(0.92f, 0.97f, 1f, 0.9f);

            _sparksPs = MakeSystem("StrikeSparks", _bodyMaterial, soft);
            var sparkMain = _sparksPs.main;
            sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            sparkMain.startColor = new Color(0.75f, 0.95f, 1f);
            sparkMain.gravityModifier = 0.35f;

            _smokePs = MakeSystem("StrikeSmoke", _smokeMaterial, soft);
            var smokeMain = _smokePs.main;
            smokeMain.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.7f);
            smokeMain.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            smokeMain.startColor = new Color(0.22f, 0.23f, 0.27f, 0.55f);
            smokeMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var smokeSize = _smokePs.sizeOverLifetime;
            smokeSize.enabled = true;
            smokeSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.55f, 1f, 2.3f));
            var smokeAlpha = _smokePs.colorOverLifetime;
            smokeAlpha.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.45f, 0.4f), new GradientAlphaKey(0f, 1f) });
            smokeAlpha.color = grad;
        }

        private ParticleSystem MakeSystem(string name, Material material, Texture2D soft)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 128;
            var emission = ps.emission;
            emission.rateOverTime = 0f; // manual Emit only
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            var instance = new Material(material);
            instance.SetTexture(BaseMapId, soft); // URP particle shader — mainTexture does NOT map here
            renderer.material = instance;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        private Bolt CreateBolt()
        {
            var bolt = new Bolt
            {
                Root = new GameObject("SkyBolt"),
                Points = new Vector3[Segments],
                Branches = new LineRenderer[BranchCount]
            };
            bolt.Root.transform.SetParent(transform, false);
            bolt.Glow = MakeLine(bolt.Root.transform, "Glow", _glowMaterial, 4);
            bolt.Body = MakeLine(bolt.Root.transform, "Body", _bodyMaterial, 2);
            bolt.Core = MakeLine(bolt.Root.transform, "Core", _coreMaterial, 2);
            for (int b = 0; b < BranchCount; b++)
            {
                bolt.Branches[b] = MakeLine(bolt.Root.transform, "Branch" + b, _bodyMaterial, 1);
                bolt.Branches[b].positionCount = BranchPoints;
            }
            bolt.Root.SetActive(false);
            _bolts.Add(bolt);
            return bolt;
        }

        private GroundRing CreateRing()
        {
            var ring = new GroundRing { Root = new GameObject("StrikeRing"), Arcs = new LineRenderer[RingArcs] };
            ring.Root.transform.SetParent(transform, false);
            for (int i = 0; i < RingArcs; i++)
            {
                ring.Arcs[i] = MakeLine(ring.Root.transform, "Arc" + i, _bodyMaterial, 2);
                ring.Arcs[i].positionCount = RingPoints;
            }
            ring.Root.SetActive(false);
            _rings.Add(ring);
            return ring;
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
            lr.startColor = lr.endColor = Color.white; // color lives in the HDR materials
            return lr;
        }

        // ---------------- bolt shaping (Tesla jag algorithm, sky-scaled) ----------------

        private void ApplyBoltWidths(Bolt bolt, float strength)
        {
            // ~1.7× the Tesla beam — this is the sky's own artillery.
            bolt.Core.widthMultiplier = 0.10f * strength;
            bolt.Body.widthMultiplier = 0.30f * strength;
            bolt.Glow.widthMultiplier = 0.95f * (0.8f + 0.35f * strength);
            for (int b = 0; b < bolt.Branches.Length; b++)
                bolt.Branches[b].widthMultiplier = 0.12f * strength;
        }

        private void RebuildBolt(Bolt bolt)
        {
            Vector3 dir = bolt.To - bolt.From;
            float length = dir.magnitude;
            if (length < 0.05f) return;
            dir /= length;
            Vector3 perp1 = Vector3.Cross(dir, Vector3.up);
            if (perp1.sqrMagnitude < 0.01f) perp1 = Vector3.right; else perp1.Normalize();
            Vector3 perp2 = Vector3.Cross(dir, perp1);

            int kinkIndex = 1 + (bolt.Seed + bolt.JagPhase) % (Segments - 2);
            for (int p = 0; p < Segments; p++)
            {
                float u = p / (float)(Segments - 1);
                Vector3 point = Vector3.LerpUnclamped(bolt.From, bolt.To, u);
                if (p > 0 && p < Segments - 1)
                {
                    float envelope = Mathf.Sin(u * Mathf.PI);
                    float amp = JagAmplitude * (p == kinkIndex ? 1.7f : 1f) * envelope;
                    float n1 = Hash01(bolt.Seed + p * 7 + bolt.JagPhase * 131) * 2f - 1f;
                    float n2 = Hash01(bolt.Seed + p * 13 + bolt.JagPhase * 71) * 2f - 1f;
                    float f1 = Hash01(bolt.Seed + p * 29 + bolt.JagPhase * 17) * 2f - 1f;
                    point += perp1 * (n1 * amp + f1 * amp * 0.35f) + perp2 * (n2 * amp * 0.6f);
                }
                bolt.Points[p] = point;
            }

            bolt.Core.positionCount = Segments;
            bolt.Body.positionCount = Segments;
            bolt.Glow.positionCount = Segments;
            bolt.Core.SetPositions(bolt.Points);
            bolt.Body.SetPositions(bolt.Points);
            bolt.Glow.SetPositions(bolt.Points);

            for (int b = 0; b < bolt.Branches.Length; b++)
            {
                var branch = bolt.Branches[b];
                int rootIndex = 1 + (bolt.Seed + b * 3 + bolt.JagPhase / 2) % (Segments - 3);
                Vector3 root = bolt.Points[rootIndex];
                float side = Hash01(bolt.Seed + b * 97 + bolt.JagPhase * 5) > 0.5f ? 1f : -1f;
                Vector3 branchDir = (perp1 * side + dir * 0.35f + Vector3.down * 0.2f).normalized;
                float branchLength = 0.7f + 0.7f * Hash01(bolt.Seed + b * 41 + bolt.JagPhase * 11);
                for (int p = 0; p < BranchPoints; p++)
                {
                    float u = p / (float)(BranchPoints - 1);
                    Vector3 point = root + branchDir * (branchLength * u);
                    if (p > 0 && p < BranchPoints - 1)
                    {
                        float n = Hash01(bolt.Seed + b * 53 + p * 19 + bolt.JagPhase * 13) * 2f - 1f;
                        point += perp2 * (n * 0.2f) + perp1 * (n * 0.14f * side);
                    }
                    BranchScratch[p] = point;
                }
                branch.SetPositions(BranchScratch);
            }
        }

        private void TickRing(GroundRing ring, float dt)
        {
            ring.Timer -= dt;
            bool refresh = ring.Timer <= 0f;
            if (refresh) { ring.Timer += 1f / 25f; ring.Phase++; }

            float fade = Mathf.Clamp01(ring.Life / RingLife);
            for (int i = 0; i < ring.Arcs.Length; i++)
            {
                var arc = ring.Arcs[i];
                arc.widthMultiplier = 0.07f * fade;
                if (!refresh) continue;

                float baseAngle = ring.Phase * 0.4f + i * 2.6f;
                for (int p = 0; p < RingPoints; p++)
                {
                    float u = p / (float)(RingPoints - 1);
                    float angle = baseAngle + u * 2.6f;
                    float radius = 0.65f * (0.85f + 0.3f * (Hash01(ring.Seed + ring.Phase * 31 + i * 7 + p * 13) - 0.5f));
                    float y = 0.35f * Hash01(ring.Seed + ring.Phase * 17 + i * 11 + p * 5);
                    RingScratch[p] = ring.Center + new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                }
                arc.SetPositions(RingScratch);
            }
        }

        /// <summary>Runtime-generated radial soft dot — no asset dependency (same recipe as TeslaTargetGlow).</summary>
        private static Texture2D SoftDot
        {
            get
            {
                if (_softDot != null) return _softDot;
                const int size = 64;
                _softDot = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                float half = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.4f);
                    _softDot.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                _softDot.Apply(false, true);
                return _softDot;
            }
        }

        private static float Hash01(int n)
        {
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }
    }
}

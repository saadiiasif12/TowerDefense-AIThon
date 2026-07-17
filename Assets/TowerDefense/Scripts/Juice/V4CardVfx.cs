using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Core;
using RoyalSiege.Data;

namespace RoyalSiege.Juice
{
    /// <summary>
    /// v4 card choreography (view-only, event-driven, pooled, zero RNG):
    ///  · EARTHQUAKE — a procedural ground-crack decal (runtime jagged-radial texture)
    ///    splits open under the cast point, dust + pebbles boil over it and the camera
    ///    rumbles in pulses for the zone's duration, then the crack seals (fade).
    ///  · LOG — a spinning wooden log rolls the spell's exact path with a dust wake,
    ///    then breaks apart (shrink + puff) at the end of the run.
    ///  · KNIGHTS — deploy dust pop per knight, steel clash spark on every landed hit,
    ///    a puff when one falls.
    /// Durations/distances are read from the same effect SOs the sim uses — the visuals
    /// can never drift from the gameplay.
    /// </summary>
    public sealed class V4CardVfx : MonoBehaviour
    {
        [SerializeField] private GameContext _context;
        [Tooltip("Alpha-blended particle material (Mat_SmokeSoft) — cloned for dust/cracks.")]
        [SerializeField] private Material _softMaterial;
        [SerializeField] private float _quakeShakePulse = 0.12f;
        [Tooltip("Rolling log model for the Log spell. Falls back to a brown primitive cylinder if empty.")]
        [SerializeField] private GameObject _logPrefab;

        // Visual log dimensions (independent of the effect's damage band width).
        private const float LogLength = 1.8f;
        private const float LogRadius = 0.35f;

        private GameEvents _events;
        private IClock _clock;
        private CameraShaker _shaker;

        private ParticleSystem _dustPs;    // shared: quake dust, log wake, knight deploys/deaths
        private ParticleSystem _sparkPs;   // knight clash sparks
        private ParticleSystem _pebblePs;  // quake debris
        private Texture2D _crackTexture;
        private Material _crackMaterial;

        private readonly List<CrackDecal> _cracks = new();
        private readonly List<LogRollView> _logs = new();
        private static Texture2D _softDot;

        private sealed class CrackDecal
        {
            public GameObject Root;
            public Material Mat;
            public Vector3 Point;
            public float Radius;
            public float Duration;
            public float Elapsed;
            public float NextRumble;
            public float NextDust;
        }

        private sealed class LogRollView
        {
            public GameObject Root;
            public Transform Model;
            public Vector3 Start;
            public Vector3 Direction;
            public float Distance;
            public float Speed;
            public float Travelled;
            public Vector3 Axle;      // horizontal axis the log lies along + rolls about
            public Quaternion BaseRot; // orientation with the log's long axis on the axle
            public float Radius;      // measured from the model (drives ground height + roll speed)
        }

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

            BuildShared();

            _events.SpellCast += OnSpellCast;
            _events.KnightSpawned += OnKnightSpawned;
            _events.KnightStruck += OnKnightStruck;
            _events.KnightDied += OnKnightDied;
        }

        private void OnDestroy()
        {
            if (_events == null) return;
            _events.SpellCast -= OnSpellCast;
            _events.KnightSpawned -= OnKnightSpawned;
            _events.KnightStruck -= OnKnightStruck;
            _events.KnightDied -= OnKnightDied;
        }

        // ---------------- events ----------------

        private void OnSpellCast(SpellCardSO card, Vector3 point)
        {
            switch (card.id)
            {
                case "Earthquake":
                    float duration = card.effect is EarthquakeEffectSO quake ? quake.duration : 4f;
                    StartCrack(point, card.radius, duration);
                    _shaker?.AddTrauma(0.3f);
                    break;

                case "Log":
                    if (card.effect is LogEffectSO log)
                    {
                        // Match the sim exactly: outward from the map center through the point.
                        Vector3 direction = RangeMath.PlanarDirection(
                            _context != null ? _context.MapCenter : Vector3.zero, point);
                        StartLog(point, direction, log.rollDistance, log.rollSpeed);
                    }
                    break;
            }
        }

        private void OnKnightSpawned(Vector3 position)
        {
            EmitDust(position, 8, 0.5f, new Color(0.75f, 0.72f, 0.6f, 0.6f));
        }

        private void OnKnightStruck(Vector3 position)
        {
            var emit = new ParticleSystem.EmitParams { position = position + Vector3.up * 0.7f };
            for (int i = 0; i < 6; i++)
            {
                float a = Hash01((int)(position.x * 131f) + i * 17) * Mathf.PI * 2f;
                emit.velocity = new Vector3(Mathf.Cos(a) * 2.2f, 1.6f + Hash01(i * 31) * 1.4f, Mathf.Sin(a) * 2.2f);
                _sparkPs.Emit(emit, 1);
            }
        }

        private void OnKnightDied(Vector3 position)
        {
            EmitDust(position, 12, 0.7f, new Color(0.5f, 0.5f, 0.55f, 0.65f));
        }

        // ---------------- earthquake crack ----------------

        private void StartCrack(Vector3 point, float radius, float duration)
        {
            CrackDecal crack = null;
            for (int i = 0; i < _cracks.Count; i++)
                if (!_cracks[i].Root.activeSelf) { crack = _cracks[i]; break; }
            crack ??= CreateCrack();

            crack.Point = point;
            crack.Radius = radius;
            crack.Duration = duration;
            crack.Elapsed = 0f;
            crack.NextRumble = 0f;
            crack.NextDust = 0f;
            crack.Root.transform.SetPositionAndRotation(
                point + Vector3.up * 0.04f,
                // Deterministic per-cast spin so repeat casts don't look copy-pasted.
                Quaternion.Euler(90f, Hash01((int)(point.x * 97f + point.z * 31f)) * 360f, 0f));
            crack.Root.transform.localScale = Vector3.one * 0.1f;
            crack.Mat.color = new Color(1f, 1f, 1f, 0f);
            crack.Root.SetActive(true);
        }

        private CrackDecal CreateCrack()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "QuakeCrack";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            var renderer = go.GetComponent<MeshRenderer>();
            var mat = new Material(_crackMaterial);
            renderer.material = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var crack = new CrackDecal { Root = go, Mat = mat };
            go.SetActive(false);
            _cracks.Add(crack);
            return crack;
        }

        // ---------------- log roll ----------------

        private void StartLog(Vector3 point, Vector3 direction, float distance, float speed)
        {
            LogRollView log = null;
            for (int i = 0; i < _logs.Count; i++)
                if (!_logs[i].Root.activeSelf) { log = _logs[i]; break; }
            log ??= CreateLog();

            log.Start = point;
            log.Direction = direction;
            log.Distance = distance;
            log.Speed = speed;
            log.Travelled = 0f;
            // The log lies ALONG the horizontal axle (perpendicular to the roll direction) and
            // rolls about it. BaseRot puts the model's +Z (its long axis) on that axle.
            log.Axle = Vector3.Cross(Vector3.up, direction).normalized;
            log.BaseRot = Quaternion.LookRotation(log.Axle, Vector3.up);
            log.Model.rotation = log.BaseRot;
            log.Root.transform.position = point + Vector3.up * log.Radius;
            log.Root.SetActive(true);
        }

        /// <summary>
        /// Root moves along the path; Model is a pivot that rolls; the visual (Log prefab kept
        /// at its AUTHORED scale, or a fallback cylinder) is Model's child. The Log prefab is
        /// never rescaled at runtime — its real radius (from renderer bounds) drives ground
        /// height and roll speed so it still grips the ground with no foot-slide.
        /// </summary>
        private LogRollView CreateLog()
        {
            var root = new GameObject("LogRoll");
            root.transform.SetParent(transform, false);
            var model = new GameObject("LogPivot").transform;
            model.SetParent(root.transform, false);

            float radius = LogRadius;
            if (_logPrefab != null)
            {
                var vis = Instantiate(_logPrefab, model);
                vis.transform.localPosition = Vector3.zero;
                vis.transform.localRotation = Quaternion.identity;
                foreach (var c in vis.GetComponentsInChildren<Collider>(true)) Destroy(c);
                radius = MeasureRadius(vis);   // read-only — scale left exactly as authored
            }
            else
            {
                var prim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(prim.GetComponent<Collider>());
                prim.transform.SetParent(model, false);
                // Unity's cylinder is Y-long; turn it so its length lies on +Z like the prefab.
                prim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                prim.transform.localScale = new Vector3(LogRadius * 2f, LogLength * 0.5f, LogRadius * 2f);
                var r = prim.GetComponent<MeshRenderer>();
                var mat = new Material(r.sharedMaterial) { color = new Color(0.45f, 0.3f, 0.16f) };
                r.material = mat;
            }

            var log = new LogRollView { Root = root, Model = model, Radius = radius };
            root.SetActive(false);
            _logs.Add(log);
            return log;
        }

        /// <summary>Rolling radius = half the model's smaller cross-section (long axis is +Z).
        /// Read-only: never changes the model's scale.</summary>
        private static float MeasureRadius(GameObject vis)
        {
            var rends = vis.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return LogRadius;
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float cross = Mathf.Min(b.size.x, b.size.y);
            return cross > 1e-4f ? cross * 0.5f : LogRadius;
        }

        // ---------------- per-frame ----------------

        private void Update()
        {
            if (_clock == null) return;
            float dt = _clock.ScaledDeltaTime;

            for (int i = 0; i < _cracks.Count; i++)
            {
                var crack = _cracks[i];
                if (!crack.Root.activeSelf) continue;
                crack.Elapsed += dt;
                float openT = Mathf.Clamp01(crack.Elapsed / 0.25f);                       // crack splits open fast
                float sealT = Mathf.Clamp01((crack.Elapsed - crack.Duration) / 0.6f);     // then seals after the zone ends
                crack.Root.transform.localScale = Vector3.one * (crack.Radius * 2f * Mathf.Lerp(0.35f, 1f, EaseOutBack(openT)));
                crack.Mat.color = new Color(1f, 1f, 1f, Mathf.Lerp(openT, 0f, sealT));

                if (crack.Elapsed < crack.Duration)
                {
                    if ((crack.NextRumble -= dt) <= 0f) { crack.NextRumble = 0.45f; _shaker?.AddTrauma(_quakeShakePulse); }
                    if ((crack.NextDust -= dt) <= 0f)
                    {
                        crack.NextDust = 0.18f;
                        int seed = (int)(crack.Elapsed * 53f) + i * 977;
                        float a = Hash01(seed) * Mathf.PI * 2f;
                        float r = Mathf.Sqrt(Hash01(seed * 7)) * crack.Radius * 0.85f;
                        Vector3 at = crack.Point + new Vector3(Mathf.Cos(a) * r, 0.1f, Mathf.Sin(a) * r);
                        EmitDust(at, 2, 0.45f, new Color(0.5f, 0.42f, 0.3f, 0.55f));
                        var emit = new ParticleSystem.EmitParams { position = at };
                        emit.velocity = new Vector3(Mathf.Cos(a) * 0.4f, 2.2f + Hash01(seed * 13) * 1.5f, Mathf.Sin(a) * 0.4f);
                        _pebblePs.Emit(emit, 1);
                    }
                }
                else if (sealT >= 1f)
                {
                    crack.Root.SetActive(false);
                }
            }

            for (int i = 0; i < _logs.Count; i++)
            {
                var log = _logs[i];
                if (!log.Root.activeSelf) continue;

                float step = log.Speed * dt;
                log.Travelled += step;
                Vector3 at = log.Start + log.Direction * Mathf.Min(log.Travelled, log.Distance) + Vector3.up * log.Radius;
                log.Root.transform.position = at;
                // Roll: spin about the axle by arc length / radius so the log grips the ground.
                float rollDeg = log.Travelled / log.Radius * Mathf.Rad2Deg;
                log.Model.rotation = Quaternion.AngleAxis(rollDeg, log.Axle) * log.BaseRot;
                if ((int)(log.Travelled * 6f) != (int)((log.Travelled - step) * 6f))
                    EmitDust(at - log.Direction * 0.4f + Vector3.down * 0.2f, 1, 0.4f, new Color(0.55f, 0.48f, 0.35f, 0.5f));

                // End of run: dust puff + disappear (no scale change — the log stays authored size).
                if (log.Travelled >= log.Distance)
                {
                    EmitDust(log.Root.transform.position, 6, 0.6f, new Color(0.45f, 0.35f, 0.2f, 0.6f));
                    log.Root.SetActive(false);
                }
            }
        }

        private void EmitDust(Vector3 at, int count, float size, Color color)
        {
            var emit = new ParticleSystem.EmitParams { position = at, startColor = color };
            for (int i = 0; i < count; i++)
            {
                int seed = (int)(at.x * 131f + at.z * 517f) + i * 29;
                float a = Hash01(seed) * Mathf.PI * 2f;
                emit.velocity = new Vector3(Mathf.Cos(a) * 0.8f, 0.6f + Hash01(seed * 3) * 0.8f, Mathf.Sin(a) * 0.8f);
                emit.startSize = size * (0.7f + Hash01(seed * 7) * 0.6f);
                _dustPs.Emit(emit, 1);
            }
        }

        // ---------------- construction ----------------

        private void BuildShared()
        {
            var soft = SoftDot;

            _dustPs = MakeSystem("V4Dust", _softMaterial, soft);
            var dustMain = _dustPs.main;
            dustMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            dustMain.startColor = new Color(0.6f, 0.55f, 0.45f, 0.55f);
            var dustColor = _dustPs.colorOverLifetime;
            dustColor.enabled = true;
            var dustGrad = new Gradient();
            dustGrad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            dustColor.color = dustGrad;

            _sparkPs = MakeSystem("V4Sparks", _softMaterial, soft);
            var sparkMain = _sparkPs.main;
            sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            sparkMain.startColor = new Color(1f, 0.9f, 0.5f);
            sparkMain.gravityModifier = 1.2f;

            _pebblePs = MakeSystem("V4Pebbles", _softMaterial, soft);
            var pebbleMain = _pebblePs.main;
            pebbleMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            pebbleMain.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
            pebbleMain.startColor = new Color(0.35f, 0.3f, 0.24f);
            pebbleMain.gravityModifier = 2.2f;

            _crackTexture = BuildCrackTexture();
            _crackMaterial = new Material(_softMaterial);
            _crackMaterial.SetTexture("_BaseMap", _crackTexture);
            _crackMaterial.mainTexture = _crackTexture;
        }

        private ParticleSystem MakeSystem(string name, Material material, Texture2D texture)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 256;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            var instance = new Material(material);
            instance.SetTexture("_BaseMap", texture);
            renderer.material = instance;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        /// <summary>
        /// Procedural ground-break: 8 jagged radial fissures with side branches, thick and
        /// dark near the center, tapering to nothing — drawn once, reused by every cast.
        /// </summary>
        private static Texture2D BuildCrackTexture()
        {
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            tex.SetPixels32(pixels); // fully transparent

            const float half = size * 0.5f;
            var crackColor = new Color(0.07f, 0.05f, 0.03f, 1f);

            for (int arm = 0; arm < 8; arm++)
            {
                float angle = arm * Mathf.PI * 2f / 8f + (Hash01(arm * 97) - 0.5f) * 0.6f;
                Vector2 pos = new(half, half);
                Vector2 dir = new(Mathf.Cos(angle), Mathf.Sin(angle));
                float reach = half * (0.72f + Hash01(arm * 31) * 0.24f);
                float travelled = 0f;
                int seg = 0;
                while (travelled < reach)
                {
                    float step = 14f + Hash01(arm * 131 + seg * 17) * 18f;
                    float turn = (Hash01(arm * 53 + seg * 29) - 0.5f) * 0.9f;
                    dir = Rotate(dir, turn * 0.5f);
                    Vector2 next = pos + dir * step;
                    float progress = travelled / reach;
                    float width = Mathf.Lerp(17f, 2.5f, progress); // reads at gameplay camera distance
                    float alpha = Mathf.Lerp(1f, 0.35f, progress);
                    DrawLine(tex, pos, next, width, crackColor, alpha);

                    // Occasional side branch — shorter, thinner.
                    if (Hash01(arm * 211 + seg * 41) > 0.62f && progress > 0.15f && progress < 0.8f)
                    {
                        Vector2 branchDir = Rotate(dir, (Hash01(arm * 7 + seg * 13) > 0.5f ? 1f : -1f) * (0.7f + Hash01(seg * 61) * 0.5f));
                        DrawLine(tex, next, next + branchDir * step * 1.4f, width * 0.5f, crackColor, alpha * 0.8f);
                    }

                    pos = next;
                    travelled += step;
                    seg++;
                }
            }

            tex.Apply(false, false);
            return tex;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private static void DrawLine(Texture2D tex, Vector2 from, Vector2 to, float width, Color color, float alpha)
        {
            int steps = Mathf.CeilToInt((to - from).magnitude);
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(from, to, i / (float)steps);
                int r = Mathf.CeilToInt(width * 0.5f);
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = (int)p.x + dx, y = (int)p.y + dy;
                    if (x < 0 || y < 0 || x >= tex.width || y >= tex.height) continue;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Max(1f, width * 0.5f);
                    if (d > 1f) continue;
                    float a = alpha * (1f - d * d);
                    var existing = tex.GetPixel(x, y);
                    if (existing.a >= a) continue;
                    tex.SetPixel(x, y, new Color(color.r, color.g, color.b, a));
                }
            }
        }

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

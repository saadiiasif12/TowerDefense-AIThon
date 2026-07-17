using UnityEngine;
using RoyalSiege.Combat;

namespace RoyalSiege.Juice
{
    /// <summary>
    /// The ONE shared electric-impact effect on an enemy being hit by Tesla(s): a bright
    /// contact flash on connect, a soft cyan body glow, small lightning arcs crawling around
    /// the body, and fast outward sparks. Follows the victim every frame WITHOUT parenting
    /// (survives enemy pooling), sustains while zaps keep arriving, scales brightness with
    /// the number of simultaneous beams (never duplicates the whole effect), afterglows
    /// briefly when the last beam disconnects, and cleans up on death. Fully pooled by
    /// ZapArcRenderer; built procedurally once — no prefab, no per-frame allocations.
    /// </summary>
    public sealed class TeslaTargetGlow : MonoBehaviour
    {
        private const float SustainSeconds = 1.35f;  // > Tesla period so single-Tesla fire reads continuous
        private const float AfterglowSeconds = 0.28f;
        private const int ArcPoints = 8;
        private const float ArcRefreshHz = 25f;
        private const float BaseSparkRate = 13f;
        private const float BaseGlowSize = 1.6f;     // × body radius

        private static Texture2D _softDot;

        /// <summary>Runtime-generated radial soft dot — no asset/builtin dependency.</summary>
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

        private ParticleSystem _glowPs;
        private ParticleSystem _sparksPs;
        private ParticleSystem _flashPs;
        private LineRenderer[] _arcs;
        private Vector3[] _arcScratch;

        private IEnemyTarget _target;
        private Vector3 _followPoint;
        private float _bodyRadius = 0.5f;
        private float _sparkScale = 1f;
        private int _activeArcs = 2;
        private float _ttl;
        private float _afterglow = -1f;
        private float _intensity = 1f;
        private float _arcTimer;
        private int _arcPhase;

        /// <summary>Set by ZapArcRenderer each frame: beams currently attached to this enemy.</summary>
        public int FrameConnections;

        public bool IsFinished { get; private set; }

        public static TeslaTargetGlow Create(Transform parent, Material arcMaterial, Material glowMaterial)
        {
            var go = new GameObject("TeslaTargetGlow");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<TeslaTargetGlow>();
            view.Build(arcMaterial, glowMaterial);
            go.SetActive(false);
            return view;
        }

        private void Build(Material arcMaterial, Material glowMaterial)
        {
            var soft = SoftDot;

            // Soft cyan body glow: a slow loop of large additive billboards.
            _glowPs = MakeSystem("Glow", glowMaterial, soft);
            var glowMain = _glowPs.main;
            glowMain.startLifetime = 0.35f;
            glowMain.startSpeed = 0f;
            glowMain.startSize = 1f; // scaled per-target in Attach
            glowMain.startColor = new Color(0.35f, 0.85f, 1f, 0.3f);
            var glowEmission = _glowPs.emission;
            glowEmission.rateOverTime = 9f;

            // Fast outward electric sparks.
            _sparksPs = MakeSystem("Sparks", arcMaterial, soft);
            var sparkMain = _sparksPs.main;
            sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.4f);
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4.2f);
            sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            sparkMain.startColor = new Color(0.75f, 0.95f, 1f);
            sparkMain.gravityModifier = 0.25f;
            var sparkEmission = _sparksPs.emission;
            sparkEmission.rateOverTime = BaseSparkRate;
            var sparkShape = _sparksPs.shape;
            sparkShape.enabled = true;
            sparkShape.shapeType = ParticleSystemShapeType.Sphere;
            sparkShape.radius = 0.25f;

            // Contact flash: a single big white-blue pop, emitted manually on connect.
            _flashPs = MakeSystem("Flash", glowMaterial, soft);
            var flashMain = _flashPs.main;
            flashMain.startLifetime = 0.12f;
            flashMain.startSpeed = 0f;
            flashMain.startSize = 1.2f;
            flashMain.startColor = new Color(0.9f, 0.98f, 1f, 0.85f);
            var flashEmission = _flashPs.emission;
            flashEmission.rateOverTime = 0f;

            // Small lightning arcs crawling around the body.
            _arcs = new LineRenderer[3];
            _arcScratch = new Vector3[ArcPoints];
            for (int i = 0; i < _arcs.Length; i++)
            {
                var arcGo = new GameObject("CrawlArc" + i);
                arcGo.transform.SetParent(transform, false);
                var lr = arcGo.AddComponent<LineRenderer>();
                lr.material = arcMaterial;
                lr.positionCount = ArcPoints;
                lr.widthMultiplier = 0.045f;
                lr.numCapVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.startColor = lr.endColor = Color.white;
                lr.useWorldSpace = true;
                _arcs[i] = lr;
            }
        }

        private ParticleSystem MakeSystem(string name, Material material, Texture2D soft)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            var instance = new Material(material);
            instance.SetTexture("_BaseMap", soft); // URP particle shader — mainTexture does NOT map here
            renderer.material = instance;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        // ---------------- lifecycle ----------------

        public void Attach(IEnemyTarget target, float sparkScale, int crawlArcCount)
        {
            _target = target;
            _bodyRadius = Mathf.Max(0.3f, target.BodyRadius);
            _sparkScale = sparkScale;
            _activeArcs = Mathf.Clamp(crawlArcCount, 0, _arcs.Length);
            _followPoint = target.Position + Vector3.up * 0.5f;
            _ttl = SustainSeconds;
            _afterglow = -1f;
            _intensity = 1f;
            IsFinished = false;

            transform.position = _followPoint;
            var glowMain = _glowPs.main;
            glowMain.startSize = BaseGlowSize * _bodyRadius;
            var sparkShape = _sparksPs.shape;
            sparkShape.radius = _bodyRadius * 0.55f;
            var flashMain = _flashPs.main;
            flashMain.startSize = 1.8f * _bodyRadius;

            gameObject.SetActive(true);
            _glowPs.Clear(true);
            _glowPs.Play(true);
            _sparksPs.Play(true);
            for (int i = 0; i < _arcs.Length; i++) _arcs[i].enabled = i < _activeArcs;
        }

        /// <summary>A beam (re)connected — sustain, and pop the contact flash.</summary>
        public void NotifyZap()
        {
            if (_afterglow >= 0f) { _afterglow = -1f; _sparksPs.Play(true); _glowPs.Play(true); } // re-attacked during afterglow
            _ttl = SustainSeconds;
            _flashPs.Emit(1);
            _sparksPs.Emit(Mathf.RoundToInt(8f * _sparkScale));
        }

        public void TickFrame(float dt)
        {
            if (IsFinished) return;

            bool targetAlive = _target != null && _target.IsAlive;
            if (targetAlive) _followPoint = _target.Position + Vector3.up * 0.5f;
            transform.position = _followPoint;

            // Extra simultaneous Teslas raise intensity — never a duplicate effect.
            float targetIntensity = 1f + Mathf.Min(FrameConnections - 1, 3) * 0.22f;
            _intensity = Mathf.Lerp(_intensity, Mathf.Max(1f, targetIntensity), 1f - Mathf.Exp(-8f * dt));
            var sparkEmission = _sparksPs.emission;
            sparkEmission.rateOverTime = BaseSparkRate * _sparkScale * _intensity;

            if (_afterglow < 0f)
            {
                _ttl -= dt;
                if (_ttl <= 0f || !targetAlive) BeginAfterglow();
            }
            else
            {
                _afterglow -= dt;
                if (_afterglow <= 0f) IsFinished = true;
            }

            TickArcs(dt);
        }

        private void BeginAfterglow()
        {
            _afterglow = AfterglowSeconds;
            _glowPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _sparksPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void TickArcs(float dt)
        {
            _arcTimer -= dt;
            bool refresh = _arcTimer <= 0f;
            if (refresh) { _arcTimer += 1f / ArcRefreshHz; _arcPhase++; }

            float fade = _afterglow < 0f ? 1f : Mathf.Clamp01(_afterglow / AfterglowSeconds);
            for (int i = 0; i < _activeArcs; i++)
            {
                var arc = _arcs[i];
                arc.widthMultiplier = 0.045f * _intensity * fade;
                if (!refresh) continue;

                // A partial ring of jittered points hugging the body, each arc at its own
                // drifting phase — reads as electricity crawling over the enemy.
                float baseAngle = (_arcPhase * 0.35f + i * 2.1f);
                for (int p = 0; p < ArcPoints; p++)
                {
                    float u = p / (float)(ArcPoints - 1);
                    float angle = baseAngle + u * 2.4f; // ~140° span
                    float radius = _bodyRadius * (0.85f + 0.25f * (Hash01(_arcPhase * 31 + i * 7 + p * 13) - 0.5f));
                    float y = 0.15f + 0.5f * Hash01(_arcPhase * 17 + i * 11 + p * 5);
                    _arcScratch[p] = _followPoint + new Vector3(Mathf.Cos(angle) * radius, y - 0.3f, Mathf.Sin(angle) * radius);
                }
                arc.SetPositions(_arcScratch);
            }
        }

        public void Detach()
        {
            _target = null;
            FrameConnections = 0;
            for (int i = 0; i < _arcs.Length; i++) _arcs[i].enabled = false;
            gameObject.SetActive(false);
        }

        private static float Hash01(int n)
        {
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Core;

namespace RoyalSiege.Juice
{
    /// <summary>
    /// 18-Jul juice: floating damage numbers on every enemy hit (view-only, event-driven,
    /// pooled, zero RNG — jitter is hashed off a hit counter). Numbers pop with an
    /// ease-out-back scale, drift up along a slight arc, then fade. Killing blows read
    /// bigger and hotter. Built on TextMesh (no canvas, no TMP dependency) with a shared
    /// pool — no allocation during combat beyond first-time number strings (cached).
    /// </summary>
    public sealed class DamageNumbersView : MonoBehaviour
    {
        [SerializeField] private GameContext _context;
        [SerializeField] private int _poolSize = 24;
        [SerializeField] private float _lifeSeconds = 0.75f;
        [SerializeField] private float _riseWorldUnits = 1.4f;
        [SerializeField] private float _baseSize = 38f;          // character size at hit (orthographic far camera)
        [SerializeField] private Color _normalColor = new(1f, 0.95f, 0.75f);
        [SerializeField] private Color _killColor = new(1f, 0.55f, 0.15f);

        private sealed class Entry
        {
            public TextMesh Text;
            public TextMesh Shadow;   // black offset copy — readability on any background
            public Transform Transform;
            public float Age;      // -1 = free
            public Vector3 Origin;
            public float DriftX;
            public float SizeMul;
            public Color Color;
        }

        private readonly List<Entry> _pool = new();
        private static readonly Dictionary<int, string> NumberCache = new();
        private GameEvents _events;
        private Camera _camera;
        private int _hitCounter;
        private int _next; // round-robin reuse when saturated

        private void Start()
        {
            if (_context == null) _context = FindFirstObjectByType<GameContext>();
            if (_context == null || _context.Events == null) return;
            _events = _context.Events;
            _camera = Camera.main;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject("DmgNum", typeof(TextMesh));
                go.transform.SetParent(transform, false);
                var tm = go.GetComponent<TextMesh>();
                tm.font = font;
                tm.fontSize = 46;
                tm.fontStyle = FontStyle.Bold;
                tm.alignment = TextAlignment.Center;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.characterSize = 0.1f;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = font.material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // black offset copy behind the number — keeps it readable over bright grass
                var shGo = new GameObject("Shadow", typeof(TextMesh));
                shGo.transform.SetParent(go.transform, false);
                shGo.transform.localPosition = new Vector3(0.05f, -0.05f, 0.02f);
                var sh = shGo.GetComponent<TextMesh>();
                sh.font = font; sh.fontSize = 46; sh.fontStyle = FontStyle.Bold;
                sh.alignment = TextAlignment.Center; sh.anchor = TextAnchor.MiddleCenter;
                sh.characterSize = 0.1f;
                var shR = shGo.GetComponent<MeshRenderer>();
                shR.sharedMaterial = font.material;
                shR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                go.SetActive(false);
                _pool.Add(new Entry { Text = tm, Shadow = sh, Transform = go.transform, Age = -1f });
            }

            _events.EnemyDamaged += OnEnemyDamaged;
        }

        private void OnDestroy()
        {
            if (_events != null) _events.EnemyDamaged -= OnEnemyDamaged;
        }

        private void OnEnemyDamaged(Vector3 position, float amount, bool killingBlow)
        {
            // Find a free entry; saturated -> steal round-robin (oldest hits vanish first).
            Entry entry = null;
            for (int i = 0; i < _pool.Count; i++)
                if (_pool[i].Age < 0f) { entry = _pool[i]; break; }
            if (entry == null) { entry = _pool[_next]; _next = (_next + 1) % _pool.Count; }

            int value = Mathf.Max(1, Mathf.RoundToInt(amount));
            if (!NumberCache.TryGetValue(value, out string label))
            {
                label = value.ToString();
                NumberCache[value] = label;
            }

            _hitCounter++;
            entry.Age = 0f;
            entry.Origin = position + Vector3.up * 1.5f;
            entry.DriftX = (Hash01(_hitCounter * 7919) - 0.5f) * 1.1f; // hashed sideways drift
            entry.SizeMul = killingBlow ? 1.5f : Mathf.Lerp(0.85f, 1.2f, Mathf.InverseLerp(20f, 150f, value));
            entry.Color = killingBlow ? _killColor : _normalColor;
            entry.Text.text = label;
            entry.Text.color = entry.Color;
            entry.Shadow.text = label;
            entry.Shadow.color = new Color(0f, 0f, 0f, 0.8f);
            entry.Transform.position = entry.Origin;
            entry.Transform.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_camera == null) { _camera = Camera.main; if (_camera == null) return; }
            float dt = Time.deltaTime; // juice runs on wall-clock: fades keep moving while sim pauses
            var camRot = _camera.transform.rotation;

            for (int i = 0; i < _pool.Count; i++)
            {
                var e = _pool[i];
                if (e.Age < 0f) continue;
                e.Age += dt;
                float t = e.Age / _lifeSeconds;
                if (t >= 1f)
                {
                    e.Age = -1f;
                    e.Transform.gameObject.SetActive(false);
                    continue;
                }

                // Position: rise with ease-out + slight sideways arc.
                float rise = 1f - (1f - t) * (1f - t);
                e.Transform.position = e.Origin
                    + Vector3.up * (_riseWorldUnits * rise)
                    + _camera.transform.right * (e.DriftX * t);
                e.Transform.rotation = camRot; // billboard

                // Scale: pop in (ease-out-back over the first 25%), settle, shrink at the end.
                float pop = t < 0.25f ? EaseOutBack(t / 0.25f) : 1f;
                float shrink = t > 0.75f ? 1f - (t - 0.75f) / 0.25f * 0.35f : 1f;
                float size = _baseSize * e.SizeMul * pop * shrink * 0.1f;
                e.Transform.localScale = Vector3.one * size;

                // Fade out over the last 35%.
                float alpha = t > 0.65f ? 1f - (t - 0.65f) / 0.35f : 1f;
                var c = e.Color; c.a = alpha;
                e.Text.color = c;
                e.Shadow.color = new Color(0f, 0f, 0f, 0.8f * alpha);
            }
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private static float Hash01(int n)
        {
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }
    }
}

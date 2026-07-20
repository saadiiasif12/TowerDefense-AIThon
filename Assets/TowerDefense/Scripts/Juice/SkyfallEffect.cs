using System;
using UnityEngine;
using RoyalSiege.Core;
using RoyalSiege.Data;

namespace RoyalSiege.Juice
{
    /// <summary>
    /// Pooled "falls from the sky" choreography: N clones of a template visual drop onto
    /// deterministic points inside the target area (golden-angle spiral, per-cast rotation
    /// hashed from the cast point — zero RNG), staggered, stick in the ground briefly, then
    /// shrink away. count=15 → arrow volley; count=1 + a trail child → fireball meteor.
    /// Purely visual — the matching damage timing is the card's fallDelaySeconds.
    /// With a config assigned, every item launches from the shared GameConfig.skyPoint
    /// (18-Jul ruling: one fixed sky origin for all skyfall spells); otherwise it falls
    /// back to the per-landing dropHeight + lateralOffset.
    /// </summary>
    public sealed class SkyfallEffect : MonoBehaviour
    {
        [SerializeField] private Transform _template;
        [Tooltip("Optional: when set, items start at the shared config.skyPoint instead of dropHeight/lateralOffset.")]
        [SerializeField] private GameConfigSO _config;
        [SerializeField] private int _count = 15;
        [SerializeField] private float _dropHeight = 9f;
        [Tooltip("Fallback horizontal spawn offset (only used when no config is assigned).")]
        [SerializeField] private Vector2 _lateralOffset = new(-2.2f, -0.9f);
        [SerializeField] private float _fallSeconds = 0.38f;
        [Tooltip("Start times are spread across this window (deterministic order shuffle).")]
        [SerializeField] private float _staggerSeconds = 0.14f;
        [SerializeField] private float _stickSeconds = 0.5f;
        [SerializeField] private float _fadeSeconds = 0.25f;
        [Tooltip("How much of the spell radius the pattern fills (keeps visuals inside the ring).")]
        [SerializeField] private float _radiusFill = 0.85f;
        [SerializeField] private ParticleSystem _landPuff;
        [SerializeField] private float _landPuffScale = 0.5f;
        [SerializeField] private Color _landPuffTint = Color.white;

        private Transform[] _items;
        private TrailRenderer[][] _itemTrails;
        private float[] _delays;
        private Vector3[] _from;
        private Vector3[] _to;
        private bool[] _landed;
        private float _elapsed;
        private bool _playing;
        private IVfxSpawner _vfx;
        private IClock _clock;
        private Action<SkyfallEffect> _onDone;

        public void Play(Vector3 point, float areaRadius, IVfxSpawner vfx, IClock clock, Action<SkyfallEffect> onDone)
        {
            EnsureItems();
            _vfx = vfx;
            _clock = clock;
            _onDone = onDone;
            _elapsed = 0f;
            _playing = true;

            // Golden-angle spiral, rotated per cast by a hash of the point — repeat casts
            // don't look copy-pasted, and it's still fully deterministic.
            float baseAngle = Mathf.Abs(point.x * 12.9898f + point.z * 78.233f) * 360f;
            for (int i = 0; i < _count; i++)
            {
                float u = (i + 0.5f) / _count;
                float r = _count == 1 ? 0f : areaRadius * _radiusFill * Mathf.Sqrt(u);
                float ang = (baseAngle + i * 137.508f) * Mathf.Deg2Rad;
                Vector3 land = point + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r;
                _to[i] = land;
                _from[i] = _config != null
                    ? _config.skyPoint
                    : land + new Vector3(_lateralOffset.x, _dropHeight, _lateralOffset.y);
                _delays[i] = _count <= 1 ? 0f : _staggerSeconds * ((i * 7) % _count) / _count;
                _landed[i] = false;
                _items[i].gameObject.SetActive(false);
                _items[i].localScale = Vector3.one;
            }
        }

        private void EnsureItems()
        {
            if (_items != null) return;
            _items = new Transform[_count];
            _itemTrails = new TrailRenderer[_count][];
            _delays = new float[_count];
            _from = new Vector3[_count];
            _to = new Vector3[_count];
            _landed = new bool[_count];
            _template.gameObject.SetActive(false);
            for (int i = 0; i < _count; i++)
            {
                _items[i] = Instantiate(_template, transform);
                _itemTrails[i] = _items[i].GetComponentsInChildren<TrailRenderer>(true);
            }
        }

        private void Update()
        {
            if (!_playing) return;
            // Follows the game clock — the landing stays in sync with fallDelaySeconds at
            // any game speed (0.25× trails inspection, 3× fast-forward, pause).
            _elapsed += _clock != null ? _clock.ScaledDeltaTime : Time.deltaTime;
            bool allDone = true;
            float total = _fallSeconds + _stickSeconds + _fadeSeconds;

            for (int i = 0; i < _count; i++)
            {
                float t = _elapsed - _delays[i];
                var item = _items[i];
                if (t < 0f) { allDone = false; continue; }
                if (t >= total)
                {
                    if (item.gameObject.activeSelf) item.gameObject.SetActive(false);
                    continue;
                }
                allDone = false;

                if (!item.gameObject.activeSelf)
                {
                    item.SetPositionAndRotation(_from[i], Quaternion.LookRotation((_to[i] - _from[i]).normalized));
                    item.gameObject.SetActive(true);
                    foreach (var trail in _itemTrails[i]) trail.Clear(); // pooled reuse: no ghost streaks
                }

                if (t < _fallSeconds)
                {
                    float u = t / _fallSeconds;
                    u *= u; // accelerate — gravity feel
                    item.position = Vector3.Lerp(_from[i], _to[i], u);
                }
                else
                {
                    if (!_landed[i])
                    {
                        _landed[i] = true;
                        item.position = _to[i];
                        if (_landPuff != null)
                            _vfx?.Spawn(_landPuff, _to[i] + Vector3.up * 0.06f, Quaternion.identity, _landPuffScale, _landPuffTint);
                    }
                    float sinceStick = t - _fallSeconds - _stickSeconds;
                    if (sinceStick > 0f)
                        item.localScale = Vector3.one * Mathf.Max(0.001f, 1f - sinceStick / _fadeSeconds);
                }
            }

            if (allDone)
            {
                _playing = false;
                var done = _onDone;
                _onDone = null;
                done?.Invoke(this);
            }
        }
    }
}

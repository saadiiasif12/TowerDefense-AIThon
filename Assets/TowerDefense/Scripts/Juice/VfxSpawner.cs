using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoyalSiege.Juice
{
    public interface IVfxSpawner
    {
        void Spawn(ParticleSystem prefab, Vector3 position);
        void Spawn(ParticleSystem prefab, Vector3 position, Quaternion rotation, float scale = 1f, Color? tint = null);
    }

    /// <summary>
    /// Pooled one-shot particle playback (factory + pool, same pattern as projectiles).
    /// Instances auto-release after the system's own duration via VfxInstance.
    /// </summary>
    public sealed class VfxSpawner : IVfxSpawner
    {
        private readonly Dictionary<ParticleSystem, Stack<VfxInstance>> _pools = new();
        private readonly Dictionary<ParticleSystem, float> _durations = new();
        private readonly Transform _parent;

        public VfxSpawner(Transform parent) => _parent = parent;

        public void Spawn(ParticleSystem prefab, Vector3 position) =>
            Spawn(prefab, position, Quaternion.identity);

        public void Spawn(ParticleSystem prefab, Vector3 position, Quaternion rotation, float scale = 1f, Color? tint = null)
        {
            if (prefab == null) return;

            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new Stack<VfxInstance>();
                _pools[prefab] = pool;
                _durations[prefab] = MeasureDuration(prefab);
            }

            VfxInstance vfx = pool.Count > 0 ? pool.Pop() : Create(prefab);
            var t = vfx.transform;
            t.SetPositionAndRotation(position, rotation == default ? Quaternion.identity : rotation);
            t.localScale = Vector3.one * scale;

            // White = "as authored": never stomp a hand-tuned prefab's colors (18-Jul user
            // ruling — the bomb blast's orange fire kept turning white). Non-white tints
            // still recolor the root system; the authored color is restored on pooled
            // reuse so a previous tint never leaks into an untinted spawn.
            var main = vfx.System.main;
            main.startColor = tint.HasValue && tint.Value != Color.white
                ? new ParticleSystem.MinMaxGradient(tint.Value)
                : vfx.OriginalStartColor;

            vfx.gameObject.SetActive(true);
            vfx.Play(_durations[prefab], instance => { instance.gameObject.SetActive(false); pool.Push(instance); });
        }

        private VfxInstance Create(ParticleSystem prefab)
        {
            var ps = UnityEngine.Object.Instantiate(prefab, _parent);
            var instance = ps.gameObject.AddComponent<VfxInstance>();
            instance.System = ps;
            instance.OriginalStartColor = ps.main.startColor; // preserves gradients too
            return instance;
        }

        private static float MeasureDuration(ParticleSystem prefab)
        {
            float duration = 0f;
            foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                duration = Mathf.Max(duration, main.duration + main.startLifetime.constantMax);
            }
            return duration + 0.1f;
        }
    }

    /// <summary>Plays a pooled one-shot system (and its AudioSource, if any) and returns itself to the pool when finished.</summary>
    public sealed class VfxInstance : MonoBehaviour
    {
        public ParticleSystem System;
        /// <summary>The prefab's authored root startColor — reapplied on untinted spawns.</summary>
        public ParticleSystem.MinMaxGradient OriginalStartColor;

        private float _remaining;
        private Action<VfxInstance> _onDone;
        private AudioSource _audio;
        private bool _audioChecked;

        public void Play(float duration, Action<VfxInstance> onDone)
        {
            _remaining = duration;
            _onDone = onDone;
            System.Clear(true);
            System.Play(true);

            if (!_audioChecked) { _audio = GetComponent<AudioSource>(); _audioChecked = true; }
            if (_audio != null && _audio.clip != null) _audio.Play();
        }

        private void Update()
        {
            if (_onDone == null) return;
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
            {
                var done = _onDone;
                _onDone = null;
                done(this);
            }
        }
    }
}

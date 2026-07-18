using System;
using UnityEngine;
using RoyalSiege.Core;
using RoyalSiege.Data;
using RoyalSiege.Juice;

namespace RoyalSiege.Combat
{
    /// <summary>
    /// Generic pooled homing projectile used by EVERY shooter (buildings, tower, Mage).
    /// Flies a parabolic arc (ProjectileSettingsSO.arcHeight); always hits unless the
    /// target dies mid-flight — then it FIZZLES (GDD ruling: prevents double bounty).
    /// Juice: optional spin (cannonballs tumble, bolts face velocity), pooled impact VFX
    /// on real hits. Splash and side effects belong to the shooter via onImpact.
    /// </summary>
    public sealed class Projectile : MonoBehaviour
    {
        private ProjectileSettingsSO _settings;
        private IDamageable _target;
        private IClock _clock;
        private IVfxSpawner _vfx;
        private Action<Projectile> _release;
        private Action<Vector3, IDamageable> _onImpact;

        private Vector3 _start;
        private float _damage;
        private float _travelTime;
        private float _elapsed;
        private Vector3 _lastPosition;
        private bool _active;
        private Vector3 _rangeOrigin;
        private float _maxRange; // 0 = unclamped (enemy shooters)

        private TrailRenderer[] _trails;
        private ParticleSystem[] _systems;
        private Light[] _lights;
        private float _fadeSeconds;
        private float _fadeRemaining;

        private void Awake()
        {
            _trails = GetComponentsInChildren<TrailRenderer>(true);
            _systems = GetComponentsInChildren<ParticleSystem>(true);
            _lights = GetComponentsInChildren<Light>(true);
            foreach (var trail in _trails) _fadeSeconds = Mathf.Max(_fadeSeconds, trail.time);
        }

        /// <param name="rangeOrigin">With <paramref name="maxRange"/> &gt; 0: the shooter's
        /// position — the flight (and impact) is clamped to that radius so a fleeing target
        /// can't drag the shot beyond the drawn range ring (QA 17-Jul DT-002).</param>
        public void Launch(Vector3 from, IDamageable target, float damage,
            ProjectileSettingsSO settings, IClock clock, IVfxSpawner vfx,
            Action<Projectile> release, Action<Vector3, IDamageable> onImpact = null,
            Vector3 rangeOrigin = default, float maxRange = 0f)
        {
            _settings = settings;
            _target = target;
            _damage = damage;
            _clock = clock;
            _vfx = vfx;
            _release = release;
            _onImpact = onImpact;
            _rangeOrigin = rangeOrigin;
            _maxRange = maxRange;

            _start = from + Vector3.up * settings.spawnHeightOffset;
            _elapsed = 0f;
            float distance = RangeMath.PlanarDistance(from, target.Position);
            _travelTime = Mathf.Max(0.05f, distance / settings.speed);
            transform.position = _start;
            _lastPosition = _start;
            _active = true;

            // Hard-reset every pooled FX at the new spawn point: no stale trail segments,
            // no leftover particles from the previous flight flashing on reuse.
            foreach (var trail in _trails) trail.Clear();
            foreach (var ps in _systems) { ps.Clear(false); ps.Play(false); }
            foreach (var light in _lights) light.enabled = true;
        }

        private void Update()
        {
            if (!_active)
            {
                // Impact/fizzle happened: hold in place until the trail ribbon has faded,
                // otherwise releasing to the pool cuts the streak mid-air on the hit frame.
                if (_fadeRemaining > 0f)
                {
                    _fadeRemaining -= _clock.ScaledDeltaTime;
                    if (_fadeRemaining <= 0f) _release?.Invoke(this);
                }
                return;
            }

            // Target died mid-flight → fizzle: no damage, no impact callback, no VFX.
            if (_target == null || !_target.IsAlive)
            {
                Finish();
                return;
            }

            float dt = _clock.ScaledDeltaTime;
            _elapsed += dt;
            float u = Mathf.Clamp01(_elapsed / _travelTime);

            Vector3 end = ClampToRange(_target.Position) + Vector3.up * _settings.impactHeightOffset;
            Vector3 position = Vector3.Lerp(_start, end, u);
            position.y += _settings.arcHeight * 4f * u * (1f - u);

            Vector3 velocity = position - _lastPosition;
            if (_settings.spinDegreesPerSecond > 0f)
                transform.Rotate(Vector3.right, _settings.spinDegreesPerSecond * dt, Space.Self);
            else if (velocity.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(velocity);
            _lastPosition = position;
            transform.position = position;

            if (u >= 1f)
            {
                // Impact resolves at the range boundary if the target slipped past it —
                // the hit still lands (deterministic guarantee) but nothing ever renders
                // beyond the shooter's drawn radius.
                Vector3 impactPoint = ClampToRange(_target.Position);
                _target.TakeDamage(_damage);
                _vfx?.Spawn(_settings.impactVfx, impactPoint + Vector3.up * _settings.impactHeightOffset,
                    Quaternion.identity, 1f, _settings.impactTint);
                if (_settings.impactShake > 0f) CameraShaker.Main?.AddTrauma(_settings.impactShake);
                _onImpact?.Invoke(impactPoint, _target);
                Finish();
            }
        }

        private Vector3 ClampToRange(Vector3 point)
        {
            if (_maxRange <= 0f) return point;
            var offset = new Vector3(point.x - _rangeOrigin.x, 0f, point.z - _rangeOrigin.z);
            float dist = offset.magnitude;
            if (dist <= _maxRange) return point;
            var clamped = _rangeOrigin + offset * (_maxRange / dist);
            clamped.y = point.y;
            return clamped;
        }

        private void Finish()
        {
            _active = false;
            _target = null;
            foreach (var ps in _systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            foreach (var light in _lights) light.enabled = false;
            _fadeRemaining = _fadeSeconds;
            if (_fadeRemaining <= 0f) _release?.Invoke(this);
        }
    }
}

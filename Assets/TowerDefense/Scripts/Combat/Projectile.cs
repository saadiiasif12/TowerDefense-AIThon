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

        public void Launch(Vector3 from, IDamageable target, float damage,
            ProjectileSettingsSO settings, IClock clock, IVfxSpawner vfx,
            Action<Projectile> release, Action<Vector3, IDamageable> onImpact = null)
        {
            _settings = settings;
            _target = target;
            _damage = damage;
            _clock = clock;
            _vfx = vfx;
            _release = release;
            _onImpact = onImpact;

            _start = from + Vector3.up * settings.spawnHeightOffset;
            _elapsed = 0f;
            float distance = RangeMath.PlanarDistance(from, target.Position);
            _travelTime = Mathf.Max(0.05f, distance / settings.speed);
            transform.position = _start;
            _lastPosition = _start;
            _active = true;

            // Clear any pooled trail so it doesn't streak from the previous shot's position.
            foreach (var trail in GetComponentsInChildren<TrailRenderer>())
                trail.Clear();
        }

        private void Update()
        {
            if (!_active) return;

            // Target died mid-flight → fizzle: no damage, no impact callback, no VFX.
            if (_target == null || !_target.IsAlive)
            {
                Finish();
                return;
            }

            float dt = _clock.ScaledDeltaTime;
            _elapsed += dt;
            float u = Mathf.Clamp01(_elapsed / _travelTime);

            Vector3 end = _target.Position + Vector3.up * _settings.impactHeightOffset;
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
                Vector3 impactPoint = _target.Position;
                _target.TakeDamage(_damage);
                _vfx?.Spawn(_settings.impactVfx, impactPoint + Vector3.up * _settings.impactHeightOffset,
                    Quaternion.identity, 1f, _settings.impactTint);
                _onImpact?.Invoke(impactPoint, _target);
                Finish();
            }
        }

        private void Finish()
        {
            _active = false;
            _target = null;
            _release?.Invoke(this);
        }
    }
}

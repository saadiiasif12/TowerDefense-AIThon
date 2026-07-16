using System;
using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Core;

namespace RoyalSiege.Buildings
{
    /// <summary>
    /// Purely-visual turret behaviour (never touches gameplay — targeting/damage stay in
    /// StructureAttack): yaw-rotates a pivot toward the current target, anticipates during
    /// the windup (slight pull-back), kicks back on fire with spring recovery, and pops the
    /// whole building in on placement (ease-out-back scale punch).
    /// Tesla mode (_scalePunchMode): no barrel — charge-squash then discharge-pop instead.
    /// </summary>
    public sealed class BuildingTurret : MonoBehaviour
    {
        [Header("Nodes (optional — leave empty to skip that behaviour)")]
        [SerializeField] private Transform _yawPivot;
        [SerializeField] private Transform _recoilNode;
        [SerializeField] private Transform _muzzle;
        [Tooltip("Ammo model sitting on the weapon (X-Bow's nocked arrow): hidden when the shot fires, pops back in on the next windup.")]
        [SerializeField] private Transform _loadedAmmo;

        [Header("Feel")]
        [SerializeField] private float _turnDegreesPerSecond = 480f;
        [SerializeField] private float _yawModelOffsetDegrees;   // if the mesh's barrel isn't +Z
        [SerializeField] private float _recoilDistance = 0.16f;
        [SerializeField] private float _recoilRecoverSharpness = 9f;
        [SerializeField] private float _anticipationDistance = 0.05f;
        [SerializeField] private bool _scalePunchMode;            // Tesla: squash/pop instead of recoil
        [SerializeField] private float _spawnPunchSeconds = 0.35f;

        private IClock _clock;
        private Func<IEnemyTarget> _targetGetter;
        private Vector3 _recoilBasePosition;
        private Vector3 _nodeBaseScale;
        private Vector3 _rootBaseScale;
        private float _recoilOffset;
        private float _anticipation;
        private float _anticipationTarget;
        private float _scalePunch;
        private float _spawnPunchT = 1f;
        private Vector3 _ammoBaseScale = Vector3.one;
        private float _ammoPopT = 1f;

        public Vector3 MuzzlePosition => _muzzle != null ? _muzzle.position : transform.position + Vector3.up;

        public void Init(IClock clock, Func<IEnemyTarget> targetGetter)
        {
            _clock = clock;
            _targetGetter = targetGetter;
            _rootBaseScale = transform.localScale;
            if (_recoilNode != null)
            {
                _recoilBasePosition = _recoilNode.localPosition;
                _nodeBaseScale = _recoilNode.localScale;
            }
            if (_loadedAmmo != null) _ammoBaseScale = _loadedAmmo.localScale;
            _spawnPunchT = 0f; // pop-in on placement
        }

        /// <summary>Windup started — lean back slightly; a fresh arrow pops onto the weapon.</summary>
        public void OnSwing()
        {
            _anticipationTarget = _anticipationDistance;
            if (_loadedAmmo != null && !_loadedAmmo.gameObject.activeSelf)
            {
                _loadedAmmo.gameObject.SetActive(true);
                _ammoPopT = 0f; // nock-in pop
            }
        }

        /// <summary>Shot went out — kick; the nocked arrow "becomes" the projectile.</summary>
        public void OnFire()
        {
            _anticipationTarget = 0f;
            _recoilOffset = _recoilDistance;
            if (_scalePunchMode) _scalePunch = 1f;
            if (_loadedAmmo != null) _loadedAmmo.gameObject.SetActive(false);
        }

        private void Update()
        {
            float dt = _clock != null ? _clock.ScaledDeltaTime : Time.deltaTime;
            if (dt <= 0f) return;

            TickSpawnPunch(dt);
            TickYaw(dt);
            TickRecoil(dt);
            TickAmmoPop(dt);
        }

        private void TickAmmoPop(float dt)
        {
            if (_loadedAmmo == null || _ammoPopT >= 1f || !_loadedAmmo.gameObject.activeSelf) return;
            _ammoPopT = Mathf.Min(1f, _ammoPopT + dt / 0.12f);
            _loadedAmmo.localScale = _ammoBaseScale * EaseOutBack(_ammoPopT);
        }

        private void TickSpawnPunch(float dt)
        {
            if (_spawnPunchT >= 1f) return;
            _spawnPunchT = Mathf.Min(1f, _spawnPunchT + dt / _spawnPunchSeconds);
            transform.localScale = _rootBaseScale * EaseOutBack(_spawnPunchT);
        }

        private void TickYaw(float dt)
        {
            if (_yawPivot == null || _targetGetter == null) return;
            var target = _targetGetter();
            if (target == null || !target.IsAlive) return;

            Vector3 dir = RangeMath.PlanarDirection(_yawPivot.position, target.Position);
            var desired = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, _yawModelOffsetDegrees, 0f);
            _yawPivot.rotation = Quaternion.RotateTowards(_yawPivot.rotation, desired, _turnDegreesPerSecond * dt);
        }

        private void TickRecoil(float dt)
        {
            if (_recoilNode == null) return;

            _recoilOffset = Mathf.Lerp(_recoilOffset, 0f, 1f - Mathf.Exp(-_recoilRecoverSharpness * dt));
            _anticipation = Mathf.MoveTowards(_anticipation, _anticipationTarget, dt * 0.35f);

            if (_scalePunchMode)
            {
                _scalePunch = Mathf.Lerp(_scalePunch, 0f, 1f - Mathf.Exp(-_recoilRecoverSharpness * dt));
                float squash = _anticipation / Mathf.Max(0.01f, _anticipationDistance); // 0..1 charge
                float y = 1f - 0.12f * squash + 0.28f * _scalePunch;
                float xz = 1f + 0.08f * squash - 0.12f * _scalePunch;
                _recoilNode.localScale = new Vector3(_nodeBaseScale.x * xz, _nodeBaseScale.y * y, _nodeBaseScale.z * xz);
            }
            else
            {
                // Pull back along the barrel's local -Z: anticipation is slow, recoil snaps.
                _recoilNode.localPosition = _recoilBasePosition - Vector3.forward * (_recoilOffset + _anticipation);
            }
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}

using System;
using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Core;
using RoyalSiege.Juice;

namespace RoyalSiege.Buildings
{
    /// <summary>
    /// 18-Jul delta: the Royal Tower's attack fires from a MORTAR on the roof; the King is
    /// static decoration (idle only). Purely visual — targeting/damage stay in StructureAttack,
    /// which drives this through the same hooks KingView used (OnSwing / OnFire / FirePoint).
    /// Behaviour: heavy yaw-tracking toward the current target, barrel charge-dip on windup,
    /// snap recoil + muzzle flash + small camera shake on fire. The mount must have UNIFORM
    /// world scale (counter-scaled holder under the tower's non-uniform root) or yawing shears.
    /// </summary>
    public sealed class MortarView : MonoBehaviour
    {
        [Header("Nodes")]
        [Tooltip("Rotates to face the target (this transform if empty).")]
        [SerializeField] private Transform _yawPivot;
        [Tooltip("Kicks back/down on fire (spring recovery).")]
        [SerializeField] private Transform _barrel;
        [Tooltip("World point the shell leaves from (the tube mouth).")]
        [SerializeField] private Transform _muzzle;

        [Header("Feel")]
        [SerializeField] private float _turnDegreesPerSecond = 240f;  // heavy piece, slower than turrets
        [SerializeField] private float _yawModelOffsetDegrees;
        [Tooltip("Facing when idle — default: toward the camera/south.")]
        [SerializeField] private Vector3 _idleFacing = Vector3.back;
        [SerializeField] private float _recoilDistance = 0.14f;
        [SerializeField] private float _recoilRecoverSharpness = 8f;
        [Tooltip("Windup charge: the barrel dips down slightly before the shot.")]
        [SerializeField] private float _chargeDip = 0.05f;
        [Tooltip("Camera trauma added per shot (0 = none).")]
        [SerializeField] private float _fireShake = 0.18f;
        [Tooltip("Optional muzzle flash, played at the muzzle on every shot.")]
        [SerializeField] private ParticleSystem _muzzleFlash;

        private IClock _clock;
        private Func<IEnemyTarget> _targetGetter;
        private CameraShaker _shaker;
        private Vector3 _barrelBasePosition;
        private float _recoilOffset;
        private float _charge;          // 0..1 windup dip
        private float _chargeTarget;

        /// <summary>World-space point the shell spawns at (the tube mouth).</summary>
        public Vector3 FirePoint => _muzzle != null ? _muzzle.position
            : (_barrel != null ? _barrel.position + Vector3.up * 0.5f : transform.position + Vector3.up * 0.5f);

        public void Init(IClock clock, Func<IEnemyTarget> targetGetter)
        {
            _clock = clock;
            _targetGetter = targetGetter;
            if (_yawPivot == null) _yawPivot = transform;
            if (_barrel != null) _barrelBasePosition = _barrel.localPosition;
            _shaker = Camera.main != null ? Camera.main.GetComponent<CameraShaker>() : null;
        }

        /// <summary>Windup started — charge: the tube dips as the crew loads.</summary>
        public void OnSwing(float attackPeriod) => _chargeTarget = 1f;

        /// <summary>Shell went out — snap recoil, muzzle flash, thump the camera.</summary>
        public void OnFire()
        {
            _chargeTarget = 0f;
            _recoilOffset = _recoilDistance;
            if (_muzzleFlash != null)
            {
                _muzzleFlash.transform.position = FirePoint;
                _muzzleFlash.Play(true);
            }
            if (_fireShake > 0f) _shaker?.AddTrauma(_fireShake);
        }

        private void Update()
        {
            float dt = _clock != null ? _clock.ScaledDeltaTime : Time.deltaTime;
            if (dt <= 0f) return;

            // Heavy yaw toward the target; settle back to the idle facing when none.
            var target = _targetGetter?.Invoke();
            Vector3 facing = target != null && target.IsAlive
                ? RangeMath.PlanarDirection(_yawPivot.position, target.Position)
                : _idleFacing;
            var desired = Quaternion.LookRotation(facing) * Quaternion.Euler(0f, _yawModelOffsetDegrees, 0f);
            _yawPivot.rotation = Quaternion.RotateTowards(_yawPivot.rotation, desired, _turnDegreesPerSecond * dt);

            if (_barrel == null) return;
            _recoilOffset = Mathf.Lerp(_recoilOffset, 0f, 1f - Mathf.Exp(-_recoilRecoverSharpness * dt));
            _charge = Mathf.MoveTowards(_charge, _chargeTarget, dt * 2.2f);
            // Charge dips the tube straight down; recoil kicks back along local -Z and down.
            _barrel.localPosition = _barrelBasePosition
                + Vector3.down * (_chargeDip * _charge + _recoilOffset * 0.5f)
                - Vector3.forward * _recoilOffset;
        }
    }
}

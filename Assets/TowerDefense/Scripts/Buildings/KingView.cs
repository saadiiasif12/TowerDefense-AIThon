using System;
using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Core;
using RoyalSiege.Units;

namespace RoyalSiege.Buildings
{
    /// <summary>
    /// Purely-visual king on the Royal Tower: idles until the king attack has a target,
    /// smoothly turns to face it, and plays the throw animation in sync with the attack
    /// cycle (damage timing stays in StructureAttack — this never touches gameplay).
    /// </summary>
    public sealed class KingView : MonoBehaviour
    {
        [SerializeField] private float _turnDegreesPerSecond = 420f;
        [SerializeField] private float _yawModelOffsetDegrees;
        [Tooltip("Facing when idle (no target) — default: toward the camera/south.")]
        [SerializeField] private Vector3 _idleFacing = Vector3.back;
        [Tooltip("Bone the magic bolt is cast from. Auto-found by name if left empty.")]
        [SerializeField] private Transform _castHand;

        private const string CastHandBoneName = "RightHand";

        private IClock _clock;
        private Func<IEnemyTarget> _targetGetter;
        private UnitAnimator _animator;

        /// <summary>World-space point the projectile should leave from (the casting hand).</summary>
        public Vector3 CastPoint => _castHand != null
            ? _castHand.position
            : transform.position + Vector3.up * 1.5f;

        private void Awake()
        {
            if (_castHand != null) return;
            foreach (var t in GetComponentsInChildren<Transform>())
                if (t.name == CastHandBoneName) { _castHand = t; break; }
        }

        public void Init(IClock clock, Func<IEnemyTarget> targetGetter)
        {
            _clock = clock;
            _targetGetter = targetGetter;
            _animator = GetComponent<UnitAnimator>();
        }

        /// <summary>Throw started — play the cast animation scaled to the attack period.</summary>
        public void OnSwing(float attackPeriod) => _animator?.PlayAttack(attackPeriod);

        private void Update()
        {
            if (_clock == null) return;
            float dt = _clock.ScaledDeltaTime;
            if (dt <= 0f) return;

            _animator?.SetPlaybackSpeed(_clock.IsPaused ? 0f : _clock.SpeedMultiplier);

            var target = _targetGetter?.Invoke();
            Vector3 facing = target != null && target.IsAlive
                ? RangeMath.PlanarDirection(transform.position, target.Position)
                : _idleFacing;

            var desired = Quaternion.LookRotation(facing) * Quaternion.Euler(0f, _yawModelOffsetDegrees, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, _turnDegreesPerSecond * dt);
        }
    }
}

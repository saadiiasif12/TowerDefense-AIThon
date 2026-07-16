using UnityEngine;

namespace RoyalSiege.Units
{
    /// <summary>
    /// Thin Animator wrapper keeping animation synchronized with combat timing:
    /// PlayAttack scales the clip so exactly one swing fits the attack period, and the
    /// AttackCycle fires damage at impactFraction of that same period.
    /// All methods are safe on prefabs with no Animator (dummy placeholders).
    /// Animator contract: bool "Moving", trigger "Attack", trigger "Die", float "AttackSpeed"
    /// (attack state speed multiplier), float "MoveSpeed" (walk state speed multiplier).
    /// </summary>
    public sealed class UnitAnimator : MonoBehaviour
    {
        private static readonly int MovingHash = Animator.StringToHash("Moving");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int DieHash = Animator.StringToHash("Die");
        private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");
        private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");

        [SerializeField] private Animator _animator;
        [Tooltip("Length (s) of the attack clip at speed 1 — used to fit one swing into the attack period.")]
        [SerializeField] private float _attackClipLength = 1f;
        [Tooltip("Move speed (tiles/s) at which the walk clip plays at speed 1.")]
        [SerializeField] private float _walkClipReferenceSpeed = 2f;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();

            // Auto-detect the attack clip length from the controller so damage timing and
            // the visible swing stay synchronized whatever clip the character uses.
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                foreach (var clip in _animator.runtimeAnimatorController.animationClips)
                {
                    if (clip != null && clip.name.ToLowerInvariant().Contains("attack"))
                    {
                        _attackClipLength = clip.length;
                        break;
                    }
                }
            }
        }

        public void SetMoving(bool moving, float speed = 0f)
        {
            if (_animator == null) return;
            _animator.SetBool(MovingHash, moving);
            // Freeze the walk pose when idle (no dedicated idle clip in the shared set yet).
            _animator.SetFloat(MoveSpeedHash, moving ? Mathf.Max(0.1f, speed / _walkClipReferenceSpeed) : 0f);
        }

        public void PlayAttack(float attackPeriod)
        {
            if (_animator == null) return;
            _animator.SetFloat(AttackSpeedHash, _attackClipLength / Mathf.Max(0.05f, attackPeriod));
            _animator.SetTrigger(AttackHash);
        }

        public void PlayDie()
        {
            if (_animator == null) return;
            _animator.SetTrigger(DieHash);
        }

        /// <summary>Reset for pool reuse.</summary>
        public void Rebind()
        {
            if (_animator == null) return;
            _animator.Rebind();
            _animator.Update(0f);
        }

        /// <summary>Pause/speed-aware playback (visuals must follow the game clock).</summary>
        public void SetPlaybackSpeed(float multiplier)
        {
            if (_animator == null) return;
            _animator.speed = multiplier;
        }
    }
}

using UnityEngine;

namespace RoyalSiege.Units
{
    /// <summary>
    /// Thin Animator wrapper keeping animation synchronized with combat and movement:
    /// - Walk playback is scaled by ACTUAL ground speed against the clip's own root-motion
    ///   speed (read from clip.averageSpeed), so feet grip the ground — no sliding.
    /// - Start/stop is pose-blended through the "Locomotion" 1D blend tree on "MoveBlend"
    ///   (0 = near-still sway stand-in for the missing idle clip, 1 = full walk) instead of
    ///   freezing the walk clip mid-stride.
    /// - PlayAttack scales the clip so exactly one swing fits the attack period; AttackCycle
    ///   fires damage at impactFraction of that same period.
    /// - Float params are damped for smooth accelerate/stop blends.
    /// All methods are safe on prefabs with no Animator (dummy placeholders).
    /// Animator contract: bool "Moving", trigger "Attack", trigger "Die", float "AttackSpeed"
    /// (attack state speed multiplier), float "MoveSpeed" (walk state speed multiplier —
    /// parked at 1 while stopped so the sway keeps playing), float "MoveBlend" (blend-tree
    /// position, 0 stopped → 1 walking).
    /// </summary>
    public sealed class UnitAnimator : MonoBehaviour
    {
        private static readonly int MovingHash = Animator.StringToHash("Moving");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int DieHash = Animator.StringToHash("Die");
        private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");
        private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
        private static readonly int MoveBlendHash = Animator.StringToHash("MoveBlend");
        private static readonly int WalkStateHash = Animator.StringToHash("Walk");

        private const float MoveDampSeconds = 0.15f;
        // Slightly softer than the speed damp: this is the visible idle<->walk crossfade.
        private const float BlendDampSeconds = 0.22f;

        [SerializeField] private Animator _animator;
        [Tooltip("Fallback if the walk clip has no root motion: tiles/s the clip visually covers at speed 1. The shared Meshy walk cycle is a slow 3.7 s amble, so this is biased low — fast units cycle their legs faster.")]
        [SerializeField] private float _fallbackWalkClipSpeed = 0.9f;

        private float _attackClipLength = 1f;
        private float _walkClipNaturalSpeed = 1.2f;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null) continue;
                string n = clip.name.ToLowerInvariant();
                if (n.Contains("attack"))
                {
                    _attackClipLength = clip.length;
                }
                else if (n.Contains("walk"))
                {
                    // Root-motion average speed = the ground speed the clip was animated for.
                    float natural = clip.averageSpeed.magnitude;
                    _walkClipNaturalSpeed = natural > 0.05f ? natural : _fallbackWalkClipSpeed;
                }
            }
        }

        /// <param name="groundSpeed">Actual displacement speed this tick (tiles/s) — pass the
        /// REAL velocity magnitude (incl. separation), not the stat, so feet always match.</param>
        public void SetMoving(bool moving, float groundSpeed = 0f)
        {
            if (_animator == null) return;
            _animator.SetBool(MovingHash, moving);

            // Blend-tree position: crossfades the pose between the near-still sway (0) and
            // the walk cycle (1) — no more hard mid-stride freeze on stop.
            _animator.SetFloat(MoveBlendHash, moving ? 1f : 0f, BlendDampSeconds, Time.deltaTime);

            // State speed multiplier keeps feet matched to actual ground speed while walking.
            // While stopped it parks at 1 (NOT 0 — that would freeze the sway too): the tree's
            // idle child carries its own 0.08 timescale.
            float target = moving
                ? Mathf.Clamp(groundSpeed / _walkClipNaturalSpeed, 0.4f, 2.5f)
                : 1f;
            _animator.SetFloat(MoveSpeedHash, target, MoveDampSeconds, Time.deltaTime);
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

        /// <summary>De-sync pack members: start the walk cycle at a given phase (0..1).</summary>
        public void SetWalkPhase(float normalizedPhase)
        {
            if (_animator == null) return;
            _animator.Play(WalkStateHash, 0, Mathf.Repeat(normalizedPhase, 1f));
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

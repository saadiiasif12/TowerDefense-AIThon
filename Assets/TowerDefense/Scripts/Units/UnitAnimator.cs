using System.Collections.Generic;
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
    /// Animator contract: bool "Moving", trigger "Attack", trigger "Die", trigger "Hurt",
    /// float "AttackSpeed" (attack state speed multiplier), float "HurtSpeed" (hurt state
    /// speed multiplier), float "MoveSpeed" (walk state speed multiplier — parked at 1 while
    /// stopped so the sway keeps playing), float "MoveBlend" (blend-tree position,
    /// 0 stopped → 1 walking).
    /// </summary>
    public sealed class UnitAnimator : MonoBehaviour
    {
        private static readonly int MovingHash = Animator.StringToHash("Moving");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int AttackingHash = Animator.StringToHash("Attacking");
        private static readonly int DieHash = Animator.StringToHash("Die");
        private static readonly int HurtHash = Animator.StringToHash("Hurt");
        private static readonly int HurtSpeedHash = Animator.StringToHash("HurtSpeed");
        private static readonly int AttackSpeedHash = Animator.StringToHash("AttackSpeed");
        private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
        private static readonly int MoveBlendHash = Animator.StringToHash("MoveBlend");
        private static readonly int WalkStateHash = Animator.StringToHash("Walk");

        private const float MoveDampSeconds = 0.15f;
        // Slightly softer than the speed damp: this is the visible idle<->walk crossfade.
        private const float BlendDampSeconds = 0.22f;
        // Fraction of the Hurt clip the state plays before its exit transition fires.
        // MUST match the Hurt state's exit-time in AC_Unit.controller (0.35): PlayHurt
        // scales HurtSpeed so exactly this slice fills the requested stagger duration.
        private const float HurtExitFraction = 0.35f;

        [SerializeField] private Animator _animator;
        [Tooltip("Fallback if the walk clip has no root motion: tiles/s the clip visually covers at speed 1. The shared Meshy walk cycle is a slow 3.7 s amble, so this is biased low — fast units cycle their legs faster.")]
        [SerializeField] private float _fallbackWalkClipSpeed = 0.9f;

        private float _attackClipLength = 1f;
        private float _walkClipNaturalSpeed = 1.2f;
        private float _hurtClipLength = 2.4f;
        private float _moveBlendTarget;
        private float _moveSpeedTarget = 1f;
        private float _walkSpeedMultiplier = 1f; // artistic walk-anim speed factor (1 = feet synced)

        // Parameters present on THIS controller. Not every rig has the full contract
        // (AC_King has no MoveBlend/Hurt) — setting a missing param logs a warning
        // every frame, so every Set* below is gated on this.
        private readonly HashSet<int> _availableParams = new();

        private bool Has(int paramHash) => _availableParams.Contains(paramHash);

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            foreach (var parameter in _animator.parameters)
                _availableParams.Add(parameter.nameHash);

            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null) continue;
                string n = clip.name.ToLowerInvariant();
                if (n.Contains("hurt"))
                {
                    _hurtClipLength = clip.length;
                }
                else if (n.Contains("attack"))
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

        /// <summary>
        /// Sets the walk-animation speed factor: 1 = feet synced to ground speed (no slide),
        /// &lt;1 = slower/calmer legs, &gt;1 = busier. Enemies pass GameConfig's global factor ×
        /// their per-def scale; other units (knight/buildings) leave it at 1.
        /// </summary>
        public void ConfigureWalk(float speedMultiplier) =>
            _walkSpeedMultiplier = Mathf.Max(0.05f, speedMultiplier);

        /// <param name="groundSpeed">Actual displacement speed this tick (tiles/s) — pass the
        /// REAL velocity magnitude (incl. separation), not the stat, so feet always match.</param>
        public void SetMoving(bool moving, float groundSpeed = 0f)
        {
            if (_animator == null) return;
            if (Has(MovingHash)) _animator.SetBool(MovingHash, moving);

            // Blend-tree position: crossfades the pose between the near-still sway (0) and
            // the walk cycle (1) — no more hard mid-stride freeze on stop.
            _moveBlendTarget = moving ? 1f : 0f;

            // State speed multiplier keeps feet matched to actual ground speed while walking.
            // While stopped it parks at 1 (NOT 0 — that would freeze the sway too): the tree's
            // idle child carries its own 0.08 timescale.
            _moveSpeedTarget = moving
                ? Mathf.Clamp(groundSpeed / _walkClipNaturalSpeed * _walkSpeedMultiplier, 0.4f, 3.5f)
                : 1f;
        }

        private void Update()
        {
            // Damping must integrate every FRAME. SetMoving arrives at the 10 Hz logic tick;
            // damping there only advances ~10 frame-deltas per second (~0.16 s of progress
            // per real second), which parks MoveBlend near 0 — the near-static sway pose.
            if (_animator == null || Time.deltaTime <= 0f) return;
            if (Has(MoveBlendHash))
                _animator.SetFloat(MoveBlendHash, _moveBlendTarget, BlendDampSeconds, Time.deltaTime);
            if (Has(MoveSpeedHash))
                _animator.SetFloat(MoveSpeedHash, _moveSpeedTarget, MoveDampSeconds, Time.deltaTime);
        }

        /// <summary>
        /// Called on each swing: scales the LOOPING attack state so one swing ≈ one attack
        /// period, clamped so a short period never makes it frantic nor a long one crawl.
        /// Entering/leaving the attack state is driven by <see cref="SetAttacking"/> (bool)
        /// for smooth crossfades — no per-swing trigger, so no restart stutter.
        /// </summary>
        public void PlayAttack(float attackPeriod)
        {
            if (_animator == null || !Has(AttackSpeedHash)) return;
            float scale = Mathf.Clamp(_attackClipLength / Mathf.Max(0.05f, attackPeriod), 0.6f, 1.6f);
            _animator.SetFloat(AttackSpeedHash, scale);
        }

        /// <summary>Hold the attack state (looping) while true; crossfades back to locomotion when false.</summary>
        public void SetAttacking(bool attacking)
        {
            if (_animator == null || !Has(AttackingHash)) return;
            _animator.SetBool(AttackingHash, attacking);
        }

        public void PlayDie()
        {
            if (_animator == null) return;
            if (Has(DieHash)) _animator.SetTrigger(DieHash);
        }

        /// <summary>
        /// Hit-stagger flinch. Scales HurtSpeed so the slice of the Hurt clip the state
        /// actually plays (HurtExitFraction of it) fills exactly staggerSeconds, then the
        /// state crossfades back to Walk on its own — the caller only pauses movement.
        /// Deliberately does NOT touch the walk params: the Hurt state overrides the pose
        /// while it runs, so the exit blends straight back into the mid-stride walk cycle.
        /// </summary>
        public void PlayHurt(float staggerSeconds)
        {
            if (_animator == null || !Has(HurtHash)) return;
            if (Has(HurtSpeedHash))
                _animator.SetFloat(HurtSpeedHash,
                    _hurtClipLength * HurtExitFraction / Mathf.Max(0.05f, staggerSeconds));
            _animator.SetTrigger(HurtHash);
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
            // Snap params to sane spawn values — Rebind resets them to controller defaults,
            // and damping from a stale/zero value would leave the unit posed wrong for a beat.
            _moveBlendTarget = 0f;
            _moveSpeedTarget = 1f;
            if (Has(MoveBlendHash)) _animator.SetFloat(MoveBlendHash, 0f);
            if (Has(MoveSpeedHash)) _animator.SetFloat(MoveSpeedHash, 1f);
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

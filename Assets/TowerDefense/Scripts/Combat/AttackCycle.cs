using System;
using UnityEngine;

namespace RoyalSiege.Combat
{
    /// <summary>
    /// Reusable attack timer shared by ALL attackers (buildings, tower, enemies).
    /// Keeps animation and damage synchronized: onSwing fires at cycle start (play the
    /// attack anim scaled to the attack period), onImpact fires at impactFraction of the
    /// period (deal damage / launch projectile) — the anim hit moment and the damage moment
    /// are the same by construction.
    /// </summary>
    public sealed class AttackCycle
    {
        private readonly float _period;
        private readonly float _impactFraction;
        private readonly Action _onImpact;
        private readonly Action _onSwing;

        private float _cooldownRemaining;
        private float _impactTimer;
        private bool _impactPending;

        /// <summary>True between swing start and the impact moment.</summary>
        public bool IsSwinging => _impactPending;

        public AttackCycle(float attackPeriod, float impactFraction, Action onImpact, Action onSwing = null)
        {
            _period = Mathf.Max(0.05f, attackPeriod);
            _impactFraction = Mathf.Clamp01(impactFraction);
            _onImpact = onImpact;
            _onSwing = onSwing;
        }

        public void Tick(float dt, bool wantsToAttack)
        {
            if (_impactPending)
            {
                _impactTimer -= dt;
                if (_impactTimer <= 0f)
                {
                    _impactPending = false;
                    _onImpact?.Invoke();
                }
            }

            if (_cooldownRemaining > 0f) _cooldownRemaining -= dt;

            if (wantsToAttack && !_impactPending && _cooldownRemaining <= 0f)
            {
                _cooldownRemaining = _period;
                _impactPending = true;
                _impactTimer = _period * _impactFraction;
                _onSwing?.Invoke();
            }
        }

        public void Reset()
        {
            _cooldownRemaining = 0f;
            _impactPending = false;
        }
    }
}

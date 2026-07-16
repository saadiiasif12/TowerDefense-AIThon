using UnityEngine;

namespace RoyalSiege.Combat
{
    /// <summary>
    /// Freeze + stun state for one enemy. Durations REFRESH, never stack (GDD ruling).
    /// Frozen/stunned enemies stop moving and attacking but still take damage.
    /// </summary>
    public sealed class StatusController
    {
        private float _freezeRemaining;
        private float _stunRemaining;

        public bool IsFrozen => _freezeRemaining > 0f;
        public bool IsStunned => _stunRemaining > 0f;
        public bool IsBlocked => IsFrozen || IsStunned;

        public void ApplyFreeze(float seconds) => _freezeRemaining = Mathf.Max(_freezeRemaining, seconds);
        public void ApplyStun(float seconds) => _stunRemaining = Mathf.Max(_stunRemaining, seconds);

        public void Tick(float dt)
        {
            if (_freezeRemaining > 0f) _freezeRemaining -= dt;
            if (_stunRemaining > 0f) _stunRemaining -= dt;
        }

        public void Reset()
        {
            _freezeRemaining = 0f;
            _stunRemaining = 0f;
        }
    }
}

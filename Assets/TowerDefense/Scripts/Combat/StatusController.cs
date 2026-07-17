using UnityEngine;

namespace RoyalSiege.Combat
{
    /// <summary>
    /// Freeze + stun + slow state for one enemy. Durations REFRESH, never stack (GDD ruling).
    /// Frozen/stunned enemies stop moving and attacking but still take damage.
    /// v4: slow (Earthquake) is a movement multiplier; freeze overrides it trivially
    /// (a blocked unit doesn't move at all). Slow strength never stacks — max wins.
    /// </summary>
    public sealed class StatusController
    {
        private float _freezeRemaining;
        private float _stunRemaining;
        private float _slowRemaining;
        private float _slowStrength;

        public bool IsFrozen => _freezeRemaining > 0f;
        public bool IsStunned => _stunRemaining > 0f;
        public bool IsBlocked => IsFrozen || IsStunned;
        /// <summary>Movement speed multiplier (1 = normal, 0.65 = 35% slow).</summary>
        public float MoveFactor => _slowRemaining > 0f ? 1f - _slowStrength : 1f;

        public void ApplyFreeze(float seconds) => _freezeRemaining = Mathf.Max(_freezeRemaining, seconds);
        public void ApplyStun(float seconds) => _stunRemaining = Mathf.Max(_stunRemaining, seconds);

        public void ApplySlow(float strength, float seconds)
        {
            _slowStrength = _slowRemaining > 0f ? Mathf.Max(_slowStrength, strength) : strength;
            _slowRemaining = Mathf.Max(_slowRemaining, seconds);
        }

        public void Tick(float dt)
        {
            if (_freezeRemaining > 0f) _freezeRemaining -= dt;
            if (_stunRemaining > 0f) _stunRemaining -= dt;
            if (_slowRemaining > 0f) _slowRemaining -= dt;
        }

        public void Reset()
        {
            _freezeRemaining = 0f;
            _stunRemaining = 0f;
            _slowRemaining = 0f;
            _slowStrength = 0f;
        }
    }
}

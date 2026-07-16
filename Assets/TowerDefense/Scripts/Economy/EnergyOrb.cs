using System;
using UnityEngine;
using RoyalSiege.Core;

namespace RoyalSiege.Economy
{
    /// <summary>
    /// The visible income: flies from a corpse to the energy bar anchor; the bank is
    /// credited ON ARRIVAL (the delay is the feel of the economy — never credit early).
    /// Pooled; movement respects the game clock (pause/speed).
    /// </summary>
    public sealed class EnergyOrb : MonoBehaviour
    {
        private IClock _clock;
        private Transform _anchor;
        private Action<EnergyOrb, float> _onArrived;
        private Vector3 _start;
        private float _value;
        private float _duration;
        private float _elapsed;
        private bool _active;

        public void Launch(Vector3 from, Transform anchor, float value, float duration,
            IClock clock, Action<EnergyOrb, float> onArrived)
        {
            _start = from + Vector3.up * 0.5f;
            _anchor = anchor;
            _value = value;
            _duration = Mathf.Max(0.05f, duration);
            _clock = clock;
            _onArrived = onArrived;
            _elapsed = 0f;
            _active = true;
            transform.position = _start;
        }

        private void Update()
        {
            if (!_active) return;

            _elapsed += _clock.ScaledDeltaTime;
            float u = Mathf.Clamp01(_elapsed / _duration);

            Vector3 position = Vector3.Lerp(_start, _anchor.position, u);
            position.y += 1.2f * 4f * u * (1f - u); // small arc for readability
            transform.position = position;

            if (u >= 1f)
            {
                _active = false;
                _onArrived(this, _value);
            }
        }
    }
}

using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Core;

namespace RoyalSiege.Buildings
{
    /// <summary>
    /// Target selection for one player attack: closest enemy whose CENTER is strictly
    /// inside range. Keeps the current target until it dies or leaves range (GDD ruling),
    /// then waits retargetDelay (Tesla 0.2 s) before acquiring a new one.
    /// </summary>
    public sealed class RangeTargeter
    {
        private readonly ITargetQuery _query;
        private readonly float _range;
        private readonly float _retargetDelay;
        private float _delayRemaining;

        public IEnemyTarget Current { get; private set; }
        public bool HasTarget => Current != null;

        public RangeTargeter(ITargetQuery query, float range, float retargetDelay = 0f)
        {
            _query = query;
            _range = range;
            _retargetDelay = retargetDelay;
        }

        public void Tick(float dt, Vector3 position)
        {
            if (Current != null &&
                (!Current.IsAlive || !RangeMath.IsInside(position, Current.Position, _range)))
            {
                Current = null;
                _delayRemaining = _retargetDelay;
            }

            if (Current == null)
            {
                if (_delayRemaining > 0f)
                {
                    _delayRemaining -= dt;
                    return;
                }
                Current = _query.ClosestEnemyInRange(position, _range);
            }
        }
    }
}

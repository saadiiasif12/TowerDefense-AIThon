using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Core;
using RoyalSiege.Data;

namespace RoyalSiege.Spells
{
    public interface ISpellCaster
    {
        void Cast(SpellCardSO card, Vector3 point);
    }

    /// <summary>
    /// Gathers valid targets (center inside the spell radius AND inside the map circle —
    /// strict-range rule) and delegates the outcome to the card's SpellEffectSO strategy.
    /// </summary>
    public sealed class SpellCaster : ISpellCaster
    {
        private readonly ITargetQuery _query;
        private readonly GameEvents _events;
        private readonly Vector3 _mapCenter;
        private readonly float _mapRadius;
        private readonly List<IEnemyTarget> _buffer = new();
        private readonly List<IEnemyTarget> _filtered = new();

        public SpellCaster(ITargetQuery query, GameConfigSO config, Vector3 mapCenter, GameEvents events)
        {
            _query = query;
            _events = events;
            _mapCenter = mapCenter;
            _mapRadius = config.mapRadius;
        }

        public void Cast(SpellCardSO card, Vector3 point)
        {
            _query.EnemiesInRadius(point, card.radius, _buffer);

            _filtered.Clear();
            for (int i = 0; i < _buffer.Count; i++)
                if (RangeMath.IsInside(_mapCenter, _buffer[i].Position, _mapRadius))
                    _filtered.Add(_buffer[i]);

            card.effect.Apply(new SpellContext(point, card.radius, _filtered));
            _events.RaiseSpellCast(card, point);
        }
    }
}

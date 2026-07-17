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
    /// 17-Jul delta: cards with fallDelaySeconds > 0 (Arrows/Fireball) resolve that many
    /// seconds AFTER the cast so damage lands exactly when the sky-fall visual does.
    /// Targets are CAPTURED at cast (CR-style: the volley tracks them); the delay is a
    /// fixed constant riding the 10 Hz tick — no RNG, determinism intact.
    /// </summary>
    public sealed class SpellCaster : ISpellCaster, ITickable, ISpellRuntime
    {
        private sealed class PendingCast
        {
            public SpellCardSO Card;
            public Vector3 Point;
            public List<IEnemyTarget> Targets;
            public float Remaining;
        }

        private readonly ITargetQuery _query;
        private readonly GameEvents _events;
        private readonly Vector3 _mapCenter;
        private readonly float _mapRadius;
        private readonly List<IEnemyTarget> _buffer = new();
        private readonly List<PendingCast> _pending = new();
        private readonly List<ISpellZone> _zones = new(); // v4: Earthquake DoT, Log roll
        private readonly Stack<List<IEnemyTarget>> _listPool = new();
        private readonly List<Vector3> _hitReport = new();

        // ---- ISpellRuntime (v4 zone effects) ----
        public ITargetQuery Query => _query;
        public Vector3 MapCenter => _mapCenter;
        public void AddZone(ISpellZone zone) => _zones.Add(zone);

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

            var targets = _listPool.Count > 0 ? _listPool.Pop() : new List<IEnemyTarget>();
            targets.Clear();
            for (int i = 0; i < _buffer.Count; i++)
                if (RangeMath.IsInside(_mapCenter, _buffer[i].Position, _mapRadius))
                    targets.Add(_buffer[i]);

            _events.RaiseSpellCast(card, point); // visuals start now (volley launches)

            if (card.fallDelaySeconds <= 0f)
                Resolve(card, point, targets);
            else
                _pending.Add(new PendingCast { Card = card, Point = point, Targets = targets, Remaining = card.fallDelaySeconds });
        }

        public void Tick(float dt)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var p = _pending[i];
                p.Remaining -= dt;
                if (p.Remaining > 0f) continue;
                _pending.RemoveAt(i);
                Resolve(p.Card, p.Point, p.Targets);
            }

            for (int i = _zones.Count - 1; i >= 0; i--)
                if (!_zones[i].Tick(dt)) _zones.RemoveAt(i);
        }

        private void Resolve(SpellCardSO card, Vector3 point, List<IEnemyTarget> targets)
        {
            // Targets killed during the fall are skipped (Health ignores the dead anyway).
            // QA 17-Jul (DT-005/006): targets that WALKED OUT of the radius during the fall
            // are spared too — damage may only ever land inside the drawn circle.
            targets.RemoveAll(t => t == null || !t.IsAlive ||
                !RangeMath.IsInside(point, t.Position, card.radius));

            _hitReport.Clear();
            card.effect.Apply(new SpellContext(point, card.radius, targets, _hitReport, this));
            _events.RaiseSpellResolved(card, point, _hitReport);

            targets.Clear();
            _listPool.Push(targets);
        }
    }
}

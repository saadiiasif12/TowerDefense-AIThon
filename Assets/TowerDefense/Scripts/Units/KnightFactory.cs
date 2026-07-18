using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Core;
using RoyalSiege.Data;

namespace RoyalSiege.Units
{
    /// <summary>How many troops are alive — the hand greys the Knights card off this (v4 §2).</summary>
    public interface ITroopRoster
    {
        int ActiveCount { get; }
    }

    public interface IKnightFactory : ITroopRoster
    {
        void Deploy(TroopCardSO card, Vector3 point);
        /// <summary>Checkpoint retry: the field resumes empty (v4 §0.5).</summary>
        void DespawnAll();
    }

    /// <summary>Pooled spawner for the Knights card; owns the active roster count.</summary>
    public sealed class KnightFactory : IKnightFactory
    {
        private readonly Transform _parent;
        private readonly KnightRuntimeDeps _deps;
        private readonly Stack<KnightUnit> _pool = new();
        private readonly List<KnightUnit> _active = new();
        private GameObject _prefab;

        public int ActiveCount => _active.Count;

        public KnightFactory(Transform parent, KnightRuntimeDeps deps)
        {
            _parent = parent;
            _deps = deps;
            _deps.Release = Release;
        }

        public void Deploy(TroopCardSO card, Vector3 point)
        {
            _prefab = card.troopPrefab;
            // The pair lands shoulder to shoulder, perpendicular to the outward direction.
            Vector3 outward = RangeMath.PlanarDirection(_deps.MapCenter, point);
            Vector3 side = Vector3.Cross(Vector3.up, outward).normalized;
            for (int i = 0; i < card.countPerCast; i++)
            {
                float offset = (i - (card.countPerCast - 1) * 0.5f) * (card.unitRadius * 2.4f);
                Vector3 position = point + side * offset;
                KnightUnit knight = _pool.Count > 0 ? _pool.Pop() : Create();
                knight.gameObject.SetActive(true);
                knight.Init(card, position, _deps);
                _active.Add(knight);
                _deps.Events.RaiseKnightSpawned(position);
            }
        }

        public void DespawnAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var knight = _active[i];
                knight.gameObject.SetActive(false);
                _pool.Push(knight);
            }
            _active.Clear();
        }

        private void Release(KnightUnit knight)
        {
            _active.Remove(knight);
            knight.gameObject.SetActive(false);
            _pool.Push(knight);
        }

        private KnightUnit Create()
        {
            var go = Object.Instantiate(_prefab, _parent);
            var knight = go.GetComponent<KnightUnit>();
            if (knight == null) knight = go.AddComponent<KnightUnit>();
            return knight;
        }
    }
}

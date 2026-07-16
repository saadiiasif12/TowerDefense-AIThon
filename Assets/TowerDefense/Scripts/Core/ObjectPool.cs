using System.Collections.Generic;
using UnityEngine;

namespace RoyalSiege.Core
{
    /// <summary>Simple component pool. Instances are deactivated on release, reactivated on get.</summary>
    public sealed class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _free = new();

        public ObjectPool(T prefab, Transform parent, int prewarm = 0)
        {
            _prefab = prefab;
            _parent = parent;
            for (int i = 0; i < prewarm; i++) Release(CreateNew());
        }

        public T Get()
        {
            T item = _free.Count > 0 ? _free.Pop() : CreateNew();
            item.gameObject.SetActive(true);
            return item;
        }

        public void Release(T item)
        {
            item.gameObject.SetActive(false);
            item.transform.SetParent(_parent, false);
            _free.Push(item);
        }

        private T CreateNew() => Object.Instantiate(_prefab, _parent);
    }
}

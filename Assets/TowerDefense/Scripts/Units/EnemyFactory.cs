using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Data;

namespace RoyalSiege.Units
{
    public interface IEnemyFactory
    {
        EnemyAgent Spawn(EnemyDefinitionSO definition, Vector3 position, int waveIndex);
    }

    /// <summary>
    /// Factory + pool: one pool per enemy definition (101 enemies per match — pooling is mandatory).
    /// Missing EnemyAgent/UnitAnimator components are added to the INSTANCE, never the prefab asset.
    /// </summary>
    public sealed class EnemyFactory : IEnemyFactory
    {
        private readonly Dictionary<EnemyDefinitionSO, Stack<EnemyAgent>> _pools = new();
        private readonly Transform _parent;
        private readonly EnemyRuntimeDeps _deps;

        public EnemyFactory(Transform parent, EnemyRuntimeDeps deps)
        {
            _parent = parent;
            _deps = deps;
            _deps.Release = Release;
        }

        public EnemyAgent Spawn(EnemyDefinitionSO definition, Vector3 position, int waveIndex)
        {
            if (!_pools.TryGetValue(definition, out var pool))
            {
                pool = new Stack<EnemyAgent>();
                _pools[definition] = pool;
            }

            EnemyAgent agent = pool.Count > 0 ? pool.Pop() : CreateInstance(definition);
            agent.gameObject.SetActive(true);
            agent.Init(definition, position, waveIndex, _deps);
            _deps.Events.RaiseEnemySpawned(definition, position);
            return agent;
        }

        private void Release(EnemyAgent agent)
        {
            agent.gameObject.SetActive(false);
            agent.transform.SetParent(_parent, false);
            _pools[agent.Definition].Push(agent);
        }

        private EnemyAgent CreateInstance(EnemyDefinitionSO definition)
        {
            var go = Object.Instantiate(definition.prefab, _parent);
            if (!Mathf.Approximately(definition.modelScale, 1f))
                go.transform.localScale *= definition.modelScale; // v4: type-2 variants read bigger
            var agent = go.GetComponent<EnemyAgent>();
            if (agent == null) agent = go.AddComponent<EnemyAgent>();
            if (go.GetComponent<UnitAnimator>() == null) go.AddComponent<UnitAnimator>();
            return agent;
        }
    }
}

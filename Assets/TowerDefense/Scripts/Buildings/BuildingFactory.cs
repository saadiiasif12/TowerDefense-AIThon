using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Core;
using RoyalSiege.Data;

namespace RoyalSiege.Buildings
{
    public interface IBuildingFactory
    {
        BuildingUnit Place(BuildingCardSO card, Vector3 position);
    }

    public sealed class BuildingFactory : IBuildingFactory
    {
        private readonly Transform _parent;
        private readonly ITargetRegistry _registry;
        private readonly IProjectileLauncher _launcher;
        private readonly GameEvents _events;
        private readonly ITicker _ticker;
        private readonly IClock _clock;

        public BuildingFactory(Transform parent, ITargetRegistry registry,
            IProjectileLauncher launcher, GameEvents events, ITicker ticker, IClock clock)
        {
            _parent = parent;
            _registry = registry;
            _launcher = launcher;
            _events = events;
            _ticker = ticker;
            _clock = clock;
        }

        public BuildingUnit Place(BuildingCardSO card, Vector3 position)
        {
            var go = Object.Instantiate(card.buildingPrefab, position, Quaternion.identity, _parent);
            var unit = go.GetComponent<BuildingUnit>();
            if (unit == null) unit = go.AddComponent<BuildingUnit>();
            unit.Init(card, _registry, _launcher, _events, _ticker, _clock);
            _events.RaiseBuildingPlaced(unit);
            return unit;
        }
    }
}

using UnityEngine;
using RoyalSiege.Combat;
using RoyalSiege.Core;
using RoyalSiege.Data;

namespace RoyalSiege.Placement
{
    /// <summary>
    /// Spatial placement rules (GDD §3): buildings inside the deployment circle, snapped to
    /// the grid, no overlap, max 4 on field. Spells anywhere inside the map circle.
    /// Affordability/cooldown belong to CardPlayService, not here (SRP).
    /// </summary>
    public sealed class PlacementValidator
    {
        private readonly GameConfigSO _config;
        private readonly ITargetQuery _query;
        private readonly Vector3 _center;

        public PlacementValidator(GameConfigSO config, ITargetQuery query, Vector3 mapCenter)
        {
            _config = config;
            _query = query;
            _center = mapCenter;
        }

        public Vector3 Snap(Vector3 point)
        {
            float snap = _config.placementSnap;
            return new Vector3(
                Mathf.Round(point.x / snap) * snap,
                0f,
                Mathf.Round(point.z / snap) * snap);
        }

        public bool IsValidBuildingSpot(BuildingCardSO card, Vector3 snappedPoint)
        {
            if (!RangeMath.IsInside(_center, snappedPoint, _config.deploymentRadius)) return false;
            if (_query.BuildingCount >= _config.maxPlayerBuildings) return false;
            return !Overlaps(snappedPoint, card.footprintRadius);
        }

        public bool IsValidSpellSpot(Vector3 point) =>
            RangeMath.IsInside(_center, point, _config.mapRadius);

        private bool Overlaps(Vector3 point, float footprint)
        {
            var structures = _query.Structures;
            for (int i = 0; i < structures.Count; i++)
            {
                var s = structures[i];
                if (!s.IsAlive) continue;
                if (RangeMath.PlanarDistance(point, s.Position) < footprint + s.FootprintRadius)
                    return true;
            }
            return false;
        }
    }
}

using UnityEngine;
using RoyalSiege.Buildings;
using RoyalSiege.Cards;
using RoyalSiege.Data;
using RoyalSiege.Spells;
using RoyalSiege.Units;

namespace RoyalSiege.Placement
{
    /// <summary>
    /// Drag-to-play flow (GDD §3): ghost hovers ghostOffsetTiles above the fingertip
    /// (toward screen-up), buildings snap to the grid with green/red validity, spells
    /// preview their exact radius. Cost/cooldown are validated at RELEASE via
    /// CardPlayService — a failed release returns the card with nothing spent.
    /// Driven by the hand UI (or DebugInputDriver): BeginDrag / UpdateDrag / EndDrag.
    /// </summary>
    public sealed class PlacementController : MonoBehaviour
    {
        [SerializeField] private GhostView _buildingGhost;
        [SerializeField] private GhostView _spellGhost;
        [Tooltip("Optional: snap-grid overlay shown while dragging a building.")]
        [SerializeField] private GridOverlayView _gridOverlay;
        [Tooltip("Optional: the dashed deployment circle — brightened while dragging (18-Jul).")]
        [SerializeField] private Buildings.DeployRingView _deployRing;

        private Camera _camera;
        private ICardPlayService _playService;
        private PlacementValidator _validator;
        private IBuildingFactory _buildingFactory;
        private ISpellCaster _spellCaster;
        private IKnightFactory _knightFactory;
        private GameConfigSO _config;

        private int _slot = -1;
        private Vector3 _point;
        private bool _isValid;

        public bool IsDragging => _slot >= 0;

        public void Init(Camera camera, ICardPlayService playService, PlacementValidator validator,
            IBuildingFactory buildingFactory, ISpellCaster spellCaster, GameConfigSO config,
            Vector3 mapCenter, IKnightFactory knightFactory = null)
        {
            _camera = camera;
            _playService = playService;
            _validator = validator;
            _buildingFactory = buildingFactory;
            _spellCaster = spellCaster;
            _knightFactory = knightFactory;
            _config = config;
            _buildingGhost?.Hide();
            _spellGhost?.Hide();
            _buildingGhost?.SetMapClip(mapCenter, config.mapRadius);
            _spellGhost?.SetMapClip(mapCenter, config.mapRadius);
            _gridOverlay?.Init(config.deploymentRadius, config.placementSnap);
        }

        public void BeginDrag(int slot)
        {
            if (IsDragging) return; // single-touch lock (GDD ruling)
            var card = _playService.CardAt(slot);
            if (card == null) return;

            _slot = slot;
            var ghost = GhostFor(card);
            float radius = card switch
            {
                BuildingCardSO b => b.range,
                SpellCardSO s => s.radius,
                TroopCardSO t => t.unitRadius * 3f, // small deploy-spread marker
                _ => 1f
            };
            ghost?.Show(radius, card.displayName); // 18-Jul: name floats above the preview
            if (card is BuildingCardSO or TroopCardSO) _gridOverlay?.Show();
            _deployRing?.SetDragHighlight(true);   // circle brightens while a card is held
        }

        /// <summary>
        /// 18-Jul CR-style handoff: the hand tells us whether the pointer is over the
        /// battlefield (preview visible) or still over the HUD (floating card visible).
        /// Pure view — validation and commit are untouched.
        /// </summary>
        public void SetFieldHover(bool overField)
        {
            if (!IsDragging) return;
            GhostFor(_playService.CardAt(_slot))?.SetFieldVisible(overField);
        }

        public void UpdateDrag(Vector2 screenPosition)
        {
            if (!IsDragging) return;

            var card = _playService.CardAt(_slot);
            _point = ScreenToGround(screenPosition) + GhostWorldOffset();

            if (card is BuildingCardSO building)
            {
                _point = _validator.Snap(_point);
                _isValid = _validator.IsValidBuildingSpot(building, _point) && _playService.CanPlay(_slot);
            }
            else if (card is TroopCardSO)
            {
                _point = _validator.Snap(_point);
                _isValid = _validator.IsValidTroopSpot(_point) && _playService.CanPlay(_slot);
            }
            else
            {
                _isValid = _validator.IsValidSpellSpot((SpellCardSO)card, _point) && _playService.CanPlay(_slot);
            }

            var ghost = GhostFor(card);
            if (ghost != null)
            {
                ghost.SetPosition(_point);
                ghost.SetValid(_isValid);
            }
        }

        public void EndDrag(Vector2 screenPosition)
        {
            if (!IsDragging) return;
            UpdateDrag(screenPosition);

            var card = _playService.CardAt(_slot);
            if (_isValid && _playService.TryCommitPlay(_slot))
            {
                switch (card)
                {
                    case BuildingCardSO building: _buildingFactory.Place(building, _point); break;
                    case TroopCardSO troop: _knightFactory?.Deploy(troop, _point); break;
                    case SpellCardSO spell: _spellCaster.Cast(spell, _point); break;
                }
                // 18-Jul commit confirm: the successful ghost freezes, pulses and fades out
                // on its own; everything else clears immediately.
                var committed = GhostFor(card);
                _slot = -1;
                if (committed == _buildingGhost) _spellGhost?.Hide(); else _buildingGhost?.Hide();
                _gridOverlay?.Hide();
                _deployRing?.SetDragHighlight(false);
                committed?.PlayCommitConfirm();
                return;
            }
            CancelDrag();
        }

        public void CancelDrag()
        {
            _slot = -1;
            _buildingGhost?.Hide();
            _spellGhost?.Hide();
            _gridOverlay?.Hide();
            _deployRing?.SetDragHighlight(false);
        }

        private GhostView GhostFor(CardDefinitionSO card) =>
            card is BuildingCardSO ? _buildingGhost : _spellGhost;

        private Vector3 ScreenToGround(Vector2 screenPosition)
        {
            var ray = _camera.ScreenPointToRay(screenPosition);
            var ground = new Plane(Vector3.up, Vector3.zero);
            return ground.Raycast(ray, out float distance) ? ray.GetPoint(distance) : Vector3.zero;
        }

        /// <summary>Ghost sits ghostOffsetTiles above the fingertip, along screen-up projected onto the ground.</summary>
        private Vector3 GhostWorldOffset()
        {
            var up = Vector3.ProjectOnPlane(_camera.transform.up, Vector3.up).normalized;
            return up * _config.ghostOffsetTiles;
        }
    }
}

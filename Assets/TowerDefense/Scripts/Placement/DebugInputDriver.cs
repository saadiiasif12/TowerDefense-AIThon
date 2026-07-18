using UnityEngine;
using UnityEngine.InputSystem;

namespace RoyalSiege.Placement
{
    /// <summary>
    /// TEMPORARY editor/desktop input until the hand-bar UI lands (Day 2):
    /// hold 1–4 hand-slot keys is not needed — press 1–4 to pick up that card, move the
    /// mouse to aim, left-click to play, right-click/Esc to cancel.
    /// Delete this component when HandBarView provides real drag input.
    /// </summary>
    public sealed class DebugInputDriver : MonoBehaviour
    {
        [SerializeField] private PlacementController _placement;

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null || _placement == null) return;

            if (!_placement.IsDragging)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) _placement.BeginDrag(0);
                else if (keyboard.digit2Key.wasPressedThisFrame) _placement.BeginDrag(1);
                else if (keyboard.digit3Key.wasPressedThisFrame) _placement.BeginDrag(2);
                else if (keyboard.digit4Key.wasPressedThisFrame) _placement.BeginDrag(3);
                return;
            }

            _placement.UpdateDrag(mouse.position.ReadValue());

            if (mouse.leftButton.wasPressedThisFrame)
                _placement.EndDrag(mouse.position.ReadValue());
            else if (mouse.rightButton.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                _placement.CancelDrag();
        }
    }
}

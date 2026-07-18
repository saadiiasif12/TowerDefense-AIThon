using UnityEngine;

namespace RoyalSiege.Data
{
    /// <summary>Base card. Deck/hand/cooldown/cost code only ever sees this type (LSP).</summary>
    public abstract class CardDefinitionSO : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;
        [Min(0f)] public float cost = 3f;
        [Tooltip("Per-CARD cooldown (lives on the card, not the hand slot).")]
        [Min(0f)] public float cooldown = 8f;
    }
}

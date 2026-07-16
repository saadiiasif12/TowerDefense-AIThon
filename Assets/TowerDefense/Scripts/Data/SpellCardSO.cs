using UnityEngine;

namespace RoyalSiege.Data
{
    [CreateAssetMenu(menuName = "RoyalSiege/Cards/Spell Card", fileName = "Card_")]
    public sealed class SpellCardSO : CardDefinitionSO
    {
        [Header("Spell")]
        [Min(0f)] public float radius = 4f;
        [Tooltip("Strategy: what happens to enemies caught in the radius.")]
        public SpellEffectSO effect;
    }
}

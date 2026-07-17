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
        [Tooltip("Seconds between cast and the effect landing — matches the sky-fall visual " +
                 "(17-Jul delta: Arrows/Fireball fall from the sky). Targets are captured at " +
                 "CAST; 0 = instant (Freeze/Lightning).")]
        [Min(0f)] public float fallDelaySeconds;
    }
}

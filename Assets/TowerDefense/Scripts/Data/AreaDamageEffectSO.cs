using UnityEngine;

namespace RoyalSiege.Data
{
    /// <summary>Arrows (80) / Fireball (260). Damage must stay BELOW its counter-target's HP (no one-shots).</summary>
    [CreateAssetMenu(menuName = "RoyalSiege/Spell Effects/Area Damage", fileName = "Effect_AreaDamage_")]
    public sealed class AreaDamageEffectSO : SpellEffectSO
    {
        [Min(0f)] public float damage = 80f;

        public override void Apply(in SpellContext context)
        {
            for (int i = 0; i < context.Targets.Count; i++)
                context.Targets[i].TakeDamage(damage);
        }
    }
}

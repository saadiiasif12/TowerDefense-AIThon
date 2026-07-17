using UnityEngine;
using RoyalSiege.Core;

namespace RoyalSiege.Data
{
    /// <summary>
    /// Arrows (90) / Fireball (250 + knockback 0.6). v4 tiered rule: light tier may die to
    /// one cast; damage must stay BELOW every heavy's HP (Fireball 250 &lt; Goblin 2's 320).
    /// </summary>
    [CreateAssetMenu(menuName = "RoyalSiege/Spell Effects/Area Damage", fileName = "Effect_AreaDamage_")]
    public sealed class AreaDamageEffectSO : SpellEffectSO
    {
        [Min(0f)] public float damage = 80f;
        [Tooltip("v4 Fireball: survivors are shoved this far away from the blast point (their knockbackFactor applies).")]
        [Min(0f)] public float knockback;

        public override void Apply(in SpellContext context)
        {
            for (int i = 0; i < context.Targets.Count; i++)
            {
                var target = context.Targets[i];
                target.TakeDamage(damage);
                context.HitReport?.Add(target.Position);
                if (knockback > 0f && target.IsAlive)
                    target.ApplyKnockback(RangeMath.PlanarDirection(context.Point, target.Position) * knockback);
            }
        }
    }
}

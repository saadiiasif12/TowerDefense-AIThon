using UnityEngine;

namespace RoyalSiege.Data
{
    /// <summary>Stops movement AND attacks. Duration refreshes, never stacks. Bosses resist (2 s).</summary>
    [CreateAssetMenu(menuName = "RoyalSiege/Spell Effects/Freeze", fileName = "Effect_Freeze")]
    public sealed class FreezeEffectSO : SpellEffectSO
    {
        [Min(0f)] public float duration = 4f;
        [Min(0f)] public float bossDuration = 2f;

        public override void Apply(in SpellContext context)
        {
            for (int i = 0; i < context.Targets.Count; i++)
            {
                var target = context.Targets[i];
                target.ApplyFreeze(target.IsBoss ? bossDuration : duration);
                context.HitReport?.Add(target.Position);
            }
        }
    }
}

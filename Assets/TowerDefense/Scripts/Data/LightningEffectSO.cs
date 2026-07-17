using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Combat;

namespace RoyalSiege.Data
{
    /// <summary>Hits the N highest-HP enemies in the radius + short stun. The Ogre/boss chunker.</summary>
    [CreateAssetMenu(menuName = "RoyalSiege/Spell Effects/Lightning", fileName = "Effect_Lightning")]
    public sealed class LightningEffectSO : SpellEffectSO
    {
        [Min(0f)] public float damage = 275f;
        [Min(1)] public int targetCount = 3;
        [Min(0f)] public float stunSeconds = 0.5f;

        private static readonly List<IEnemyTarget> Sorted = new();

        public override void Apply(in SpellContext context)
        {
            Sorted.Clear();
            Sorted.AddRange(context.Targets);
            Sorted.Sort((a, b) => b.CurrentHp.CompareTo(a.CurrentHp));

            int hits = Mathf.Min(targetCount, Sorted.Count);
            for (int i = 0; i < hits; i++)
            {
                context.HitReport?.Add(Sorted[i].Position);
                Sorted[i].TakeDamage(damage);
                if (Sorted[i].IsAlive) Sorted[i].ApplyStun(stunSeconds);
            }
        }
    }
}

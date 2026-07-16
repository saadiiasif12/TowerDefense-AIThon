using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Core;
using RoyalSiege.Data;

namespace RoyalSiege.Cards
{
    /// <summary>
    /// Per-CARD cooldowns (they live on the card, not the hand slot — GDD ruling), so a
    /// card can rotate back into the hand while still recharging.
    /// </summary>
    public sealed class CardCooldowns : ITickable
    {
        private readonly Dictionary<CardDefinitionSO, float> _remaining = new();

        public bool IsReady(CardDefinitionSO card) =>
            !_remaining.TryGetValue(card, out float t) || t <= 0f;

        public float Remaining(CardDefinitionSO card) =>
            _remaining.TryGetValue(card, out float t) ? Mathf.Max(0f, t) : 0f;

        /// <summary>0 = ready, 1 = just played. For radial UI.</summary>
        public float Remaining01(CardDefinitionSO card) =>
            card.cooldown <= 0f ? 0f : Remaining(card) / card.cooldown;

        public void StartCooldown(CardDefinitionSO card) => _remaining[card] = card.cooldown;

        public void Tick(float dt)
        {
            // Keys are stable (7 cards); mutate values via key list to avoid enumerator issues.
            if (_remaining.Count == 0) return;
            var keys = ListCache;
            keys.Clear();
            keys.AddRange(_remaining.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                float t = _remaining[keys[i]];
                if (t > 0f) _remaining[keys[i]] = t - dt;
            }
        }

        private static readonly List<CardDefinitionSO> ListCache = new();
    }
}

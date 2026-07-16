using RoyalSiege.Core;
using RoyalSiege.Data;
using RoyalSiege.Economy;

namespace RoyalSiege.Cards
{
    public interface ICardPlayService
    {
        bool CanPlay(int slot);
        CardDefinitionSO CardAt(int slot);
        /// <summary>Spend + cooldown + cycle. Call ONLY after spatial validation succeeded.</summary>
        bool TryCommitPlay(int slot);
    }

    /// <summary>
    /// The single gate for playing a card: affordable ∧ off cooldown. Cost is validated at
    /// RELEASE of the drag (GDD ruling) — i.e. right here at commit time, not at drag start.
    /// </summary>
    public sealed class CardPlayService : ICardPlayService
    {
        private readonly DeckService _deck;
        private readonly CardCooldowns _cooldowns;
        private readonly IEnergyBank _bank;
        private readonly GameEvents _events;

        public CardPlayService(DeckService deck, CardCooldowns cooldowns, IEnergyBank bank, GameEvents events)
        {
            _deck = deck;
            _cooldowns = cooldowns;
            _bank = bank;
            _events = events;
        }

        public CardDefinitionSO CardAt(int slot) => _deck.Hand[slot];

        public bool CanPlay(int slot)
        {
            var card = _deck.Hand[slot];
            return card != null && _cooldowns.IsReady(card) && _bank.CanAfford(card.cost);
        }

        public bool TryCommitPlay(int slot)
        {
            if (!CanPlay(slot)) return false;

            var card = _deck.Hand[slot];
            _bank.TrySpend(card.cost);
            _cooldowns.StartCooldown(card);
            _deck.CyclePlayed(slot);
            _events.RaiseCardPlayed(card);
            return true;
        }
    }
}

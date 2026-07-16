using System;
using System.Collections.Generic;
using RoyalSiege.Core;
using RoyalSiege.Data;

namespace RoyalSiege.Cards
{
    /// <summary>
    /// The deterministic ring buffer (GDD §3): ONE shuffle at match start — the game's only
    /// RNG — then pure FIFO. Hand of 4; playing a card sends it to the back of the queue and
    /// pulls the queue front into the SAME hand slot. 7-card deck → hand 4 + NEXT + 2 hidden.
    /// </summary>
    public sealed class DeckService
    {
        public const int HandSize = 4;

        private readonly CardDefinitionSO[] _hand = new CardDefinitionSO[HandSize];
        private readonly Queue<CardDefinitionSO> _queue = new();
        private readonly GameEvents _events;

        public IReadOnlyList<CardDefinitionSO> Hand => _hand;
        public CardDefinitionSO NextPreview => _queue.Count > 0 ? _queue.Peek() : null;
        public int QueueCount => _queue.Count;

        public DeckService(DeckSO deck, GameEvents events, int seed = 0)
        {
            _events = events;

            if (deck.cards.Count <= HandSize)
                throw new ArgumentException($"Deck must have more than {HandSize} cards (has {deck.cards.Count}).");

            var shuffled = new List<CardDefinitionSO>(deck.cards);
            Shuffle(shuffled, seed == 0 ? Environment.TickCount : seed);

            for (int i = 0; i < HandSize; i++) _hand[i] = shuffled[i];
            for (int i = HandSize; i < shuffled.Count; i++) _queue.Enqueue(shuffled[i]);
        }

        /// <summary>Cycle the played card out of the given slot. Validation happens BEFORE this.</summary>
        public void CyclePlayed(int slot)
        {
            var played = _hand[slot];
            _queue.Enqueue(played);
            _hand[slot] = _queue.Dequeue();
            _events.RaiseHandChanged();
        }

        private static void Shuffle(List<CardDefinitionSO> list, int seed)
        {
            var rng = new Random(seed); // the ONLY RNG in the entire game
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace RoyalSiege.Data
{
    /// <summary>The 7-card deck (3 buildings + 4 spells — 16 Jul meeting delta).</summary>
    [CreateAssetMenu(menuName = "RoyalSiege/Deck", fileName = "Deck_")]
    public sealed class DeckSO : ScriptableObject
    {
        public List<CardDefinitionSO> cards = new();
    }
}

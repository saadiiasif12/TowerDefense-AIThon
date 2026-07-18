using System;
using System.Collections.Generic;
using UnityEngine;
using RoyalSiege.Buildings;
using RoyalSiege.Cards;
using RoyalSiege.Data;

namespace RoyalSiege.Core
{
    /// <summary>Saved journey state (v4 §9): written on checkpoints ONLY, read on scene load.</summary>
    [Serializable]
    public sealed class CampaignProfile
    {
        public int nextWave = 1;
        public int towerLevel = 1;
        public List<string> unlockedCardIds = new();

        public const string Key = "royalDefense.profile.v1";

        public static CampaignProfile Load()
        {
            if (!PlayerPrefs.HasKey(Key)) return new CampaignProfile();
            try { return JsonUtility.FromJson<CampaignProfile>(PlayerPrefs.GetString(Key)) ?? new CampaignProfile(); }
            catch { return new CampaignProfile(); }
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public static void Clear() => PlayerPrefs.DeleteKey(Key);
    }

    /// <summary>
    /// v4 checkpoints (§9): listens for WaveCleared, and when the cleared wave matches a
    /// CheckpointDef it (1) pauses the sim, (2) applies the tower level (full heal) and
    /// enqueues the unlock card, (3) SAVES the profile, (4) raises CheckpointReached for
    /// the level-up / stage-complete screen. The screen calls Dismissed() to resume — the
    /// wave gap was frozen by the pause, so the next wave holds exactly per spec.
    /// </summary>
    public sealed class CheckpointService
    {
        private readonly ProgressionConfigSO _progression;
        private readonly CampaignSO _campaign;
        private readonly RoyalTower _tower;
        private readonly DeckService _deck;
        private readonly GameEvents _events;
        private readonly IClock _clock;
        private readonly GameConfigSO _gameConfig;
        private readonly CampaignProfile _profile;

        public CheckpointService(ProgressionConfigSO progression, CampaignSO campaign, RoyalTower tower,
            DeckService deck, GameConfigSO gameConfig, GameEvents events, IClock clock, CampaignProfile profile)
        {
            _progression = progression;
            _campaign = campaign;
            _tower = tower;
            _deck = deck;
            _gameConfig = gameConfig;
            _events = events;
            _clock = clock;
            _profile = profile;

            _events.WaveCleared += OnWaveCleared;
        }

        private void OnWaveCleared(int globalWave)
        {
            CheckpointDef checkpoint = null;
            for (int i = 0; i < _progression.checkpoints.Count; i++)
                if (_progression.checkpoints[i].afterWave == globalWave) { checkpoint = _progression.checkpoints[i]; break; }
            if (checkpoint == null) return;

            // Apply rewards BEFORE saving: the checkpoint stores the earned state.
            // Loop-safe: on repeat loops the tower is already at/above the checkpoint's level —
            // never downgrade, always keep the full-heal mercy (level-ups heal, stage
            // transitions heal, repeat checkpoints heal — §9 + loop ruling).
            int targetLevel = Mathf.Clamp(Mathf.Max(checkpoint.towerLevel, _tower.Level), 1, _progression.towerLevels.Count);
            bool leveledUp = targetLevel > _tower.Level;
            _tower.ApplyLevel(targetLevel, _progression.towerLevels[targetLevel - 1]);

            // Loop-safe: a card unlocks ONCE — repeat loops must not enqueue a duplicate.
            bool newUnlock = checkpoint.unlockCard != null
                && !_profile.unlockedCardIds.Contains(checkpoint.unlockCard.id);
            if (newUnlock)
            {
                _deck.AddCard(checkpoint.unlockCard);
                _profile.unlockedCardIds.Add(checkpoint.unlockCard.id);
            }

            int nextWave = globalWave + 1;
            if (_campaign.loopStages && nextWave > _campaign.TotalWaves) nextWave = 1; // wrap the save too
            _profile.nextWave = nextWave;
            _profile.towerLevel = _tower.Level;
            _profile.Save();

            // Repeat-loop level-up checkpoints have nothing new to show: silent save + heal,
            // no pause, no modal. Stage-complete screens always show (stars + rhythm break).
            if (!checkpoint.isStageComplete && !leveledUp && !newUnlock) return;

            int stars = 1;
            if (checkpoint.isStageComplete)
            {
                float hpPct = _tower.HpPct;
                if (hpPct >= _gameConfig.twoStarHpPct) stars = 2;
                if (hpPct >= _gameConfig.threeStarHpPct) stars = 3;
            }

            _clock.IsPaused = true; // full sim pause — decay, orbs, gap all freeze (§9)
            _events.RaiseCheckpointReached(new CheckpointReachedArgs(
                globalWave, leveledUp ? targetLevel : 0, newUnlock ? checkpoint.unlockCard : null,
                checkpoint.isStageComplete, stars,
                dismissed: () => _clock.IsPaused = false));
        }
    }
}

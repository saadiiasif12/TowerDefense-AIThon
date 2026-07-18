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
        private readonly RoyalTower _tower;
        private readonly DeckService _deck;
        private readonly GameEvents _events;
        private readonly IClock _clock;
        private readonly GameConfigSO _gameConfig;
        private readonly CampaignProfile _profile;

        public CheckpointService(ProgressionConfigSO progression, RoyalTower tower, DeckService deck,
            GameConfigSO gameConfig, GameEvents events, IClock clock, CampaignProfile profile)
        {
            _progression = progression;
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
            if (checkpoint.towerLevel > 0 && checkpoint.towerLevel <= _progression.towerLevels.Count)
                _tower.ApplyLevel(checkpoint.towerLevel, _progression.towerLevels[checkpoint.towerLevel - 1]);
            else if (checkpoint.isStageComplete)
                _tower.ApplyLevel(_tower.Level, _progression.towerLevels[_tower.Level - 1]); // stage transition fully heals (§9 ruling)

            if (checkpoint.unlockCard != null)
            {
                _deck.AddCard(checkpoint.unlockCard);
                if (!_profile.unlockedCardIds.Contains(checkpoint.unlockCard.id))
                    _profile.unlockedCardIds.Add(checkpoint.unlockCard.id);
            }

            _profile.nextWave = globalWave + 1;
            _profile.towerLevel = _tower.Level;
            _profile.Save();

            int stars = 1;
            if (checkpoint.isStageComplete)
            {
                float hpPct = _tower.HpPct;
                if (hpPct >= _gameConfig.twoStarHpPct) stars = 2;
                if (hpPct >= _gameConfig.threeStarHpPct) stars = 3;
            }

            _clock.IsPaused = true; // full sim pause — decay, orbs, gap all freeze (§9)
            _events.RaiseCheckpointReached(new CheckpointReachedArgs(
                globalWave, checkpoint.towerLevel, checkpoint.unlockCard,
                checkpoint.isStageComplete, stars,
                dismissed: () => _clock.IsPaused = false));
        }
    }
}

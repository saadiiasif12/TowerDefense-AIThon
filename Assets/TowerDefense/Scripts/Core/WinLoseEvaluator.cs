using RoyalSiege.Buildings;
using RoyalSiege.Combat;
using RoyalSiege.Data;
using RoyalSiege.Units;

namespace RoyalSiege.Core
{
    /// <summary>
    /// Defeat: tower HP 0. Victory ONLY when the spawn queue is empty, the last wave has
    /// started AND zero enemies are alive (a wave-11 straggler can outlive the boss — GDD).
    /// Stars: ★ win, ★★ ≥50% tower HP, ★★★ ≥80%.
    /// </summary>
    public sealed class WinLoseEvaluator : ITickable
    {
        private readonly RoyalTower _tower;
        private readonly WaveScheduler _scheduler;
        private readonly ITargetQuery _query;
        private readonly GameConfigSO _config;
        private readonly GameEvents _events;
        private readonly IClock _clock;
        private bool _ended;

        public WinLoseEvaluator(RoyalTower tower, WaveScheduler scheduler, ITargetQuery query,
            GameConfigSO config, GameEvents events, IClock clock)
        {
            _tower = tower;
            _scheduler = scheduler;
            _query = query;
            _config = config;
            _events = events;
            _clock = clock;
            _events.ReviveRequested += OnRevive;
        }

        /// <summary>
        /// Fail-screen revive: refill the tower and resume from exactly where it fell. The sim
        /// state (enemies, waves, energy, buildings) was only paused by <see cref="End"/>, so
        /// clearing _ended + unpausing the clock is all it takes to carry on — no rebuild.
        /// </summary>
        private void OnRevive()
        {
            if (!_ended) return;
            _ended = false;
            _tower.ReviveToFull();
            _clock.IsPaused = false;
        }

        public void Tick(float dt)
        {
            if (_ended) return;

            if (!_tower.IsAlive)
            {
                End(new MatchResult(false, 0, _scheduler.CurrentWaveNumber, 0f));
                return;
            }

            if (_scheduler.AllSpawned && _scheduler.LastWaveStarted && AliveEnemyCount() == 0)
            {
                float hpPct = _tower.HpPct;
                int stars = 1;
                if (hpPct >= _config.twoStarHpPct) stars = 2;
                if (hpPct >= _config.threeStarHpPct) stars = 3;
                End(new MatchResult(true, stars, _scheduler.WaveCount, hpPct));
            }
        }

        private int AliveEnemyCount()
        {
            int count = 0;
            var enemies = _query.Enemies;
            for (int i = 0; i < enemies.Count; i++)
                if (enemies[i].IsAlive) count++;
            return count;
        }

        private void End(MatchResult result)
        {
            _ended = true;
            _clock.IsPaused = true;
            _events.RaiseMatchEnded(result);
        }
    }
}

using UnityEngine;
using RoyalSiege.Core;
using RoyalSiege.Data;

namespace RoyalSiege.Economy
{
    /// <summary>
    /// Economy glue: EnemyKilled → orb flies → credit on arrival; WaveCleared → +bonus
    /// (unconditional, v2). Owns the orb pool.
    /// </summary>
    public sealed class OrbSpawner : MonoBehaviour
    {
        [SerializeField] private EnergyOrb _orbPrefab;
        [Tooltip("World-space anchor the orbs fly to (place near the energy bar).")]
        [SerializeField] private Transform _barAnchor;

        private ObjectPool<EnergyOrb> _pool;
        private IEnergyBank _bank;
        private EconomyConfigSO _config;
        private IClock _clock;
        private GameEvents _events;

        public void Init(GameEvents events, IEnergyBank bank, EconomyConfigSO config, IClock clock)
        {
            _events = events;
            _bank = bank;
            _config = config;
            _clock = clock;
            _pool = new ObjectPool<EnergyOrb>(_orbPrefab, transform, prewarm: 8);

            _events.EnemyKilled += OnEnemyKilled;
            _events.WaveCleared += OnWaveCleared;
        }

        private void OnDestroy()
        {
            if (_events == null) return;
            _events.EnemyKilled -= OnEnemyKilled;
            _events.WaveCleared -= OnWaveCleared;
        }

        private void OnEnemyKilled(EnemyKilledArgs args)
        {
            var orb = _pool.Get();
            orb.Launch(args.Position, _barAnchor, args.Bounty, _config.orbFlightSeconds, _clock, OnOrbArrived);
        }

        private void OnOrbArrived(EnergyOrb orb, float value)
        {
            _bank.Credit(value); // clamped at cap — overflow wasted (GDD ruling)
            _pool.Release(orb);
        }

        private void OnWaveCleared(int wave) => _bank.Credit(_config.waveClearBonus);
    }
}

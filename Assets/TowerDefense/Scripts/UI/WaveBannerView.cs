using UnityEngine;
using UnityEngine.UI;
using RoyalSiege.Core;

namespace RoyalSiege.UI
{
    /// <summary>
    /// Top banner: "WAVE n/12 · NEXT IN Xs" live countdown, plus a brief wave-cleared
    /// bonus flash. Countdown reads WaveScheduler.TimeToNextWave every frame.
    /// </summary>
    public sealed class WaveBannerView : MonoBehaviour
    {
        [SerializeField] private GameContext _context;
        [SerializeField] private Text _waveLabel;
        [SerializeField] private Text _bonusLabel;

        private float _bonusHideAt = -1f;

        private void Start()
        {
            _bonusLabel.text = "";
            _context.Events.WaveCleared += OnWaveCleared;
        }

        private void OnDestroy()
        {
            if (_context != null && _context.Events != null) _context.Events.WaveCleared -= OnWaveCleared;
        }

        private void OnWaveCleared(int wave)
        {
            _bonusLabel.text = "WAVE " + wave + " CLEARED  +3 ENERGY";
            _bonusHideAt = Time.time + 2f;
        }

        private void Update()
        {
            if (_bonusHideAt > 0f && Time.time >= _bonusHideAt)
            {
                _bonusLabel.text = "";
                _bonusHideAt = -1f;
            }

            if (_context.Waves == null) return;
            int current = _context.Waves.CurrentWaveNumber;
            int total = _context.Waves.WaveCount;
            float toNext = _context.Waves.TimeToNextWave;

            if (toNext >= 0f)
            {
                string countdown = "NEXT IN " + Mathf.CeilToInt(toNext) + "s";
                _waveLabel.text = current > 0
                    ? "WAVE " + current + "/" + total + "    " + countdown
                    : countdown;
            }
            else
            {
                _waveLabel.text = "WAVE " + current + "/" + total + "    FINAL WAVE";
            }
        }
    }
}

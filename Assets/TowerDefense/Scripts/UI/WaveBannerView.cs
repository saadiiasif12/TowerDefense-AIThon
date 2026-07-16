using UnityEngine;
using UnityEngine.UI;
using RoyalSiege.Core;

namespace RoyalSiege.UI
{
    /// <summary>Top banner: "WAVE n/12", plus a brief wave-cleared bonus flash.</summary>
    public sealed class WaveBannerView : MonoBehaviour
    {
        [SerializeField] private GameContext _context;
        [SerializeField] private Text _waveLabel;
        [SerializeField] private Text _bonusLabel;

        private float _bonusHideAt = -1f;

        private void Start()
        {
            _waveLabel.text = "GET READY";
            _bonusLabel.text = "";
            _context.Events.WaveStarted += OnWaveStarted;
            _context.Events.WaveCleared += OnWaveCleared;
        }

        private void OnDestroy()
        {
            if (_context == null || _context.Events == null) return;
            _context.Events.WaveStarted -= OnWaveStarted;
            _context.Events.WaveCleared -= OnWaveCleared;
        }

        private void OnWaveStarted(int wave) => _waveLabel.text = "WAVE " + wave + "/" + _context.Waves.WaveCount;

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
        }
    }
}

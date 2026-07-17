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
        private int _stageIndex;
        private int _waveInStage;
        private int _wavesInStage = 20;

        private void Start()
        {
            _bonusLabel.text = "";
            _context.Events.WaveCleared += OnWaveCleared;
            _context.Events.WaveProgressChanged += OnWaveProgress;
        }

        private void OnDestroy()
        {
            if (_context == null || _context.Events == null) return;
            _context.Events.WaveCleared -= OnWaveCleared;
            _context.Events.WaveProgressChanged -= OnWaveProgress;
        }

        private void OnWaveCleared(int wave)
        {
            _bonusLabel.text = "WAVE " + wave + " CLEARED";
            _bonusHideAt = Time.time + 2f;
        }

        private void OnWaveProgress(int stage, int waveInStage, int wavesInStage)
        {
            _stageIndex = stage;
            _waveInStage = waveInStage;
            _wavesInStage = wavesInStage;
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

            // v4 stage header: stage-local progress + the global wave number (§1).
            string stagePart = "STAGE " + (_stageIndex + 1) + " · " + _waveInStage + "/" + _wavesInStage;

            if (_context.Waves.CampaignComplete)
                _waveLabel.text = stagePart + "    CLEARED";
            else if (toNext >= 0f)
                _waveLabel.text = (current > 0 ? stagePart + "    " : "") + "NEXT IN " + Mathf.CeilToInt(toNext) + "s";
            else
                _waveLabel.text = stagePart + "    WAVE " + current + "/" + total;
        }
    }
}

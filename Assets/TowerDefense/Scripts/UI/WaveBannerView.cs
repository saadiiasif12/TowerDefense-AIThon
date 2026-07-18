using UnityEngine;
using UnityEngine.UI;
using RoyalSiege.Core;
using TMPro;

namespace RoyalSiege.UI
{
    /// <summary>
    /// Top banner: "WAVE n/12 · NEXT IN Xs" live countdown, plus a brief wave-cleared
    /// bonus flash. Countdown reads WaveScheduler.TimeToNextWave every frame.
    /// </summary>
    public sealed class WaveBannerView : MonoBehaviour
    {
        [SerializeField] private GameContext _context;
        [SerializeField] private TextMeshProUGUI _waveLabel;
        [SerializeField] private Text _bonusLabel;
        [Tooltip("Stage progress as a Unity Slider (9-sliced base + fill).")]
        [SerializeField] private Slider _stageSlider;

        private float _bonusHideAt = -1f;
        private int _stageIndex;
        private int _waveInStage;
        private int _wavesInStage = 20;

        private void Start()
        {
            // HUD prefab can't serialize a scene ref — resolve the GameContext at runtime.
            if (_context == null) _context = FindFirstObjectByType<GameContext>();
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
            // 18-Jul user ruling: no wave information in the header — the stage progress
            // bar is the only wave feedback. (Kept as a hook for future SFX/haptics.)
        }

        private void OnWaveProgress(int stage, int waveInStage, int wavesInStage)
        {
            _stageIndex = stage;
            _waveInStage = waveInStage;
            _wavesInStage = wavesInStage;
            if (_stageSlider != null)
                _stageSlider.value = wavesInStage > 0 ? (float)waveInStage / wavesInStage : 0f;
        }

        private void Update()
        {
            if (_context == null || _context.Waves == null) return;

            // Header per mock_1 + 18-Jul ruling: clean "Stage N" title above the green stage
            // progress bar — NO wave information (no countdown, no wave numbers).
            //_waveLabel.text = "Stage " + (_stageIndex + 1);
            if (_context.Waves.CampaignComplete) _bonusLabel.text = "CAMPAIGN CLEARED";
            else if (_bonusLabel.text.Length > 0) _bonusLabel.text = "";
        }
    }
}

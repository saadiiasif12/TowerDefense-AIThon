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
            _bonusLabel.text = "WAVE " + wave + " CLEARED";
            _bonusHideAt = Time.time + 2f;
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
            if (_bonusHideAt > 0f && Time.time >= _bonusHideAt)
            {
                _bonusLabel.text = "";
                _bonusHideAt = -1f;
            }

            if (_context.Waves == null) return;

            // Header per mock_1: clean "Stage N" title above the green progress bar.
            _waveLabel.text = "Stage " + (_stageIndex + 1);

            // The transient line (below the bar) shows the live countdown / final state — the
            // wave-cleared flash overrides it briefly (kept from the bonus flash).
            if (_bonusHideAt > 0f) return; // a cleared flash is showing
            if (_context.Waves.CampaignComplete)
                _bonusLabel.text = "CAMPAIGN CLEARED";
            else
            {
                float toNext = _context.Waves.TimeToNextWave;
                _bonusLabel.text = toNext >= 0f
                    ? "NEXT WAVE IN " + Mathf.CeilToInt(toNext) + "s"
                    : "WAVE " + _context.Waves.CurrentWaveNumber + " / " + _context.Waves.WaveCount;
            }
        }
    }
}

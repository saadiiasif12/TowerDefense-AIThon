using UnityEngine;
using UnityEngine.UI;
using RoyalSiege.Core;

namespace RoyalSiege.UI
{
    /// <summary>Magenta 0–10 bar (mock direction). Pulses gold at cap to nudge spending.</summary>
    public sealed class EnergyBarView : MonoBehaviour
    {
        [SerializeField] private GameContext _context;
        [SerializeField] private Image _fill;
        [SerializeField] private Text _label;
        [SerializeField] private Color _normalColor = new(0.95f, 0.2f, 0.75f);
        [SerializeField] private Color _capColor = new(1f, 0.85f, 0.2f);

        private float _current;
        private float _max = 10f;

        private void Start()
        {
            _context.Events.EnergyChanged += OnEnergyChanged;
            OnEnergyChanged(_context.Energy.Current, _context.Energy.Max);
        }

        private void OnDestroy()
        {
            if (_context != null && _context.Events != null) _context.Events.EnergyChanged -= OnEnergyChanged;
        }

        private void OnEnergyChanged(float current, float max)
        {
            _current = current;
            _max = max;
            _fill.fillAmount = current / max;
            _label.text = Mathf.FloorToInt(current) + " / " + Mathf.FloorToInt(max);
        }

        private void Update()
        {
            bool atCap = _current >= _max - 0.001f;
            _fill.color = atCap
                ? Color.Lerp(_normalColor, _capColor, Mathf.PingPong(Time.time * 2f, 1f))
                : _normalColor;
        }
    }
}

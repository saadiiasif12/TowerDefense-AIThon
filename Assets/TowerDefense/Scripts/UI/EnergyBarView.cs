using UnityEngine;
using UnityEngine.UI;
using RoyalSiege.Core;

namespace RoyalSiege.UI
{
    /// <summary>
    /// Lightning-gold elixir bar (mock_1 / ChatGPT fill reference, 17-Jul UI pass). The fill
    /// is NOT a stretched image — it's discrete NATIVE-SIZE segment cells (one per elixir
    /// point) that attach left-to-right as elixir grows, exactly like the reference. Pulses
    /// brighter at cap to nudge spending. Segments are pre-built once (pooled, no per-frame
    /// alloc); EnergyBank keeps elixir integral so whole cells always read cleanly.
    /// </summary>
    public sealed class EnergyBarView : MonoBehaviour
    {
        [SerializeField] private GameContext _context;
        [Tooltip("Row that holds the segment cells (inside the bar, minus its rounded caps).")]
        [SerializeField] private RectTransform _segmentRoot;
        [Tooltip("fill_bar_mana — drawn at NATIVE size, one copy per elixir point.")]
        [SerializeField] private Sprite _segmentSprite;
        [SerializeField] private Text _label;
        [SerializeField] private Vector2 _segmentSize = new(51f, 27f);
        [SerializeField] private float _segmentGap = 5f;
        [SerializeField] private Color _normalColor = new(1f, 0.78f, 0.12f);
        [SerializeField] private Color _capColor = new(1f, 0.95f, 0.5f);

        private Image[] _segments;
        private float _current;
        private float _max = 10f;

        private void Start()
        {
            // HUD prefab can't serialize a scene ref — resolve the GameContext at runtime.
            if (_context == null) _context = FindFirstObjectByType<GameContext>();
            BuildSegments();
            _context.Events.EnergyChanged += OnEnergyChanged;
            OnEnergyChanged(_context.Energy.Current, _context.Energy.Max);
        }

        private void OnDestroy()
        {
            if (_context != null && _context.Events != null) _context.Events.EnergyChanged -= OnEnergyChanged;
        }

        private void BuildSegments()
        {
            int max = _context != null ? Mathf.RoundToInt(_context.Energy.Max) : 10;
            if (max <= 0) max = 10;
            _max = max;
            _segments = new Image[max];

            float totalW = max * _segmentSize.x + (max - 1) * _segmentGap;
            float startX = -totalW * 0.5f + _segmentSize.x * 0.5f;
            for (int i = 0; i < max; i++)
            {
                var go = new GameObject("Seg" + i, typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(_segmentRoot, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = _segmentSize;
                rt.anchoredPosition = new Vector2(startX + i * (_segmentSize.x + _segmentGap), 0f);
                var img = go.GetComponent<Image>();
                img.sprite = _segmentSprite;
                img.color = _normalColor;
                img.raycastTarget = false;
                _segments[i] = img;
                go.SetActive(false);
            }
        }

        private void OnEnergyChanged(float current, float max)
        {
            _current = current;
            _max = max;
            if (_segments == null) return;
            int filled = Mathf.Clamp(Mathf.FloorToInt(current + 0.001f), 0, _segments.Length);
            for (int i = 0; i < _segments.Length; i++)
            {
                bool on = i < filled;
                if (_segments[i].gameObject.activeSelf != on) _segments[i].gameObject.SetActive(on);
            }
            if (_label != null) _label.text = Mathf.FloorToInt(current).ToString();
        }

        private void Update()
        {
            if (_segments == null) return;
            bool atCap = _current >= _max - 0.001f;
            Color c = atCap ? Color.Lerp(_normalColor, _capColor, Mathf.PingPong(Time.time * 2f, 1f)) : _normalColor;
            for (int i = 0; i < _segments.Length; i++)
                if (_segments[i].gameObject.activeSelf) _segments[i].color = c;
        }
    }
}

using UnityEngine;
using UnityEngine.UI;
using RoyalSiege.Core;

namespace RoyalSiege.UI
{
    /// <summary>
    /// Lightning-gold elixir bar — discrete NATIVE-SIZE segment cells (one per point), exactly
    /// like the mock. 18-Jul spend/regen presentation (config-driven, unscaled time, display
    /// only — EnergyBank stays authoritative):
    ///  · SPEND: the emptied cells linger as a bright white-pink TRAIL that fades out after a
    ///    short delay (the CR "trailing fill" adapted to segments),
    ///  · REGEN: the newly filled cell scale-pops in softly (no per-frame bouncing),
    ///  · FULL:  one restrained glint when the bar REACHES cap (no endless flashing loop).
    /// Segments are pre-built once (pooled); zero allocation during play.
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
        [SerializeField] private Data.CardInteractionAnimationConfig _animConfig;

        private static readonly Color TrailColor = new(1f, 0.9f, 0.95f, 1f); // bright white-pink

        private Image[] _segments;
        private float[] _ghostT;   // >=0: emptied cell fading out (spend trail); -1 idle
        private float[] _popT;     // >=0: fresh cell scaling in; -1 idle
        private int _shownFilled;
        private float _current;
        private float _max = 10f;
        private float _capGlintT = 1f; // one-shot glint on reaching cap

        private float TrailDelay => _animConfig != null ? _animConfig.energySpendTrailDelay : 0.05f;
        private float TrailDuration => _animConfig != null ? _animConfig.energyTrailDuration : 0.28f;
        private float PopDuration => _animConfig != null ? _animConfig.energyRegenPopDuration : 0.12f;

        private void Start()
        {
            // HUD prefab can't serialize a scene ref — resolve the GameContext at runtime.
            if (_context == null) _context = FindFirstObjectByType<GameContext>();
            BuildSegments();
            _context.Events.EnergyChanged += OnEnergyChanged;
            OnEnergyChanged(_context.Energy.Current, _context.Energy.Max);
            _shownFilled = Mathf.Clamp(Mathf.FloorToInt(_current + 0.001f), 0, _segments.Length);
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
            _ghostT = new float[max];
            _popT = new float[max];

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
                _ghostT[i] = float.MinValue; // idle sentinel
                _popT[i] = -1f;
                go.SetActive(false);
            }
        }

        private void OnEnergyChanged(float current, float max)
        {
            bool wasAtCap = _current >= _max - 0.001f;
            _current = current;
            _max = max;
            if (_segments == null) return;
            int filled = Mathf.Clamp(Mathf.FloorToInt(current + 0.001f), 0, _segments.Length);

            if (filled < _shownFilled)
            {
                // SPEND: emptied cells become bright ghosts that fade (trailing fill).
                for (int i = filled; i < _shownFilled; i++)
                {
                    _ghostT[i] = -TrailDelay; // negative = still in the hold-at-old-value delay
                    _popT[i] = -1f;
                    _segments[i].gameObject.SetActive(true);
                    _segments[i].color = TrailColor;
                    _segments[i].rectTransform.localScale = Vector3.one;
                }
            }
            else if (filled > _shownFilled)
            {
                // REGEN: new cells pop in softly.
                for (int i = _shownFilled; i < filled; i++)
                {
                    _ghostT[i] = float.MinValue;
                    _popT[i] = 0f;
                    _segments[i].gameObject.SetActive(true);
                    _segments[i].color = _normalColor;
                    _segments[i].rectTransform.localScale = Vector3.one * 0.6f;
                }
            }
            _shownFilled = filled;

            bool atCap = current >= max - 0.001f;
            if (atCap && !wasAtCap) _capGlintT = 0f; // ONE glint on reaching full

            if (_label != null) _label.text = Mathf.FloorToInt(current).ToString();
        }

        private void Update()
        {
            if (_segments == null) return;
            float dt = Time.unscaledDeltaTime;
            bool atCap = _current >= _max - 0.001f;

            // one-shot cap glint (0..1), then settle on the steady cap color
            float glint = 0f;
            if (_capGlintT < 1f)
            {
                _capGlintT = Mathf.Min(1f, _capGlintT + dt / 0.6f);
                glint = Mathf.Sin(_capGlintT * Mathf.PI);
            }
            Color filledColor = atCap
                ? Color.Lerp(_capColor, Color.white, glint * 0.5f)
                : _normalColor;

            for (int i = 0; i < _segments.Length; i++)
            {
                var seg = _segments[i];

                if (_ghostT[i] > float.MinValue && i >= _shownFilled)
                {
                    // spend trail: hold at the old value briefly, then fade out and deactivate
                    _ghostT[i] += dt;
                    if (_ghostT[i] >= 0f)
                    {
                        float t = Mathf.Clamp01(_ghostT[i] / TrailDuration);
                        var c = TrailColor; c.a = 1f - t;
                        seg.color = c;
                        if (t >= 1f) { _ghostT[i] = float.MinValue; seg.gameObject.SetActive(false); }
                    }
                    continue;
                }
                if (_ghostT[i] > float.MinValue) _ghostT[i] = float.MinValue; // refilled mid-fade

                bool on = i < _shownFilled;
                if (seg.gameObject.activeSelf != on) seg.gameObject.SetActive(on);
                if (!on) continue;

                if (_popT[i] >= 0f)
                {
                    _popT[i] = Mathf.Min(1f, _popT[i] + dt / PopDuration);
                    float e = EaseOutBack(_popT[i]);
                    seg.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, e);
                    if (_popT[i] >= 1f) { _popT[i] = -1f; seg.rectTransform.localScale = Vector3.one; }
                }
                seg.color = filledColor;
            }
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}

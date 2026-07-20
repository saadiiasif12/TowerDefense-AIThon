using UnityEngine;
using UnityEngine.UI;
using RoyalSiege.Core;

namespace RoyalSiege.UI
{
    /// <summary>
    /// Lightning-gold elixir bar — discrete segment cells (one per point), exactly like the
    /// mock, but each cell now fills CONTINUOUSLY (Image fill-amount) as elixir accrues, so the
    /// player watches the current point fill up over time instead of segments snapping on
    /// (19-Jul, Clash-Royale style). Display only — EnergyBank stays authoritative and is
    /// polled each frame for the live fractional value; a light SmoothDamp glides the fill so
    /// the 10 Hz sim ticks read as buttery 60 fps motion. Feedback preserved:
    ///  · SPEND: the emptied cells linger as a bright white-pink TRAIL that fades out,
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
        [Tooltip("How quickly the visible fill catches up to the live elixir (smaller = snappier). " +
                 "Purely cosmetic smoothing of the 10 Hz accrual into 60 fps motion.")]
        [SerializeField] private float _fillSmoothTime = 0.12f;
        [SerializeField] private Data.CardInteractionAnimationConfig _animConfig;

        private static readonly Color TrailColor = new(1f, 0.9f, 0.95f, 1f); // bright white-pink

        private Image[] _segments;
        private float[] _ghostT;   // >float.MinValue: emptied cell fading out (spend trail); MinValue = idle
        private float _current;    // last DISCRETE event value (label / trail bookkeeping)
        private float _max = 10f;
        private float _display;    // smoothed fractional fill actually shown
        private float _displayVel; // SmoothDamp velocity
        private float _capGlintT = 1f; // one-shot glint on reaching cap

        private float TrailDelay => _animConfig != null ? _animConfig.energySpendTrailDelay : 0.05f;
        private float TrailDuration => _animConfig != null ? _animConfig.energyTrailDuration : 0.28f;

        private void Start()
        {
            // HUD prefab can't serialize a scene ref — resolve the GameContext at runtime.
            if (_context == null) _context = FindFirstObjectByType<GameContext>();
            BuildSegments();
            _context.Events.EnergyChanged += OnEnergyChanged;
            _current = _display = _context.Energy.Current;
            _max = _context.Energy.Max;
            if (_label != null) _label.text = Mathf.FloorToInt(_current).ToString();
        }

        private void OnDestroy()
        {
            if (_context != null && _context.Events != null) _context.Events.EnergyChanged -= OnEnergyChanged;
        }

        private void BuildSegments()
        {
            int max = _context != null ? Mathf.RoundToInt(_context.Energy.Max) : 10;
            if (max <= 0) max = 10;

            // Prefer the segment cells the designer hand-placed under _segmentRoot: their
            // EXACT authored fitting is used (position/size/sprite kept, no layout group
            // needed). Only if none are pre-placed do we build procedural cells as a fallback.
            var placed = new System.Collections.Generic.List<Image>();
            if (_segmentRoot != null)
                for (int c = 0; c < _segmentRoot.childCount; c++)
                {
                    var img = _segmentRoot.GetChild(c).GetComponent<Image>();
                    if (img != null) placed.Add(img);
                }
            bool usePlaced = placed.Count >= max;

            _max = max;
            _segments = new Image[max];
            _ghostT = new float[max];

            float totalW = max * _segmentSize.x + (max - 1) * _segmentGap;
            float startX = -totalW * 0.5f + _segmentSize.x * 0.5f;
            for (int i = 0; i < max; i++)
            {
                Image img;
                if (usePlaced)
                {
                    img = placed[i]; // designer's exact cell — transform/sprite untouched
                }
                else
                {
                    var go = new GameObject("Seg" + i, typeof(RectTransform), typeof(Image));
                    var rt = (RectTransform)go.transform;
                    rt.SetParent(_segmentRoot, false);
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = _segmentSize;
                    rt.anchoredPosition = new Vector2(startX + i * (_segmentSize.x + _segmentGap), 0f);
                    img = go.GetComponent<Image>();
                    img.sprite = _segmentSprite;
                }
                // Left-to-right horizontal fill: this is what makes the cell VISIBLY fill up.
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Horizontal;
                img.fillOrigin = (int)Image.OriginHorizontal.Left;
                img.fillAmount = 0f;
                img.color = _normalColor;
                img.raycastTarget = false;
                _segments[i] = img;
                _ghostT[i] = float.MinValue; // idle sentinel
                img.gameObject.SetActive(false);
            }

            // Never show more than max patches: hide any extra authored cells.
            if (usePlaced)
                for (int i = max; i < placed.Count; i++) placed[i].gameObject.SetActive(false);
        }

        private void OnEnergyChanged(float current, float max)
        {
            float old = _current;
            bool wasAtCap = old >= _max - 0.001f;
            _max = max;

            if (_segments != null && current < old - 0.0001f)
            {
                // SPEND (or any drop): the cells that lost their fill linger as bright ghosts
                // that fade out (the CR "trailing fill" adapted to segments).
                int oldCells = Mathf.CeilToInt(old - 0.0001f);
                int newCells = Mathf.CeilToInt(current - 0.0001f);
                for (int i = newCells; i < oldCells && i < _segments.Length; i++)
                {
                    _ghostT[i] = -TrailDelay; // negative = still in the hold-at-old-value delay
                    _segments[i].gameObject.SetActive(true);
                    _segments[i].color = TrailColor;
                    _segments[i].fillAmount = Mathf.Clamp01(old - i);
                }
            }

            _current = current;
            bool atCap = current >= max - 0.001f;
            if (atCap && !wasAtCap) _capGlintT = 0f; // ONE glint on reaching full
            if (_label != null) _label.text = Mathf.FloorToInt(current).ToString();
        }

        private void Update()
        {
            if (_segments == null) return;
            float dt = Time.unscaledDeltaTime;

            // Poll the LIVE fractional elixir so the bar fills smoothly between 10 Hz sim ticks.
            float live = _context != null && _context.Energy != null ? _context.Energy.Current : _current;
            if (live < _display - 0.0001f) { _display = live; _displayVel = 0f; } // drop → snap instantly
            else _display = Mathf.SmoothDamp(_display, live, ref _displayVel, _fillSmoothTime, Mathf.Infinity, dt);

            bool atCap = live >= _max - 0.001f;
            float glint = 0f;
            if (_capGlintT < 1f)
            {
                _capGlintT = Mathf.Min(1f, _capGlintT + dt / 0.6f);
                glint = Mathf.Sin(_capGlintT * Mathf.PI);
            }
            Color filledColor = atCap ? Color.Lerp(_capColor, Color.white, glint * 0.5f) : _normalColor;

            for (int i = 0; i < _segments.Length; i++)
            {
                var seg = _segments[i];
                float fill = Mathf.Clamp01(_display - i);

                // A ghosted cell that has refilled drops its trail and rejoins the live fill.
                if (fill > 0.0001f && _ghostT[i] > float.MinValue) _ghostT[i] = float.MinValue;

                if (_ghostT[i] > float.MinValue)
                {
                    // spend trail: hold briefly, then fade out and deactivate
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

                bool on = fill > 0.0001f;
                if (seg.gameObject.activeSelf != on) seg.gameObject.SetActive(on);
                if (!on) continue;
                seg.fillAmount = fill;
                seg.color = filledColor;
            }
        }
    }
}

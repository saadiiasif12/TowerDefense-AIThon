using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace RoyalSiege.UI
{
    /// <summary>
    /// Reusable sprite-sheet animation controller for UI (RawImage): shows exactly ONE
    /// frame at a time and swaps it at Swap Speed fps by sliding uvRect across the grid —
    /// the full sheet is never displayed, in play mode OR in the editor (ExecuteAlways
    /// keeps the editor preview cropped to frame 0). Loop toggle (off = play once, hold
    /// the last frame), play-on-enable, Play()/Stop() API and SetSheet() for outcome
    /// swaps. Unscaled time (keeps animating while the sim is paused), zero per-frame
    /// allocations (uvRect only written when the frame index changes), half-texel inset
    /// against neighbour-frame bleed, and auto-syncs a sibling AspectRatioFitter to the
    /// frame shape so differently-shaped sheets never stretch.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RawImage))]
    public sealed class UiSpriteSheetFlipbook : MonoBehaviour
    {
        [Header("Sheet")]
        [SerializeField] private Texture _sheet;
        [SerializeField] private int _columns = 5;
        [SerializeField] private int _rows = 5;
        [Tooltip("0 = use every grid cell (columns × rows).")]
        [SerializeField] private int _frameCount = 0;

        [Header("Playback")]
        [Tooltip("Frame swaps per second — higher = faster, snappier animation.")]
        [FormerlySerializedAs("_framesPerSecond")]
        [SerializeField] private float _swapSpeed = 15f;
        [Tooltip("ON = repeat forever. OFF = play once and hold the last frame.")]
        [SerializeField] private bool _loop = true;
        [SerializeField] private bool _playOnEnable = true;

        private RawImage _image;
        private AspectRatioFitter _fitter; // optional — kept in sync with the frame aspect
        private float _time;
        private int _frame = -1;
        private bool _playing;

        public bool IsPlaying => _playing;
        private int TotalFrames => _frameCount > 0 ? _frameCount : _columns * _rows;

        private void Awake()
        {
            _image = GetComponent<RawImage>();
            _fitter = GetComponent<AspectRatioFitter>();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) { ShowFrame(0); return; } // editor: cropped preview, never the raw sheet
            if (_playOnEnable) Play();
            else ShowFrame(0);
        }

        /// <summary>Restart playback from frame 0 at the configured swap speed.</summary>
        public void Play()
        {
            _time = 0f;
            _playing = true;
            ShowFrame(0);
        }

        /// <summary>Freeze on the current frame (Play() to restart).</summary>
        public void Stop() => _playing = false;

        /// <summary>Swap the sheet (e.g. happy/sad outcome art) — restarts from frame 0.</summary>
        public void SetSheet(Texture sheet)
        {
            _sheet = sheet;
            _frame = -1;
            if (Application.isPlaying && isActiveAndEnabled) Play();
            else ShowFrame(0);
        }

        private void Update()
        {
            if (!Application.isPlaying) { ShowFrame(0); return; }
            if (!_playing || _sheet == null) return;

            _time += Time.unscaledDeltaTime;
            int total = TotalFrames;
            int frame = (int)(_time * _swapSpeed);
            if (_loop) frame = total <= 1 ? 0 : frame % total;
            else if (frame >= total) { frame = total - 1; _playing = false; } // hold last frame
            ShowFrame(frame);
        }

        private void ShowFrame(int frame)
        {
            if (_sheet == null || frame == _frame) return;
            if (_image == null) _image = GetComponent<RawImage>();
            if (_fitter == null) _fitter = GetComponent<AspectRatioFitter>();
            _frame = frame;

            if (_image.texture != _sheet)
            {
                _image.texture = _sheet;
                // sheets can have different frame shapes (happy = portrait, sad = landscape)
                if (_fitter != null && _sheet.height > 0)
                    _fitter.aspectRatio = ((float)_sheet.width / _columns) / ((float)_sheet.height / _rows);
            }

            float w = 1f / _columns, h = 1f / _rows;
            float ix = _sheet.width > 0 ? 0.5f / _sheet.width : 0f;
            float iy = _sheet.height > 0 ? 0.5f / _sheet.height : 0f;
            int col = frame % _columns;
            int row = frame / _columns; // row 0 = TOP row of the sheet; uv origin is bottom-left
            _image.uvRect = new Rect(col * w + ix, 1f - (row + 1) * h + iy, w - 2f * ix, h - 2f * iy);
        }
    }
}

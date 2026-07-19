using UnityEngine;
using UnityEngine.UI;
using RoyalSiege.Core;
using RoyalSiege.Cards;
using RoyalSiege.Data;
using RoyalSiege.Buildings;
using TMPro;

namespace RoyalSiege.UI
{
    /// <summary>
    /// First-play forced tutorial: after the intro cinematic, the player MUST drag one taught
    /// card onto the deployment ring before the game lets them do anything else. Input is
    /// hard-gated to that one card (HandBarView.LockToSlot); a looping finger gesture traces
    /// card → ring, the ring pulses, and a prompt explains it. Completing it (the card is
    /// actually deployed) unlocks the hand and stamps a PlayerPrefs flag so it never repeats.
    /// Self-builds its overlay UI — nothing to author in the scene. Skips instantly if the
    /// flag is set or the refs can't be resolved.
    /// </summary>
    public sealed class FirstPlayTutorial : MonoBehaviour
    {
        private const string DoneKey = "royalDefense.tutorialDone.v1";

        [SerializeField] private GameContext _context;
        [SerializeField] private float _gestureSeconds = 1.5f;
        [Tooltip("The game's Praxis TMP font so the prompt matches the rest of the UI.")]
        [SerializeField] private TMP_FontAsset _font;
        [Tooltip("Optional stroke/underlay material preset for the prompt (readable on the field).")]
        [SerializeField] private Material _fontMaterial;

        private HandBarView _hand;
        private GameIntroSequence _intro;
        private DeployRingView _ring;
        private Camera _camera;

        private int _targetSlot = -1;
        private bool _active;
        private RectTransform _dot;
        private TextMeshProUGUI _prompt;
        private RectTransform _targetSlotRect;
        private float _gestureT;

        private void Start()
        {
            if (PlayerPrefs.GetInt(DoneKey, 0) == 1) { enabled = false; return; }
            if (_context == null) _context = FindFirstObjectByType<GameContext>();
            _hand = FindFirstObjectByType<HandBarView>();
            _intro = FindFirstObjectByType<GameIntroSequence>();
            _ring = FindFirstObjectByType<DeployRingView>();
            _camera = Camera.main;
            if (_context == null || _hand == null || _camera == null) { enabled = false; return; }

            // Only teach on a genuinely fresh run (start of the journey), never on a resume.
            var profile = CampaignProfile.Load();
            if (profile.nextWave > 1) { PlayerPrefs.SetInt(DoneKey, 1); enabled = false; return; }

            if (_intro != null && !_intro.IsComplete) _intro.Completed += Begin;
            else Begin();
        }

        private void Begin()
        {
            if (_intro != null) _intro.Completed -= Begin;

            _targetSlot = PickTeachSlot();
            if (_targetSlot < 0) { enabled = false; return; } // nothing affordable yet — retry next launch, don't stamp done
            _targetSlotRect = ResolveSlotRect(_targetSlot);

            _hand.LockToSlot(_targetSlot);
            _ring?.SetDragHighlight(true);
            BuildOverlay();
            _context.Events.CardPlayed += OnCardPlayed;
            _active = true;
        }

        /// <summary>Prefer an affordable building/troop (they MUST land on the ring); else any affordable card.</summary>
        private int PickTeachSlot()
        {
            int fallback = -1;
            for (int i = 0; i < DeckService.HandSize; i++)
            {
                if (!_context.CardPlay.CanPlay(i)) continue;
                var card = _context.CardPlay.CardAt(i);
                if (card is BuildingCardSO or TroopCardSO) return i;
                if (fallback < 0) fallback = i;
            }
            return fallback;
        }

        private void OnCardPlayed(CardDefinitionSO card)
        {
            if (_active) Finish(true); // the taught card was deployed on the ring
        }

        private void Update()
        {
            if (!_active) return;
            // A dropped-but-invalid attempt re-locks nothing; keep gating on the same slot in case
            // the hand cycled the taught card away after an accidental play elsewhere is impossible
            // (locked), so just keep the gesture alive.
            _gestureT += Time.unscaledDeltaTime / Mathf.Max(0.1f, _gestureSeconds);
            float phase = Mathf.Repeat(_gestureT, 1f);

            Vector2 from = _targetSlotRect != null
                ? (Vector2)_targetSlotRect.position          // overlay canvas → .position is screen pixels
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.22f);
            Vector2 to = _camera.WorldToScreenPoint(_context.MapCenter);

            // ease-in-out along the path + a little arc lift, fade near the ends
            float e = Mathf.SmoothStep(0f, 1f, phase);
            Vector2 pos = Vector2.Lerp(from, to, e);
            pos.y += Mathf.Sin(phase * Mathf.PI) * 60f;
            if (_dot != null)
            {
                _dot.position = pos;
                float a = Mathf.Sin(phase * Mathf.PI);          // 0→1→0 across the drag
                float s = 0.8f + 0.25f * Mathf.Sin(phase * Mathf.PI);
                _dot.localScale = Vector3.one * s;
                var img = _dot.GetComponent<Image>();
                if (img != null) { var c = img.color; c.a = Mathf.Clamp01(a * 1.4f); img.color = c; }
            }
        }

        private void Finish(bool completed)
        {
            _active = false;
            if (_context != null && _context.Events != null) _context.Events.CardPlayed -= OnCardPlayed;
            _hand?.Unlock();
            _ring?.SetDragHighlight(false);
            if (_overlay != null) Destroy(_overlay);
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
            enabled = false;
        }

        // ---------------- overlay UI (self-built) ----------------

        private GameObject _overlay;

        private void BuildOverlay()
        {
            _overlay = new GameObject("TutorialOverlay", typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = _overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;            // above the HUD, below nothing
            _overlay.GetComponent<GraphicRaycaster>().enabled = false; // never eats touches — the card must

            // prompt: a dark rounded strip behind bold white text (a runtime outline would
            // mutate the SHARED TMP material and tint every HUD label — so use a plate instead).
            var plateGo = new GameObject("PromptPlate", typeof(RectTransform), typeof(Image));
            var plate = (RectTransform)plateGo.transform;
            plate.SetParent(_overlay.transform, false);
            plate.anchorMin = new Vector2(0.5f, 1f); plate.anchorMax = new Vector2(0.5f, 1f); plate.pivot = new Vector2(0.5f, 1f);
            plate.anchoredPosition = new Vector2(0f, -Screen.height * 0.24f);
            plate.sizeDelta = new Vector2(Screen.width * 0.86f, 130f);
            var pimg = plateGo.GetComponent<Image>();
            pimg.sprite = SoftCircleSprite();     // soft rounded fill
            pimg.type = Image.Type.Sliced;
            pimg.color = new Color(0f, 0f, 0f, 0.62f);
            pimg.raycastTarget = false;

            var textGo = new GameObject("Prompt", typeof(RectTransform), typeof(TextMeshProUGUI));
            var trt = (RectTransform)textGo.transform;
            trt.SetParent(plate, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.pivot = new Vector2(0.5f, 0.5f);
            trt.offsetMin = new Vector2(24f, 10f); trt.offsetMax = new Vector2(-24f, -10f);
            _prompt = textGo.GetComponent<TextMeshProUGUI>();
            if (_font != null) _prompt.font = _font;                       // match the game's Praxis UI
            if (_fontMaterial != null) _prompt.fontSharedMaterial = _fontMaterial;
            _prompt.text = "Drag the glowing card onto the ring to deploy!";
            _prompt.enableAutoSizing = true; _prompt.fontSizeMin = 26; _prompt.fontSizeMax = 42;
            _prompt.alignment = TextAlignmentOptions.Center;
            _prompt.color = Color.white;
            _prompt.raycastTarget = false;

            // touch dot (finger gesture)
            var dotGo = new GameObject("TouchDot", typeof(RectTransform), typeof(Image));
            _dot = (RectTransform)dotGo.transform;
            _dot.SetParent(_overlay.transform, false);
            _dot.sizeDelta = new Vector2(90f, 90f);
            var dimg = dotGo.GetComponent<Image>();
            dimg.sprite = SoftCircleSprite();
            dimg.color = new Color(1f, 1f, 1f, 0.9f);
            dimg.raycastTarget = false;
        }

        private static Sprite _circle;
        private static Sprite SoftCircleSprite()
        {
            if (_circle != null) return _circle;
            const int S = 64; var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (S - 1) * 0.5f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - d);           // soft radial falloff
                    a = a * a;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            _circle = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            return _circle;
        }

        private RectTransform ResolveSlotRect(int slot)
        {
            var slotsRoot = FindByName("Slots");
            if (slotsRoot != null && slot < slotsRoot.childCount) return (RectTransform)slotsRoot.GetChild(slot);
            return null;
        }

        private static Transform FindByName(string name)
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (t.name == name) return t;
            return null;
        }
    }
}

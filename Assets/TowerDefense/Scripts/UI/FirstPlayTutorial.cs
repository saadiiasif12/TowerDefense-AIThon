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
    /// First-play tutorial. After the intro deals the hand, everything is blocked (dim overlay)
    /// EXCEPT the Cannon card, which is forced into the hand, lifted bright above the dim, and
    /// pointed at by a big cartoon hand tracing card → ring. The moment the player TOUCHES the
    /// Cannon the tutorial ends (overlay + block cleared) and they finish the drag themselves.
    /// Cannon (a building) is used deliberately — no enemies are in the ring yet, so a spell
    /// would be wasted. Shows only on a genuinely fresh run; stamps a PlayerPrefs flag after.
    /// </summary>
    public sealed class FirstPlayTutorial : MonoBehaviour
    {
        private const string DoneKey = "royalDefense.tutorialDone.v1";

        [SerializeField] private GameContext _context;
        [SerializeField] private float _gestureSeconds = 1.6f;
        [Tooltip("Cartoon hand sprite (points at the card).")]
        [SerializeField] private Sprite _handSprite;
        [Tooltip("The game's Praxis TMP font so the prompt matches the rest of the UI.")]
        [SerializeField] private TMP_FontAsset _font;
        [Tooltip("Praxis-Black navy-stroke material preset for the prompt.")]
        [SerializeField] private Material _fontMaterial;

        private HandBarView _hand;
        private GameIntroSequence _intro;
        private DeployRingView _ring;
        private Camera _camera;

        private int _targetSlot = -1;
        private bool _active;
        private RectTransform _hand2d;         // the finger
        private RectTransform _targetSlotRect;
        private Canvas _slotLift;              // temporary canvas that pops the Cannon above the dim
        private GraphicRaycaster _slotRaycaster; // its own raycaster (a nested canvas needs one to stay clickable)
        private float _gestureT;
        private GameObject _overlay;

        private void Start()
        {
            if (PlayerPrefs.GetInt(DoneKey, 0) == 1) { enabled = false; return; }
            if (_context == null) _context = FindFirstObjectByType<GameContext>();
            _hand = FindFirstObjectByType<HandBarView>();
            _intro = FindFirstObjectByType<GameIntroSequence>();
            _ring = FindFirstObjectByType<DeployRingView>();
            _camera = Camera.main;
            if (_context == null || _hand == null || _camera == null) { enabled = false; return; }

            // Fresh journey only (never on a resume).
            if (CampaignProfile.Load().nextWave > 1) { PlayerPrefs.SetInt(DoneKey, 1); enabled = false; return; }

            // Force the Cannon into the opening hand NOW so the intro deals it in slot 0.
            _targetSlot = _context.Deck.EnsureCardInHand("Cannon", 0);

            if (_intro != null && !_intro.IsComplete) _intro.Completed += Begin;
            else Begin();
        }

        private void Begin()
        {
            if (_intro != null) _intro.Completed -= Begin;

            // Re-resolve in case the hand cycled during the intro.
            if (_targetSlot < 0 || SlotCard(_targetSlot)?.id != "Cannon")
                _targetSlot = _context.Deck.EnsureCardInHand("Cannon", 0);
            if (_targetSlot < 0) { enabled = false; return; } // no Cannon in the deck — skip, retry next launch

            _targetSlotRect = ResolveSlotRect(_targetSlot);
            _hand.LockToSlot(_targetSlot);
            _ring?.SetDragHighlight(true);
            LiftCannonAboveDim();
            BuildOverlay();
            _active = true;
        }

        private CardDefinitionSO SlotCard(int slot) =>
            slot >= 0 && slot < DeckService.HandSize ? _context.CardPlay.CardAt(slot) : null;

        private void Update()
        {
            if (!_active) return;

            // END THE MOMENT THE CANNON IS TOUCHED (then the player finishes the drag freely).
            if (_hand.PressedSlot == _targetSlot) { Finish(); return; }

            // finger traces card → ring (shows the intended drag), looping
            _gestureT += Time.unscaledDeltaTime / Mathf.Max(0.1f, _gestureSeconds);
            float phase = Mathf.Repeat(_gestureT, 1f);
            Vector2 from = _targetSlotRect != null ? (Vector2)_targetSlotRect.position
                                                   : new Vector2(Screen.width * 0.5f, Screen.height * 0.18f);
            // Point at EMPTY ground inside the ring (toward the player), NOT the King/tower at center.
            Vector2 to = _camera.WorldToScreenPoint(_context.MapCenter + new Vector3(0f, 0f, -3.6f));
            float e = Mathf.SmoothStep(0f, 1f, phase);
            Vector2 pos = Vector2.Lerp(from, to, e);
            pos.y += Mathf.Sin(phase * Mathf.PI) * 55f; // slight arc
            if (_hand2d != null)
            {
                _hand2d.position = pos;
                float tap = 0.9f + 0.14f * Mathf.Cos(phase * Mathf.PI * 2f); // little press bob
                _hand2d.localScale = Vector3.one * tap;
                var img = _hand2d.GetComponent<Image>();
                if (img != null) { var c = img.color; c.a = Mathf.Lerp(0.55f, 1f, Mathf.Sin(phase * Mathf.PI)); img.color = c; }
            }
        }

        private void Finish()
        {
            _active = false;
            _hand?.Unlock();
            _ring?.SetDragHighlight(false);
            if (_slotRaycaster != null) Destroy(_slotRaycaster); // raycaster before its canvas
            if (_slotLift != null) Destroy(_slotLift);
            if (_overlay != null) Destroy(_overlay);
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
            enabled = false;
        }

        // ---------------- highlight + overlay ----------------

        /// <summary>Pop the Cannon slot onto its own high-sorting canvas so it renders BRIGHT above the dim.</summary>
        private void LiftCannonAboveDim()
        {
            if (_targetSlotRect == null) return;
            var go = _targetSlotRect.gameObject;
            _slotLift = go.AddComponent<Canvas>();
            _slotLift.overrideSorting = true;
            _slotLift.sortingOrder = 6000; // renders BRIGHT above the dim overlay (5000)
            // A nested canvas needs its OWN raycaster or its card stops receiving taps.
            _slotRaycaster = go.AddComponent<GraphicRaycaster>();
        }

        private void BuildOverlay()
        {
            _overlay = new GameObject("TutorialOverlay", typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = _overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;

            // dim + block everything (the lifted Cannon at 6000 stays clickable/bright)
            var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            var drt = (RectTransform)dimGo.transform;
            drt.SetParent(_overlay.transform, false);
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            var dim = dimGo.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.78f); // darker so the Cannon really pops
            // Visual-only: a raycast-blocking overlay would ALSO eat the Cannon's tap (nested-canvas
            // overrideSorting changes render order, not raycast priority). Input is gated instead by
            // HandBarView.LockToSlot (only the Cannon responds), so this just darkens.
            dim.raycastTarget = false;
            _overlay.GetComponent<GraphicRaycaster>().enabled = false;

            // prompt: short, Praxis navy-stroke on a subtle plate
            var plateGo = new GameObject("PromptPlate", typeof(RectTransform), typeof(Image));
            var plate = (RectTransform)plateGo.transform;
            plate.SetParent(_overlay.transform, false);
            plate.anchorMin = new Vector2(0.5f, 1f); plate.anchorMax = new Vector2(0.5f, 1f); plate.pivot = new Vector2(0.5f, 1f);
            plate.anchoredPosition = new Vector2(0f, -Screen.height * 0.2f);
            plate.sizeDelta = new Vector2(Screen.width * 0.8f, 120f);
            var pimg = plateGo.GetComponent<Image>();
            pimg.sprite = SoftCircleSprite(); pimg.type = Image.Type.Sliced;
            pimg.color = new Color(0f, 0f, 0f, 0.35f); pimg.raycastTarget = false;

            var textGo = new GameObject("Prompt", typeof(RectTransform), typeof(TextMeshProUGUI));
            var trt = (RectTransform)textGo.transform;
            trt.SetParent(plate, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(20f, 8f); trt.offsetMax = new Vector2(-20f, -8f);
            var prompt = textGo.GetComponent<TextMeshProUGUI>();
            if (_font != null) prompt.font = _font;
            if (_fontMaterial != null) prompt.fontSharedMaterial = _fontMaterial;
            prompt.text = "Drag and drop the card into the ring";
            prompt.enableAutoSizing = true; prompt.fontSizeMin = 24; prompt.fontSizeMax = 40;
            prompt.alignment = TextAlignmentOptions.Center;
            prompt.raycastTarget = false;

            // cartoon hand finger
            var handGo = new GameObject("Hand", typeof(RectTransform), typeof(Image));
            _hand2d = (RectTransform)handGo.transform;
            _hand2d.SetParent(_overlay.transform, false);
            _hand2d.sizeDelta = new Vector2(150f, 156f);
            _hand2d.pivot = new Vector2(0.25f, 0.9f); // fingertip near the top-left of the sprite
            var himg = handGo.GetComponent<Image>();
            himg.sprite = _handSprite != null ? _handSprite : SoftCircleSprite();
            himg.raycastTarget = false;
            himg.preserveAspect = true;
            // Hand renders ON TOP of everything (above the lifted Cannon at 6000 and the dim).
            var handCanvas = handGo.AddComponent<Canvas>();
            handCanvas.overrideSorting = true;
            handCanvas.sortingOrder = 7000;
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
                    float a = Mathf.Clamp01(1f - d); a = a * a;
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

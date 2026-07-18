using UnityEngine;
using UnityEngine.UI;
using RoyalSiege.Core;

namespace RoyalSiege.UI
{
    /// <summary>
    /// v4 level-up / stage-complete screen, skinned to the 18-Jul LevelCompleteFail mocks:
    /// dark scrim → gold ribbon banner ("Level Up!") → "You saved the tower!" → checkpoint
    /// count → happy king illustration → "Cards Unlocked:" + the unlocked card → Continue
    /// button. Runtime-built from serialized sprites; the sim is already paused by
    /// CheckpointService and CONTINUE calls Dismissed() to resume the held wave gap.
    /// </summary>
    public sealed class CheckpointScreenView : MonoBehaviour
    {
        [SerializeField] private GameContext _context;
        [Header("Mock art (Assets/TowerDefense/UI/LevelCompleteFail)")]
        [SerializeField] private Sprite _titleBanner;   // title_base_passl
        [SerializeField] private Sprite _kingHappy;     // ill_pass
        [SerializeField] private Sprite _buttonBase;    // button_base
        [SerializeField] private Sprite _cardFrame;     // Frame

        private GameObject _panel;
        private Text _title;
        private Text _subtitle;
        private Text _detail;
        private Text _unlockLabel;
        private Image _unlockIcon;
        private GameObject _unlockGroup;
        private System.Action _dismiss;
        private int _checkpointsSeen;

        private void Start()
        {
            if (_context == null) _context = FindFirstObjectByType<GameContext>();
            BuildUi();
            _context.Events.CheckpointReached += OnCheckpoint;
        }

        private void OnDestroy()
        {
            if (_context != null && _context.Events != null)
                _context.Events.CheckpointReached -= OnCheckpoint;
        }

        private void OnCheckpoint(CheckpointReachedArgs args)
        {
            _dismiss = args.Dismissed;
            _checkpointsSeen++;

            if (args.IsStageComplete)
            {
                _title.text = "Stage Complete!";
                _subtitle.text = "You saved the tower!";
                _detail.text = new string('★', args.Stars) + new string('☆', 3 - args.Stars);
            }
            else
            {
                _title.text = "Level Up!";
                _subtitle.text = "You saved the tower!";
                _detail.text = "— " + _checkpointsSeen + " Checkpoint" + (_checkpointsSeen > 1 ? "s" : "") + " Completed —";
            }

            bool hasUnlock = args.Unlock != null;
            _unlockGroup.SetActive(hasUnlock);
            if (hasUnlock)
            {
                _unlockLabel.text = args.Unlock.displayName;
                _unlockIcon.sprite = args.Unlock.icon;
                _unlockIcon.enabled = args.Unlock.icon != null;
            }

            _panel.transform.SetAsLastSibling();
            _panel.SetActive(true);
        }

        private void OnContinue()
        {
            _panel.SetActive(false);
            var dismiss = _dismiss;
            _dismiss = null;
            dismiss?.Invoke();
        }

        private void BuildUi()
        {
            var canvas = GetComponentInParent<Canvas>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _panel = new GameObject("CheckpointScreen", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)_panel.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            _panel.GetComponent<Image>().color = new Color(0.05f, 0.09f, 0.04f, 0.92f); // dark green scrim per mock

            // Gold ribbon banner + title
            var banner = MakeImage(rect, _titleBanner, new Vector2(640f, 150f), new Vector2(0f, 560f));
            _title = MakeText((RectTransform)banner.transform, font, 52, Vector2.zero, Color.white);
            _title.fontStyle = FontStyle.Bold;

            _subtitle = MakeText(rect, font, 38, new Vector2(0f, 440f), Color.white);
            _detail = MakeText(rect, font, 36, new Vector2(0f, 370f), new Color(1f, 0.85f, 0.35f));
            _detail.fontStyle = FontStyle.Bold;

            // Happy king illustration (mock center-piece)
            MakeImage(rect, _kingHappy, new Vector2(560f, 560f), new Vector2(0f, 20f));

            // Cards Unlocked group
            _unlockGroup = new GameObject("UnlockGroup", typeof(RectTransform));
            var ug = (RectTransform)_unlockGroup.transform;
            ug.SetParent(rect, false);
            ug.anchoredPosition = new Vector2(0f, -330f);
            ug.sizeDelta = new Vector2(600f, 320f);
            var unlockTitle = MakeText(ug, font, 40, new Vector2(0f, 130f), new Color(1f, 0.85f, 0.35f));
            unlockTitle.text = "Cards Unlocked:";
            unlockTitle.fontStyle = FontStyle.Bold;
            var frame = MakeImage(ug, _cardFrame, new Vector2(170f, 215f), new Vector2(0f, -30f));
            _unlockIcon = MakeImage((RectTransform)frame.transform, null, new Vector2(140f, 130f), new Vector2(0f, 18f));
            _unlockLabel = MakeText(ug, font, 30, new Vector2(0f, -165f), Color.white);

            // Continue button (mock blue button base)
            var buttonGo = new GameObject("Continue", typeof(RectTransform), typeof(Image), typeof(Button));
            var buttonRect = (RectTransform)buttonGo.transform;
            buttonRect.SetParent(rect, false);
            buttonRect.sizeDelta = new Vector2(430f, 125f);
            buttonRect.anchoredPosition = new Vector2(0f, -570f);
            var bi = buttonGo.GetComponent<Image>();
            if (_buttonBase != null) { bi.sprite = _buttonBase; bi.type = Image.Type.Sliced; }
            else bi.color = new Color(0.2f, 0.55f, 0.95f);
            buttonGo.GetComponent<Button>().onClick.AddListener(OnContinue);
            var buttonLabel = MakeText(buttonRect, font, 44, Vector2.zero, Color.white);
            buttonLabel.text = "Continue";
            buttonLabel.fontStyle = FontStyle.Bold;

            _panel.SetActive(false);
        }

        private static Image MakeImage(RectTransform parent, Sprite sprite, Vector2 size, Vector2 offset)
        {
            var go = new GameObject("Img", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.enabled = sprite != null;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        private static Text MakeText(RectTransform parent, Font font, int size, Vector2 offset, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(950f, 130f);
            rect.anchoredPosition = offset;
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }
    }
}

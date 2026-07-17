using UnityEngine;
using UnityEngine.UI;
using RoyalSiege.Core;

namespace RoyalSiege.UI
{
    /// <summary>
    /// v4 level-up / stage-complete screen (§9). Runtime-built greybox modal: dark scrim,
    /// "ROYAL TOWER LEVEL N" (or "STAGE COMPLETE" + stars), the unlocked card reveal, and
    /// a CONTINUE button. The sim is already paused by CheckpointService; CONTINUE calls
    /// Dismissed() which unpauses — the held wave gap then resumes.
    /// </summary>
    public sealed class CheckpointScreenView : MonoBehaviour
    {
        [SerializeField] private GameContext _context;

        private GameObject _panel;
        private Text _title;
        private Text _detail;
        private Text _unlockLabel;
        private System.Action _dismiss;

        private void Start()
        {
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

            if (args.IsStageComplete)
            {
                _title.text = "STAGE COMPLETE";
                _detail.text = new string('★', args.Stars) + new string('☆', 3 - args.Stars)
                               + "\nCheckpoint saved · wave " + (args.AfterWave + 1) + " next";
            }
            else
            {
                _title.text = "ROYAL TOWER LEVEL " + args.TowerLevel;
                _detail.text = "Tower fully repaired\nCheckpoint saved · wave " + (args.AfterWave + 1) + " next";
            }

            _unlockLabel.text = args.Unlock != null
                ? "NEW CARD  ·  " + args.Unlock.displayName + "\njoins your deck now"
                : "";

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
            _panel.GetComponent<Image>().color = new Color(0.03f, 0.04f, 0.09f, 0.93f);

            _title = MakeText(rect, font, 74, new Vector2(0f, 320f), new Color(1f, 0.87f, 0.35f));
            _title.fontStyle = FontStyle.Bold;
            _detail = MakeText(rect, font, 40, new Vector2(0f, 140f), Color.white);
            _unlockLabel = MakeText(rect, font, 46, new Vector2(0f, -80f), new Color(0.6f, 0.85f, 1f));
            _unlockLabel.fontStyle = FontStyle.Bold;

            var buttonGo = new GameObject("Continue", typeof(RectTransform), typeof(Image), typeof(Button));
            var buttonRect = (RectTransform)buttonGo.transform;
            buttonRect.SetParent(rect, false);
            buttonRect.sizeDelta = new Vector2(520f, 130f);
            buttonRect.anchoredPosition = new Vector2(0f, -420f);
            buttonGo.GetComponent<Image>().color = new Color(0.25f, 0.65f, 0.3f);
            buttonGo.GetComponent<Button>().onClick.AddListener(OnContinue);
            var buttonLabel = MakeText(buttonRect, font, 48, Vector2.zero, Color.white);
            buttonLabel.text = "CONTINUE";
            buttonLabel.fontStyle = FontStyle.Bold;

            _panel.SetActive(false);
        }

        private static Text MakeText(RectTransform parent, Font font, int size, Vector2 offset, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(950f, 240f);
            rect.anchoredPosition = offset;
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }
    }
}

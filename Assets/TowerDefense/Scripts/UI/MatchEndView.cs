using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using RoyalSiege.Core;

namespace RoyalSiege.UI
{
    /// <summary>
    /// Victory/defeat overlay. Greybox — skinned on Day 3.
    /// v4 (§9): defeat offers "RETRY FROM CHECKPOINT" — the profile was saved at the last
    /// checkpoint, so a scene reload IS the retry (full-HP tower at its level, 5 elixir,
    /// empty field, fresh shuffle). Victory offers "PLAY AGAIN" (clears the profile).
    /// </summary>
    public sealed class MatchEndView : MonoBehaviour
    {
        [SerializeField] private GameContext _context;
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _detailLabel;

        private Button _actionButton;
        private Text _actionLabel;
        private bool _victory;

        private void Start()
        {
            _panel.SetActive(false);
            BuildActionButton();
            _context.Events.MatchEnded += OnMatchEnded;
        }

        private void OnDestroy()
        {
            if (_context != null && _context.Events != null) _context.Events.MatchEnded -= OnMatchEnded;
        }

        private void OnMatchEnded(MatchResult result)
        {
            // Always render above every other UI element (QA 17-Jul DT-009).
            _panel.transform.SetAsLastSibling();
            _panel.SetActive(true);
            _victory = result.Victory;
            if (result.Victory)
            {
                _titleLabel.text = "CAMPAIGN COMPLETE";
                _detailLabel.text = new string('★', result.Stars) + new string('☆', 3 - result.Stars)
                    + "\nTower HP " + Mathf.RoundToInt(result.TowerHpPct * 100f) + "%";
                _actionLabel.text = "PLAY AGAIN";
            }
            else
            {
                _titleLabel.text = "DEFEAT";
                _detailLabel.text = "Fallen at wave " + result.WaveReached + "/" + _context.Waves.WaveCount;
                _actionLabel.text = "RETRY FROM CHECKPOINT";
            }
        }

        private void OnAction()
        {
            if (_victory) CampaignProfile.Clear();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void BuildActionButton()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("ActionButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_panel.transform, false);
            rect.sizeDelta = new Vector2(640f, 130f);
            rect.anchoredPosition = new Vector2(0f, -380f);
            go.GetComponent<Image>().color = new Color(0.25f, 0.55f, 0.85f);
            _actionButton = go.GetComponent<Button>();
            _actionButton.onClick.AddListener(OnAction);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero; labelRect.offsetMax = Vector2.zero;
            _actionLabel = labelGo.GetComponent<Text>();
            _actionLabel.font = font;
            _actionLabel.fontSize = 44;
            _actionLabel.fontStyle = FontStyle.Bold;
            _actionLabel.alignment = TextAnchor.MiddleCenter;
            _actionLabel.color = Color.white;
        }
    }
}

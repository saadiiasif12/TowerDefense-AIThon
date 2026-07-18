using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using RoyalSiege.Core;

namespace RoyalSiege.UI
{
    /// <summary>
    /// Victory/defeat overlay, skinned to the 18-Jul LevelCompleteFail mocks.
    /// DEFEAT (mock fail): red ribbon "The tower was invaded!" → "Do something fast!" →
    /// sad king illustration → red wave circle → blue "Revive" button (v4 §9: the profile
    /// was saved at the last checkpoint, so a scene reload IS the revive/retry).
    /// VICTORY: gold ribbon "Campaign Complete!" → happy king → stars → "Play Again".
    /// </summary>
    public sealed class MatchEndView : MonoBehaviour
    {
        [SerializeField] private GameContext _context;
        [Header("Mock art (Assets/TowerDefense/UI/LevelCompleteFail)")]
        [SerializeField] private Sprite _titleBannerPass;   // title_base_passl
        [SerializeField] private Sprite _titleBannerFail;   // title_base_fail
        [SerializeField] private Sprite _kingHappy;         // ill_pass
        [SerializeField] private Sprite _kingSad;           // ill_fail
        [SerializeField] private Sprite _buttonBase;        // button_base
        [SerializeField] private Sprite _circleSprite;      // filled circle for the wave badge

        private GameObject _panel;
        private Image _banner;
        private Text _title;
        private Text _subtitle;
        private Image _king;
        private GameObject _waveCircle;
        private Text _waveNumber;
        private Text _detail;
        private Text _actionLabel;
        private bool _victory;

        private void Start()
        {
            if (_context == null) _context = FindFirstObjectByType<GameContext>();
            BuildUi();
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
                _banner.sprite = _titleBannerPass;
                _title.text = "Campaign Complete!";
                _subtitle.text = "The realm is safe!";
                _king.sprite = _kingHappy;
                _waveCircle.SetActive(false);
                _detail.text = new string('★', result.Stars) + new string('☆', 3 - result.Stars)
                    + "\nTower HP " + Mathf.RoundToInt(result.TowerHpPct * 100f) + "%";
                _actionLabel.text = "Play Again";
            }
            else
            {
                _banner.sprite = _titleBannerFail;
                _title.text = "The tower was invaded!";
                _subtitle.text = "Do something fast!";
                _king.sprite = _kingSad;
                _waveCircle.SetActive(true);
                _waveNumber.text = result.WaveReached.ToString();
                _detail.text = "";
                _actionLabel.text = "Revive";
            }
        }

        private void OnAction()
        {
            if (_victory) CampaignProfile.Clear();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void BuildUi()
        {
            var canvas = GetComponentInParent<Canvas>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _panel = new GameObject("MatchEndScreen", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)_panel.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            _panel.GetComponent<Image>().color = new Color(0.07f, 0.05f, 0.04f, 0.93f); // dark scrim

            var bannerGo = new GameObject("Banner", typeof(RectTransform), typeof(Image));
            var bannerRect = (RectTransform)bannerGo.transform;
            bannerRect.SetParent(rect, false);
            bannerRect.sizeDelta = new Vector2(680f, 150f);
            bannerRect.anchoredPosition = new Vector2(0f, 560f);
            _banner = bannerGo.GetComponent<Image>();
            _banner.preserveAspect = true;
            _banner.raycastTarget = false;
            _title = MakeText(bannerRect, font, 44, Vector2.zero, Color.white);
            _title.fontStyle = FontStyle.Bold;

            _subtitle = MakeText(rect, font, 38, new Vector2(0f, 440f), Color.white);

            var kingGo = new GameObject("King", typeof(RectTransform), typeof(Image));
            var kingRect = (RectTransform)kingGo.transform;
            kingRect.SetParent(rect, false);
            kingRect.sizeDelta = new Vector2(560f, 560f);
            kingRect.anchoredPosition = new Vector2(0f, 60f);
            _king = kingGo.GetComponent<Image>();
            _king.preserveAspect = true;
            _king.raycastTarget = false;

            // Red wave circle (mock fail center-piece): disc + ring + big number.
            _waveCircle = new GameObject("WaveCircle", typeof(RectTransform), typeof(Image));
            var wc = (RectTransform)_waveCircle.transform;
            wc.SetParent(rect, false);
            wc.sizeDelta = new Vector2(210f, 210f);
            wc.anchoredPosition = new Vector2(0f, -330f);
            var disc = _waveCircle.GetComponent<Image>();
            disc.color = new Color(0.82f, 0.14f, 0.12f);
            disc.raycastTarget = false;
            disc.sprite = _circleSprite; // hard-edged filled circle (asset)
            disc.preserveAspect = true;
            _waveNumber = MakeText(wc, font, 92, Vector2.zero, Color.white);
            _waveNumber.fontStyle = FontStyle.Bold;

            _detail = MakeText(rect, font, 40, new Vector2(0f, -330f), new Color(1f, 0.85f, 0.35f));

            var buttonGo = new GameObject("Action", typeof(RectTransform), typeof(Image), typeof(Button));
            var buttonRect = (RectTransform)buttonGo.transform;
            buttonRect.SetParent(rect, false);
            buttonRect.sizeDelta = new Vector2(430f, 125f);
            buttonRect.anchoredPosition = new Vector2(0f, -570f);
            var bi = buttonGo.GetComponent<Image>();
            if (_buttonBase != null) { bi.sprite = _buttonBase; bi.type = Image.Type.Sliced; }
            else bi.color = new Color(0.2f, 0.55f, 0.95f);
            buttonGo.GetComponent<Button>().onClick.AddListener(OnAction);
            _actionLabel = MakeText(buttonRect, font, 44, Vector2.zero, Color.white);
            _actionLabel.fontStyle = FontStyle.Bold;

            _panel.SetActive(false);
        }

        private static Text MakeText(RectTransform parent, Font font, int size, Vector2 offset, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(950f, 150f);
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

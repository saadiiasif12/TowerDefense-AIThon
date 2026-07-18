using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using RoyalSiege.Core;
using TMPro;

namespace RoyalSiege.UI
{
    /// <summary>
    /// Victory/defeat overlay. 18-Jul: the visuals are a real PREFAB
    /// (Prefabs/UI/MatchEndScreen) — banner, king illustration, wave circle, texts and the
    /// action button are serialized children UI can restyle freely in the editor. This
    /// component only swaps CONTENT per outcome and handles the action:
    /// DEFEAT → "The tower was invaded!" + sad king + wave circle + Revive (v4 §9: the
    /// profile was saved at the last checkpoint, so a scene reload IS the revive/retry).
    /// VICTORY → pass banner + happy king + stars + "Play Again" (clears the profile).
    /// All part refs are null-guarded so a partially-styled prefab never throws.
    /// </summary>
    public sealed class MatchEndView : MonoBehaviour
    {
        [SerializeField] private GameContext _context;

        [Header("Parts (preset on the prefab — restyle freely)")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Image _banner;        // ribbon image (pass/fail sprite swapped)
        [SerializeField] private TextMeshProUGUI _title;
        [SerializeField] private TextMeshProUGUI _subtitle;
        [SerializeField] private Image _king;          // happy/sad illustration
        [Tooltip("Red circle + number shown on DEFEAT only.")]
        [SerializeField] private GameObject _waveCircle;
        [SerializeField] private TextMeshProUGUI _waveNumber;
        [SerializeField] private TextMeshProUGUI _detail;         // stars + HP on victory
        [SerializeField] private Button _actionButton;
        [SerializeField] private TextMeshProUGUI _actionLabel;

        [Header("Outcome art (swapped by code)")]
        [SerializeField] private Sprite _titleBannerPass;   // title_base_passl
        [SerializeField] private Sprite _titleBannerFail;   // title_base_fail
        [SerializeField] private Sprite _kingHappy;         // ill_pass
        [SerializeField] private Sprite _kingSad;           // ill_fail

        [Header("King reaction flipbook (18-Jul — looping sprite-sheet reactions)")]
        [SerializeField] private UiSpriteSheetFlipbook _kingFlipbook;
        [SerializeField] private Texture _kingHappySheet;   // thumka_king_sheet
        [SerializeField] private Texture _kingSadSheet;     // fail_king_sheet

        [Header("Audio")]
        [Tooltip("Played on the Revive tap — on a DontDestroyOnLoad temp source so the scene reload can't cut it.")]
        [SerializeField] private AudioClip _reviveSfx;

        private bool _victory;
        // 18-Jul tower cinematic gate: on DEFEAT the tower's fire-blast/ruin sequence starts
        // BEFORE this handler runs (TowerLevelView subscribed first), so the fail screen
        // buffers and shows on TowerTransitionCompleted. Timeout = never soft-lock.
        private bool _transitionRunning;
        private bool _showPending;
        private float _pendingTimeout;
        private const float ShowTimeoutSeconds = 6f;

        private void Start()
        {
            if (_context == null) _context = FindFirstObjectByType<GameContext>();
            if (_panel != null) _panel.SetActive(false);
            if (_actionButton != null) _actionButton.onClick.AddListener(OnAction);
            if (_context != null && _context.Events != null)
            {
                _context.Events.MatchEnded += OnMatchEnded;
                _context.Events.TowerTransitionStarted += OnTransitionStarted;
                _context.Events.TowerTransitionCompleted += OnTransitionCompleted;
            }
        }

        private void OnDestroy()
        {
            if (_actionButton != null) _actionButton.onClick.RemoveListener(OnAction);
            if (_context != null && _context.Events != null)
            {
                _context.Events.MatchEnded -= OnMatchEnded;
                _context.Events.TowerTransitionStarted -= OnTransitionStarted;
                _context.Events.TowerTransitionCompleted -= OnTransitionCompleted;
            }
        }

        private void OnTransitionStarted() => _transitionRunning = true;

        private void OnTransitionCompleted()
        {
            _transitionRunning = false;
            if (_showPending) ShowPanel();
        }

        private void Update()
        {
            if (!_showPending) return;
            _pendingTimeout -= Time.unscaledDeltaTime;
            if (_pendingTimeout <= 0f) ShowPanel(); // failsafe: sequence never signalled
        }

        private void ShowPanel()
        {
            _showPending = false;
            if (_panel != null)
            {
                // Always render above every other UI element (QA 17-Jul DT-009).
                _panel.transform.SetAsLastSibling();
                _panel.SetActive(true);
            }
        }

        private void OnMatchEnded(MatchResult result)
        {
            _victory = result.Victory;

            if (result.Victory)
            {
                if (_banner != null && _titleBannerPass != null) _banner.sprite = _titleBannerPass;
                if (_title != null) _title.text = "Campaign Complete!";
                if (_subtitle != null) _subtitle.text = "The realm is safe!";
                if (_king != null && _kingHappy != null) _king.sprite = _kingHappy;
                if (_kingFlipbook != null && _kingHappySheet != null) _kingFlipbook.SetSheet(_kingHappySheet);
                if (_waveCircle != null) _waveCircle.SetActive(false);
                if (_detail != null) _detail.text =
                    new string('★', result.Stars) + new string('☆', 3 - result.Stars)
                    + "\nTower HP " + Mathf.RoundToInt(result.TowerHpPct * 100f) + "%";
                if (_actionLabel != null) _actionLabel.text = "Play Again";
            }
            else
            {
                if (_banner != null && _titleBannerFail != null) _banner.sprite = _titleBannerFail;
                if (_title != null) _title.text = "The tower was invaded!";
                if (_subtitle != null) _subtitle.text = "Do something fast!";
                if (_king != null && _kingSad != null) _king.sprite = _kingSad;
                if (_kingFlipbook != null && _kingSadSheet != null) _kingFlipbook.SetSheet(_kingSadSheet);
                if (_waveCircle != null) _waveCircle.SetActive(true);
                if (_waveNumber != null) _waveNumber.text = result.WaveReached.ToString();
                if (_detail != null) _detail.text = "";
                if (_actionLabel != null) _actionLabel.text = "Revive";
            }

            // Tower blast/ruin cinematic first, screen second (18-Jul sequence spec).
            // Victory has no tower transition — shows immediately.
            if (_transitionRunning)
            {
                _showPending = true;
                _pendingTimeout = ShowTimeoutSeconds;
            }
            else ShowPanel();
        }

        /// <summary>Wired to the action button (also callable from custom UI).</summary>
        public void OnAction()
        {
            if (_victory) CampaignProfile.Clear();
            else PlayPersistent(_reviveSfx);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private static void PlayPersistent(AudioClip clip)
        {
            if (clip == null) return;
            var go = new GameObject("ReviveSfx");
            DontDestroyOnLoad(go);
            var source = go.AddComponent<AudioSource>();
            source.spatialBlend = 0f;
            source.PlayOneShot(clip);
            Destroy(go, clip.length + 0.1f);
        }
    }
}

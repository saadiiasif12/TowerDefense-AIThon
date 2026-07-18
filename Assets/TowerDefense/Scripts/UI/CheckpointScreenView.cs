using UnityEngine;
using UnityEngine.UI;
using RoyalSiege.Core;
using TMPro;

namespace RoyalSiege.UI
{
    /// <summary>
    /// v4 level-up / stage-complete screen. 18-Jul: the visuals are a real PREFAB
    /// (Prefabs/UI/CheckpointScreen) — banner, king art, texts, unlock card and the
    /// Continue button are serialized children UI can restyle freely in the editor.
    /// This component only drives CONTENT and show/hide; the sim is already paused by
    /// CheckpointService and CONTINUE calls Dismissed() to resume the held wave gap.
    /// All part refs are null-guarded so a partially-styled prefab never throws.
    /// </summary>
    public sealed class CheckpointScreenView : MonoBehaviour
    {
        [SerializeField] private GameContext _context;

        [Header("Parts (preset on the prefab — restyle freely)")]
        [Tooltip("Root panel shown on checkpoint, hidden on Continue.")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TextMeshProUGUI _title;          // "Level Up!" / "Stage Complete!"
        [SerializeField] private TextMeshProUGUI _subtitle;       // "You saved the tower!"
        [SerializeField] private TextMeshProUGUI _detail;         // checkpoint count / stars
        [Tooltip("Whole 'Cards Unlocked' group — hidden when the checkpoint has no unlock.")]
        [SerializeField] private GameObject _unlockGroup;
        [SerializeField] private Image _unlockIcon;
        [SerializeField] private TextMeshProUGUI _unlockLabel;
        [Tooltip("The unlocked card's energy cost (number next to the mana gem).")]
        [SerializeField] private TextMeshProUGUI _unlockCost;
        [Tooltip("The unlocked card's description text.")]
        [SerializeField] private TextMeshProUGUI _unlockDescription;
        [SerializeField] private Button _continueButton;

        private System.Action _dismiss;
        private int _checkpointsSeen;
        // 18-Jul tower cinematic gate: the tower's sink/rise sequence starts BEFORE
        // CheckpointReached is raised (same call stack), so when a transition is running the
        // screen buffers and shows on TowerTransitionCompleted. Timeout = never soft-lock.
        private bool _transitionRunning;
        private bool _showPending;
        private float _pendingTimeout;
        private const float ShowTimeoutSeconds = 6f;

        private void Start()
        {
            if (_context == null) _context = FindFirstObjectByType<GameContext>();
            if (_panel != null) _panel.SetActive(false);
            if (_continueButton != null) _continueButton.onClick.AddListener(OnContinue);
            if (_context != null && _context.Events != null)
            {
                _context.Events.CheckpointReached += OnCheckpoint;
                _context.Events.TowerTransitionStarted += OnTransitionStarted;
                _context.Events.TowerTransitionCompleted += OnTransitionCompleted;
            }
        }

        private void OnDestroy()
        {
            if (_continueButton != null) _continueButton.onClick.RemoveListener(OnContinue);
            if (_context != null && _context.Events != null)
            {
                _context.Events.CheckpointReached -= OnCheckpoint;
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
                _panel.transform.SetAsLastSibling(); // always above the rest of the HUD
                _panel.SetActive(true);
            }
        }

        private void OnCheckpoint(CheckpointReachedArgs args)
        {
            _dismiss = args.Dismissed;
            _checkpointsSeen++;

            if (args.IsStageComplete)
            {
                if (_title != null) _title.text = "Stage Complete!";
                if (_subtitle != null) _subtitle.text = "You saved the tower!";
                if (_detail != null) _detail.text = new string('★', args.Stars) + new string('☆', 3 - args.Stars);
            }
            else
            {
                if (_title != null) _title.text = "Level Up!";
                if (_subtitle != null) _subtitle.text = "You saved the tower!";
                if (_detail != null) _detail.text =
                    "— " + _checkpointsSeen + " Checkpoint" + (_checkpointsSeen > 1 ? "s" : "") + " Completed —";
            }

            bool hasUnlock = args.Unlock != null;
            if (_unlockGroup != null) _unlockGroup.SetActive(hasUnlock);
            if (hasUnlock)
            {
                if (_unlockLabel != null) _unlockLabel.text = args.Unlock.displayName;
                if (_unlockIcon != null)
                {
                    _unlockIcon.sprite = args.Unlock.icon;
                    _unlockIcon.enabled = args.Unlock.icon != null;
                }
                // 18-Jul: the reveal also teaches the card — energy cost + description.
                if (_unlockCost != null) _unlockCost.text = args.Unlock.cost.ToString("0");
                if (_unlockDescription != null) _unlockDescription.text = args.Unlock.description;
            }

            // Tower cinematic first, screen second (18-Jul sequence spec). If no tower
            // transition is running (e.g. stage-complete without a level change), show now.
            if (_transitionRunning)
            {
                _showPending = true;
                _pendingTimeout = ShowTimeoutSeconds;
            }
            else ShowPanel();
        }

        /// <summary>Wired to the Continue button (also callable from custom UI).</summary>
        public void OnContinue()
        {
            if (_panel != null) _panel.SetActive(false);
            var dismiss = _dismiss;
            _dismiss = null;
            dismiss?.Invoke();
        }
    }
}

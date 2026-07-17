using UnityEngine;
using UnityEngine.UI;
using RoyalSiege.Core;

namespace RoyalSiege.UI
{
    /// <summary>Victory/defeat overlay with stars. Greybox — skinned on Day 3.</summary>
    public sealed class MatchEndView : MonoBehaviour
    {
        [SerializeField] private GameContext _context;
        [SerializeField] private GameObject _panel;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _detailLabel;

        private void Start()
        {
            _panel.SetActive(false);
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
            if (result.Victory)
            {
                _titleLabel.text = "VICTORY";
                _detailLabel.text = new string('★', result.Stars) + new string('☆', 3 - result.Stars)
                    + "\nTower HP " + Mathf.RoundToInt(result.TowerHpPct * 100f) + "%";
            }
            else
            {
                _titleLabel.text = "DEFEAT";
                _detailLabel.text = "Reached wave " + result.WaveReached + "/" + _context.Waves.WaveCount;
            }
        }
    }
}

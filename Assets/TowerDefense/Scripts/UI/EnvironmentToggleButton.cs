using UnityEngine;
using UnityEngine.UI;
using RoyalSiege.Juice;

namespace RoyalSiege.UI
{
    /// <summary>
    /// One circular HUD button that flips the arena environment and swaps its own icon to the
    /// biome now showing. Icon i pairs with environment index i (Grass=0, Snow=1, …). View-only:
    /// it just calls StageEnvironmentView.CycleManual() and reflects the returned index.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class EnvironmentToggleButton : MonoBehaviour
    {
        [SerializeField] private StageEnvironmentView _environment;
        [Tooltip("The circle image whose sprite swaps to match the active environment.")]
        [SerializeField] private Image _icon;
        [Tooltip("One sprite per environment index (element i shown when environment i is active).")]
        [SerializeField] private Sprite[] _iconPerEnvironment;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClick);
        }

        private void Start()
        {
            if (_environment == null) _environment = FindFirstObjectByType<StageEnvironmentView>();
            if (_environment != null)
            {
                _environment.Changed += SyncIcon; // stage swaps update the icon too, not just clicks
                SyncIcon(Mathf.Max(0, _environment.ActiveIndex));
            }
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(OnClick);
            if (_environment != null) _environment.Changed -= SyncIcon;
        }

        private void OnClick()
        {
            if (_environment == null) return;
            _environment.CycleManual(); // the Changed event drives SyncIcon
        }

        private void SyncIcon(int index)
        {
            if (_icon == null || _iconPerEnvironment == null || _iconPerEnvironment.Length == 0) return;
            int i = ((index % _iconPerEnvironment.Length) + _iconPerEnvironment.Length) % _iconPerEnvironment.Length;
            if (_iconPerEnvironment[i] != null) _icon.sprite = _iconPerEnvironment[i];
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RoyalSiege.Data;

namespace RoyalSiege.UI
{
    /// <summary>
    /// One hand slot. 17-Jul UI pass: the visual parts are now real serialized children of a
    /// PREFAB (CardSlot / CardSlotNext) so they can be preset in the editor — this component
    /// only drives content (art/cost/cooldown/affordability) and the select/refill/shake/carry
    /// tweens on the Lift container (the slot rect itself never moves — spec rule). A "next"
    /// slot has _isNext = true (shows no cost, takes no input) and carries its own "Next Up"
    /// label as static prefab text. All part refs are null-guarded so a partial prefab is safe.
    /// </summary>
    public sealed class CardSlotView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Type")]
        [Tooltip("True on the CardSlotNext prefab: no cost badge, no input.")]
        [SerializeField] private bool _isNext;

        [Header("Parts (assigned in the prefab)")]
        [Tooltip("Container that scales/lifts on select — holds all card visuals.")]
        [SerializeField] private RectTransform _lift;
        [SerializeField] private Image _art;              // card thumbnail (icon)
        [SerializeField] private Image _frame;            // blue border
        [SerializeField] private Image _glow;             // gold selection glow (optional)
        [SerializeField] private Image _cooldownOverlay;  // radial dark sweep (optional)
        [SerializeField] private GameObject _costBadge;   // gem + number group (normal only)
        [SerializeField] private Image _gem;              // lightning cost gem
        [SerializeField] private Text _costLabel;         // cost number

        private const float SelectScale = 1.10f;
        private const float SelectLiftPx = 16f;
        private const float SelectInSeconds = 0.1f;
        private const float SelectOutSeconds = 0.16f;
        private const float RefillSeconds = 0.09f;
        private const float ShakeSeconds = 0.15f;
        private const float ShakePx = 5f;

        private static readonly Color GemLocked = new(0.55f, 0.55f, 0.6f);

        private int _slot = -1;
        private HandBarView _owner;
        private CardDefinitionSO _card;
        private bool _affordable = true;
        private Data.CardInteractionAnimationConfig _config;

        private bool _selected;
        private float _selectT = 1f;
        private float _refillT = 1f;
        private float _shakeT = 1f;
        private bool _carried;
        private bool _holdEmpty;              // 18-Jul deck cycle: stay empty until the flyer lands
        private CardDefinitionSO _pendingCard;
        private bool _hasPendingCard;
        private float _affordPulseT = 1f;     // 18-Jul: highlight pulse when affordable again
        private float _squashT = 1f;          // 18-Jul: cancel-landing / refill-landing squash

        public bool IsCarried => _carried;
        public CardDefinitionSO Card => _card;
        public bool IsNext => _isNext;
        public RectTransform Rect => (RectTransform)transform;
        /// <summary>The card-visual container the drag proxy clones for a pixel-exact floating card.</summary>
        public RectTransform LiftRect => _lift;
        /// <summary>Slot size, so the floating clone matches the tray card dimensions exactly.</summary>
        public Vector2 CardSize => ((RectTransform)transform).rect.size;

        public void Init(int slot, HandBarView owner, Data.CardInteractionAnimationConfig config = null)
        {
            _slot = slot;
            _owner = owner;
            _config = config;
            if (_glow != null) _glow.gameObject.SetActive(false);
            if (_costBadge != null) _costBadge.SetActive(false);
        }

        // ---------------- content ----------------

        public void SetCard(CardDefinitionSO card)
        {
            if (_carried || _holdEmpty) { _pendingCard = card; _hasPendingCard = true; return; }
            ApplyCard(card);
        }

        private void ApplyCard(CardDefinitionSO card)
        {
            _card = card;
            bool has = card != null;
            if (_frame != null) _frame.enabled = has;
            if (_art != null)
            {
                _art.enabled = has && card.icon != null;
                if (_art.enabled) _art.sprite = card.icon;
            }
            if (_costBadge != null) _costBadge.SetActive(has && !_isNext);
            if (has && _costLabel != null) _costLabel.text = card.cost.ToString("0");
        }

        public void SetState(float cooldown01, bool affordable)
        {
            if (_cooldownOverlay != null) _cooldownOverlay.fillAmount = Mathf.Clamp01(cooldown01);
            bool playable = affordable && cooldown01 <= 0f;
            if (playable == _affordable) return;
            bool becameAffordable = playable && !_affordable;
            _affordable = playable;

            if (_art != null) _art.color = playable ? Color.white : new Color(0.38f, 0.4f, 0.46f, 1f);
            if (_gem != null) _gem.color = playable ? Color.white : GemLocked;
            if (_costLabel != null) _costLabel.color = playable ? Color.white : new Color(0.72f, 0.72f, 0.78f);

            // 18-Jul: energy just reached the cost — soft highlight pulse welcomes it back.
            if (becameAffordable && !_carried && !_holdEmpty) _affordPulseT = 0f;
        }

        // ---------------- interaction states ----------------

        public void Select()
        {
            _selected = true;
            _selectT = 0f;
            if (_glow != null) _glow.gameObject.SetActive(true);
        }

        public void Deselect()
        {
            if (!_selected) return;
            _selected = false;
            _selectT = 0f;
            if (_glow != null) _glow.gameObject.SetActive(false);
        }

        public void SetCarried(bool carried)
        {
            if (_carried == carried) return;
            _carried = carried;
            if (_lift != null) _lift.gameObject.SetActive(!carried);

            if (!carried)
            {
                Deselect();
                if (_hasPendingCard)
                {
                    ApplyCard(_pendingCard);
                    _hasPendingCard = false;
                    PlayRefillPop();
                }
            }
        }

        /// <summary>
        /// 18-Jul deck cycle: the card was COMMITTED — stop being carried but keep the slot
        /// visibly empty (dark placeholder) until the Next-Up flyer lands. SetCard keeps
        /// buffering into pending while held.
        /// </summary>
        public void ReleaseCarriedHoldEmpty()
        {
            _carried = false;
            _holdEmpty = true;
            Deselect();
            if (_lift != null) _lift.gameObject.SetActive(false);
        }

        /// <summary>The flyer landed: show the new card with a restrained squash-settle.</summary>
        public void CompleteRefill()
        {
            _holdEmpty = false;
            if (_lift != null) _lift.gameObject.SetActive(true);
            if (_hasPendingCard)
            {
                ApplyCard(_pendingCard);
                _hasPendingCard = false;
            }
            _squashT = 0f;
        }

        /// <summary>Cancel-return landing squash (1.03×0.97 → 1×1).</summary>
        public void PlayLandingSquash() => _squashT = 0f;

        public void PlayRefillPop() => _refillT = 0f;
        public void ShakeUnaffordable() => _shakeT = 0f;

        private void Update()
        {
            if (_lift == null) return;
            float dt = Time.unscaledDeltaTime;

            float selectScale, lift;
            if (_selected)
            {
                _selectT = Mathf.Min(1f, _selectT + dt / SelectInSeconds);
                float e = EaseOutBack(_selectT);
                selectScale = Mathf.LerpUnclamped(1f, SelectScale, e);
                lift = Mathf.LerpUnclamped(0f, SelectLiftPx, e);
            }
            else
            {
                _selectT = Mathf.Min(1f, _selectT + dt / SelectOutSeconds);
                float e = 1f - (1f - _selectT) * (1f - _selectT);
                selectScale = Mathf.Lerp(SelectScale, 1f, e);
                lift = Mathf.Lerp(SelectLiftPx, 0f, e);
            }

            float refillScale = 1f;
            if (_refillT < 1f)
            {
                _refillT = Mathf.Min(1f, _refillT + dt / RefillSeconds);
                refillScale = _refillT < 0.6f
                    ? Mathf.Lerp(0.8f, 1.05f, _refillT / 0.6f)
                    : Mathf.Lerp(1.05f, 1f, (_refillT - 0.6f) / 0.4f);
            }

            float shakeX = 0f;
            if (_shakeT < 1f)
            {
                float shakeSeconds = _config != null ? _config.insufficientEnergyShakeDuration : ShakeSeconds;
                _shakeT = Mathf.Min(1f, _shakeT + dt / shakeSeconds);
                shakeX = Mathf.Sin(_shakeT * 24f) * ShakePx * (1f - _shakeT);
            }

            // 18-Jul: affordable-again highlight pulse (soft glow + tiny scale swell).
            float affordScale = 1f;
            if (_affordPulseT < 1f)
            {
                float pulseSeconds = _config != null ? _config.affordablePulseDuration : 0.15f;
                _affordPulseT = Mathf.Min(1f, _affordPulseT + dt / pulseSeconds);
                float wave = Mathf.Sin(_affordPulseT * Mathf.PI);
                affordScale = 1f + 0.05f * wave;
                if (_art != null) _art.color = Color.Lerp(Color.white, new Color(1f, 1f, 0.85f), wave * 0.6f);
            }

            // 18-Jul: landing squash (cancel-return / refill settle) — X and Y separately.
            float squashX = 1f, squashY = 1f;
            if (_squashT < 1f)
            {
                float squashSeconds = _config != null ? _config.cancelLandingDuration : 0.07f;
                float sqX = _config != null ? _config.cancelLandingSquashX : 1.03f;
                float sqY = _config != null ? _config.cancelLandingSquashY : 0.97f;
                _squashT = Mathf.Min(1f, _squashT + dt / squashSeconds);
                squashX = Mathf.Lerp(sqX, 1f, _squashT);
                squashY = Mathf.Lerp(sqY, 1f, _squashT);
            }

            float baseScale = selectScale * refillScale * affordScale;
            _lift.localScale = new Vector3(baseScale * squashX, baseScale * squashY, 1f);
            _lift.anchoredPosition = new Vector2(shakeX, lift);

            if (_glow != null && _glow.gameObject.activeSelf)
            {
                float pulse = 0.55f + 0.35f * Mathf.PingPong(Time.unscaledTime * 2.4f, 1f);
                var c = _glow.color; _glow.color = new Color(c.r, c.g, c.b, pulse);
            }
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        // ---------------- pointer forwarding ----------------

        public void OnPointerDown(PointerEventData e) { if (_slot >= 0) _owner.OnSlotPointerDown(_slot, e.position, e.pointerId); }
        public void OnDrag(PointerEventData e) { if (_slot >= 0) _owner.OnSlotDrag(_slot, e.position, e.pointerId); }
        public void OnPointerUp(PointerEventData e) { if (_slot >= 0) _owner.OnSlotPointerUp(_slot, e.position, e.pointerId); }
    }
}

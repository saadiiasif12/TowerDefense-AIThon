using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RoyalSiege.Data;

namespace RoyalSiege.UI
{
    /// <summary>
    /// 18-Jul deck-cycle animation (CR-style): after a successful commit the used slot stays
    /// visibly EMPTY for refillDelay, then the Next-Up card FLIES from the preview panel into
    /// the slot along a shallow arc (scale 0.44 → 1.05 → 1, rotation −6° → 0), lands with a
    /// squash, and the Next-Up panel crossfades to the following card during the final 30%.
    /// Pure view: the deck (FIFO) already cycled authoritatively — this only presents it.
    /// Flyers are pooled (up to hand size concurrent flights); all timing unscaled.
    /// </summary>
    public sealed class DeckCycleAnimator : MonoBehaviour
    {
        private sealed class Flight
        {
            public int Slot;
            public CardDefinitionSO FlyingCard;
            public CardDefinitionSO NextAfter;
            public float T;                 // seconds since enqueue (includes the delay)
            public RectTransform Flyer;
            public Image FlyerArt;
            public Image FlyerFrame;
            public Text FlyerCost;
            public CanvasGroup FlyerGroup;
            public Vector2 From, To;
            public bool CrossfadedNext;
            public bool Active;
        }

        private CardInteractionAnimationConfig _config;
        private RectTransform _canvasRect;
        private Font _font;
        private Sprite _frameSprite, _gemSprite;
        private CardSlotView[] _slots;
        private CardSlotView _nextSlot;
        private readonly List<Flight> _flights = new();

        public bool AnyActive
        {
            get { for (int i = 0; i < _flights.Count; i++) if (_flights[i].Active) return true; return false; }
        }

        public static DeckCycleAnimator Create(RectTransform canvasRect, Font font,
            CardInteractionAnimationConfig config, Sprite frame, Sprite gem,
            CardSlotView[] slots, CardSlotView nextSlot)
        {
            var go = new GameObject("DeckCycleAnimator", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvasRect, false);
            var animator = go.AddComponent<DeckCycleAnimator>();
            animator._canvasRect = canvasRect;
            animator._font = font;
            animator._config = config;
            animator._frameSprite = frame;
            animator._gemSprite = gem;
            animator._slots = slots;
            animator._nextSlot = nextSlot;
            return animator;
        }

        /// <summary>
        /// A commit just happened for this slot (deck already cycled). Captures the new card
        /// (already sitting in the deck's hand array) and the upcoming Next for the crossfade.
        /// </summary>
        public void Enqueue(int slot, CardDefinitionSO flyingCard, CardDefinitionSO nextAfter)
        {
            var flight = GetFreeFlight();
            flight.Slot = slot;
            flight.FlyingCard = flyingCard;
            flight.NextAfter = nextAfter;
            flight.T = 0f;
            flight.CrossfadedNext = false;
            flight.Active = true;
            flight.Flyer.gameObject.SetActive(false); // hidden through the delay
        }

        /// <summary>Match ended / focus lost: land everything instantly, no dangling flyers.</summary>
        public void CompleteAllInstantly()
        {
            for (int i = 0; i < _flights.Count; i++)
            {
                var f = _flights[i];
                if (!f.Active) continue;
                Land(f);
            }
        }

        private void Update()
        {
            if (_config == null) return;
            float dt = Time.unscaledDeltaTime;

            for (int i = 0; i < _flights.Count; i++)
            {
                var f = _flights[i];
                if (!f.Active) continue;
                f.T += dt;

                float delay = _config.refillDelay;
                float travel = Mathf.Max(0.01f, _config.refillTravelDuration);

                if (f.T < delay) continue;

                float u = Mathf.Clamp01((f.T - delay) / travel);

                if (!f.Flyer.gameObject.activeSelf)
                {
                    // flight starts now: bind art + endpoints (slot positions are stable)
                    BindFlyer(f);
                    f.Flyer.gameObject.SetActive(true);
                }

                // shallow arc: lerp + parabolic lift
                float arc = _config.refillArcHeightPixels * 4f * u * (1f - u);
                Vector2 pos = Vector2.LerpUnclamped(f.From, f.To, EaseOutCubic(u));
                pos.y += arc;
                f.Flyer.anchoredPosition = pos;

                // scale: start small → overshoot → 1 (ease-out-back flavoured)
                float scaleU = EaseOutBack(u);
                float scale = Mathf.LerpUnclamped(_config.refillStartScale, 1f, scaleU);
                if (u > 0.8f) scale = Mathf.Lerp(_config.refillOvershootScale, 1f, (u - 0.8f) / 0.2f);
                f.Flyer.localScale = Vector3.one * scale;
                f.Flyer.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(_config.refillStartRotation, 0f, u));

                // final 30%: the Next-Up preview advances to the following card
                if (!f.CrossfadedNext && u >= 0.7f)
                {
                    f.CrossfadedNext = true;
                    _nextSlot.SetCard(f.NextAfter);
                    _nextSlot.PlayRefillPop();
                }

                if (u >= 1f) Land(f);
            }
        }

        private void Land(Flight f)
        {
            f.Active = false;
            f.Flyer.gameObject.SetActive(false);
            _slots[f.Slot].CompleteRefill();           // shows the new card + squash-settle
            if (!f.CrossfadedNext)
            {
                _nextSlot.SetCard(f.NextAfter);
                _nextSlot.PlayRefillPop();
            }
        }

        private void BindFlyer(Flight f)
        {
            f.FlyerArt.sprite = f.FlyingCard != null ? f.FlyingCard.icon : null;
            f.FlyerArt.enabled = f.FlyerArt.sprite != null;
            f.FlyerFrame.sprite = _frameSprite;
            f.FlyerCost.text = f.FlyingCard != null ? f.FlyingCard.cost.ToString("0") : "";
            f.From = WorldToCanvas(_nextSlot.Rect);
            f.To = WorldToCanvas(_slots[f.Slot].Rect);
        }

        private Vector2 WorldToCanvas(RectTransform target)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, target.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 local);
            return local;
        }

        private Flight GetFreeFlight()
        {
            for (int i = 0; i < _flights.Count; i++)
                if (!_flights[i].Active) return _flights[i];

            // build a pooled flyer (mini card: frame + art + cost badge)
            var go = new GameObject("CardFlyer", typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.sizeDelta = new Vector2(165f, 210f);
            var group = go.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            Image Sub(string name, Vector2 aMin, Vector2 aMax)
            {
                var g = new GameObject(name, typeof(RectTransform), typeof(Image));
                var r = (RectTransform)g.transform;
                r.SetParent(rect, false);
                r.anchorMin = aMin; r.anchorMax = aMax;
                r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
                var img = g.GetComponent<Image>();
                img.raycastTarget = false;
                return img;
            }

            var art = Sub("Art", new Vector2(0.075f, 0.225f), new Vector2(0.925f, 0.815f));
            var frame = Sub("Frame", Vector2.zero, Vector2.one);

            var badge = new GameObject("Cost", typeof(RectTransform));
            var badgeRect = (RectTransform)badge.transform;
            badgeRect.SetParent(rect, false);
            badgeRect.anchorMin = new Vector2(0.5f, 0f); badgeRect.anchorMax = new Vector2(0.5f, 0f);
            badgeRect.sizeDelta = new Vector2(70f, 46f);
            badgeRect.anchoredPosition = new Vector2(0f, 20f);
            var gemImg = Sub("Gem", Vector2.zero, Vector2.zero);
            gemImg.rectTransform.SetParent(badgeRect, false);
            gemImg.rectTransform.anchorMin = new Vector2(0f, 0.5f); gemImg.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            gemImg.rectTransform.sizeDelta = new Vector2(26f, 42f);
            gemImg.rectTransform.anchoredPosition = new Vector2(16f, 0f);
            gemImg.sprite = _gemSprite;

            var costGo = new GameObject("Num", typeof(RectTransform), typeof(Text));
            var costRect = (RectTransform)costGo.transform;
            costRect.SetParent(badgeRect, false);
            costRect.anchorMin = Vector2.zero; costRect.anchorMax = Vector2.one;
            costRect.offsetMin = new Vector2(34f, 0f); costRect.offsetMax = Vector2.zero;
            var cost = costGo.GetComponent<Text>();
            cost.font = _font;
            cost.fontSize = 34;
            cost.fontStyle = FontStyle.Bold;
            cost.alignment = TextAnchor.MiddleLeft;
            cost.color = new Color(1f, 0.85f, 0.2f);
            cost.raycastTarget = false;
            var outline = costGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            go.SetActive(false);
            var flight = new Flight
            {
                Flyer = rect, FlyerArt = art, FlyerFrame = frame, FlyerCost = cost, FlyerGroup = group
            };
            _flights.Add(flight);
            return flight;
        }

        private static float EaseOutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}

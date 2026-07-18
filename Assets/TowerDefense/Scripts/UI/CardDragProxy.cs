using System;
using UnityEngine;
using UnityEngine.UI;

namespace RoyalSiege.UI
{
    /// <summary>
    /// The mini card that follows the finger during a drag (spec: zero-lag follow, scale
    /// driven by POSITION not time — 100% at the hand shrinking to 50% toward the board).
    /// The floating card is a PIXEL-EXACT CLONE of the slot's resting card visual (its "Lift"
    /// subtree), so the picked-up card matches the tray card exactly — no reflow of the art
    /// window or cost badge (18-Jul fix: the old hand-built proxy used different art insets /
    /// gem size / card size and visibly misaligned on pickup). On a successful deploy it
    /// dissolves in ~120 ms; on cancel it tweens home. Built once, cloned per drag.
    /// </summary>
    public sealed class CardDragProxy : MonoBehaviour
    {
        private const float DissolveSeconds = 0.12f;
        private const float ReturnSeconds = 0.18f;

        private RectTransform _rect;
        private RectTransform _canvasRect;
        private CanvasGroup _group;
        private GameObject _clone;   // per-drag copy of the slot's card visual

        private float _dissolveT = -1f;
        private float _returnT = -1f;
        private Vector2 _returnFrom;
        private Vector2 _returnTo;
        private Action _onReturned;

        // 18-Jul CR-style drag: fixed drag scale + fade toward the battlefield preview.
        private float _dragScale = 1.12f;
        private float _fadeTarget = 1f;
        private float _fadeSpeed = 12f;   // 1/duration, set per fade
        private float _fade = 1f;
        private Vector2 _velocity;         // SmoothDamp state (no per-frame alloc)
        private float _smoothTime;

        public static CardDragProxy Create(RectTransform canvasRect)
        {
            var go = new GameObject("CardDragProxy", typeof(RectTransform), typeof(CanvasGroup));
            var proxy = go.AddComponent<CardDragProxy>();
            proxy._canvasRect = canvasRect;
            proxy._rect = (RectTransform)go.transform;
            proxy._rect.SetParent(canvasRect, false);
            proxy._group = go.GetComponent<CanvasGroup>();
            proxy._group.blocksRaycasts = false;
            proxy._group.interactable = false;

            go.SetActive(false);
            return proxy;
        }

        /// <summary>
        /// Show a pixel-identical copy of the slot's RESTING card visual. <paramref name="sourceLift"/>
        /// is the slot's "Lift" container (already carrying this card's art/frame/cost). The clone
        /// is reset to the resting transform (no select-scale/lift) and its gold selection glow is
        /// hidden, so the floating card reads exactly like the card sitting in the tray.
        /// </summary>
        public void Show(RectTransform sourceLift, Vector2 cardSize, Vector2 screenPosition,
            float dragScale = 1.12f, float smoothTime = 0f)
        {
            _dissolveT = -1f;
            _returnT = -1f;
            _fade = 1f; _fadeTarget = 1f;
            _dragScale = dragScale;
            _smoothTime = smoothTime;
            _velocity = Vector2.zero;
            _group.alpha = 1f;

            if (_clone != null) Destroy(_clone);
            _rect.sizeDelta = cardSize;

            if (sourceLift != null)
            {
                _clone = Instantiate(sourceLift.gameObject, _rect);
                var cr = (RectTransform)_clone.transform;
                cr.anchorMin = sourceLift.anchorMin;
                cr.anchorMax = sourceLift.anchorMax;
                cr.pivot = sourceLift.pivot;
                cr.sizeDelta = sourceLift.sizeDelta;
                cr.anchoredPosition = Vector2.zero; // resting position (source may be lift-tweened)
                cr.localScale = Vector3.one;         // resting scale (source may be select-scaled)
                _clone.SetActive(true);
                // The floating card should read like the card at REST — kill the selection glow.
                var glow = cr.Find("Glow");
                if (glow != null) glow.gameObject.SetActive(false);
            }

            gameObject.SetActive(true);
            _rect.anchoredPosition = ScreenToCanvas(screenPosition);
            _rect.localScale = Vector3.one * _dragScale;
        }

        /// <summary>
        /// Follow the (already finger-offset) screen position. Direct tracking, or a very
        /// small SmoothDamp when a smoothTime was configured — responsive, never floaty.
        /// </summary>
        public void Follow(Vector2 screenPosition)
        {
            if (_returnT >= 0f || _dissolveT >= 0f) return;
            Vector2 target = ScreenToCanvas(screenPosition);
            _rect.anchoredPosition = _smoothTime <= 0f
                ? target
                : Vector2.SmoothDamp(_rect.anchoredPosition, target, ref _velocity, _smoothTime,
                    float.PositiveInfinity, Time.unscaledDeltaTime);
            _rect.localScale = Vector3.one * (_dragScale * Mathf.Lerp(0.92f, 1f, _fade));
            _group.alpha = _fade;
        }

        /// <summary>Fade toward 0 when the pointer is over the battlefield (the world preview
        /// takes over) and back to 1 over the HUD. Duration from config.</summary>
        public void SetFieldFade(bool overField, float duration)
        {
            _fadeTarget = overField ? 0f : 1f;
            _fadeSpeed = duration > 0.001f ? 1f / duration : 1000f;
        }

        public void Dissolve() => _dissolveT = 0f;

        public void ReturnTo(Vector2 slotScreenPosition, Action onReturned)
        {
            _returnT = 0f;
            _returnFrom = _rect.anchoredPosition;
            _returnTo = ScreenToCanvas(slotScreenPosition);
            _onReturned = onReturned;
        }

        public void HideImmediate()
        {
            _dissolveT = -1f;
            _returnT = -1f;
            _onReturned = null;
            if (_clone != null) { Destroy(_clone); _clone = null; }
            gameObject.SetActive(false);
        }

        private Vector2 ScreenToCanvas(Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPosition, null, out Vector2 local);
            return local;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Field fade integrates every frame (drag keeps calling Follow for pos/scale).
            if (_dissolveT < 0f && _returnT < 0f && !Mathf.Approximately(_fade, _fadeTarget))
                _fade = Mathf.MoveTowards(_fade, _fadeTarget, _fadeSpeed * dt);

            if (_dissolveT >= 0f)
            {
                _dissolveT += dt / DissolveSeconds;
                _group.alpha = 1f - _dissolveT * _dissolveT;
                if (_dissolveT >= 1f) HideImmediate();
                return;
            }

            if (_returnT >= 0f)
            {
                _returnT += dt / ReturnSeconds;
                float e = 1f - (1f - Mathf.Clamp01(_returnT)) * (1f - Mathf.Clamp01(_returnT));
                _rect.anchoredPosition = Vector2.Lerp(_returnFrom, _returnTo, e);
                _rect.localScale = Vector3.one * Mathf.Lerp(_rect.localScale.x, 1f, e);
                if (_returnT >= 1f)
                {
                    var done = _onReturned;
                    HideImmediate();
                    done?.Invoke();
                }
            }
        }
    }
}

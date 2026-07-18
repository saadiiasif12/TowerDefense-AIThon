using UnityEngine;

namespace RoyalSiege.Data
{
    /// <summary>
    /// 18-Jul card-interaction polish: EVERY timing/scale/alpha the hand, drag, preview,
    /// energy and deck-cycle animations use, in one inspector-tunable asset (zero hardcoded
    /// stats rule applies to juice too). Views read this; changing values needs no code.
    /// All UI animation runs on UNSCALED time (checkpoint pause must not freeze the hand).
    /// </summary>
    [CreateAssetMenu(menuName = "RoyalSiege/Card Interaction Animation Config", fileName = "CardAnimConfig")]
    public sealed class CardInteractionAnimationConfig : ScriptableObject
    {
        [Header("Pickup (pointer down)")]
        public float pressDuration = 0.06f;
        public float pressScale = 1.06f;
        public float pressLiftPixels = 10f;

        [Header("HUD drag")]
        public float dragScale = 1.12f;
        [Tooltip("Card floats this many reference pixels above the finger so art stays visible.")]
        public float dragFingerOffsetPixels = 96f;
        [Tooltip("0 = direct follow. Small smoothing time in seconds otherwise.")]
        public float dragSmoothTime = 0.03f;

        [Header("HUD → battlefield swap")]
        [Tooltip("Extra reference pixels above the hand panel's top edge where the battlefield begins.")]
        public float fieldBoundaryMarginPixels = 30f;
        public float hudToFieldFadeDuration = 0.08f;
        public float previewInDuration = 0.10f;
        public float previewStartScale = 0.82f;

        [Header("Preview feedback")]
        public float validPulsePeriod = 0.45f;
        public float validPulseScaleMin = 0.98f;
        public float validPulseScaleMax = 1.02f;
        public float invalidEnterShakeDuration = 0.12f;
        public float invalidEnterShakePixels = 0.06f;   // world units for the ghost
        [Tooltip("Deployment ring alpha multiplier while dragging a card (1 = unchanged).")]
        public float ringBrightenFactor = 1.6f;

        [Header("Commit")]
        public float commitHoldDuration = 0.06f;
        public float commitConfirmDuration = 0.12f;
        public float commitConfirmScale = 1.06f;
        public float previewFadeOutDuration = 0.12f;

        [Header("Cancel / return")]
        public float cancelReturnDuration = 0.18f;
        public float cancelLandingDuration = 0.07f;
        public float cancelLandingSquashX = 1.03f;
        public float cancelLandingSquashY = 0.97f;

        [Header("Energy bar")]
        public float energySpendTrailDelay = 0.05f;
        public float energyTrailDuration = 0.28f;
        public float energyRegenPopDuration = 0.12f;
        public float energyFlashDuration = 0.10f;

        [Header("Affordability feedback")]
        public float affordablePulseDuration = 0.15f;
        public float insufficientEnergyShakeDuration = 0.18f;

        [Header("Deck cycle (Next Up → empty slot)")]
        public float refillDelay = 0.84f;
        public float refillTravelDuration = 0.18f;
        public float refillStartScale = 0.44f;
        public float refillOvershootScale = 1.05f;
        public float refillStartRotation = -6f;
        public float refillArcHeightPixels = 42f;
        public float refillLandingDuration = 0.08f;
        public float nextPreviewCrossfadeDuration = 0.08f;
    }
}

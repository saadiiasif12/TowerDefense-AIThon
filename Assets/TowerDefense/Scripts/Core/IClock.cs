namespace RoyalSiege.Core
{
    /// <summary>
    /// Game-speed-aware time source. Logic runs on the tick; visuals use
    /// <see cref="ScaledDeltaTime"/> / <see cref="InterpolationAlpha"/> so pause and
    /// 1x/2x speed never touch Time.timeScale.
    /// </summary>
    public interface IClock
    {
        float LogicTime { get; }
        float SpeedMultiplier { get; set; }
        bool IsPaused { get; set; }
        /// <summary>Frame delta scaled by speed; 0 while paused. For view-only movement.</summary>
        float ScaledDeltaTime { get; }
        /// <summary>0..1 progress between the last and next logic tick, for view interpolation.</summary>
        float InterpolationAlpha { get; }
    }
}

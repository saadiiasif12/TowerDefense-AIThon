using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Aggressive frame-rate maximizer for mobile (Android + iOS).
/// Pushes every device to the highest sustainable FPS its panel supports,
/// scaling render resolution down on weaker hardware to keep the frame rate high.
/// </summary>
public class FrameRateSetter : MonoBehaviour
{
    public enum DeviceTier { Low, Mid, High, Ultra }

    [Header("Frame Rate Strategy")]
    [Tooltip("If true, ignores the per-tier caps below and always targets the panel's native refresh rate (best for max FPS).")]
    public bool matchPanelRefreshRate = true;

    [Tooltip("Hard ceiling in case a device reports a crazy refresh rate. 240 covers all current phones.")]
    public int absoluteMaxFPS = 240;

    [Header("Per-Tier FPS Caps (used when matchPanelRefreshRate = false)")]
    public int ultraFPS = 120;
    public int highFPS  = 120;
    public int midFPS   = 90;
    public int lowFPS   = 60;

    [Header("Render Scale — per-tier, applied on both Android and iOS")]
    [Tooltip("Ultra devices can afford native resolution.")]
    [Range(0.4f, 1.0f)] public float ultraScale = 1.0f;
    [Tooltip("High-tier phones: slight downscale for headroom.")]
    [Range(0.4f, 1.0f)] public float highScale  = 0.9f;
    [Tooltip("Mid-tier: noticeable downscale to hit high FPS.")]
    [Range(0.4f, 1.0f)] public float midScale   = 0.75f;
    [Tooltip("Low-tier: aggressive downscale — FPS over fidelity.")]
    [Range(0.4f, 1.0f)] public float lowScale   = 0.6f;

    [Header("Extra Optimizations")]
    [Tooltip("Disable VSync — required for targetFrameRate to work and for uncapped FPS.")]
    public bool disableVSync = true;
    [Tooltip("Disable OnDemandRendering (it artificially throttles frame rate).")]
    public bool disableOnDemandRendering = true;
    [Tooltip("Request sustained performance mode on Android (prevents thermal throttling spikes but locks to a steady ceiling).")]
    public bool sustainedPerformanceMode = false;
    [Tooltip("Raise fixed timestep so physics doesn't eat CPU at high FPS (60 FixedUpdates/sec is plenty for most games).")]
    public bool optimizeFixedTimestep = true;

    [Header("Debug")]
    public bool logDeviceInfo = true;

    private DeviceTier currentTier;
    private int resolvedTargetFPS;

    // Cached for editor-restore so we don't dirty the URP asset on disk
    private UniversalRenderPipelineAsset cachedUrpAsset;
    private float originalRenderScale = 1.0f;

    // The maximum refresh rate the display supports (discovered once at startup).
    private float _maxRefreshRate;

    void Awake()
    {
        _maxRefreshRate = DiscoverMaxRefreshRate();
        ApplyAll();
    }

    void Start()
    {
        // Re-apply — some URP settings aren't ready until after Awake.
        ApplyAll();
        CheckCameraPostProcessing();
        if (logDeviceInfo) LogDeviceInfo();
    }

    // Android resets targetFrameRate when the app is backgrounded. Re-apply on resume.
    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) ApplyFrameRate();
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused) ApplyFrameRate();
    }

#if UNITY_EDITOR
    void OnDisable()
    {
        // Restore URP asset's original renderScale so exiting play mode doesn't dirty the asset file.
        if (cachedUrpAsset != null)
            cachedUrpAsset.renderScale = originalRenderScale;
    }
#endif

    void ApplyAll()
    {
        currentTier = DetectDeviceTier();
        ApplyGlobalSettings();
        ApplyFrameRate();
        ApplyRenderScale();
    }

    // ---------------------------------------------------------------
    // GLOBAL TOGGLES THAT AFFECT FPS
    // ---------------------------------------------------------------
    void ApplyGlobalSettings()
    {
        if (disableVSync)
            QualitySettings.vSyncCount = 0;

        if (disableOnDemandRendering)
        {
            // OnDemandRendering throttles by skipping frames — kill it for max FPS.
            OnDemandRendering.renderFrameInterval = 1;
        }

        if (optimizeFixedTimestep)
        {
            // Default is 0.02 (50Hz). Many projects end up with 0.0166 (60Hz) which is fine.
            // Anything higher than 60Hz physics tick rarely matters for gameplay but costs CPU.
            if (Time.fixedDeltaTime < 1f / 60f)
                Time.fixedDeltaTime = 1f / 60f;

            // Cap maximumDeltaTime so a hitch doesn't cause a physics death-spiral.
            Time.maximumDeltaTime = 0.1f;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        if (sustainedPerformanceMode)
        {
            // Tells Android to keep the CPU/GPU at a steady level instead of boost-then-throttle.
            // Trades peak FPS for stable FPS under sustained load.
            UnityEngine.Android.AndroidDevice.SetSustainedPerformanceMode(true);
        }
#endif
    }

    // ---------------------------------------------------------------
    // DEVICE TIER DETECTION
    // ---------------------------------------------------------------
    DeviceTier DetectDeviceTier()
    {
        int ram = SystemInfo.systemMemorySize;          // MB
        int cpuCount = SystemInfo.processorCount;
        int cpuFreq = SystemInfo.processorFrequency;    // MHz
        float refreshRate = _maxRefreshRate;

#if UNITY_IOS && !UNITY_EDITOR
        // iOS devices are consistent — tier primarily by refresh rate and RAM.
        if (refreshRate >= 120f && ram >= 4000) return DeviceTier.Ultra;
        if (ram >= 3000) return DeviceTier.High;
        if (ram >= 2000) return DeviceTier.Mid;
        return DeviceTier.Low;
#else
        // Android is fragmented — use combined signals.
        if (refreshRate >= 120f && ram >= 6000 && cpuCount >= 8 && cpuFreq >= 2400)
            return DeviceTier.Ultra;

        if (ram >= 6000 && cpuCount >= 8 && cpuFreq >= 2000)
            return DeviceTier.High;

        if (ram >= 3000 && cpuCount >= 6)
            return DeviceTier.Mid;

        return DeviceTier.Low;
#endif
    }

    // ---------------------------------------------------------------
    // FRAME RATE
    // ---------------------------------------------------------------
    void ApplyFrameRate()
    {
        // vSync must be off for targetFrameRate to take effect.
        if (disableVSync)
            QualitySettings.vSyncCount = 0;

        int target;

        if (matchPanelRefreshRate)
        {
            // Use the MAX discovered rate, not the current adaptive rate.
            // Samsung adaptive refresh can report 60Hz even on a 120Hz panel.
            target = Mathf.RoundToInt(_maxRefreshRate);
        }
        else
        {
            switch (currentTier)
            {
                case DeviceTier.Ultra: target = ultraFPS; break;
                case DeviceTier.High:  target = highFPS;  break;
                case DeviceTier.Mid:   target = midFPS;   break;
                default:               target = lowFPS;   break;
            }
        }

        // Sanity clamp.
        target = Mathf.Clamp(target, 30, absoluteMaxFPS);

        Application.targetFrameRate = target;
        resolvedTargetFPS = target;
    }

    // ---------------------------------------------------------------
    // RENDER SCALE  (the lever that actually buys FPS on weak GPUs)
    // ---------------------------------------------------------------
    void ApplyRenderScale()
    {
        float scale;

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        switch (currentTier)
        {
            case DeviceTier.Ultra: scale = ultraScale; break;
            case DeviceTier.High:  scale = highScale;  break;
            case DeviceTier.Mid:   scale = midScale;   break;
            case DeviceTier.Low:   scale = lowScale;   break;
            default:               scale = highScale;  break;
        }
#else
        scale = 1.0f; // Editor / desktop — don't mess with it
#endif

        // Prefer URP's renderScale when available (it handles post + UI correctly).
        // Unity 6: check quality-level override first, then default pipeline, then current.
        var urpAsset = (QualitySettings.renderPipeline as UniversalRenderPipelineAsset)
                    ?? (GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset)
                    ?? (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset);

        if (urpAsset != null)
        {
            if (cachedUrpAsset == null)
            {
                cachedUrpAsset = urpAsset;
                originalRenderScale = urpAsset.renderScale;
            }
            urpAsset.renderScale = scale;
        }
        else
        {
#if !UNITY_EDITOR
            // Fallback for Built-in RP.
            ScalableBufferManager.ResizeBuffers(scale, scale);
#endif
        }
    }

    // ---------------------------------------------------------------
    // HELPERS
    // ---------------------------------------------------------------
    /// <summary>
    /// Discovers the MAXIMUM refresh rate the display supports by scanning
    /// all available resolutions. Falls back to the current resolution rate.
    /// This avoids Samsung adaptive refresh reporting 60Hz on a 120Hz panel.
    /// </summary>
    float DiscoverMaxRefreshRate()
    {
        float maxHz = 0f;

        // Screen.resolutions contains every supported mode — find the highest refresh rate.
        var resolutions = Screen.resolutions;
        for (int i = 0; i < resolutions.Length; i++)
        {
#if UNITY_2022_2_OR_NEWER
            float hz = (float)resolutions[i].refreshRateRatio.value;
#else
            float hz = resolutions[i].refreshRate;
#endif
            if (hz > maxHz) maxHz = hz;
        }

        // Fallback: if Screen.resolutions was empty or all zero, read current.
        if (maxHz <= 0f)
        {
#if UNITY_2022_2_OR_NEWER
            maxHz = (float)Screen.currentResolution.refreshRateRatio.value;
#else
            maxHz = Screen.currentResolution.refreshRate;
#endif
        }

        return maxHz > 0f ? maxHz : 60f;
    }

    void LogDeviceInfo()
    {
        Debug.Log(
            $"[FrameRateSetter] Tier={currentTier} | " +
            $"Target={resolvedTargetFPS}fps | " +
            $"Panel(max)={_maxRefreshRate}Hz | " +
            $"VSync={QualitySettings.vSyncCount} | " +
            $"RAM={SystemInfo.systemMemorySize}MB | " +
            $"GPU={SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize}MB) | " +
            $"CPU={SystemInfo.processorType} x{SystemInfo.processorCount} @ {SystemInfo.processorFrequency}MHz | " +
            $"RenderScale={(cachedUrpAsset != null ? cachedUrpAsset.renderScale : 1f):F2}"
        );
    }

    // URP only applies renderScale when the camera has HDR or post-processing enabled.
    // If neither is on, the scale change silently does nothing — warn once.
    void CheckCameraPostProcessing()
    {
        if (cachedUrpAsset == null) return;

        var cam = Camera.main;
        if (cam == null) return;

        var camData = cam.GetUniversalAdditionalCameraData();
        if (camData == null) return;

        if (!cam.allowHDR && !camData.renderPostProcessing)
        {
            Debug.LogWarning(
                "[FrameRateSetter] Main camera has both HDR and Post-Processing DISABLED. " +
                "URP renderScale will have NO EFFECT until at least one is enabled. " +
                "Enable Post Processing on the camera (Rendering section) to activate render scale."
            );
        }
    }
}
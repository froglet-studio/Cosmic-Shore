namespace CosmicShore.Data
{
    // Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
    // Always assign a static numeric value to your enum types.

    /// <summary>How the game window is presented. Maps to <see cref="UnityEngine.FullScreenMode"/>.</summary>
    public enum DisplayModeSetting
    {
        // ExclusiveFullScreen - true fullscreen, lowest latency, but slow alt-tab.
        Fullscreen = 0,
        // FullScreenWindow - borderless; the PC/Steam default for instant alt-tab.
        Borderless = 1,
        // Windowed - draggable window.
        Windowed = 2,
    }

    /// <summary>VSync count written to <see cref="UnityEngine.QualitySettings.vSyncCount"/>.</summary>
    public enum VSyncSetting
    {
        Off = 0,   // 0 - uncapped present, can tear, burns CPU/GPU on unseen frames.
        On = 1,    // 1 - present every VBlank (locks to refresh).
        Half = 2,  // 2 - present every other VBlank (half refresh).
    }

    /// <summary>
    /// Quality tier. The first six map 1:1 onto the project's QualitySettings tiers
    /// (Very Low..Ultra = levels 0..5). Custom means the player has overridden one or
    /// more individual graphics options, so no single tier describes the current state.
    /// </summary>
    public enum QualityPresetSetting
    {
        Custom = -1,
        VeryLow = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        VeryHigh = 4,
        Ultra = 5,
    }

    /// <summary>
    /// Anti-aliasing mode. MSAA is applied globally on the URP asset; the post-AA modes
    /// (FXAA/SMAA/TAA) are applied per-camera. On a CPU-bound game the GPU is mostly idle,
    /// so MSAA here is a near-free beauty win that fixes neon/prism edge shimmer.
    /// </summary>
    public enum AntiAliasingSetting
    {
        Off = 0,
        FXAA = 1,   // Fast, post-process, cheapest, slightly soft.
        SMAA = 2,   // Sharper post-process AA - good default for our shader edges.
        MSAA2x = 3,
        MSAA4x = 4,
        MSAA8x = 5,
        TAA = 6,    // Temporal - best coverage, needs motion vectors, can ghost.
    }

    /// <summary>
    /// GPU upscaling filter (URP <c>UpscalingFilterSelection</c>). Low priority for us - we are
    /// CPU-bound, so upscaling mostly buys GPU headroom we already have. Kept for weak iGPUs.
    /// </summary>
    public enum UpscalingSetting
    {
        Auto = 0,    // URP picks (linear/point) based on render scale.
        Linear = 1,
        FSR = 2,     // FidelityFX Super Resolution 1.0 (spatial).
        STP = 3,     // Spatial-Temporal Post-processing (Unity 6).
    }

    /// <summary>
    /// CPU-side simulation density - the real framerate lever on this engine. Lowers how many
    /// living things are *spawned/simulated* (spawn rate + seed floor), never how much existing
    /// mass survives. Conserved-mass-safe: it tunes production, it does not impose decay.
    /// </summary>
    public enum EcosystemDensitySetting
    {
        Sparse = 0,
        Normal = 1,
        Lush = 2,
    }

    /// <summary>Active-collider fidelity. Colliders are a top per-prism CPU cost.</summary>
    public enum PhysicsDetailSetting
    {
        Low = 0,
        High = 1,
    }

    /// <summary>
    /// Aggressiveness of adaptive animation throttling under CPU load. Currently
    /// INERT: its consumer (the retired CPU animation-manager frame-skip) was
    /// deleted when prism animation moved to the GPU clock (Docs/PRISM_ANIMATION.md);
    /// the setting persists for save-data compatibility and future adaptive systems.
    /// </summary>
    public enum AdaptivePerformanceSetting
    {
        Off = 0,
        Balanced = 1,
        Aggressive = 2,
    }

    /// <summary>
    /// The vision a player's colours are made for. Domains (Jade/Ruby/Gold/Blue, plus Fire as danger and Lime as the
    /// free pickup) are the board, so this is gameplay-critical accessibility, not cosmetic. Applied by wearing the
    /// colour set made for that vision (<c>ColorSetCatalogSO.Entry.vision</c>; the sets are defined and measured in
    /// <c>Docs/VesselModels/colour_sets.json</c>, rule 5), not by a screen-space filter: a filter can only remap the
    /// colours a set already has, a set chooses colours the viewer can tell apart. Saved as an int: never renumber.
    /// </summary>
    public enum ColorblindModeSetting
    {
        Off = 0,
        Protanopia = 1,    // no red cones (red-weak: protanomaly is the milder form)
        Deuteranopia = 2,  // no green cones (green-weak: deuteranomaly, the commonest)
        Tritanopia = 3,    // no blue cones (blue-weak)
        Achromatopsia = 4, // no colour at all (rod monochromacy): lightness only
        LowVision = 5,     // reduced contrast sensitivity, cataract: lightness carries more than hue
        Contrast = 6,      // typical vision, the domains as far apart as the rules allow
    }
}

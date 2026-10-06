using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// First-pass "Auto-Detect Best Settings" - an instant <see cref="SystemInfo"/> heuristic that
    /// recommends a quality preset (and sensible display/perf defaults) for this machine. CPU core
    /// count is weighted heavily and VRAM lightly, then the result is scaled against how many
    /// PIXELS this display actually asks us to fill. For an accurate result the player can follow
    /// up with the in-scene Benchmark, which measures real frame cost.
    ///
    /// The pixel term is not cosmetic. A machine's capability score says nothing about its display,
    /// and the two are wildly decoupled: a Retina MacBook and a 1080p desktop can score identically
    /// while the Mac is asked to render ~4x the pixels every frame. Since the rendering frontier on
    /// this title is transparent-prism overdraw (Docs/PERFORMANCE_OPTIMIZATION.md §0, capture #4),
    /// pixel count is a first-order framerate term - so the recommendation budgets it explicitly
    /// via render scale and MSAA rather than pretending every display is 1080p.
    ///
    /// <para><b>Device tiers.</b> The capability score is desktop-shaped: it counts CORES, and a
    /// budget Android phone has more of them (8 slow) than an iPhone (2 fast + 4 efficiency), so it
    /// ranked a 4 GB Samsung above an iPhone. <see cref="RecommendSettings"/> therefore asks the
    /// device's <see cref="PlatformProfileSO"/> first (<c>PlatformProfile.Current</c>): a profile that
    /// keeps <c>useCapabilityHeuristic</c> (Desktop, MobileHigh) gets exactly the heuristic below, and
    /// one that turns it off (MobileLow) gets its own authored recommendation
    /// (<see cref="RecommendFromProfile"/>). <c>Docs/PLATFORM_UNIFICATION.md</c> §1.3, §3.</para>
    /// </summary>
    public static class SettingsAutoDetector
    {
        // ── Pixel budgets ──────────────────────────────────────────────
        // Rendered pixels per frame each tier is willing to pay for, BEFORE upscaling to the
        // display. Anything above its budget gets a render-scale reduction; anything below renders
        // native (we never supersample by default). Reference points: 1080p = 2.07M,
        // 1440p = 3.69M, 4K = 8.29M, 16" MacBook Pro native = 7.72M, 5K = 14.75M.
        const long PixelBudgetVeryLow = 1_300_000;
        const long PixelBudgetLow = 2_100_000;
        const long PixelBudgetMedium = 2_100_000;
        const long PixelBudgetHigh = 3_700_000;
        const long PixelBudgetVeryHigh = 5_500_000;
        const long PixelBudgetUltra = 8_300_000;

        /// <summary>Never drop render scale below this - past it the neon/prism edges fall apart.</summary>
        const int MinRenderScalePercent = 50;

        // MSAA cost scales with the pixels it resolves, so the AA choice reads the EFFECTIVE
        // (post-render-scale) pixel count rather than the tier alone.
        const long MsaaModeratePixels = 3_700_000;  // above 1440p-equivalent: 4x -> 2x
        const long MsaaHeavyPixels = 5_500_000;     // well above that: drop to post-process AA

        /// <summary>Rough capability score 0..7 from cores (CPU-bound → heaviest), RAM, then VRAM.</summary>
        public static int CapabilityScore()
        {
            int cores = SystemInfo.processorCount;
            long ram = SystemInfo.systemMemorySize;     // MB
            long vram = SystemInfo.graphicsMemorySize;  // MB

            int score = 0;
            if (cores >= 12) score += 3;
            else if (cores >= 8) score += 2;
            else if (cores >= 4) score += 1;

            if (ram >= 16000) score += 2;
            else if (ram >= 8000) score += 1;

            if (vram >= 8000) score += 2;
            else if (vram >= 4000) score += 1;

            return Mathf.Clamp(score, 0, 7);
        }

        public static QualityPresetSetting RecommendPreset()
        {
            return CapabilityScore() switch
            {
                <= 1 => QualityPresetSetting.VeryLow,
                2 => QualityPresetSetting.Low,
                3 => QualityPresetSetting.Medium,
                4 or 5 => QualityPresetSetting.High,
                6 => QualityPresetSetting.VeryHigh,
                _ => QualityPresetSetting.Ultra,
            };
        }

        /// <summary>
        /// Native pixels this machine will be asked to fill. Reads the same source
        /// <see cref="GraphicsSettingsApplier.ApplyDisplay"/> resolves "native" from, so the budget
        /// below and the resolution actually set can never disagree. Returns 0 when no display is
        /// available (batch mode / CI), which callers treat as "no pixel information".
        /// </summary>
        public static long NativePixelCount()
        {
            long w = Display.main != null ? Display.main.systemWidth : Screen.currentResolution.width;
            long h = Display.main != null ? Display.main.systemHeight : Screen.currentResolution.height;
            return w > 0 && h > 0 ? w * h : 0L;
        }

        /// <summary>Pixels-per-frame this tier is willing to render before upscaling.</summary>
        public static long PixelBudgetFor(QualityPresetSetting preset) => preset switch
        {
            QualityPresetSetting.VeryLow => PixelBudgetVeryLow,
            QualityPresetSetting.Low => PixelBudgetLow,
            QualityPresetSetting.Medium => PixelBudgetMedium,
            QualityPresetSetting.High => PixelBudgetHigh,
            QualityPresetSetting.VeryHigh => PixelBudgetVeryHigh,
            _ => PixelBudgetUltra,
        };

        /// <summary>
        /// Render scale (percent) that brings <paramref name="nativePixels"/> down to the tier's
        /// budget. Render scale is a LINEAR factor on each axis, so the area ratio is square-rooted.
        /// Clamped to [<see cref="MinRenderScalePercent"/>, 100] - we downscale to protect the
        /// framerate but never supersample uninvited. A pixel count of 0 (no display) means
        /// "unknown", which yields 100 rather than a guess.
        /// </summary>
        public static int RecommendRenderScalePercent(QualityPresetSetting preset, long nativePixels) =>
            RecommendRenderScalePercent(PixelBudgetFor(preset), nativePixels, MinRenderScalePercent);

        /// <summary>
        /// Render scale (percent) that brings <paramref name="nativePixels"/> down to
        /// <paramref name="pixelBudget"/>, clamped to [<paramref name="minPercent"/>, 100]. The
        /// tier-agnostic core of <see cref="RecommendRenderScalePercent(QualityPresetSetting, long)"/>.
        /// </summary>
        public static int RecommendRenderScalePercent(long pixelBudget, long nativePixels, int minPercent)
        {
            if (nativePixels <= 0) return 100;
            if (nativePixels <= pixelBudget) return 100;

            int percent = Mathf.RoundToInt(Mathf.Sqrt((float)pixelBudget / nativePixels) * 100f);
            return Mathf.Clamp(percent, minPercent, 100);
        }

        /// <summary>
        /// AA mode for a tier that is actually rendering <paramref name="effectivePixels"/> per
        /// frame (i.e. native pixels already reduced by render scale). MSAA resolves every one of
        /// those pixels, so a high-DPI panel steps down the sample count instead of paying 4x
        /// on 4x the pixels.
        /// </summary>
        public static AntiAliasingSetting RecommendAntiAliasing(QualityPresetSetting preset, long effectivePixels)
        {
            if (preset < QualityPresetSetting.Medium) return AntiAliasingSetting.FXAA;
            if (preset < QualityPresetSetting.High) return AntiAliasingSetting.SMAA;

            if (effectivePixels > MsaaHeavyPixels) return AntiAliasingSetting.SMAA;
            if (effectivePixels > MsaaModeratePixels) return AntiAliasingSetting.MSAA2x;
            return AntiAliasingSetting.MSAA4x;
        }

        /// <summary>
        /// Full recommended snapshot for THIS device: the device tier's authored recommendation when
        /// its profile has one, otherwise the capability heuristic (<see cref="RecommendByCapability"/>,
        /// which is also what every device got before tiers existed, and what Desktop and MobileHigh
        /// still get).
        /// </summary>
        public static GraphicsSettingsData RecommendSettings()
        {
            var profile = PlatformProfile.Current;
            if (!profile) return RecommendByCapability();

            var auto = profile.AutoDetect;
            return auto.UseCapabilityHeuristic
                ? RecommendByCapability()
                : RecommendFromProfile(auto, NativePixelCount(), MonitorRefreshHz(), SystemInfo.processorCount);
        }

        /// <summary>
        /// A profile's authored recommendation: its preset, AA and frame cap, and a render scale that
        /// fits <paramref name="nativePixels"/> into its pixel budget, upscaled with the profile's
        /// filter when scaled. Display and CPU knobs follow the same rules as the heuristic.
        /// </summary>
        public static GraphicsSettingsData RecommendFromProfile(in PlatformAutoDetect auto, long nativePixels,
                                                                int monitorHz, int cores)
        {
            var d = new GraphicsSettingsData
            {
                QualityPreset = auto.Preset,
                DisplayMode = DisplayModeSetting.Borderless,
                VSync = VSyncSetting.On,
                ResolutionWidth = 0,   // native
                ResolutionHeight = 0,
                RefreshRateHz = 0,     // native
                AntiAliasing = auto.AntiAliasing,
            };

            int cap = Mathf.Max(1, auto.MaxTargetFrameRate);
            d.TargetFrameRate = monitorHz > 0 ? Mathf.Min(monitorHz, cap) : Mathf.Min(60, cap);

            ApplyCpuKnobs(d, cores);

            d.RenderScalePercent = RecommendRenderScalePercent(auto.PixelBudget, nativePixels,
                auto.MinRenderScalePercent);
            d.Upscaling = d.RenderScalePercent < 100 ? auto.UpscalingWhenScaled : UpscalingSetting.Auto;
            return d;
        }

        /// <summary>The display's refresh rate in Hz, or 0 when unknown.</summary>
        static int MonitorRefreshHz() =>
            Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);

        /// <summary>
        /// The capability-score heuristic: preset + native display + a refresh-rate-aware cap + CPU
        /// knobs. Every device's recommendation before tiers existed; still Desktop's and
        /// MobileHigh's (their profiles keep <c>useCapabilityHeuristic</c>).
        /// </summary>
        public static GraphicsSettingsData RecommendByCapability()
        {
            var d = new GraphicsSettingsData
            {
                QualityPreset = RecommendPreset(),
                DisplayMode = DisplayModeSetting.Borderless,
                VSync = VSyncSetting.On,
                ResolutionWidth = 0,   // native
                ResolutionHeight = 0,
                RefreshRateHz = 0,     // native
            };

            // Frame cap follows the monitor (clamped to a sane 120 ceiling for a CPU-bound title).
            int monitorHz = MonitorRefreshHz();
            d.TargetFrameRate = monitorHz > 0 ? Mathf.Min(monitorHz, 120) : 60;

            ApplyCpuKnobs(d, SystemInfo.processorCount);

            // Pixel budget: a high-DPI panel (any Retina Mac, any 4K monitor) renders far more
            // pixels than the capability score knows about. Scale down to the tier's budget and
            // pick an upscaler when we do, so the display still gets a full-resolution image.
            long nativePixels = NativePixelCount();
            d.RenderScalePercent = RecommendRenderScalePercent(d.QualityPreset, nativePixels);
            d.Upscaling = d.RenderScalePercent < 100 ? UpscalingSetting.FSR : UpscalingSetting.Auto;

            // Match AA to what we will actually be resolving, not to the tier in the abstract.
            long scale = d.RenderScalePercent;
            long effectivePixels = nativePixels > 0 ? nativePixels * scale * scale / 10_000L : 0L;
            d.AntiAliasing = RecommendAntiAliasing(d.QualityPreset, effectivePixels);

            return d;
        }

        /// <summary>CPU knobs scale with core count - the real lever on this engine.</summary>
        static void ApplyCpuKnobs(GraphicsSettingsData d, int cores)
        {
            if (cores >= 8)
            {
                d.EcosystemDensity = EcosystemDensitySetting.Lush;
                d.PhysicsDetail = PhysicsDetailSetting.High;
                d.AiCrowdSize = 4;
                d.AdaptivePerformance = AdaptivePerformanceSetting.Balanced;
            }
            else if (cores >= 4)
            {
                d.EcosystemDensity = EcosystemDensitySetting.Normal;
                d.PhysicsDetail = PhysicsDetailSetting.High;
                d.AiCrowdSize = 3;
                d.AdaptivePerformance = AdaptivePerformanceSetting.Balanced;
            }
            else
            {
                d.EcosystemDensity = EcosystemDensitySetting.Sparse;
                d.PhysicsDetail = PhysicsDetailSetting.Low;
                d.AiCrowdSize = 1;
                d.AdaptivePerformance = AdaptivePerformanceSetting.Aggressive;
            }
        }
    }
}

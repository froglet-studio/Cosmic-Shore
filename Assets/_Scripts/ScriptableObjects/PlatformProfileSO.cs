using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Everything that differs between hardware tiers, for ONE <see cref="DeviceTier"/>. The three
    /// profiles (Desktop, MobileHigh, MobileLow) are held by <see cref="PlatformProfileSetSO"/>, and
    /// the device's one is <c>PlatformProfile.Current</c>. This replaces the Android strip branch's
    /// compile-time <c>PerfStrip</c> flags: a per-platform choice is a field here, read at runtime,
    /// so one build serves every platform (<c>Docs/PLATFORM_UNIFICATION.md</c> §3).
    ///
    /// Today it carries the first-run graphics recommendation only. Render and content choices
    /// join it as later steps of that plan port them; each new field must leave the Desktop asset
    /// behaving exactly as bleeding-edge did before it existed.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PlatformProfile",
        menuName = "ScriptableObjects/Platform/" + nameof(PlatformProfileSO))]
    public class PlatformProfileSO : ScriptableObject
    {
        [Header("First-run graphics recommendation (SettingsAutoDetector)")]
        [Tooltip("Use the capability-score heuristic (CPU cores, RAM, VRAM, display pixels) - what " +
                 "every device got before tiers existed. Leave ON for Desktop. MobileHigh keeps it ON " +
                 "so iPhones are unchanged; MobileLow turns it OFF, because core count ranks a budget " +
                 "8-core phone ABOVE an iPhone. When OFF, the fields below are the recommendation.")]
        [SerializeField] bool useCapabilityHeuristic = true;

        [Tooltip("Quality preset (QualitySettings level) recommended on first run.")]
        [SerializeField] QualityPresetSetting preset = QualityPresetSetting.VeryLow;

        [Tooltip("Rendered pixels per frame before upscaling. A display above it gets a render-scale " +
                 "reduction to fit; 1,300,000 puts a 1080x2400 phone at ~71%.")]
        [SerializeField, Min(0)] int pixelBudget = 1_300_000;

        [Tooltip("Never recommend a render scale below this, whatever the display.")]
        [SerializeField, Range(25, 100)] int minRenderScalePercent = 50;

        [Tooltip("Upscaler used when the recommended render scale is below 100%. Linear is one " +
                 "bilinear blit; FSR adds an ALU-heavy pass a low-end mobile GPU pays for.")]
        [SerializeField] UpscalingSetting upscalingWhenScaled = UpscalingSetting.Linear;

        [Tooltip("Anti-aliasing recommended on first run.")]
        [SerializeField] AntiAliasingSetting antiAliasing = AntiAliasingSetting.FXAA;

        [Tooltip("Frame-rate cap recommended on first run: the display's refresh rate, but never " +
                 "above this.")]
        [SerializeField, Min(1)] int maxTargetFrameRate = 60;

        /// <summary>This profile's first-run graphics recommendation, as plain data.</summary>
        public PlatformAutoDetect AutoDetect => new(useCapabilityHeuristic, preset, pixelBudget,
            minRenderScalePercent, upscalingWhenScaled, antiAliasing, maxTargetFrameRate);
    }

    /// <summary>
    /// A <see cref="PlatformProfileSO"/>'s first-run graphics recommendation, as plain data so
    /// <c>SettingsAutoDetector.RecommendFromProfile</c> can be tested without an asset.
    /// </summary>
    public readonly struct PlatformAutoDetect
    {
        public readonly bool UseCapabilityHeuristic;
        public readonly QualityPresetSetting Preset;
        public readonly long PixelBudget;
        public readonly int MinRenderScalePercent;
        public readonly UpscalingSetting UpscalingWhenScaled;
        public readonly AntiAliasingSetting AntiAliasing;
        public readonly int MaxTargetFrameRate;

        public PlatformAutoDetect(bool useCapabilityHeuristic, QualityPresetSetting preset, long pixelBudget,
                                  int minRenderScalePercent, UpscalingSetting upscalingWhenScaled,
                                  AntiAliasingSetting antiAliasing, int maxTargetFrameRate)
        {
            UseCapabilityHeuristic = useCapabilityHeuristic;
            Preset = preset;
            PixelBudget = pixelBudget;
            MinRenderScalePercent = minRenderScalePercent;
            UpscalingWhenScaled = upscalingWhenScaled;
            AntiAliasing = antiAliasing;
            MaxTargetFrameRate = maxTargetFrameRate;
        }
    }
}

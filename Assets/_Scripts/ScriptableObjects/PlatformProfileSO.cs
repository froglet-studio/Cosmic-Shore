using System;
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
    /// It carries the first-run graphics recommendation (Step 3) and the per-tier render choices
    /// (Step 4); content choices join it in Step 5. Every field's DEFAULT is "no change", and the
    /// Desktop and MobileHigh assets keep those defaults, so Windows and iOS behave exactly as
    /// bleeding-edge did before the field existed - only an asset that sets a field changes anything.
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

        [Header("Render (applied at runtime on this tier)")]
        [Tooltip("Turn the active URP asset's HDR off on this tier (GraphicsSettingsApplier). An LDR " +
                 "colour buffer is half the bandwidth of an HDR one, and lets 4x MSAA sit in a mobile " +
                 "GPU's tile memory. Bloom still runs: the gameplay profile's threshold 0.2 / clamp 0.5 " +
                 "never reads above the LDR range. Values above 1 (HDR crystal cores, emissive " +
                 "highlights) clamp, so they bloom less. Off = the URP asset's own setting.")]
        [SerializeField] bool disableHdr;

        [Tooltip("Skybox materials replaced on this tier: when a scene's RenderSettings.skybox is an " +
                 "entry's authored material, the replacement draws instead (PlatformRenderApplier, on " +
                 "every scene load). MobileLow swaps the 767-line procedural HyperSea sky for its offline " +
                 "bake (Tools/Build/bake_static_skybox.py) - one texture sample per pixel.")]
        [SerializeField] SkyboxReplacement[] skyboxReplacements = Array.Empty<SkyboxReplacement>();

        [Tooltip("Cap on a CapsuleMembrane's icosphere subdivision level on this tier; -1 = no cap. " +
                 "The membrane then draws only the capsules of that level (642 at 3 instead of 2,562 " +
                 "at 4) - the same seeded layout a membrane authored at that level would have, played " +
                 "from the same baked animation. Radius, and so every gameplay reader of it, is untouched.")]
        [SerializeField, Range(-1, 4)] int membraneMaxSubdivisions = -1;

        [Tooltip("Ceiling on a Butterfly fold-gate window's render resolution, as a fraction of the " +
                 "gameplay camera's (FoldGatePortalView). The window renders only its own footprint, so " +
                 "this only bites when a gate fills the screen - the approach and the carry through. " +
                 "1 = no cap beyond the gate's own portalWindowRenderScale.")]
        [SerializeField, Range(0.1f, 1f)] float foldGateWindowMaxRenderScale = 1f;

        /// <summary>This profile's first-run graphics recommendation, as plain data.</summary>
        public PlatformAutoDetect AutoDetect => new(useCapabilityHeuristic, preset, pixelBudget,
            minRenderScalePercent, upscalingWhenScaled, antiAliasing, maxTargetFrameRate);

        /// <summary>True when this tier turns the URP asset's HDR off.</summary>
        public bool DisableHdr => disableHdr;

        /// <summary>Highest CapsuleMembrane subdivision level drawn on this tier; -1 = no cap.</summary>
        public int MembraneMaxSubdivisions => membraneMaxSubdivisions;

        /// <summary>Ceiling on a fold-gate window's render scale on this tier.</summary>
        public float FoldGateWindowMaxRenderScale => foldGateWindowMaxRenderScale;

        /// <summary>The skybox this tier draws in place of <paramref name="authored"/>, or null to keep it.</summary>
        public Material SkyboxReplacementFor(Material authored)
        {
            if (!authored || skyboxReplacements == null) return null;
            foreach (var entry in skyboxReplacements)
                if (entry.Authored == authored && entry.Replacement)
                    return entry.Replacement;
            return null;
        }

        /// <summary>One skybox swap: draw <see cref="Replacement"/> wherever a scene authored
        /// <see cref="Authored"/>.</summary>
        [Serializable]
        public struct SkyboxReplacement
        {
            [Tooltip("The skybox material a scene authors (RenderSettings).")]
            [SerializeField] Material authored;

            [Tooltip("The material drawn instead on this tier.")]
            [SerializeField] Material replacement;

            public Material Authored => authored;
            public Material Replacement => replacement;
        }
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

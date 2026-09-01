using System;
using CosmicShore.Data;

namespace CosmicShore.Core
{
    /// <summary>
    /// Plain serializable container for all DEVICE-LOCAL settings (display, graphics, and the
    /// CPU/simulation "performance" knobs). These intentionally do NOT cloud-sync - a phone and a
    /// PC must not share a resolution or quality tier. Roaming prefs (audio, input, accessibility,
    /// consent) live in <see cref="GameSetting"/> / <see cref="PlayerSettingsCloudData"/> instead.
    ///
    /// Defaults here are the "safe medium" fallback used before auto-detect runs.
    /// </summary>
    [Serializable]
    public class GraphicsSettingsData
    {
        // ── Display ──
        public DisplayModeSetting DisplayMode = DisplayModeSetting.Borderless;
        public int ResolutionWidth = 0;        // 0 = native
        public int ResolutionHeight = 0;       // 0 = native
        public int RefreshRateHz = 0;          // 0 = native
        public VSyncSetting VSync = VSyncSetting.On;
        public int TargetFrameRate = 60;       // <= 0 = uncapped (-1)
        public float FieldOfView = DefaultFieldOfView;

        // ── Graphics ──
        public QualityPresetSetting QualityPreset = QualityPresetSetting.High;
        public AntiAliasingSetting AntiAliasing = AntiAliasingSetting.SMAA;
        public int RenderScalePercent = 100;   // 25..200
        public UpscalingSetting Upscaling = UpscalingSetting.Auto;
        public int TextureQuality = 0;         // globalTextureMipmapLimit: 0 full, 1 half, 2 quarter, 3 eighth

        // ── Performance (CPU) ──
        public AdaptivePerformanceSetting AdaptivePerformance = AdaptivePerformanceSetting.Balanced;
        public EcosystemDensitySetting EcosystemDensity = EcosystemDensitySetting.Normal;
        public PhysicsDetailSetting PhysicsDetail = PhysicsDetailSetting.High;
        public int AiCrowdSize = 3;            // AI vessels to backfill in solo play
        public int VfxDensityPercent = 100;    // 25..100 - scales the per-frame explosion VFX budget

        /// <summary>
        /// The field of view every player starts on. A narrower lens is a deliberate pick made in
        /// the settings panel, never a state a player is dropped into.
        /// </summary>
        public const float DefaultFieldOfView = 90f;

        /// <summary>
        /// The default shipped before <see cref="DefaultFieldOfView"/> was raised. Kept so the
        /// settings migration can tell a saved value that is merely the OLD default from one the
        /// player actually chose. Do not read it for anything else.
        /// </summary>
        public const float LegacyDefaultFieldOfView = 60f;

        public GraphicsSettingsData Clone() => (GraphicsSettingsData)MemberwiseClone();
    }
}

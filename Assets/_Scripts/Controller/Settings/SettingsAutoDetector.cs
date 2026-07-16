using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// First-pass "Auto-Detect Best Settings" - an instant <see cref="SystemInfo"/> heuristic that
    /// recommends a quality preset (and sensible display/perf defaults) for this machine. Because
    /// the game is CPU-bound, CPU core count is weighted heavily and VRAM lightly. For an accurate
    /// result the player can follow up with the in-scene Benchmark, which measures real frame cost.
    /// </summary>
    public static class SettingsAutoDetector
    {
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

        /// <summary>Full recommended snapshot: preset + native display + a refresh-rate-aware cap + CPU knobs.</summary>
        public static GraphicsSettingsData RecommendSettings()
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
            int monitorHz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
            d.TargetFrameRate = monitorHz > 0 ? Mathf.Min(monitorHz, 120) : 60;

            // CPU knobs scale with core count - the real lever on this engine.
            int cores = SystemInfo.processorCount;
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

            // Match AA to the GPU tier (near-free on a CPU-bound game, so be generous on strong cards).
            d.AntiAliasing = d.QualityPreset >= QualityPresetSetting.High
                ? AntiAliasingSetting.MSAA4x
                : (d.QualityPreset >= QualityPresetSetting.Medium
                    ? AntiAliasingSetting.SMAA
                    : AntiAliasingSetting.FXAA);

            return d;
        }
    }
}

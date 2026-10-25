using CosmicShore.Data;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Core
{
    /// <summary>
    /// Stateless bridge that pushes a <see cref="GraphicsSettingsData"/> snapshot onto the Unity
    /// engine: <see cref="QualitySettings"/>, <see cref="Screen"/>, the active URP asset, and the
    /// main camera. This is the ONLY place that touches engine graphics state, so the rest of the
    /// settings system stays pure data (CLAUDE.md ▸ Config Separation).
    ///
    /// Note: in the Editor, mutating the URP asset's renderScale/msaaSampleCount/upscalingFilter/HDR
    /// changes the asset itself; <c>UrpAssetPlayModeRestore</c> (Editor) puts the authored values
    /// back when Play ends. In a build it is in-memory only.
    /// </summary>
    public static class GraphicsSettingsApplier
    {
        public static void ApplyAll(GraphicsSettingsData s)
        {
            if (s == null) return;
            ApplyQuality(s);
            ApplyFrameRate(s);
            ApplyDisplay(s);
        }

        public static void ApplyDisplay(GraphicsSettingsData s)
        {
            // Display mode and resolution are desktop window settings. On a phone or tablet the
            // app always fills the screen at the OS's resolution, and the pixel budget is the URP
            // render scale (ApplyQuality). SetResolution there could only hurt: "native" is read
            // from Display.main.systemWidth/Height, and iOS does not promise those in the
            // landscape orientation this game runs in, so it could hand the backbuffer portrait
            // dimensions. A WebGL player's canvas is sized by its page, and a full-screen mode
            // there is a request the browser grants on the next click - so the first click into
            // the browser Toybox would leave the page. The page's own fullscreen button owns that.
            if (Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer) return;

            var mode = ToFullScreenMode(s.DisplayMode);
            int w = s.ResolutionWidth > 0 ? s.ResolutionWidth : Display.main.systemWidth;
            int h = s.ResolutionHeight > 0 ? s.ResolutionHeight : Display.main.systemHeight;

            if (s.RefreshRateHz > 0)
                Screen.SetResolution(w, h, mode,
                    new RefreshRate { numerator = (uint)s.RefreshRateHz, denominator = 1 });
            else
                Screen.SetResolution(w, h, mode);
        }

        /// <summary>The URP asset's own HDR choice, read before anything here first wrote it, so a
        /// preset that lifts its HDR cut puts back what was authored (not a blanket "on").</summary>
        static UniversalRenderPipelineAsset s_hdrAsset;
        static bool s_authoredHdr;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_hdrAsset = null;

        public static void ApplyQuality(GraphicsSettingsData s)
        {
            // The preset is the master tier; Custom means individual overrides - don't stomp them.
            if (s.QualityPreset != QualityPresetSetting.Custom)
                QualitySettings.SetQualityLevel((int)s.QualityPreset, true);

            // The preset's GPU cuts (QualityPresetCutsSO) stay in force after a hand change made it
            // Custom: they follow the preset it started from. Docs/PLATFORM_UNIFICATION.md §3.10.
            GraphicsCuts.SetPreset(s.EffectivePreset);

            QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(s.TextureQuality, 0, 3);

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = Mathf.Clamp(s.RenderScalePercent / 100f, 0.25f, 2f);
                urp.msaaSampleCount = MsaaSampleCount(s.AntiAliasing);
                urp.upscalingFilter = ToUpscalingFilter(s.Upscaling);

                // The device tier (MobileLow) or the preset (Low) may turn HDR off: half the colour
                // bandwidth, and room for MSAA in tile memory. Only ever OFF: with no cut the URP
                // asset's authored value stands, so Desktop on High renders exactly as before.
                // Docs/PLATFORM_UNIFICATION.md, Step 4 and §3.10.
                if (s_hdrAsset != urp)
                {
                    s_hdrAsset = urp;
                    s_authoredHdr = urp.supportsHDR;
                }
                urp.supportsHDR = s_authoredHdr && !GraphicsCuts.DisableHdr;
            }

            ApplyCameraAntiAliasing(Camera.main, s.AntiAliasing);

            // The preset's other live cuts: bloom resolution and the baked sky.
            PostProcessCuts.Apply(GraphicsCuts.Current);
            PlatformRenderApplier.ApplySkybox();
        }

        public static void ApplyFrameRate(GraphicsSettingsData s)
        {
            QualitySettings.vSyncCount = (int)s.VSync;
            // With VSync on, targetFrameRate is ignored by Unity - set it anyway so toggling
            // VSync off later picks up the intended cap. <= 0 means uncapped.
            Application.targetFrameRate = s.TargetFrameRate <= 0 ? -1 : s.TargetFrameRate;
        }

        /// <summary>
        /// Applies the post-process AA modes (FXAA/SMAA/TAA) to a camera. MSAA is intentionally
        /// NOT set here - it lives on the URP asset and is applied in <see cref="ApplyQuality"/>.
        /// Call this whenever a new gameplay camera spawns so the choice follows scene changes.
        /// </summary>
        public static void ApplyCameraAntiAliasing(Camera cam, AntiAliasingSetting aa)
        {
            if (cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null) return;

            data.antialiasing = aa switch
            {
                AntiAliasingSetting.FXAA => AntialiasingMode.FastApproximateAntialiasing,
                AntiAliasingSetting.SMAA => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                AntiAliasingSetting.TAA => AntialiasingMode.TemporalAntiAliasing,
                _ => AntialiasingMode.None, // Off + MSAA tiers: no camera-side AA
            };
        }

        static int MsaaSampleCount(AntiAliasingSetting aa) => aa switch
        {
            AntiAliasingSetting.MSAA2x => 2,
            AntiAliasingSetting.MSAA4x => 4,
            AntiAliasingSetting.MSAA8x => 8,
            _ => 1,
        };

        static UpscalingFilterSelection ToUpscalingFilter(UpscalingSetting u) => u switch
        {
            UpscalingSetting.Linear => UpscalingFilterSelection.Linear,
            UpscalingSetting.FSR => UpscalingFilterSelection.FSR,
            UpscalingSetting.STP => UpscalingFilterSelection.STP,
            _ => UpscalingFilterSelection.Auto,
        };

        /// <summary>
        /// macOS has no exclusive-fullscreen mode - Unity only implements
        /// <see cref="FullScreenMode.ExclusiveFullScreen"/> on Windows. Asking for it there is
        /// how you get a black window, a wrong-sized backbuffer, or offset mouse coordinates,
        /// especially combined with a Retina <c>SetResolution</c>. Borderless is the correct
        /// macOS equivalent and is what the OS gives you anyway.
        ///
        /// Detected at runtime rather than with a compile guard: the editor on a Mac has the same
        /// constraint as the player, and a runtime check sidesteps the guard-correctness hazard
        /// class entirely (Docs/CONDITIONAL_COMPILATION.md).
        /// </summary>
        static bool IsMac =>
            Application.platform == RuntimePlatform.OSXPlayer ||
            Application.platform == RuntimePlatform.OSXEditor;

        static FullScreenMode ToFullScreenMode(DisplayModeSetting m) => m switch
        {
            DisplayModeSetting.Fullscreen => IsMac
                ? FullScreenMode.FullScreenWindow
                : FullScreenMode.ExclusiveFullScreen,
            DisplayModeSetting.Windowed => FullScreenMode.Windowed,
            _ => FullScreenMode.FullScreenWindow,
        };
    }
}

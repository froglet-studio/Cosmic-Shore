using UnityEngine;
using UnityEngine.Rendering.Universal;
using CosmicShore.Utilities;
using CosmicShore.Utility;

namespace CosmicShore.Utility
{
    public class MobilePerformanceManager : SingletonPersistent<MobilePerformanceManager>
    {
        public static bool IsMobile { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoCreate()
        {
            if (Instance == null)
            {
                var go = new GameObject(nameof(MobilePerformanceManager));
                go.AddComponent<MobilePerformanceManager>();
            }
        }

        public override void Awake()
        {
            base.Awake();

            if (Instance != this)
                return;

            IsMobile = Application.isMobilePlatform;

            if (IsMobile)
                ApplyMobileSettings();
        }

        void ApplyMobileSettings()
        {
            // ── Frame Rate ──────────────────────────────────────────────
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // ── Shadows: completely off ─────────────────────────────────
            QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
            QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Low;
            QualitySettings.shadowDistance = 0f;

            // ── Lighting: minimum ───────────────────────────────────────
            QualitySettings.pixelLightCount = 1;
            QualitySettings.realtimeReflectionProbes = false;

            // ── Textures / Filtering ────────────────────────────────────
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            QualitySettings.globalTextureMipmapLimit = 1; // half-res textures

            // ── Particles ───────────────────────────────────────────────
            QualitySettings.particleRaycastBudget = 4;
            QualitySettings.softParticles = false;

            // ── Geometry ────────────────────────────────────────────────
            QualitySettings.skinWeights = SkinWeights.OneBone;
            QualitySettings.lodBias = 0.5f;
            QualitySettings.maximumLODLevel = 0;

            // ── Shader LOD: force mobile SubShaders ─────────────────────
            Shader.globalMaximumLOD = 150;

            // ── URP runtime overrides ───────────────────────────────────
            StripURPAtRuntime();

            // ── Kill bloom and vignette via Volume system ───────────────
            DisablePostProcessing();

            CSDebug.Log("[MobilePerformanceManager] MAXIMUM PERFORMANCE: " +
                        "targetFPS=120, shadows=OFF, MSAA=OFF, bloom=OFF, " +
                        "renderScale=0.75, texMip=1, skinWeights=1bone, " +
                        "pixelLights=1, lodBias=0.5");
        }

        static void StripURPAtRuntime()
        {
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null) return;

            // Kill MSAA entirely — biggest single bandwidth win on tiled GPUs
            urpAsset.msaaSampleCount = 1;

            // Render at 75% resolution — massive fill rate savings
            urpAsset.renderScale = 0.75f;

            // HDR off (already in asset, belt-and-suspenders)
            urpAsset.supportsHDR = false;
        }

        static void DisablePostProcessing()
        {
            // Find all active Volume components and disable bloom/vignette
            var volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
            foreach (var vol in volumes)
            {
                if (vol.profile == null) continue;

                if (vol.profile.TryGet<Bloom>(out var bloom))
                    bloom.active = false;

                if (vol.profile.TryGet<Vignette>(out var vignette))
                    vignette.active = false;

                if (vol.profile.TryGet<MotionBlur>(out var motionBlur))
                    motionBlur.active = false;

                if (vol.profile.TryGet<ChromaticAberration>(out var chromatic))
                    chromatic.active = false;
            }
        }
    }
}

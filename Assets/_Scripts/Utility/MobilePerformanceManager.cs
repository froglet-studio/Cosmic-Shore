using UnityEngine;
using UnityEngine.Rendering;
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
            // Frame rate
            Application.targetFrameRate = 60;

            // Shadows — disable entirely on mobile for max perf
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.shadowResolution = ShadowResolution.Low;
            QualitySettings.shadowDistance = 0f;
            QualitySettings.shadowCascades = 0;

            // Particles
            QualitySettings.particleRaycastBudget = 4;
            QualitySettings.softParticles = false;

            // Reflections
            QualitySettings.realtimeReflectionProbes = false;

            // Textures — reduce filtering
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;

            // LOD — bias toward lower LODs on mobile
            QualitySettings.lodBias = 0.7f;
            QualitySettings.maximumLODLevel = 0;

            // Pixel lights — minimize per-pixel lighting
            QualitySettings.pixelLightCount = 1;

            // Skin weights — reduce bone influence for skinned meshes
            QualitySettings.skinWeights = SkinWeights.TwoBones;

            // Disable VSync — rely on targetFrameRate
            QualitySettings.vSyncCount = 0;

            // Physics — reduce fixed timestep frequency for mobile
            Time.fixedDeltaTime = 0.02f;

            // Reduce physics simulation iterations
            Physics.defaultSolverIterations = 4;
            Physics.defaultSolverVelocityIterations = 1;

            // GPU instancing hint — prefer batching
            QualitySettings.billboardsFaceCameraPosition = false;

            // Rendering — set resolution scale for mobile
            QualitySettings.resolutionScalingFixedDPIFactor = 0.85f;

            // OnDemandRendering — skip frames for background UI if needed
            OnDemandRendering.renderFrameInterval = 1;

            // Shader LOD — limit shader complexity on mobile
            Shader.globalMaximumLOD = 200;

            CSDebug.Log("[MobilePerformanceManager] Mobile settings applied: shadows=Disable, " +
                        "pixelLights=1, lodBias=0.7, skinWeights=TwoBones, particleBudget=4, " +
                        "resolutionScale=0.85, shaderLOD=200");
        }
    }
}

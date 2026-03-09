using UnityEngine;
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
            Application.targetFrameRate = 60;
            QualitySettings.shadows = ShadowQuality.HardOnly;
            QualitySettings.shadowResolution = ShadowResolution.Low;
            QualitySettings.particleRaycastBudget = 16;
            QualitySettings.softParticles = false;
            QualitySettings.realtimeReflectionProbes = false;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;

            // Select lower shader LOD on mobile so the HyperSeaSkybox (and any
            // future shaders with mobile SubShaders) automatically pick the
            // reduced-quality variant.
            Shader.globalMaximumLOD = 150;

            // Cap max LOD bias to reduce texture quality pressure on mobile GPU
            QualitySettings.maximumLODLevel = 0;
            QualitySettings.lodBias = 1.0f;

            CSDebug.Log("[MobilePerformanceManager] Mobile detected. Applied settings: " +
                        $"targetFrameRate=60, shadows=HardOnly, shadowResolution=Low, " +
                        $"particleRaycastBudget=16, softParticles=false, " +
                        $"realtimeReflectionProbes=false, anisotropicFiltering=Disable, " +
                        $"globalMaximumLOD=150");
        }
    }
}

using CosmicShore.Utilities;
using UnityEngine;
using UnityEngine.Rendering;

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

            CSDebug.Log($"[MobilePerformanceManager] Mobile settings applied: " +
                        $"targetFrameRate=60, shadows=HardOnly, shadowResolution=Low, " +
                        $"particleRaycastBudget=16, softParticles=false, " +
                        $"realtimeReflectionProbes=false, anisotropicFiltering=Disable");
        }
    }
}

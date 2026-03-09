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
            // Uncap or target max refresh rate — 120 on modern devices, no lower than 60
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;

            // Prevent OS from throttling the display
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // Shadows off entirely — the URP asset already has shadows disabled,
            // but belt-and-suspenders in case a quality level override sneaks in
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.shadowResolution = ShadowResolution.Low;

            // Particle / reflection budget
            QualitySettings.particleRaycastBudget = 16;
            QualitySettings.softParticles = false;
            QualitySettings.realtimeReflectionProbes = false;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;

            // Skin weights — two bones is plenty for mobile
            QualitySettings.skinWeights = SkinWeights.TwoBones;

            // LOD bias — push LOD transitions closer to save polys
            QualitySettings.lodBias = 0.7f;

            // Reduce max LOD level (0 = use all LODs including highest detail)
            QualitySettings.maximumLODLevel = 0;

            CSDebug.Log("[MobilePerformanceManager] Mobile optimized: " +
                        $"targetFrameRate=120, vSync=0, shadows=Disable, " +
                        $"sleepTimeout=NeverSleep, skinWeights=TwoBones, lodBias=0.7");
        }
    }
}

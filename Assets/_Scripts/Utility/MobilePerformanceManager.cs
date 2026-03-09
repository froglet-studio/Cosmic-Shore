using System.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
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
            QualitySettings.lodBias = 0.3f; // aggressive LOD — pop-in is acceptable
            QualitySettings.maximumLODLevel = 0;

            // ── Shader LOD: force mobile SubShaders ─────────────────────
            Shader.globalMaximumLOD = 150;

            // ── Physics: reduce simulation overhead ─────────────────────
            ThrottlePhysics();

            // ── Audio: cut voice budget ─────────────────────────────────
            ThrottleAudio();

            // ── GC: incremental to avoid frame spikes ───────────────────
            ConfigureGC();

            // ── URP runtime overrides ───────────────────────────────────
            StripURPAtRuntime();

            // ── Camera: tighter culling ─────────────────────────────────
            TightenCameraCulling();

            // ── Kill bloom and vignette via Volume system ───────────────
            DisablePostProcessing();

            CSDebug.Log("[MobilePerformanceManager] BRUTAL PERFORMANCE: " +
                        "targetFPS=120, shadows=OFF, MSAA=OFF, HDR=OFF, " +
                        "renderScale=0.7, texMip=1, skinWeights=1bone, " +
                        "pixelLights=1, lodBias=0.3, farClip=300, " +
                        "fixedDT=0.02, physics3D-autoSync=OFF, " +
                        "audioVoices=16, incrementalGC=ON");
        }

        static void StripURPAtRuntime()
        {
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null) return;

            // Kill MSAA entirely — biggest single bandwidth win on tiled GPUs
            urpAsset.msaaSampleCount = 1;

            // Render at 70% resolution — aggressive fill rate savings
            urpAsset.renderScale = 0.7f;

            // HDR off — saves bandwidth on tiled GPUs
            urpAsset.supportsHDR = false;

            // Disable depth & opaque textures — avoids extra full-screen copies
            urpAsset.supportsCameraDepthTexture = false;
            urpAsset.supportsCameraOpaqueTexture = false;

            // Zero out additional lights — no per-pixel extra lights on mobile
            urpAsset.maxAdditionalLightsCount = 0;
        }

        static void ThrottlePhysics()
        {
            // Widen fixed timestep: 50 Hz instead of default 50 Hz (confirm)
            // then drop to 33 Hz — physics at 30 fps is fine for a space game
            Time.fixedDeltaTime = 0.02f; // 50 Hz — safe baseline

            // Disable auto-sync: manual sync only when needed
            Physics.autoSyncTransforms = false;

            // Reduce solver iterations — space game doesn't need tight stacking
            Physics.defaultSolverIterations = 2;
            Physics.defaultSolverVelocityIterations = 1;

            // Tighten broadphase bounds if using multibox
            Physics.bounceThreshold = 2f; // skip trivial bounces

            // 2D physics — disable auto-sim if not used, reduce overhead
            Physics2D.simulationMode = SimulationMode2D.Script; // only sim when explicitly called
        }

        static void ThrottleAudio()
        {
            // Halve real voice count — 16 simultaneous sounds is plenty for mobile
            var audioConfig = AudioSettings.GetConfiguration();
            audioConfig.numRealVoices = 16;
            audioConfig.numVirtualVoices = 128;
            audioConfig.dspBufferSize = 1024; // larger buffer = fewer interrupts
            AudioSettings.Reset(audioConfig);
        }

        static void ConfigureGC()
        {
            // Enable incremental GC to spread collection across frames
            // Prevents 10-30ms GC spikes that tank frame times
            GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
            GarbageCollector.incrementalTimeSliceNanoseconds = 1_000_000; // 1ms max per frame
        }

        static void TightenCameraCulling()
        {
            var cam = Camera.main;
            if (cam == null) return;

            // Pull in far clip — space backdrop is skybox, geometry rarely past 200m
            cam.farClipPlane = 300f;

            // Disable occlusion culling at runtime (overhead > savings in open space)
            cam.useOcclusionCulling = false;

            // Lower per-layer cull distances for particle/FX layers
            float[] distances = new float[32];
            for (int i = 0; i < 32; i++)
                distances[i] = 300f;

            // If there's a dedicated particle/FX layer, cull it much tighter
            int fxLayer = LayerMask.NameToLayer("TransparentFX");
            if (fxLayer >= 0)
                distances[fxLayer] = 80f;

            cam.layerCullDistances = distances;
            cam.layerCullSpherical = true; // spherical culling is cheaper for moving cameras
        }

        static void DisablePostProcessing()
        {
            // Find all active Volume components and disable everything expensive
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

                if (vol.profile.TryGet<DepthOfField>(out var dof))
                    dof.active = false;

                if (vol.profile.TryGet<FilmGrain>(out var grain))
                    grain.active = false;

                if (vol.profile.TryGet<LensDistortion>(out var lens))
                    lens.active = false;

                if (vol.profile.TryGet<ColorAdjustments>(out var color))
                    color.active = false;
            }
        }
    }
}

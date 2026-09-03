using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Runtime hooks for the Android stripped-performance branch (see <see cref="PerfStrip"/>).
    ///
    /// SKYBOX: killed. The authored HyperSea sky is a 767-line fragment shader (two 3x3x3 Voronoi
    /// loops, 7 FBM octaves, star field + twinkle + nebulae + dust + two galaxy cores) shading
    /// nearly every pixel every frame - one of the largest GPU costs on a mid phone. Cameras fall
    /// back to a solid deep-space clear, which is also strictly cheaper than ANY skybox (no
    /// full-screen sample, no background overdraw).
    ///
    /// A runtime cubemap bake was tried and REVERTED on measurement: it restored the look but cost
    /// frames, and its failure path was worse still - it kept the procedural sky at full price. If
    /// the sky is ever wanted back, bake it OFFLINE (FrogletTools > Bake Static HyperSea Skybox
    /// writes Resources/StaticHyperSeaSkybox) so the cost is a texture sample and never a shader;
    /// this loader picks that asset up automatically if it exists.
    ///
    /// POST-PROCESSING: restored for gameplay only, and only on the camera that actually presents
    /// to the screen. See <see cref="ApplyPostProcessing"/>.
    ///
    /// ANTI-ALIASING: FXAA, Low quality, on the presenting camera in every scene. See
    /// <see cref="ApplyAntiAliasing"/> for why FXAA and not MSAA or TAA.
    /// </summary>
    public static class PerfStripRuntime
    {
        static readonly Color DeepSpace = new(0.012f, 0.008f, 0.035f, 1f);

        static Material _bakedSkybox;
        static bool _bakedSkyboxLookedUp;
        static bool _hooked;
        static int _passesLeft;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_hooked || !PerfStrip.Enabled) return;
            _hooked = true;

            var host = new GameObject("~PerfStripRuntime") { hideFlags = HideFlags.HideAndDontSave };
            host.AddComponent<PerfStripRuntimeHost>();
            Object.DontDestroyOnLoad(host);

            SceneManager.sceneLoaded += (_, _) => ScheduleApply();
            ScheduleApply();
        }

        /// <summary>
        /// Queues a few passes rather than one. Cameras are NOT scene-owned here - the gameplay
        /// scenes contain none at all; CameraManager owns a persistent set in Bootstrap and enables
        /// one at a time - and a vessel brings its own (the Squirrel nests a PipCamera) well after
        /// the scene loads. A single pass at sceneLoaded therefore decided the look before the
        /// cameras it had to decide about existed. Passes are one-time per scene, not per frame.
        /// </summary>
        internal static void ScheduleApply() => _passesLeft = 5;

        internal static bool WantsPass => _passesLeft > 0;

        internal static void RunPass()
        {
            _passesLeft--;
            Apply();
        }

        static void Apply()
        {
            ApplySkybox();
            ApplyPostProcessing();
            ApplyAntiAliasing();
        }

        static void ApplySkybox()
        {
            if (!_bakedSkyboxLookedUp)
            {
                _bakedSkyboxLookedUp = true;
                _bakedSkybox = Resources.Load<Material>("StaticHyperSeaSkybox");
            }

            RenderSettings.skybox = _bakedSkybox; // null when unbaked -> solid clear below

            foreach (var cam in AllCamerasIncludingInactive())
            {
                cam.clearFlags = _bakedSkybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                cam.backgroundColor = DeepSpace;
            }
        }

        /// <summary>
        /// Post-processing is restored for GAMEPLAY scenes and stays off everywhere else.
        ///
        /// Two active overrides live on the one persistent Volume (it rides the Bootstrap
        /// PostProcessingManager as DontDestroyOnLoad, so there is no per-scene volume to read):
        /// Bloom and PaniniProjection. Both are load-bearing for how a race FEELS - bloom is the
        /// vaporwave neon read, and Panini is HALF THE SPEED TUNNEL. Docs/SPEED_TUNNEL.md is a
        /// platform law whose FOV half is a direct camera write that survived the strip, while
        /// SetSpeedTunnelPanini drives a Volume override - so disabling post silently amputated
        /// the half that sells speed while leaving the law looking intact.
        ///
        /// Affordability is measured: threshold 0.2 with clamp 0.5 needs no HDR (bloom never reads
        /// above the LDR range it is clamped into), and maxIterations 4 / skipIterations 6 is an
        /// already-cheap, low-resolution pyramid.
        ///
        /// It is granted to ONE camera class only: a BASE camera presenting to the screen. An
        /// earlier version set the flag on every camera it could find, which handed a second full
        /// post stack to every off-screen camera that renders anyway - the Squirrel nests a
        /// PipCamera drawing into a RenderTexture - and paid for the whole chain twice a frame.
        /// A camera with a targetTexture, or an Overlay camera (URP runs post once on the base of
        /// a stack, never per overlay), is explicitly turned OFF rather than left alone.
        /// </summary>
        static void ApplyPostProcessing()
        {
            bool keepPost = PerfStrip.AllowAuthoredPostProcessing
                            && IsGameplayScene()
                            && SceneHasActivePostOverride();

            foreach (var cam in AllCamerasIncludingInactive())
            {
                var extra = cam.GetComponent<UniversalAdditionalCameraData>();
                if (!extra) continue;

                extra.renderPostProcessing = keepPost && PresentsToScreen(cam, extra);
            }
        }

        /// <summary>
        /// FXAA (Fast Approximate Anti-Aliasing), Low quality, on the presenting camera, in every
        /// scene - not gated to gameplay the way Bloom/Panini are, because FXAA is architecturally
        /// independent of the Volume-driven post stack: it reads no VolumeProfile and is applied in
        /// URP's own final blit, so it costs nothing extra when <see cref="ApplyPostProcessing"/>
        /// has post-processing off, and one texture-neighbourhood pass everywhere it's on.
        ///
        /// FXAA over the alternatives, for this exact build:
        ///   - MSAA (the URP asset's own sample-count knob) needs a multisampled render target and
        ///     an explicit resolve. On tile-based mobile GPUs that is bandwidth PER SAMPLE across
        ///     the whole frame, not a fixed one-time cost like FXAA's single full-screen pass - and
        ///     with render scale at 0.8 (an upscale blit already in the pipeline), MSAA would be
        ///     smoothing edges in a buffer the final blit immediately resamples anyway.
        ///   - TAA needs per-object motion vectors and a history buffer it re-projects every frame.
        ///     Both are real GPU/bandwidth cost on top of FXAA's, and TAA specifically ghosts on
        ///     thin, fast-moving geometry - exactly what a trail of prisms is.
        ///   - SMAA (URP's other cheap option) is higher quality than FXAA but multi-pass (edge
        ///     detection, blend-weight calculation, neighbourhood blend) against FXAA's one.
        /// FXAA is also a natural fit for this project's prism transparency, which is dithered
        /// alpha-clip rather than real blending (Docs/PRISM_ANIMATION.md SS4.7) - that dithering
        /// produces sub-pixel-noisy edges FXAA's edge-detect blur was built to soften.
        ///
        /// <see cref="AntialiasingQuality"/> is read by SMAA only and is inert for FXAA; set here
        /// anyway so a future bump to SMAA starts at the cheap tier rather than URP's Medium default.
        /// </summary>
        static void ApplyAntiAliasing()
        {
            bool keepAA = PerfStrip.AllowAntiAliasing;

            foreach (var cam in AllCamerasIncludingInactive())
            {
                var extra = cam.GetComponent<UniversalAdditionalCameraData>();
                if (!extra) continue;

                if (keepAA && PresentsToScreen(cam, extra))
                {
                    extra.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                    extra.antialiasingQuality = AntialiasingQuality.Low;
                }
                else
                {
                    extra.antialiasing = AntialiasingMode.None;
                }
            }
        }

        /// <summary>
        /// True for the ONE camera class both post-processing and anti-aliasing may cost anything
        /// on: a Base-type camera with no RenderTexture target. Shared so the two features can
        /// never independently drift on what "presents to screen" means - the Round 6 bug (every
        /// camera, including the Squirrel's RenderTexture-targeted PipCamera, paying the full post
        /// stack) was exactly two copies of this condition disagreeing.
        /// </summary>
        static bool PresentsToScreen(Camera cam, UniversalAdditionalCameraData extra)
        {
            return cam.targetTexture == null && extra.renderType == CameraRenderType.Base;
        }

        /// <summary>
        /// Every camera, INCLUDING disabled ones. Load-bearing: CameraManager keeps a persistent
        /// set (CM PlayerCam / Camera / CM EndCam / CM DeathCam) and enables one at a time, so
        /// <c>Camera.allCameras</c> - which returns only enabled cameras - reaches whichever
        /// happened to be live during the pass and silently skips the one that will render next.
        /// That is why gameplay bloom stayed off after the first attempt to restore it.
        /// </summary>
        static Camera[] AllCamerasIncludingInactive()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            return Resources.FindObjectsOfTypeAll<Camera>();
#endif
        }

        /// <summary>
        /// True when this is a playable minigame scene. Probed by the presence of the scene's
        /// <see cref="CosmicShore.Gameplay.MiniGameControllerBase"/> - exactly one per gameplay
        /// scene and none elsewhere, the same self-resolving idiom Docs/GAMECANVAS.md uses, so a
        /// new mode gets its authored look without joining a scene-name allow-list.
        /// </summary>
        static bool IsGameplayScene()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<CosmicShore.Gameplay.MiniGameControllerBase>(
                       FindObjectsInactive.Include) != null;
#else
            return Object.FindObjectOfType<CosmicShore.Gameplay.MiniGameControllerBase>(true) != null;
#endif
        }

        /// <summary>True when some enabled Volume carries at least one ACTIVE override.</summary>
        static bool SceneHasActivePostOverride()
        {
#if UNITY_2023_1_OR_NEWER
            var volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
#else
            var volumes = Object.FindObjectsOfType<Volume>();
#endif
            foreach (var volume in volumes)
            {
                if (!volume || !volume.enabled || !volume.gameObject.activeInHierarchy) continue;

                var profile = volume.sharedProfile; // never .profile - that CLONES the asset
                if (profile == null) continue;

                foreach (var component in profile.components)
                    if (component != null && component.active) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Drives <see cref="PerfStripRuntime"/>'s deferred passes. A plain hidden DontDestroyOnLoad
    /// host: the strip's decisions depend on objects (the render camera, the game controller) that
    /// do not all exist at sceneLoaded, and a static class has no frame to wait on. Goes quiet
    /// once the queued passes are spent - there is no standing per-frame work.
    /// </summary>
    internal class PerfStripRuntimeHost : MonoBehaviour
    {
        float _next;

        void LateUpdate()
        {
            if (!PerfStripRuntime.WantsPass) return;
            if (Time.unscaledTime < _next) return;

            _next = Time.unscaledTime + 0.5f; // covers vessel spawn (~0.4s) and cell init (~1s)
            PerfStripRuntime.RunPass();
        }
    }
}

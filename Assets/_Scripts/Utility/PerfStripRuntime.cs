using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Runtime hooks for the Android stripped-performance branch (see <see cref="PerfStrip"/>).
    ///
    /// Owns two things the strip has to get right per scene:
    ///
    /// SKYBOX. The authored HyperSea sky is a 767-line fragment shader (two 3x3x3 Voronoi loops,
    /// 7 FBM octaves, star field + twinkle + nebulae + dust + two galaxy cores) shading nearly
    /// every pixel every frame. It is baked ONCE into a cubemap at runtime and then drawn as a
    /// single texture sample per pixel. The bake is cheap by construction: six 256px faces is
    /// ~0.4 MP, i.e. LESS pixel work than one 1080p frame of the procedural shader, paid once.
    /// If the bake cannot run, the authored sky is KEPT rather than cleared - the previous version
    /// cleared it and fell back to a solid colour, which shipped a black void whenever the
    /// pre-baked asset was missing.
    ///
    /// POST-PROCESSING. Restored for gameplay, off elsewhere. See <see cref="ApplyPostProcessing"/>.
    /// </summary>
    public static class PerfStripRuntime
    {
        const int BakeFaceSize = 256;

        // Authored sky -> its baked cubemap stand-in. Keyed per material because the strip walks
        // scenes with DIFFERENT authored skies (Bootstrap is BlackSkybox, gameplay + menu are the
        // procedural HyperSea); a single cached bake would pin whichever scene loaded first onto
        // every later one - and Bootstrap loads first, so that cache would have been the black one.
        static readonly System.Collections.Generic.Dictionary<Material, Material> _baked = new();
        static readonly System.Collections.Generic.HashSet<Material> _bakeFailed = new();
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
        /// Queues several passes rather than one. Cameras are NOT scene-owned here - the gameplay
        /// scenes contain none at all; CameraManager owns a persistent set in Bootstrap and enables
        /// one at a time - and a vessel's camera is handed over well after the scene loads
        /// (preSpawnDelayMs, then the cell's own InitDelayMs). A single pass at sceneLoaded
        /// therefore decided the look before the camera that renders the game existed.
        /// </summary>
        internal static void ScheduleApply() => _passesLeft = 6;

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
        }

        static void ApplySkybox()
        {
            var current = RenderSettings.skybox;
            if (current != null && !_baked.ContainsValue(current))
            {
                if (_baked.TryGetValue(current, out var cached))
                {
                    RenderSettings.skybox = cached;
                }
                else if (!_bakeFailed.Contains(current))
                {
                    var baked = TryBakeStaticSkybox(current);
                    if (baked != null)
                    {
                        _baked[current] = baked;
                        RenderSettings.skybox = baked;
                    }
                    else
                    {
                        // Keep the authored sky. The previous version cleared it and fell back to a
                        // solid colour, which shipped a black void whenever the bake was missing.
                        _bakeFailed.Add(current);
                    }
                }
            }

            foreach (var cam in AllCamerasIncludingInactive())
                if (cam.clearFlags == CameraClearFlags.SolidColor)
                    cam.clearFlags = CameraClearFlags.Skybox;
        }

        /// <summary>
        /// Renders the authored procedural sky into a cubemap once and wraps it in a
        /// <c>Skybox/Cubemap</c> material. Returns null when the pipeline or shader is unavailable
        /// (a Release build can strip a builtin shader no material references), in which case the
        /// caller keeps the authored sky - correct look, higher cost, never a black void.
        /// </summary>
        static Material TryBakeStaticSkybox(Material authored)
        {
            var cubemapShader = Shader.Find("Skybox/Cubemap");
            if (cubemapShader == null)
            {
                Debug.LogWarning("[PerfStrip] Skybox/Cubemap shader unavailable; keeping the " +
                                 "procedural sky (correct, but full per-pixel cost).");
                return null;
            }

            GameObject rig = null;
            try
            {
                var cubemap = new Cubemap(BakeFaceSize, TextureFormat.RGBA32, false);

                rig = new GameObject("~SkyboxBake") { hideFlags = HideFlags.HideAndDontSave };
                var cam = rig.AddComponent<Camera>();
                cam.enabled = false;             // rendered explicitly, never in the normal loop
                cam.cullingMask = 0;             // sky only - no scene geometry in the bake
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.farClipPlane = 10f;

                if (!cam.RenderToCubemap(cubemap))
                {
                    Debug.LogWarning("[PerfStrip] RenderToCubemap failed; keeping the procedural sky.");
                    return null;
                }

                cubemap.Apply(false, true); // upload, then drop the CPU copy

                var mat = new Material(cubemapShader) { hideFlags = HideFlags.HideAndDontSave };
                mat.SetTexture("_Tex", cubemap);
                Debug.Log($"[PerfStrip] Baked '{authored.name}' to a {BakeFaceSize}px static cubemap.");
                return mat;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PerfStrip] Skybox bake failed ({e.Message}); keeping the procedural sky.");
                return null;
            }
            finally
            {
                if (rig != null) Object.Destroy(rig);
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
        /// The gate is the SCENE, not the profile - that single persistent Volume is equally
        /// "active" in the menu, where the lava lamp / conveyor would pay the UberPost blit and the
        /// 32-cubed colour-grading LUT for a look the strip deliberately traded away.
        /// </summary>
        static void ApplyPostProcessing()
        {
            bool keepPost = PerfStrip.AllowAuthoredPostProcessing
                            && IsGameplayScene()
                            && SceneHasActivePostOverride();

            foreach (var cam in AllCamerasIncludingInactive())
            {
                var extra = cam.GetComponent<UniversalAdditionalCameraData>();
                if (extra) extra.renderPostProcessing = keepPost;
            }
        }

        /// <summary>
        /// Every camera, INCLUDING disabled ones. Load-bearing: CameraManager keeps a persistent
        /// set (CM PlayerCam / Camera / CM EndCam / CM DeathCam) and enables one at a time, so
        /// <c>Camera.allCameras</c> - which returns only enabled cameras - reaches whichever
        /// happened to be live during the pass and silently skips the one that will render next.
        /// That is why gameplay bloom stayed off: the menu pass disabled post on the then-live
        /// camera, and the gameplay pass could not re-enable the camera that was still inactive.
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
    /// do not all exist at sceneLoaded, and a static class has no frame to wait on.
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

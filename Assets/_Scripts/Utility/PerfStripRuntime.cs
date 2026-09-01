using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Runtime hooks for the Android stripped-performance branch (see <see cref="PerfStrip"/>).
    ///
    /// Kills the procedural HyperSea skybox: a 767-line fragment shader (two 3×3×3 Voronoi loops,
    /// 7 FBM octaves, star field + twinkle + nebulae + dust + two galaxy cores) that shades nearly
    /// every pixel every frame — one of the largest GPU costs on a mid phone. With
    /// <c>RenderSettings.skybox = null</c> a Skybox-clear camera falls back to its solid background
    /// colour, so we also paint every camera a deep-space tone to keep the void readable. Applied
    /// on every scene load (the setting is per-scene).
    /// </summary>
    public static class PerfStripRuntime
    {
        static readonly Color DeepSpace = new(0.012f, 0.008f, 0.035f, 1f);
        static Material _bakedSkybox;
        static bool _bakedSkyboxLookedUp;
        static bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_hooked || !PerfStrip.Enabled) return;
            _hooked = true;

            SceneManager.sceneLoaded += (_, _) => Apply();
            Apply();
        }

        static void Apply()
        {
            // Prefer the pre-baked static cubemap of the HyperSea sky (one texture sample per
            // pixel — bake it once via FrogletTools > Bake Static HyperSea Skybox). Without it,
            // fall back to no skybox + solid deep-space clear.
            if (!_bakedSkyboxLookedUp)
            {
                _bakedSkyboxLookedUp = true;
                _bakedSkybox = Resources.Load<Material>("StaticHyperSeaSkybox");
            }

            RenderSettings.skybox = _bakedSkybox; // null when unbaked → solid clear below

            // Post-processing is restored for GAMEPLAY scenes and stays off everywhere else.
            //
            // Two active overrides live on the one persistent Volume (it rides the Bootstrap
            // PostProcessingManager as DontDestroyOnLoad, so there is no per-scene volume to read):
            // Bloom and PaniniProjection. Both are load-bearing for how a race FEELS — bloom is the
            // vaporwave neon read, and Panini is HALF THE SPEED TUNNEL. Docs/SPEED_TUNNEL.md is a
            // platform law, and its FOV half is a direct camera write that survived the strip, but
            // PostProcessingManager.SetSpeedTunnelPanini drives a Volume override — so blanket
            // -disabling post silently amputated the half that actually sells speed while leaving
            // the law looking intact.
            //
            // Affordability here is measured, not hoped: threshold 0.2 with clamp 0.5 means bloom
            // needs no HDR (it never reads above the LDR range it is clamped into), and
            // maxIterations 4 / skipIterations 6 is an already-cheap, low-resolution pyramid.
            //
            // The gate is the SCENE, not the profile: that single persistent Volume is equally
            // "active" in the menu, where the lava lamp / conveyor would pay the UberPost blit and
            // the 32³ colour-grading LUT for a look the strip deliberately traded away.
            bool keepPost = PerfStrip.AllowAuthoredPostProcessing
                            && IsGameplayScene()
                            && SceneHasActivePostOverride();

            foreach (var cam in Camera.allCameras)
            {
                cam.clearFlags = _bakedSkybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                cam.backgroundColor = DeepSpace;

                var extra = cam.GetComponent<UniversalAdditionalCameraData>();
                if (extra) extra.renderPostProcessing = keepPost;
            }
        }

        /// <summary>
        /// True when this is a playable minigame scene. Probed by the presence of the scene's
        /// <see cref="CosmicShore.Gameplay.MiniGameControllerBase"/> — there is exactly one per
        /// gameplay scene and none anywhere else, which is the same self-resolving idiom the shared
        /// GameCanvas uses (Docs/GAMECANVAS.md) rather than a scene-name allow-list a new mode
        /// would have to remember to join.
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

        /// <summary>
        /// True when some enabled Volume in the loaded scene carries at least one ACTIVE override.
        /// Deliberately derived from the authored data rather than a scene-name allow-list: a new
        /// gameplay scene then gets its authored look with nothing to remember to register, and a
        /// profile whose effects are all switched off still costs nothing.
        /// </summary>
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

                var profile = volume.sharedProfile; // never .profile — that CLONES the asset
                if (profile == null) continue;

                foreach (var component in profile.components)
                    if (component != null && component.active) return true;
            }
            return false;
        }
    }
}

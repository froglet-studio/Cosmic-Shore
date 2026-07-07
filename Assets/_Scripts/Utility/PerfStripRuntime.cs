using UnityEngine;
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

            foreach (var cam in Camera.allCameras)
            {
                cam.clearFlags = _bakedSkybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                cam.backgroundColor = DeepSpace;

                // The scene camera has renderPostProcessing=1 but every volume override is neutral
                // (bloom 0, vignette 0, tonemapping None) — the flag alone still buys a 32³ color-
                // grading LUT render + a full-screen UberPost blit + final blit every frame, for
                // literally nothing. Turn the whole chain off.
                var extra = cam.GetComponent<UniversalAdditionalCameraData>();
                if (extra) extra.renderPostProcessing = false;
            }
        }
    }
}

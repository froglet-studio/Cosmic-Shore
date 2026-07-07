using System.IO;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Bakes the procedural HyperSea skybox (a 767-line fragment shader — two 27-cell Voronoi
    /// loops, 7 FBM octaves, stars/nebulae/dust/galaxy cores per pixel, far over mobile budget)
    /// into a static cubemap once, and wraps it in a <c>Skybox/Cubemap</c> material at
    /// <c>Assets/Resources/StaticHyperSeaSkybox.mat</c>. At runtime the stripped-performance
    /// branch (<see cref="CosmicShore.Utility.PerfStripRuntime"/>) loads that material and uses it
    /// as the skybox — the full HyperSea look at ONE texture sample per pixel. If the baked asset
    /// doesn't exist, the runtime falls back to a solid deep-space clear.
    ///
    /// Run: FrogletTools ▸ Bake Static HyperSea Skybox. Re-run any time the procedural skybox
    /// changes. (Twinkle/animation freezes at the baked instant — static by design.)
    /// </summary>
    public static class BakeStaticSkybox
    {
        const int FaceSize = 512; // ~8MB RGBA32+mips; bump to 1024 if banding bothers you (~32MB)
        const string SourceMaterialPath = "Assets/_Graphics/Skyboxes/HyperSeaSkybox.mat";
        const string CubemapAssetPath = "Assets/_Graphics/Skyboxes/HyperSeaStaticCubemap.asset";
        const string RuntimeMaterialPath = "Assets/Resources/StaticHyperSeaSkybox.mat";

        [MenuItem("FrogletTools/Bake Static HyperSea Skybox")]
        public static void Bake()
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath);
            if (!source)
            {
                Debug.LogError($"[BakeStaticSkybox] Source skybox material not found at {SourceMaterialPath}");
                return;
            }

            var previousSkybox = RenderSettings.skybox;
            GameObject rigGO = null;
            try
            {
                RenderSettings.skybox = source;

                rigGO = new GameObject("~SkyboxBakeCamera");
                var cam = rigGO.AddComponent<Camera>();
                cam.transform.position = Vector3.zero;
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.cullingMask = 0;            // nothing but the skybox
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 10f;

                var cubemap = new Cubemap(FaceSize, TextureFormat.RGBA32, true);
                if (!cam.RenderToCubemap(cubemap))
                {
                    Debug.LogError("[BakeStaticSkybox] RenderToCubemap failed on this pipeline. " +
                                   "Fallback: place a Reflection Probe at the origin, bake it, and " +
                                   "assign its cubemap to a Skybox/Cubemap material saved at " +
                                   RuntimeMaterialPath);
                    Object.DestroyImmediate(cubemap);
                    return;
                }

                // Persist (replace previous bakes in place so GUIDs/references stay stable).
                var existingCube = AssetDatabase.LoadAssetAtPath<Cubemap>(CubemapAssetPath);
                if (existingCube)
                {
                    EditorUtility.CopySerialized(cubemap, existingCube);
                    Object.DestroyImmediate(cubemap);
                    cubemap = existingCube;
                }
                else
                {
                    AssetDatabase.CreateAsset(cubemap, CubemapAssetPath);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(RuntimeMaterialPath)!);
                var runtimeMat = AssetDatabase.LoadAssetAtPath<Material>(RuntimeMaterialPath);
                if (!runtimeMat)
                {
                    runtimeMat = new Material(Shader.Find("Skybox/Cubemap"));
                    AssetDatabase.CreateAsset(runtimeMat, RuntimeMaterialPath);
                }
                runtimeMat.shader = Shader.Find("Skybox/Cubemap");
                runtimeMat.SetTexture("_Tex", cubemap);
                EditorUtility.SetDirty(runtimeMat);

                AssetDatabase.SaveAssets();
                Debug.Log($"[BakeStaticSkybox] Baked {FaceSize}px/face cubemap → {CubemapAssetPath}; " +
                          $"runtime material → {RuntimeMaterialPath}. The stripped build will pick it " +
                          "up automatically on next launch.");
            }
            finally
            {
                RenderSettings.skybox = previousSkybox;
                if (rigGO) Object.DestroyImmediate(rigGO);
            }
        }
    }
}

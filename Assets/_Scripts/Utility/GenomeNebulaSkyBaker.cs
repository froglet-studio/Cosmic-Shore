using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Bakes the genome nebula sky (<c>CosmicShore/GenomeNebulaSkybox</c>, pass "Bake") ONCE into an equirect
    /// render target and draws it through <c>CosmicShore/StaticSkyPanorama</c>, so the per-frame cost is one texture
    /// sample per pixel - the same runtime cost as the MobileLow baked HyperSea sky - while the build ships no
    /// texture at all: the sky is ~30 numbers on a material, generated on the GPU in well under a frame at load.
    ///
    /// Prototype for <c>Docs/GENOME_NEBULA_SKYBOX.md</c>; nothing references it yet. Put it on any object in a
    /// scene to try the sky there. The baked sky is static: the drift animation needs the procedural pass.
    /// </summary>
    public sealed class GenomeNebulaSkyBaker : MonoBehaviour
    {
        [Header("Materials")]
        [Tooltip("A material using CosmicShore/GenomeNebulaSkybox (the genome's numbers live here).")]
        [SerializeField] Material genomeSky;

        [Tooltip("A material using CosmicShore/StaticSkyPanorama; its _MainTex is replaced by the bake.")]
        [SerializeField] Material panorama;

        [Header("Bake")]
        [Tooltip("Width of the equirect bake; height is half. 2048 is ~16 MB of RGBA8 with no mips, 1024 is ~4 MB.")]
        [SerializeField] int width = 2048;

        [Tooltip("Off: bake once and draw the panorama (cheapest). On: draw the procedural sky every frame (drift animates).")]
        [SerializeField] bool procedural;

        static readonly int MainTex = Shader.PropertyToID("_MainTex");
        const int BakePass = 1;

        RenderTexture _baked;
        Material _panoramaInstance;
        Material _previousSky;

        void OnEnable()
        {
            _previousSky = RenderSettings.skybox;
            if (procedural)
            {
                RenderSettings.skybox = genomeSky;
                return;
            }

            int w = Mathf.Max(64, width);
            _baked = new RenderTexture(w, w / 2, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                useMipMap = false,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = "GenomeNebulaSkyBake",
            };
            _baked.Create();
            Graphics.Blit(null, _baked, genomeSky, BakePass);

            _panoramaInstance = new Material(panorama) { name = "GenomeNebulaSkyPanorama (baked)" };
            _panoramaInstance.SetTexture(MainTex, _baked);
            RenderSettings.skybox = _panoramaInstance;
        }

        void OnDisable()
        {
            RenderSettings.skybox = _previousSky;
            if (_baked)
            {
                _baked.Release();
                Destroy(_baked);
                _baked = null;
            }

            if (_panoramaInstance)
            {
                Destroy(_panoramaInstance);
                _panoramaInstance = null;
            }
        }
    }
}

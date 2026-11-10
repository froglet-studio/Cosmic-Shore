using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Puts the genome nebula sky (<c>CosmicShore/GenomeNebulaSkybox</c>) on screen by one of the production methods
    /// measured in <c>Docs/GENOME_NEBULA_SKYBOX.md</c> section 6. Every method is made from the ~30 numbers on the
    /// material at load, so the build ships no sky texture.
    ///
    /// Prototype; nothing references it yet. Add it to any object in a scene to try the sky there. It replaces
    /// <c>RenderSettings.skybox</c> while enabled and puts the previous sky back when disabled.
    /// </summary>
    public sealed class GenomeNebulaSkyBaker : MonoBehaviour
    {
        public enum Method
        {
            [Tooltip("The procedural sky every frame, in the material's own mapping. Desktop.")]
            Procedural = 0,
            [Tooltip("Baked once into an equirect panorama drawn by StaticSkyPanorama (the MobileLow HyperSea layout).")]
            Panorama = 1,
            [Tooltip("Baked once into a cubemap. Best static method: one sample, no seam, no pole, smallest for its quality.")]
            Cubemap = 2,
            [Tooltip("A cubemap re-baked one face per frame, so the drift animates at a sixth of the frame rate.")]
            CubemapTimeSliced = 3,
            [Tooltip("The two noise fields baked once into a half-float 3D texture; warp and colour run live (3 fetches). Animates every frame.")]
            Noise3D = 4,
        }

        [Header("Materials")]
        [Tooltip("A material using CosmicShore/GenomeNebulaSkybox (the genome's numbers live here). It is copied, never edited.")]
        [SerializeField] Material genomeSky;

        [Tooltip("Panorama method only: a material using CosmicShore/StaticSkyPanorama.")]
        [SerializeField] Material panorama;

        [Tooltip("Cubemap methods only: a material using CosmicShore/GenomeNebulaCubeSky.")]
        [SerializeField] Material cubeSky;

        [Header("Method")]
        [SerializeField] Method method = Method.Cubemap;

        [Tooltip("Panorama width (height is half). 2048 = 8 MB RGBA8.")]
        [SerializeField] int panoramaWidth = 2048;

        [Tooltip("Cubemap face size. 512 = 6 MB RGBA8 (measured mean error 0.8/255 against the per-pixel sky), 256 = 1.5 MB (1.3/255).")]
        [SerializeField] int cubeFace = 512;

        [Tooltip("Noise3D texture size per side. 96 = 3.4 MB RGHalf (mean error 1.7/255), 128 = 8 MB (1.1/255).")]
        [SerializeField] int noise3DSize = 96;

        static readonly int MainTex = Shader.PropertyToID("_MainTex");
        static readonly int CubeId = Shader.PropertyToID("_Cube");
        static readonly int Noise3DId = Shader.PropertyToID("_Noise3D");
        static readonly int SliceId = Shader.PropertyToID("_Slice");
        static readonly int Noise3DSizeId = Shader.PropertyToID("_Noise3DSize");
        static readonly int FaceXId = Shader.PropertyToID("_FaceX");
        static readonly int FaceYId = Shader.PropertyToID("_FaceY");
        static readonly int FaceZId = Shader.PropertyToID("_FaceZ");
        const int BakeCubeFacePass = 3;

        // Per face (Unity's +X..-Z order): the s axis, the t axis and the face normal of the GL / D3D cube table.
        static readonly Vector3[] FaceAxes =
        {
            new(0, 0, -1), new(0, -1, 0), new(1, 0, 0),
            new(0, 0, 1), new(0, -1, 0), new(-1, 0, 0),
            new(1, 0, 0), new(0, 0, 1), new(0, 1, 0),
            new(1, 0, 0), new(0, 0, -1), new(0, -1, 0),
            new(1, 0, 0), new(0, -1, 0), new(0, 0, 1),
            new(-1, 0, 0), new(0, -1, 0), new(0, 0, -1),
        };
        const int BakeEquirectPass = 1;
        const int BakeNoise3DPass = 2;
        const string Equirect = "_MAPPING_EQUIRECT", Sphere = "_MAPPING_SPHERE", Noise3DKeyword = "_MAPPING_NOISE3D";

        Material _previousSky;
        Material _sky;          // our copy of genomeSky, in the mapping the method needs
        Material _drawn;        // what RenderSettings.skybox shows
        RenderTexture _target;  // panorama, cubemap or 3D texture
        Camera _bakeCamera;
        int _nextFace;

        void OnEnable()
        {
            _previousSky = RenderSettings.skybox;
            _sky = new Material(genomeSky) { name = genomeSky.name + " (baker copy)" };

            switch (method)
            {
                case Method.Procedural:
                    _drawn = _sky;
                    break;
                case Method.Panorama:
                    SetMapping(_sky, Sphere);
                    _target = NewTarget(Mathf.Max(64, panoramaWidth), Mathf.Max(32, panoramaWidth / 2), RenderTextureFormat.ARGB32,
                                        TextureDimension.Tex2D, 1);
                    Graphics.Blit(null, _target, _sky, BakeEquirectPass);
                    _drawn = new Material(panorama);
                    _drawn.SetTexture(MainTex, _target);
                    break;
                case Method.Cubemap:
                case Method.CubemapTimeSliced:
                    SetMapping(_sky, Sphere);
                    int face = Mathf.Max(16, cubeFace);
                    _target = NewTarget(face, face, RenderTextureFormat.ARGB32, TextureDimension.Cube, 1);
                    _bakeCamera = NewBakeCamera();
                    RenderFaces(63);
                    _drawn = new Material(cubeSky);
                    _drawn.SetTexture(CubeId, _target);
                    break;
                case Method.Noise3D:
                    int n = Mathf.Max(8, noise3DSize);
                    _target = NewTarget(n, n, RenderTextureFormat.RGHalf, TextureDimension.Tex3D, n);
                    _target.wrapMode = TextureWrapMode.Repeat;
                    _sky.SetFloat(Noise3DSizeId, n);
                    for (int z = 0; z < n; z++)
                    {
                        _sky.SetFloat(SliceId, z);
                        Graphics.Blit(null, _target, _sky, BakeNoise3DPass, z);
                    }
                    SetMapping(_sky, Noise3DKeyword);
                    _sky.SetTexture(Noise3DId, _target);
                    _drawn = _sky;
                    break;
            }

            RenderSettings.skybox = _drawn;
        }

        void Update()
        {
            if (method != Method.CubemapTimeSliced || !_bakeCamera) return;
            RenderFaces(1 << _nextFace);
            _nextFace = (_nextFace + 1) % 6;
        }

        void OnDisable()
        {
            RenderSettings.skybox = _previousSky;
            if (_bakeCamera) Destroy(_bakeCamera.gameObject);
            if (_target)
            {
                _target.Release();
                Destroy(_target);
            }
            if (_drawn && _drawn != _sky) Destroy(_drawn);
            if (_sky) Destroy(_sky);
            _bakeCamera = null; _target = null; _drawn = null; _sky = null;
        }

        /// <summary>Renders the procedural sky into the chosen cube faces (bit i = face i, Unity's +X..-Z order).
        /// Camera.RenderToCubemap draws RenderSettings.skybox, so the procedural sky is swapped in for the call. Where
        /// the pipeline refuses it, each face is drawn with the shader's own face pass instead (no camera).</summary>
        void RenderFaces(int faceMask)
        {
            var shown = RenderSettings.skybox;
            RenderSettings.skybox = _sky;
            bool rendered = _bakeCamera.RenderToCubemap(_target, faceMask);
            RenderSettings.skybox = shown;
            if (!rendered) BlitFaces(faceMask);
        }

        void BlitFaces(int faceMask)
        {
            var previous = RenderTexture.active;
            for (int f = 0; f < 6; f++)
            {
                if ((faceMask & (1 << f)) == 0) continue;
                _sky.SetVector(FaceXId, FaceAxes[f * 3]);
                _sky.SetVector(FaceYId, FaceAxes[f * 3 + 1]);
                _sky.SetVector(FaceZId, FaceAxes[f * 3 + 2]);
                Graphics.SetRenderTarget(_target, 0, (CubemapFace)f);
                GL.PushMatrix();
                GL.LoadOrtho();
                _sky.SetPass(BakeCubeFacePass);
                GL.Begin(GL.QUADS);
                GL.TexCoord2(0, 0); GL.Vertex3(0, 0, 0);
                GL.TexCoord2(1, 0); GL.Vertex3(1, 0, 0);
                GL.TexCoord2(1, 1); GL.Vertex3(1, 1, 0);
                GL.TexCoord2(0, 1); GL.Vertex3(0, 1, 0);
                GL.End();
                GL.PopMatrix();
            }
            RenderTexture.active = previous;
        }

        Camera NewBakeCamera()
        {
            var go = new GameObject("GenomeNebulaSkyBaker camera") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(transform, false);
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.cullingMask = 0;               // the sky only
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            return cam;
        }

        static RenderTexture NewTarget(int w, int h, RenderTextureFormat format, TextureDimension dim, int depth)
        {
            var rt = new RenderTexture(w, h, 0, format,
                                       format == RenderTextureFormat.ARGB32 ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear)
            {
                dimension = dim,
                volumeDepth = depth,
                useMipMap = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "GenomeNebulaSky " + dim,
            };
            if (dim == TextureDimension.Tex2D) rt.wrapModeU = TextureWrapMode.Repeat;
            rt.Create();
            return rt;
        }

        static void SetMapping(Material m, string keyword)
        {
            m.DisableKeyword(Equirect);
            m.DisableKeyword(Sphere);
            m.DisableKeyword(Noise3DKeyword);
            m.EnableKeyword(keyword);
        }
    }
}

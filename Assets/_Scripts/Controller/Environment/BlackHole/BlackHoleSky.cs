using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The sky the black hole's lens bends (Docs/BLACK_HOLE.md §5.1): <b>the scene's own skybox</b> —
    /// <see cref="RenderSettings.skybox"/>, whatever Lighting ▸ Environment ▸ Skybox Material names —
    /// rendered into six 90° faces of a texture array that <c>BlackHoleLens.shader</c> samples for a
    /// bent ray that leaves the screen. (A ray that stays on screen reads the opaque copy, which
    /// already holds the real sky and the real prisms.)
    ///
    /// <para><b>Why not URP's environment reflection.</b> The lens first read
    /// <c>_GlossyEnvironmentCubeMap</c>. That is the BAKED environment reflection: it is rebuilt only
    /// by Generate Lighting, so in a scene whose skybox was changed, or whose lighting was never
    /// generated, it is Unity's DEFAULT sky at 128 px — and the lens drew that default sky warped around
    /// a HyperSea hole, with a seam at the lens's rim where it met the real one.</para>
    ///
    /// <para><b>Why not a realtime ReflectionProbe.</b> They are off at the Very Low, Low and Medium
    /// quality levels, so the sky would silently vanish there. This renders the skybox material
    /// directly — one cube drawn per face with that face's view — and works at every level.</para>
    ///
    /// <para><b>Cost.</b> All six faces when a lens first appears and whenever the skybox or the
    /// resolution changes; after that <see cref="BlackHoleConfigSO.LensSkyFacesPerFrame"/> faces per
    /// frame, round-robin, so an animated sky (the HyperSea's drift and twinkle) stays in step with the
    /// real one. Nothing at all while no hole is live: the texture is freed with the last lens.</para>
    ///
    /// The face order and bases below are a contract with <c>BlackHoleSkyFaceUV</c> in
    /// <c>BlackHoleLens.hlsl</c>; <c>Tools/Shaders/verify_black_hole_lens.py</c> reads this table and
    /// checks the shader's sampling against it, so the two cannot drift.
    /// </summary>
    public static class BlackHoleSky
    {
        public const int FaceCount = 6;

        /// <summary>Each face's view direction. Face i is a camera looking along this with <see cref="FaceUp"/>[i] up.</summary>
        internal static readonly Vector3[] FaceForward =
        {
            new(1f, 0f, 0f), new(-1f, 0f, 0f), new(0f, 1f, 0f), new(0f, -1f, 0f), new(0f, 0f, 1f), new(0f, 0f, -1f),
        };

        /// <summary>Each face's up vector (right = Cross(up, forward), Unity's camera convention).</summary>
        internal static readonly Vector3[] FaceUp =
        {
            new(0f, 1f, 0f), new(0f, 1f, 0f), new(0f, 0f, -1f), new(0f, 0f, 1f), new(0f, 1f, 0f), new(0f, 1f, 0f),
        };

        static readonly int SkyId = Shader.PropertyToID("_BlackHoleSky");
        static readonly int ReadyId = Shader.PropertyToID("_BlackHoleSkyReady");

        // Near and far planes of the face cameras: the cube is drawn at half-extent 1, so its
        // corners (√3 out) sit well inside both.
        const float FaceNear = 0.05f;
        const float FaceFar = 10f;

        static int s_users;
        static RenderTexture s_faces;
        static Material s_renderedSkybox;
        static int s_nextFace;
        static bool s_ready;
        static bool s_warnedUnsupported;
        static bool s_warnedMultiPass;
        static Mesh s_cube;
        static CommandBuffer s_cmd;

        /// <summary>True once the faces hold the current skybox (the shader reads black until then).</summary>
        public static bool IsReady => s_ready;

        /// <summary>The six rendered faces (a Tex2DArray RenderTexture), or null while no lens is live.</summary>
        public static RenderTexture Faces => s_faces;

        /// <summary>
        /// The view matrix of face <paramref name="face"/>: a camera at the origin looking along
        /// <see cref="FaceForward"/> with <see cref="FaceUp"/> up — Unity's worldToCameraMatrix
        /// convention (rows right, up, −forward).
        /// </summary>
        public static Matrix4x4 FaceView(int face)
        {
            Vector3 f = FaceForward[face], u = FaceUp[face], r = Vector3.Cross(u, f);
            var m = Matrix4x4.identity;
            m.SetRow(0, new Vector4(r.x, r.y, r.z, 0f));
            m.SetRow(1, new Vector4(u.x, u.y, u.z, 0f));
            m.SetRow(2, new Vector4(-f.x, -f.y, -f.z, 0f));
            m.SetRow(3, new Vector4(0f, 0f, 0f, 1f));
            return m;
        }

        /// <summary>The 90° square projection every face is rendered with (OpenGL convention; Unity adapts it per API).</summary>
        public static Matrix4x4 FaceProjection => Matrix4x4.Perspective(90f, 1f, FaceNear, FaceFar);

        internal static void Acquire() => s_users++;

        internal static void Release()
        {
            s_users = Mathf.Max(0, s_users - 1);
            if (s_users == 0) Free();
        }

        /// <summary>Called every frame by the registry's driver: keep the faces current while a lens is live.</summary>
        internal static void Maintain(BlackHoleConfigSO config)
        {
            if (s_users == 0)
            {
                Free();
                return;
            }
            if (!SystemInfo.supports2DArrayTextures)
            {
                if (!s_warnedUnsupported)
                {
                    s_warnedUnsupported = true;
                    CSDebug.LogWarning("[BlackHole] This device has no 2D texture arrays: the lens bends the on-screen " +
                                       "scene only, and a ray bent off-screen shows black space.");
                }
                Publish(false);
                return;
            }

            var skybox = RenderSettings.skybox;
            int resolution = config.LensSkyResolution;
            bool rebuild = s_faces == null || !s_faces.IsCreated() || s_faces.width != resolution ||
                           skybox != s_renderedSkybox || !s_ready;
            if (rebuild)
            {
                Allocate(resolution);
                s_renderedSkybox = skybox;
                Render(skybox, 0, FaceCount);
                s_nextFace = 0;
                Publish(true);
                return;
            }

            int perFrame = config.LensSkyFacesPerFrame;
            if (perFrame > 0)
            {
                Render(skybox, s_nextFace, perFrame);
                s_nextFace = (s_nextFace + perFrame) % FaceCount;
            }
            // Re-published every frame: a global texture binding does not survive everything that
            // can happen between frames (a domain reload in the Editor, another system's reset).
            Publish(true);
        }

        static void Allocate(int resolution)
        {
            ReleaseTexture();
            var format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR)
                ? RenderTextureFormat.DefaultHDR
                : RenderTextureFormat.ARGB32;
            s_faces = new RenderTexture(resolution, resolution, 0, format, RenderTextureReadWrite.Linear)
            {
                name = "BlackHoleSky",
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = FaceCount,
                useMipMap = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            s_faces.Create();
        }

        /// <summary>
        /// Draw <paramref name="count"/> faces starting at <paramref name="first"/>: each face cleared
        /// to black (empty space when the scene has no skybox) and the skybox material drawn on a
        /// cube around that face's camera. A skybox shader's vertex position IS its view direction,
        /// so the cube covers every direction the face sees.
        /// </summary>
        static void Render(Material skybox, int first, int count)
        {
            if (skybox != null && skybox.passCount > 1 && !s_warnedMultiPass)
            {
                s_warnedMultiPass = true;
                CSDebug.LogWarning($"[BlackHole] The skybox '{skybox.name}' has {skybox.passCount} passes (a 6-sided " +
                                   "skybox); the lens draws its first pass only. Procedural, cubemap and panoramic " +
                                   "skyboxes are single-pass and drawn exactly.");
            }

            s_cmd ??= new CommandBuffer { name = "BlackHoleSky" };
            s_cmd.Clear();
            var projection = FaceProjection;
            for (int k = 0; k < Mathf.Min(count, FaceCount); k++)
            {
                int face = (first + k) % FaceCount;
                // Target first, then matrices: the projection Unity derives for a render texture
                // (its per-API flip) depends on the target being bound.
                s_cmd.SetRenderTarget(s_faces, 0, CubemapFace.Unknown, face);
                s_cmd.ClearRenderTarget(false, true, Color.black);
                if (skybox == null) continue;
                s_cmd.SetViewProjectionMatrices(FaceView(face), projection);
                s_cmd.DrawMesh(Cube(), Matrix4x4.identity, skybox, 0, 0);
            }
            // Leave the backbuffer bound, not the sky array: nothing after this inherits our target.
            s_cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
            Graphics.ExecuteCommandBuffer(s_cmd);
        }

        /// <summary>
        /// A cube of half-extent 1 around the origin, every face wound to face its CENTRE (Unity's
        /// front face has Cross(b − a, c − a) toward the viewer), so a skybox shader that culls back
        /// faces still draws it from inside. Skybox shaders are normally Cull Off; this does not rely on it.
        /// </summary>
        internal static Mesh Cube()
        {
            if (s_cube != null) return s_cube;
            var vertices = new Vector3[8];
            for (int i = 0; i < 8; i++)
                vertices[i] = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
            int[] quads =
            {
                1, 3, 7, 5,   0, 4, 6, 2,   2, 6, 7, 3,   0, 1, 5, 4,   4, 5, 7, 6,   0, 2, 3, 1,
            };
            var triangles = new int[36];
            for (int q = 0; q < 6; q++)
            {
                int a = quads[q * 4], b = quads[q * 4 + 1], c = quads[q * 4 + 2], d = quads[q * 4 + 3];
                int t = q * 6;
                triangles[t] = a; triangles[t + 1] = b; triangles[t + 2] = c;
                triangles[t + 3] = a; triangles[t + 4] = c; triangles[t + 5] = d;
            }
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) > 0f)
                    (triangles[t + 1], triangles[t + 2]) = (triangles[t + 2], triangles[t + 1]);
            }
            s_cube = new Mesh { name = "BlackHoleSkyCube", hideFlags = HideFlags.DontSave };
            s_cube.vertices = vertices;
            s_cube.triangles = triangles;
            s_cube.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return s_cube;
        }

        static void Publish(bool ready)
        {
            s_ready = ready && s_faces != null;
            if (s_ready) Shader.SetGlobalTexture(SkyId, s_faces);
            Shader.SetGlobalFloat(ReadyId, s_ready ? 1f : 0f);
        }

        static void Free()
        {
            if (s_faces == null && !s_ready) return;
            ReleaseTexture();
            s_renderedSkybox = null;
            s_nextFace = 0;
            Publish(false);
        }

        static void ReleaseTexture()
        {
            if (s_faces == null) return;
            s_faces.Release();
            if (Application.isPlaying) Object.Destroy(s_faces);
            else Object.DestroyImmediate(s_faces);
            s_faces = null;
            s_ready = false;
        }

        /// <summary>Play-mode (re)entry: no lens is live yet, and the shader must read "no sky".</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            s_users = 0;
            ReleaseTexture();
            s_renderedSkybox = null;
            s_nextFace = 0;
            s_warnedUnsupported = false;
            s_warnedMultiPass = false;
            Shader.SetGlobalFloat(ReadyId, 0f);
        }
    }
}

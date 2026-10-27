using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The bent space everyone sees round a Meerkat's tunnels (<c>R_VesselActions/MEERKAT.md</c>): the Vessel Studio's
    /// <c>updateTubeWarp</c> + <c>lensMat</c> + <c>setLensUniforms</c> + <c>rippleUniform</c>
    /// (<c>Docs/Studios/VesselStudio/meerkat.html</c>), the sibling of <see cref="BlackHoleLensPass"/> and
    /// <see cref="VortexRingLensPass"/> with the same plumbing: after URP has drawn the TRANSPARENTS,
    /// <list type="number">
    /// <item>every live tube's wall (<see cref="MeerkatWormholeView.WarpMesh"/>) is drawn into a half-resolution
    /// half-float target as a screen-space offset: the wall's view normal, strongest at the silhouette, bending in toward
    /// the way in and out toward the way out (<c>MeerkatWormholeWarp.shader</c>);</item>
    /// <item>the camera's colour is copied and drawn back through one full-screen triangle
    /// (<c>MeerkatWormholeLens.shader</c>) displaced by that offset, by each mouth's dent (as wide as the mouth, fading as
    /// the camera nears it) and by the collapse's ripple, a ring at the screen's EDGES only (nothing inside
    /// <c>rippleEdge</c> moves).</item>
    /// </list>
    /// <b>The contour field</b> (Garrett, 2026-10-27, from the Tube Lens Lab, <c>Docs/Studios/StoatTubeLensLab.html</c>): with
    /// <c>formedLook</c> ContourField a formed tunnel draws nothing and is ONLY bent space. The same lens draw ray-marches each
    /// pixel's ray through a thin shell on the tunnel's wall (<c>fieldInner</c> × r inside to <c>fieldOuter</c> × r outside,
    /// graded to zero at its edge). Inside the shell the ray is turned by a field whose lines on the wall are the tube's own
    /// contours (<c>fieldPitch</c> 0° along it, 90° round its rings), then it carries on; where it lands on screen is what
    /// that pixel shows. The four tunnels nearest the camera take part (<see cref="MaxTubes"/>).
    /// Injected from script on every base game camera while a tunnel or a ripple is live (URP's "inject a pass via
    /// scripting" route); a scene with none pays nothing. The camera depth texture (a vessel in front of a mouth is not
    /// bent) is held on through <see cref="BlackHoleLens.CameraSupport"/> while anything is live.
    /// </summary>
    public sealed class MeerkatWormholeLensPass : ScriptableRenderPass
    {
        public const string LensMaterialPath = "MeerkatWormholeLens";
        public const string WarpMaterialPath = "MeerkatWormholeWarp";
        /// <summary>The most mouths one pass dents (two per tunnel).</summary>
        public const int MaxWells = 8;
        /// <summary>The most tunnels one pass marches the contour field through (the nearest to the camera).</summary>
        public const int MaxTubes = 4;

        static readonly int SceneColorId = Shader.PropertyToID("_MeerkatSceneColor");
        static readonly int WarpTexId = Shader.PropertyToID("_MeerkatWarpTex");
        static readonly int WellCId = Shader.PropertyToID("_MWWellC");
        static readonly int WellPId = Shader.PropertyToID("_MWWellP");
        static readonly int RippleId = Shader.PropertyToID("_MWRipple");
        static readonly int ViewId = Shader.PropertyToID("_MWView");
        static readonly int TubeW2LId = Shader.PropertyToID("_MWTubeW2L");
        static readonly int TubeShapeId = Shader.PropertyToID("_MWTubeShape");
        static readonly int TubeWinId = Shader.PropertyToID("_MWTubeWin");
        static readonly int TubeBoundId = Shader.PropertyToID("_MWTubeBound");
        static readonly int Field0Id = Shader.PropertyToID("_MWField0");
        static readonly int Field1Id = Shader.PropertyToID("_MWField1");
        static readonly int Field2Id = Shader.PropertyToID("_MWField2");
        static readonly int Field3Id = Shader.PropertyToID("_MWField3");
        static readonly int CamPosId = Shader.PropertyToID("_MWCamPos");
        static readonly int CamRId = Shader.PropertyToID("_MWCamR");
        static readonly int CamUId = Shader.PropertyToID("_MWCamU");
        static readonly int CamFId = Shader.PropertyToID("_MWCamF");

        static MeerkatWormholeLensPass s_pass;
        static Material s_lens, s_warp;
        static bool s_resolved, s_holdingCameras;
        static readonly ProfilerMarker s_marker = new("MeerkatWormholeLens.Maintain");

        static readonly Vector4[] s_c = new Vector4[MaxWells];
        static readonly Vector4[] s_p = new Vector4[MaxWells];
        static readonly ContourTubes s_tubes = new();
        static readonly List<MeerkatWormholeExecutor.Tunnel> s_near = new();
        static readonly List<float> s_nearD = new();

        /// <summary>The contour field's bank for one camera: up to <see cref="MaxTubes"/> tunnels, the field's numbers, the eye.</summary>
        public sealed class ContourTubes
        {
            public readonly Matrix4x4[] W2L = new Matrix4x4[MaxTubes];
            public readonly Vector4[] Shape = new Vector4[MaxTubes];   // shaft S, bend radius R, theta, throat (round 7)
            public readonly Vector4[] Win = new Vector4[MaxTubes];     // s0, s1, fade, mouth
            public readonly Vector4[] Bound = new Vector4[MaxTubes];   // world centre, radius
            public Vector4 F0, F1, F2, F3, CamPos, CamR, CamU, CamF;
            public int Count;

            public void CopyTo(ContourTubes o)
            {
                System.Array.Copy(W2L, o.W2L, MaxTubes);
                System.Array.Copy(Shape, o.Shape, MaxTubes);
                System.Array.Copy(Win, o.Win, MaxTubes);
                System.Array.Copy(Bound, o.Bound, MaxTubes);
                o.F0 = F0; o.F1 = F1; o.F2 = F2; o.F3 = F3;
                o.CamPos = CamPos; o.CamR = CamR; o.CamU = CamU; o.CamF = CamF;
                o.Count = Count;
            }
        }

        class CopyData { public TextureHandle source; }

        class WarpData
        {
            public Material material;
            public Mesh mesh;
        }

        class LensData
        {
            public Material material;
            public readonly MaterialPropertyBlock block = new();
            public readonly Vector4[] c = new Vector4[MaxWells];
            public readonly Vector4[] p = new Vector4[MaxWells];
            public Vector4 ripple, view;
            public readonly ContourTubes tubes = new();
        }

        MeerkatWormholeLensPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            requiresIntermediateTexture = true;
            profilingSampler = new ProfilingSampler("MeerkatWormholeLens");
        }

        static bool AnythingLive => MeerkatWormholeExecutor.Live.Count > 0 || MeerkatWormholeExecutor.Ripples.Count > 0;

        static void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (!AnythingLive || cam == null) return;
            ScriptableRenderer renderer = null;
            if (cam.cameraType == CameraType.Game)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data == null || data.renderType != CameraRenderType.Base) return;
                renderer = data.scriptableRenderer;
            }
            else if (cam.cameraType == CameraType.SceneView)
            {
                var asset = UniversalRenderPipeline.asset;
                renderer = asset != null ? asset.scriptableRenderer : null;
            }
            if (renderer == null) return;
            s_pass ??= new MeerkatWormholeLensPass();
            renderer.EnqueuePass(s_pass);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;
            var cam = frameData.Get<UniversalCameraData>().camera;
            if (cam == null || cam.orthographic || !Resolve()) return;
            int wells = ScreenWells(cam, s_c, s_p);
            var ripple = RippleFor(cam);
            var warpMesh = MeerkatWormholeView.WarpMesh;
            bool warp = warpMesh != null && warpMesh.vertexCount > 0;
            int tubes = ContourTubesFor(cam, s_tubes);
            if (wells == 0 && ripple.x <= 0f && !warp && tubes == 0) return;
            TextureHandle colour = resources.activeColorTexture;

            var desc = renderGraph.GetTextureDesc(colour);
            desc.name = "_MeerkatSceneColor";
            desc.msaaSamples = MSAASamples.None;
            desc.bindTextureMS = false;
            desc.clearBuffer = false;
            desc.filterMode = FilterMode.Bilinear;
            desc.wrapMode = TextureWrapMode.Clamp;
            TextureHandle copy = renderGraph.CreateTexture(desc);

            // 1. The tubes' warp, half resolution (the page's rtWarp).
            var warpDesc = renderGraph.GetTextureDesc(colour);
            warpDesc.name = "_MeerkatWarpTex";
            warpDesc.width = Mathf.Max(2, warpDesc.width / 2);
            warpDesc.height = Mathf.Max(2, warpDesc.height / 2);
            warpDesc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
            warpDesc.depthBufferBits = DepthBits.None;
            warpDesc.msaaSamples = MSAASamples.None;
            warpDesc.bindTextureMS = false;
            warpDesc.clearBuffer = true;
            warpDesc.clearColor = Color.clear;
            warpDesc.filterMode = FilterMode.Bilinear;
            warpDesc.wrapMode = TextureWrapMode.Clamp;
            TextureHandle warpTex = renderGraph.CreateTexture(warpDesc);

            using (var builder = renderGraph.AddRasterRenderPass<WarpData>("Meerkat tube warp", out var data, profilingSampler))
            {
                data.material = s_warp;
                data.mesh = warp ? warpMesh : null;
                builder.SetRenderAttachment(warpTex, 0, AccessFlags.Write);
                builder.SetGlobalTextureAfterPass(warpTex, WarpTexId);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((WarpData d, RasterGraphContext ctx) =>
                {
                    if (d.mesh != null) ctx.cmd.DrawMesh(d.mesh, Matrix4x4.identity, d.material, 0, 0);
                });
            }

            // 2. The scene as drawn so far, resolved, single-sample.
            using (var builder = renderGraph.AddRasterRenderPass<CopyData>("Meerkat scene copy", out var data, profilingSampler))
            {
                data.source = colour;
                builder.UseTexture(colour, AccessFlags.Read);
                builder.SetRenderAttachment(copy, 0, AccessFlags.WriteAll);
                builder.SetGlobalTextureAfterPass(copy, SceneColorId);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((CopyData d, RasterGraphContext ctx) =>
                    Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), 0f, false));
            }

            // 3. Everything at once, bending that copy onto the camera colour.
            using (var builder = renderGraph.AddRasterRenderPass<LensData>("Meerkat wormhole lens", out var data, profilingSampler))
            {
                data.material = s_lens;
                System.Array.Copy(s_c, data.c, MaxWells);
                System.Array.Copy(s_p, data.p, MaxWells);
                data.ripple = ripple;
                data.view = new Vector4(cam.aspect, warp ? 1f : 0f, wells, tubes);
                s_tubes.CopyTo(data.tubes);
                builder.UseTexture(copy, AccessFlags.Read);
                builder.UseTexture(warpTex, AccessFlags.Read);
                if (resources.cameraDepthTexture.IsValid())
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                builder.UseAllGlobalTextures(true);
                builder.SetRenderAttachment(colour, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((LensData d, RasterGraphContext ctx) =>
                {
                    d.block.Clear();
                    d.block.SetVectorArray(WellCId, d.c);
                    d.block.SetVectorArray(WellPId, d.p);
                    d.block.SetVector(RippleId, d.ripple);
                    d.block.SetVector(ViewId, d.view);
                    var t = d.tubes;
                    d.block.SetMatrixArray(TubeW2LId, t.W2L);
                    d.block.SetVectorArray(TubeShapeId, t.Shape);
                    d.block.SetVectorArray(TubeWinId, t.Win);
                    d.block.SetVectorArray(TubeBoundId, t.Bound);
                    d.block.SetVector(Field0Id, t.F0);
                    d.block.SetVector(Field1Id, t.F1);
                    d.block.SetVector(Field2Id, t.F2);
                    d.block.SetVector(Field3Id, t.F3);
                    d.block.SetVector(CamPosId, t.CamPos);
                    d.block.SetVector(CamRId, t.CamR);
                    d.block.SetVector(CamUId, t.CamU);
                    d.block.SetVector(CamFId, t.CamF);
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, 0, MeshTopology.Triangles, 3, 1, d.block);
                });
            }
        }

        /// <summary>
        /// The page's <c>setLensUniforms</c>: each tunnel's two mouths as this camera sees them. c: xy the centre on screen
        /// (screen heights from the centre, y up), z its depth, w 1. p: x 3 for the way in (space bent in) or 4 for the way
        /// out, y the mouth's angular radius, z the dent's strength (dentStrength × the tunnel's fade × the camera's
        /// nearness), w the foreground margin (the mouth's radius).
        /// </summary>
        public static int ScreenWells(Camera cam, Vector4[] c, Vector4[] p)
        {
            int n = 0;
            var worldToCamera = cam.worldToCameraMatrix;
            var eye = cam.transform.position;
            float f = 0.5f / Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f);
            var live = MeerkatWormholeExecutor.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var t = live[i];
                var cfg = t.Config;
                if (!cfg || cfg.DentStrength <= 0f) continue;   // no dent: the pass skips the mouth (lensing off ships at 0)
                for (int end = 0; end < 2 && n < MaxWells; end++)
                {
                    float s = end == 0 ? t.W.S0 : t.W.S1;
                    var at = t.At(s, out _);
                    var view = worldToCamera.MultiplyPoint3x4(at);
                    float depth = -view.z;
                    if (depth <= cam.nearClipPlane + 1f) continue;
                    float rM = t.Radius(s), D = Vector3.Distance(eye, at);
                    float nearA = MeerkatWormholeMath.Smooth(rM * 0.6f, rM * MeerkatWormholeMath.DentNear, D);
                    c[n] = new Vector4(f * view.x / depth, f * view.y / depth, depth, 1f);
                    p[n] = new Vector4(end == 0 ? 3f : 4f, BlackHoleLens.Angular(f, rM, D),
                        Mathf.Min(0.95f, Mathf.Max(0f, cfg.DentStrength)) * t.Fade * nearA, rM);
                    n++;
                }
            }
            for (int i = n; i < MaxWells; i++) c[i] = p[i] = Vector4.zero;
            return n;
        }

        /// <summary>
        /// The contour field's bank for this camera: the <see cref="MaxTubes"/> formed tunnels nearest it whose config shows
        /// the contour field, each as its world-to-own-frame matrix (<see cref="MeerkatWormholeMath.TubeWorldToLocal"/>), its
        /// shape, its live window (so the field forms out of the centre and collapses with the tunnel) and fade, and a bounding
        /// sphere the shader's rays skip to. The field's numbers come from the nearest tunnel's config. Returns how many.
        /// </summary>
        public static int ContourTubesFor(Camera cam, ContourTubes bank)
        {
            var eye = cam.transform.position;
            s_near.Clear(); s_nearD.Clear();
            var live = MeerkatWormholeExecutor.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var t = live[i];
                var cfg = t.Config;
                if (!cfg || !cfg.ContourFieldOn || t.W.S1 - t.W.S0 < 1f || t.Fade <= 0.004f) continue;
                float d = Vector3.Distance(eye, t.At(0.5f * (t.W.S0 + t.W.S1), out _));
                int at = s_nearD.Count;
                while (at > 0 && s_nearD[at - 1] > d) at--;
                if (at >= MaxTubes) continue;
                s_near.Insert(at, t); s_nearD.Insert(at, d);
                if (s_near.Count > MaxTubes) { s_near.RemoveAt(MaxTubes); s_nearD.RemoveAt(MaxTubes); }
            }
            int n = s_near.Count;
            bank.Count = n;
            for (int i = 0; i < MaxTubes; i++)
            {
                if (i >= n) { bank.W2L[i] = Matrix4x4.identity; bank.Shape[i] = bank.Win[i] = bank.Bound[i] = Vector4.zero; continue; }
                var t = s_near[i];
                float outer = Mathf.Max(0f, t.Config.FieldOuter);
                var mid = t.At(0.5f * (t.W.S0 + t.W.S1), out _);
                bank.W2L[i] = MeerkatWormholeMath.TubeWorldToLocal(t.A, t.T0, t.Side);
                bank.Shape[i] = new Vector4(t.S, t.R, t.Theta, t.B);
                bank.Win[i] = new Vector4(t.W.S0, t.W.S1, t.Fade, t.M);
                bank.Bound[i] = new Vector4(mid.x, mid.y, mid.z, 0.5f * (t.W.S1 - t.W.S0) + Mathf.Max(t.B, t.M) * (1f + outer) + 4f);
            }
            if (n == 0) return 0;
            var c = s_near[0].Config;
            bank.F0 = new Vector4(c.FieldStrength * Mathf.Deg2Rad, c.FieldPitch * Mathf.Deg2Rad, c.FieldDrag, c.FieldTwist);
            bank.F1 = new Vector4(c.FieldGlass, c.FieldGlassIndex, c.FieldGrazing, (float)c.FieldFlow);
            bank.F2 = new Vector4(c.FieldInner, c.FieldOuter, (float)c.FieldProfile, c.FieldRibs);
            bank.F3 = new Vector4(c.FieldRibSharpness, Time.time * c.FieldRibTravel, Mathf.Max(2, c.FieldRibRings), Mathf.Max(1, c.FieldRibLines));
            var ct = cam.transform;
            bank.CamPos = new Vector4(eye.x, eye.y, eye.z, Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f));
            bank.CamR = ct.right; bank.CamU = ct.up; bank.CamF = ct.forward;
            return n;
        }

        /// <summary>The page's <c>rippleUniform</c>: the strongest live ripple as this camera sees it (x strength, y its
        /// front, z where it starts).</summary>
        public static Vector4 RippleFor(Camera cam)
        {
            float best = 0f, front = 0f, edge = 0f;
            var ripples = MeerkatWormholeExecutor.Ripples;
            for (int i = 0; i < ripples.Count; i++)
            {
                var r = ripples[i];
                if (!r.Config) continue;
                float u = (Time.time - r.Start) / Mathf.Max(0.2f, r.Config.RippleSeconds);
                float a = MeerkatWormholeMath.RippleAmplitude(r.Config.RippleStrength, u,
                    Vector3.Distance(cam.transform.position, r.Centre));
                if (a <= best) continue;
                best = a;
                edge = MeerkatWormholeMath.Clamp01(r.Config.RippleEdge);
                front = MeerkatWormholeMath.RippleFront(edge, u);
            }
            return new Vector4(best, front, edge, 0f);
        }

        static bool Resolve()
        {
            if (s_resolved) return s_lens && s_warp;
            s_resolved = true;
            s_lens = Resources.Load<Material>(LensMaterialPath);
            s_warp = Resources.Load<Material>(WarpMaterialPath);
            if (!BlackHoleLens.IsDrawable(s_lens, out string reason) || !BlackHoleLens.IsDrawable(s_warp, out reason))
            {
                CSDebug.LogWarning($"[Meerkat] The wormhole lens cannot draw: {reason}. Tunnels show their bridge, not bent space.");
                s_lens = s_warp = null;
            }
            return s_lens && s_warp;
        }

        /// <summary>Once a frame before any camera renders: hold every game camera's depth on while anything is live.</summary>
        static void OnBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            using var _ = s_marker.Auto();
            bool live = AnythingLive && Application.isPlaying;
            if (live && !s_holdingCameras)
            {
                s_holdingCameras = true;
                BlackHoleLens.CameraSupport.Acquire();
            }
            else if (!live && s_holdingCameras)
            {
                s_holdingCameras = false;
                BlackHoleLens.CameraSupport.Release();
            }
            if (s_holdingCameras) BlackHoleLens.CameraSupport.Maintain();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            s_resolved = false;
            s_lens = s_warp = null;
            s_holdingCameras = false;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
            RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
        }
    }
}

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
    /// that pixel shows. The four tubes nearest the camera take part (<see cref="MaxTubes"/>).
    /// <b>Round 25</b> (Yash, 2026-10-28: "the network of wormholes ... like our tube, same visuals"): a channel of an
    /// omnicrystal's network is a tube too. It joins the same bank as a 3D polyline (<see cref="PathPoints"/> points at even
    /// steps of s, each its radius, and the channel's carried normal), flagged by <c>Shape.y</c> 1, so the water pipe bends
    /// space round the network exactly as round a tunnel; the nearest tubes are taken, tunnels and channels alike.
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
        /// <summary>Points along each tube's centreline the shader measures it by (round 22: the arc, at even steps of s).</summary>
        public const int PathPoints = 25;

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
        static readonly int TubePathId = Shader.PropertyToID("_MWTubePath");
        static readonly int TubeNormId = Shader.PropertyToID("_MWTubeNorm");
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
        /// <summary>One tube the contour field may take: a tunnel, or (round 25) a channel of a network.</summary>
        struct Near
        {
            public MeerkatWormholeExecutor.Tunnel Tunnel;
            public MeerkatRootNetwork Net;
            public int Channel;
            public float D;
            public ScriptableObjects.MeerkatWormholeConfigSO Config => Tunnel != null ? Tunnel.Config : Net.Maker.Config;
        }
        static readonly Near[] s_near = new Near[MaxTubes];
        static int s_nearN;

        /// <summary>The contour field's bank for one camera: up to <see cref="MaxTubes"/> tunnels, the field's numbers, the eye.</summary>
        public sealed class ContourTubes
        {
            public readonly Matrix4x4[] W2L = new Matrix4x4[MaxTubes];
            public readonly Vector4[] Shape = new Vector4[MaxTubes];   // L, 0 a tunnel / 1 a channel (round 25), theta, throat (round 22)
            public readonly Vector4[] Path = new Vector4[MaxTubes * PathPoints];   // per point: across, ahead (its own frame), s, turned; a channel: x, y, z, radius
            public readonly Vector4[] Norm = new Vector4[MaxTubes * PathPoints];   // a channel's carried normal per point (round 25)
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
                System.Array.Copy(Path, o.Path, MaxTubes * PathPoints);
                System.Array.Copy(Norm, o.Norm, MaxTubes * PathPoints);
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

        static bool AnythingLive => MeerkatWormholeExecutor.Live.Count > 0 || MeerkatWormholeExecutor.Ripples.Count > 0 ||
                                    MeerkatRootNetwork.Live.Count > 0;   // round 25: a network bends space as a tunnel does

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
                    d.block.SetVectorArray(TubePathId, t.Path);
                    d.block.SetVectorArray(TubeNormId, t.Norm);
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
            s_nearN = 0;
            var live = MeerkatWormholeExecutor.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var t = live[i];
                var cfg = t.Config;
                if (!cfg || !cfg.ContourFieldOn || t.W.S1 - t.W.S0 < 1f || t.Fade <= 0.004f) continue;
                Offer(new Near { Tunnel = t, D = Vector3.Distance(eye, t.At(0.5f * (t.W.S0 + t.W.S1), out _)) });
            }
            // round 25: every channel of a network is a wormhole tube; its distance is to the nearest point of its dug window
            var nets = MeerkatRootNetwork.Live;
            var eyeD = MeerkatRootNetwork.D(eye);
            for (int n = 0; n < nets.Count; n++)
            {
                var net = nets[n];
                var cfg = net.Maker ? net.Maker.Config : null;
                if (!cfg || !cfg.ContourFieldOn || MeerkatWormholeView.ChannelWrapped(cfg)) continue;
                for (int i = 0; i < net.Channels.Count; i++)
                {
                    ref var w = ref net.Life[i];
                    if (w.Phase == MeerkatRootMath.Phase.Gone || w.S1 - w.S0 < 1 || MeerkatWormholeView.ChannelFade(w) <= 0.004f) continue;
                    var c = net.Channels[i];
                    double s = System.Math.Max(w.S0, System.Math.Min(w.S1, MeerkatRootMath.Nearest(c.Path, eyeD)));
                    var at = MeerkatRootMath.At(c.Path, s, out _);
                    float d = Mathf.Max(0f, (float)at.DistanceTo(eyeD) - (float)MeerkatRootMath.Radius(c, s, cfg.RootTip));
                    Offer(new Near { Net = net, Channel = i, D = d });
                }
            }
            int count = s_nearN;
            bank.Count = count;
            for (int i = 0; i < MaxTubes; i++)
            {
                for (int j = 0; j < PathPoints; j++) bank.Norm[i * PathPoints + j] = Vector4.zero;
                if (i >= count)
                {
                    bank.W2L[i] = Matrix4x4.identity; bank.Shape[i] = bank.Win[i] = bank.Bound[i] = Vector4.zero;
                    for (int j = 0; j < PathPoints; j++) bank.Path[i * PathPoints + j] = Vector4.zero;
                    continue;
                }
                if (s_near[i].Tunnel != null) WriteTunnel(bank, i, s_near[i].Tunnel);
                else WriteChannel(bank, i, s_near[i].Net, s_near[i].Channel);
            }
            if (count == 0) return 0;
            var c0 = s_near[0].Config;
            bank.F0 = new Vector4(c0.FieldStrength * Mathf.Deg2Rad, c0.FieldPitch * Mathf.Deg2Rad, c0.FieldDrag, c0.FieldTwist);
            bank.F1 = new Vector4(c0.FieldGlass, c0.FieldGlassIndex, c0.FieldGrazing, (float)c0.FieldFlow);
            bank.F2 = new Vector4(c0.FieldInner, c0.FieldOuter, (float)c0.FieldProfile, c0.FieldRibs);
            bank.F3 = new Vector4(c0.FieldRibSharpness, Time.time * c0.FieldRibTravel, Mathf.Max(2, c0.FieldRibRings), Mathf.Max(1, c0.FieldRibLines));
            var ct = cam.transform;
            bank.CamPos = new Vector4(eye.x, eye.y, eye.z, Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f));
            bank.CamR = ct.right; bank.CamU = ct.up; bank.CamF = ct.forward;
            return count;
        }

        /// <summary>Keeps the <see cref="MaxTubes"/> nearest tubes, nearest first.</summary>
        static void Offer(in Near c)
        {
            int at = s_nearN;
            while (at > 0 && s_near[at - 1].D > c.D) at--;
            if (at >= MaxTubes) return;
            int last = Mathf.Min(s_nearN, MaxTubes - 1);
            for (int k = last; k > at; k--) s_near[k] = s_near[k - 1];
            s_near[at] = c;
            if (s_nearN < MaxTubes) s_nearN++;
        }

        static void WriteTunnel(ContourTubes bank, int i, MeerkatWormholeExecutor.Tunnel t)
        {
            float outer = Mathf.Max(0f, t.Config.FieldOuter);
            var mid = t.At(0.5f * (t.W.S0 + t.W.S1), out _);
            bank.W2L[i] = MeerkatWormholeMath.TubeWorldToLocal(t.A, t.T0, t.Side);
            bank.Shape[i] = new Vector4(t.L, 0f, t.Theta, t.B);
            for (int j = 0; j < PathPoints; j++)   // round 22: the arc in the tube's own frame (x across, z ahead)
            {
                float sj = t.L * j / (PathPoints - 1);
                t.Path.At(sj, out float ahead, out float across, out float turned);
                bank.Path[i * PathPoints + j] = new Vector4(across, ahead, sj, turned);
            }
            bank.Win[i] = new Vector4(t.W.S0, t.W.S1, t.Fade, t.M);
            bank.Bound[i] = new Vector4(mid.x, mid.y, mid.z, 0.5f * (t.W.S1 - t.W.S0) + Mathf.Max(t.B, t.M) * (1f + outer) + 4f);
        }

        /// <summary>
        /// Round 25: a channel of a network in the bank. Its frame is the world's, moved to its source (so the numbers stay
        /// small); its path is <see cref="PathPoints"/> points at even steps of s, each (x, y, z, its radius), with the
        /// channel's carried normal beside each; its window is its dug window and fade.
        /// </summary>
        static void WriteChannel(ContourTubes bank, int i, MeerkatRootNetwork net, int ch)
        {
            var c = net.Channels[ch];
            var cfg = net.Maker.Config;
            ref var w = ref net.Life[ch];
            var a = MeerkatRootNetwork.V(c.A);
            float L = (float)c.L, outer = Mathf.Max(0f, cfg.FieldOuter), rMax = 0f;
            bank.W2L[i] = Matrix4x4.Translate(-a);
            bank.Shape[i] = new Vector4(L, 1f, 0f, 0f);
            Vector3 lo = new(float.MaxValue, float.MaxValue, float.MaxValue), hi = -lo;
            for (int j = 0; j < PathPoints; j++)
            {
                double sj = c.L * j / (PathPoints - 1);
                var p = MeerkatRootNetwork.V(MeerkatRootMath.At(c.Path, sj, out _)) - a;
                MeerkatRootMath.Frame(c.Path, sj, out var nd, out _);
                float r = (float)MeerkatRootMath.Radius(c, sj, cfg.RootTip);
                bank.Path[i * PathPoints + j] = new Vector4(p.x, p.y, p.z, r);
                var nn = MeerkatRootNetwork.V(nd);
                bank.Norm[i * PathPoints + j] = new Vector4(nn.x, nn.y, nn.z, 0f);
                if (sj < w.S0 - 1e-3 || sj > w.S1 + 1e-3) continue;
                lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); rMax = Mathf.Max(rMax, r);
            }
            if (lo.x > hi.x) { lo = hi = MeerkatRootNetwork.V(MeerkatRootMath.At(c.Path, 0.5 * (w.S0 + w.S1), out _)) - a; rMax = (float)c.M; }
            var mid = a + 0.5f * (lo + hi);
            bank.Win[i] = new Vector4((float)w.S0, (float)w.S1, MeerkatWormholeView.ChannelFade(w), 0f);
            bank.Bound[i] = new Vector4(mid.x, mid.y, mid.z, 0.5f * (hi - lo).magnitude + rMax * (1f + outer) + 4f);
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

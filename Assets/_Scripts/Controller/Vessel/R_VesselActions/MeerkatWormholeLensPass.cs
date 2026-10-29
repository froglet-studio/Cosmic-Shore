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
    /// THE wormhole look (Yash, 2026-10-29: "I want to feel I'm inside the wormhole ... pleasant calm distortion, little jelly
    /// like clear liquidy hollow ... same will be for the root"): the bent space everyone sees round a Meerkat's tunnels (the
    /// TRUNK) and an omnicrystal's network (the ROOT) (<c>R_VesselActions/MEERKAT.md</c>, <c>Docs/Shaders/WORMHOLE_SHADERS.md</c>
    /// §14), the sibling of <see cref="BlackHoleLensPass"/> and <see cref="VortexRingLensPass"/> with the same plumbing. After
    /// URP has drawn the TRANSPARENTS the camera's colour is copied and drawn back through one full-screen triangle
    /// (<c>MeerkatWormholeLens.shader</c>): each pixel's ray is marched through a thin shell on the walls of the
    /// <see cref="MaxTubes"/> tubes nearest the camera, bent along them and refracted a little at the wall (a clear jelly), the
    /// bend breathing slowly; a clear rim, a glint, a faint glow, and (the root by default) a shimmer and soft pulses.
    /// Each kind draws with its own <see cref="MeerkatWormholeLook"/>; a tube the camera is inside draws with its
    /// <see cref="MeerkatWormholeRideView"/> (calmer, a still centre); <see cref="MeerkatWormholeComfort"/> scales every motion
    /// and caps its speed. Tunnels are <c>Shape.y</c> 0; network channels 1, a 3D polyline (<see cref="PathPoints"/> points at
    /// even steps of s, each its radius, and the channel's carried normal).
    /// Injected from script on every base game camera while a tunnel or a network is live (URP's "inject a pass via
    /// scripting" route); a scene with none pays nothing, nor one whose tubes have nothing to draw. The camera depth texture (a
    /// vessel in front of a tube is not bent) is held on through <see cref="BlackHoleLens.CameraSupport"/> while anything is live.
    /// </summary>
    public sealed class MeerkatWormholeLensPass : ScriptableRenderPass
    {
        public const string LensMaterialPath = "MeerkatWormholeLens";
        /// <summary>The most tunnels one pass marches the contour field through (the nearest to the camera).</summary>
        public const int MaxTubes = 4;
        /// <summary>Points along each tube's centreline the shader measures it by (round 22: the arc, at even steps of s).</summary>
        public const int PathPoints = 25;

        static readonly int SceneColorId = Shader.PropertyToID("_MeerkatSceneColor");
        static readonly int ViewId = Shader.PropertyToID("_MWView");
        static readonly int TubeW2LId = Shader.PropertyToID("_MWTubeW2L");
        static readonly int TubeShapeId = Shader.PropertyToID("_MWTubeShape");
        static readonly int TubeWinId = Shader.PropertyToID("_MWTubeWin");
        static readonly int TubeBoundId = Shader.PropertyToID("_MWTubeBound");
        static readonly int TubePathId = Shader.PropertyToID("_MWTubePath");
        static readonly int TubeNormId = Shader.PropertyToID("_MWTubeNorm");
        static readonly int LookId = Shader.PropertyToID("_MWLook");
        static readonly int CamPosId = Shader.PropertyToID("_MWCamPos");
        static readonly int CamRId = Shader.PropertyToID("_MWCamR");
        static readonly int CamUId = Shader.PropertyToID("_MWCamU");
        static readonly int CamFId = Shader.PropertyToID("_MWCamF");
        static readonly int LightDirId = Shader.PropertyToID("_MWLightDir");

        static MeerkatWormholeLensPass s_pass;
        static Material s_lens;
        static bool s_resolved, s_holdingCameras;
        static readonly ProfilerMarker s_marker = new("MeerkatWormholeLens.Maintain");

        static readonly ContourTubes s_tubes = new();
        /// <summary>One tube the contour field may take: a tunnel, or (round 26) a channel of a network.</summary>
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

        // The looks' running phases (wobble, pulse, shimmer per kind), advanced once a frame at their comfort-capped speeds.
        static readonly float[] s_phase = new float[6];
        static int s_phaseFrame = -1;

        /// <summary>The bank for one camera: up to <see cref="MaxTubes"/> tubes, both kinds' looks, the eye.</summary>
        public sealed class ContourTubes
        {
            public readonly Matrix4x4[] W2L = new Matrix4x4[MaxTubes];
            public readonly Vector4[] Shape = new Vector4[MaxTubes];   // L, 0 a tunnel / 1 a channel, the eye inside it (0..1), throat
            public readonly Vector4[] Path = new Vector4[MaxTubes * PathPoints];   // per point: across, ahead (its own frame), s, turned; a channel: x, y, z, radius
            public readonly Vector4[] Norm = new Vector4[MaxTubes * PathPoints];   // a channel's carried normal per point (round 26)
            public readonly Vector4[] Win = new Vector4[MaxTubes];     // s0, s1, fade, mouth
            public readonly Vector4[] Bound = new Vector4[MaxTubes];   // world centre, radius
            public readonly Vector4[] Look = new Vector4[10];          // per kind (trunk 0..4, root 5..9): see PackLook
            public Vector4 CamPos, CamR, CamU, CamF;
            /// <summary>How far the eye is inside a tube (0..1), and that tube's kind's steady centre.</summary>
            public float Inside, Steady;
            public int Count;

            public void CopyTo(ContourTubes o)
            {
                System.Array.Copy(W2L, o.W2L, MaxTubes);
                System.Array.Copy(Shape, o.Shape, MaxTubes);
                System.Array.Copy(Win, o.Win, MaxTubes);
                System.Array.Copy(Bound, o.Bound, MaxTubes);
                System.Array.Copy(Path, o.Path, MaxTubes * PathPoints);
                System.Array.Copy(Norm, o.Norm, MaxTubes * PathPoints);
                System.Array.Copy(Look, o.Look, Look.Length);
                o.Inside = Inside; o.Steady = Steady;
                o.CamPos = CamPos; o.CamR = CamR; o.CamU = CamU; o.CamF = CamF;
                o.Count = Count;
            }
        }

        class CopyData { public TextureHandle source; }

        class LensData
        {
            public Material material;
            public readonly MaterialPropertyBlock block = new();
            public Vector4 view;
            public Vector4 light;
            public readonly ContourTubes tubes = new();
        }

        MeerkatWormholeLensPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            requiresIntermediateTexture = true;
            profilingSampler = new ProfilingSampler("MeerkatWormholeLens");
        }

        static bool AnythingLive => MeerkatWormholeExecutor.Live.Count > 0 ||
                                    MeerkatRootNetwork.Live.Count > 0;   // round 26: a network bends space as a tunnel does

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
            int tubes = ContourTubesFor(cam, s_tubes);
            if (tubes == 0) return;
            TextureHandle colour = resources.activeColorTexture;

            var desc = renderGraph.GetTextureDesc(colour);
            desc.name = "_MeerkatSceneColor";
            desc.msaaSamples = MSAASamples.None;
            desc.bindTextureMS = false;
            desc.clearBuffer = false;
            desc.filterMode = FilterMode.Bilinear;
            desc.wrapMode = TextureWrapMode.Clamp;
            TextureHandle copy = renderGraph.CreateTexture(desc);

            // 1. The scene as drawn so far, resolved, single-sample.
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

            // 2. The look, bending that copy onto the camera colour.
            using (var builder = renderGraph.AddRasterRenderPass<LensData>("Meerkat wormhole lens", out var data, profilingSampler))
            {
                data.material = s_lens;
                data.light = MeerkatWormholeLookTuning.LightDir();
                data.view = new Vector4(cam.aspect, s_tubes.Inside, s_tubes.Steady, tubes);
                s_tubes.CopyTo(data.tubes);
                builder.UseTexture(copy, AccessFlags.Read);
                if (resources.cameraDepthTexture.IsValid())
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                builder.UseAllGlobalTextures(true);
                builder.SetRenderAttachment(colour, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((LensData d, RasterGraphContext ctx) =>
                {
                    d.block.Clear();
                    d.block.SetVector(ViewId, d.view);
                    var t = d.tubes;
                    d.block.SetMatrixArray(TubeW2LId, t.W2L);
                    d.block.SetVectorArray(TubeShapeId, t.Shape);
                    d.block.SetVectorArray(TubeWinId, t.Win);
                    d.block.SetVectorArray(TubeBoundId, t.Bound);
                    d.block.SetVectorArray(TubePathId, t.Path);
                    d.block.SetVectorArray(TubeNormId, t.Norm);
                    d.block.SetVectorArray(LookId, t.Look);
                    d.block.SetVector(CamPosId, t.CamPos);
                    d.block.SetVector(CamRId, t.CamR);
                    d.block.SetVector(CamUId, t.CamU);
                    d.block.SetVector(CamFId, t.CamF);
                    d.block.SetVector(LightDirId, d.light);
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, 0, MeshTopology.Triangles, 3, 1, d.block);
                });
            }
        }

        /// <summary>
        /// The bank for this camera: the <see cref="MaxTubes"/> formed tunnels and network channels nearest it whose look has
        /// anything to draw, each as its world-to-own-frame matrix (<see cref="MeerkatWormholeMath.TubeWorldToLocal"/>), its
        /// shape and how far the eye is inside it, its live window (so it forms out of the centre and collapses with it) and
        /// fade, and a bounding sphere the shader's rays skip to; and both kinds' looks, from the nearest tube's config (the
        /// lab's live copies while it tunes). Returns how many.
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
                if (!cfg || !Draws(cfg, false) || t.W.S1 - t.W.S0 < 1f || t.Fade <= 0.004f) continue;
                Offer(new Near { Tunnel = t, D = Vector3.Distance(eye, t.At(0.5f * (t.W.S0 + t.W.S1), out _)) });
            }
            // every channel of a network is a wormhole tube; its distance is to the nearest point of its dug window
            var nets = MeerkatRootNetwork.Live;
            var eyeD = MeerkatRootNetwork.D(eye);
            for (int n = 0; n < nets.Count; n++)
            {
                var net = nets[n];
                var cfg = net.Maker ? net.Maker.Config : null;
                if (!cfg || !Draws(cfg, true)) continue;
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
            bank.Inside = 0f;
            bank.Steady = 0f;
            for (int i = 0; i < MaxTubes; i++)
            {
                for (int j = 0; j < PathPoints; j++) bank.Norm[i * PathPoints + j] = Vector4.zero;
                if (i >= count)
                {
                    bank.W2L[i] = Matrix4x4.identity; bank.Shape[i] = bank.Win[i] = bank.Bound[i] = Vector4.zero;
                    for (int j = 0; j < PathPoints; j++) bank.Path[i * PathPoints + j] = Vector4.zero;
                    continue;
                }
                bool root = s_near[i].Tunnel == null;
                if (!root) WriteTunnel(bank, i, s_near[i].Tunnel, eye);
                else WriteChannel(bank, i, s_near[i].Net, s_near[i].Channel, eye);
                float inside = bank.Shape[i].z;
                if (inside > bank.Inside)
                {
                    bank.Inside = inside;
                    bank.Steady = MeerkatWormholeLookTuning.RideOf(s_near[i].Config, root).SteadyCentre;
                }
            }
            if (count == 0) return 0;
            var c0 = s_near[0].Config;
            AdvancePhases(c0);
            var comfort = MeerkatWormholeLookTuning.ComfortOf(c0);
            PackLook(bank.Look, 0, MeerkatWormholeLookTuning.LookOf(c0, false), MeerkatWormholeLookTuning.RideOf(c0, false), comfort, 0);
            PackLook(bank.Look, 5, MeerkatWormholeLookTuning.LookOf(c0, true), MeerkatWormholeLookTuning.RideOf(c0, true), comfort, 3);
            var ct = cam.transform;
            bank.CamPos = new Vector4(eye.x, eye.y, eye.z, Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f));
            bank.CamR = ct.right; bank.CamU = ct.up; bank.CamF = ct.forward;
            return count;
        }

        /// <summary>Whether a tube of this kind has anything to draw (bending, or light on its wall).</summary>
        static bool Draws(ScriptableObjects.MeerkatWormholeConfigSO cfg, bool root) =>
            MeerkatWormholeLookTuning.LookOf(cfg, root).Visible(MeerkatWormholeLookTuning.ComfortOf(cfg).DistortionIntensity);

        /// <summary>
        /// One kind's look as the shader reads it (<c>_MWLook[k..k+4]</c>), with the comfort master folded in: bend, lens,
        /// wobble and pulse × Distortion intensity; the phases advance at speeds capped by Comfort (<see cref="AdvancePhases"/>).
        /// </summary>
        static void PackLook(Vector4[] o, int k, in MeerkatWormholeLook l, in MeerkatWormholeRideView r, in MeerkatWormholeComfort c, int ph)
        {
            float I = Mathf.Clamp01(c.DistortionIntensity);
            o[k] = new Vector4(l.Bend * Mathf.Deg2Rad * I, l.Lens * LensScale * I, Mathf.Max(0.02f, l.WallInside), Mathf.Max(0.02f, l.WallOutside));
            o[k + 1] = new Vector4(Mathf.Min(1f, l.Wobble * I), Mathf.Max(0.5f, l.WobbleWaves), s_phase[ph], l.Shimmer);
            o[k + 2] = new Vector4(l.Rim, Mathf.Max(1f, l.RimTightness), l.Glint, l.Glow);
            o[k + 3] = new Vector4(l.Pulse * I, Mathf.Max(1f, l.PulseCount), s_phase[ph + 1], s_phase[ph + 2]);
            o[k + 4] = new Vector4(Mathf.Clamp01(r.InsideBend), Mathf.Clamp01(r.InsideWobble), Mathf.Max(0f, r.SteadyCentre), 0f);
        }

        /// <summary>The jelly lens's refraction per unit of <see cref="MeerkatWormholeLook.Lens"/> (the Tube Lens Lab's glass index).</summary>
        public const float LensScale = 0.05f;

        /// <summary>Once a frame: each kind's wobble, pulse and shimmer phase, at speeds capped by the comfort settings.</summary>
        static void AdvancePhases(ScriptableObjects.MeerkatWormholeConfigSO cfg)
        {
            if (s_phaseFrame == Time.frameCount) return;
            s_phaseFrame = Time.frameCount;
            float dt = Time.deltaTime;
            var c = MeerkatWormholeLookTuning.ComfortOf(cfg);
            float I = Mathf.Clamp01(c.DistortionIntensity);
            for (int kind = 0; kind < 2; kind++)
            {
                var l = MeerkatWormholeLookTuning.LookOf(cfg, kind == 1);
                int b = kind * 3;
                s_phase[b] = Mathf.Repeat(s_phase[b] + dt * Mathf.Min(l.WobbleSpeed, c.MaxWobbleSpeed), 1f);
                s_phase[b + 1] = Mathf.Repeat(s_phase[b + 1] + dt * Mathf.Min(l.PulseSpeed, c.MaxPulseSpeed), 1f);
                s_phase[b + 2] = Mathf.Repeat(s_phase[b + 2] + dt * 0.05f * I, 1f);
            }
        }

        /// <summary>How far <paramref name="eye"/> is inside a tube of radius <paramref name="r"/> at distance
        /// <paramref name="d"/> from its centreline (1 well inside, fading to 0 at the wall).</summary>
        static float InsideAmount(float d, float r) => r <= 1e-3f ? 0f : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f * r, r, d));

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

        static void WriteTunnel(ContourTubes bank, int i, MeerkatWormholeExecutor.Tunnel t, Vector3 eye)
        {
            float outer = Mathf.Max(0f, MeerkatWormholeLookTuning.LookOf(t.Config, false).WallOutside);
            var mid = t.At(0.5f * (t.W.S0 + t.W.S1), out _);
            bank.W2L[i] = MeerkatWormholeMath.TubeWorldToLocal(t.A, t.T0, t.Side);
            float se = MeerkatWormholeMath.NearestAlong(t.A, t.T0, t.Side, t.Path, t.L, eye), inside = 0f;
            if (se >= t.W.S0 && se <= t.W.S1) inside = InsideAmount(Vector3.Distance(eye, t.At(se, out _)), t.Radius(se));
            bank.Shape[i] = new Vector4(t.L, 0f, inside, t.B);
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
        /// A channel of a network in the bank. Its frame is the world's, moved to its source (so the numbers stay
        /// small); its path is <see cref="PathPoints"/> points at even steps of s, each (x, y, z, its radius), with the
        /// channel's carried normal beside each; its window is its dug window and fade.
        /// </summary>
        static void WriteChannel(ContourTubes bank, int i, MeerkatRootNetwork net, int ch, Vector3 eye)
        {
            var c = net.Channels[ch];
            var cfg = net.Maker.Config;
            ref var w = ref net.Life[ch];
            var a = MeerkatRootNetwork.V(c.A);
            float L = (float)c.L, outer = Mathf.Max(0f, MeerkatWormholeLookTuning.LookOf(cfg, true).WallOutside), rMax = 0f;
            bank.W2L[i] = Matrix4x4.Translate(-a);
            var eyeD = MeerkatRootNetwork.D(eye);
            double se = MeerkatRootMath.Nearest(c.Path, eyeD);
            float inside = se >= w.S0 && se <= w.S1
                ? InsideAmount((float)MeerkatRootMath.At(c.Path, se, out _).DistanceTo(eyeD), (float)MeerkatRootMath.Radius(c, se, cfg.RootTip))
                : 0f;
            bank.Shape[i] = new Vector4(L, 1f, inside, 0f);
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

        static bool Resolve()
        {
            if (s_resolved) return s_lens;
            s_resolved = true;
            s_lens = Resources.Load<Material>(LensMaterialPath);
            if (!BlackHoleLens.IsDrawable(s_lens, out string reason))
            {
                CSDebug.LogWarning($"[Meerkat] The wormhole lens cannot draw: {reason}. Tunnels are not shown.");
                s_lens = null;
            }
            return s_lens;
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
            s_lens = null;
            s_holdingCameras = false;
            s_phaseFrame = -1;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
            RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
        }
    }
}

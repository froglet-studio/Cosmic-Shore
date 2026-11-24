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
    /// THE Soliton look, a plain wormhole (Yash, 2026-11-01: "plain worm hole with wider mouth looking cool from outside and
    /// inside, bending and warping of space time"): the bent space everyone sees round every Soliton Tunnel and every Soliton Network channel,
    /// made by a vessel or placed (<c>Docs/Shaders/WORMHOLE_SHADERS.md</c> §14), the sibling of <see cref="BlackHoleLensPass"/> and <see cref="VortexRingLensPass"/> with the same plumbing. After
    /// URP has drawn the TRANSPARENTS the camera's colour is copied and drawn back through one full-screen triangle
    /// (<c>SolitonTunnel.shader</c>): each pixel's ray is marched through a thin shell on the walls of the
    /// <see cref="MaxTubes"/> tubes nearest the camera, bent along them and refracted a little at the wall (a clear jelly), the
    /// bend breathing slowly; a clear rim, a glint, a faint glow, and its config's domain tint (<c>_MWTubeTint</c>). Each live
    /// mouth of those tubes (<see cref="ScreenWells"/>, up to <see cref="MaxWells"/>) is a gravitational lens with an Einstein
    /// ring; riding a tube, its throat stretches and streams toward the way out (<c>_MWHole</c>). Each tube draws with its own config's <see cref="SolitonTunnelLook"/>; a tube the camera is inside draws with its
    /// <see cref="SolitonTunnelRideView"/> (calmer, a still centre); <see cref="SolitonTunnelComfort"/> scales every motion
    /// and caps its speed. Tunnels are <c>Shape.y</c> 0; network channels 1, a 3D polyline (<see cref="PathPoints"/> points at
    /// even steps of s, each its radius, and the channel's carried normal).
    /// Injected from script on every base game camera while a tunnel or a network is live (URP's "inject a pass via
    /// scripting" route); a scene with none pays nothing, nor one whose tubes have nothing to draw. The camera depth texture (a
    /// vessel in front of a tube is not bent) is held on through <see cref="BlackHoleLens.CameraSupport"/> while anything is live.
    /// </summary>
    public sealed class SolitonTunnelLensPass : ScriptableRenderPass
    {
        public const string LensMaterialPath = "SolitonTunnel";
        /// <summary>The most tunnels one pass marches the contour field through (the nearest to the camera).</summary>
        public const int MaxTubes = 4;
        /// <summary>Points along each tube's centreline the shader measures it by (round 22: the arc, at even steps of s).</summary>
        public const int PathPoints = 25;
        /// <summary>The most mouths lensed at once (both ends of each banked tube).</summary>
        public const int MaxWells = 8;

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
        static readonly int TubeTintId = Shader.PropertyToID("_MWTubeTint");
        static readonly int WellCId = Shader.PropertyToID("_MWWellC");
        static readonly int WellPId = Shader.PropertyToID("_MWWellP");
        static readonly int WellTId = Shader.PropertyToID("_MWWellT");
        static readonly int HoleId = Shader.PropertyToID("_MWHole");
        static readonly int WellFId = Shader.PropertyToID("_MWWellF");
        static readonly int ThroatId = Shader.PropertyToID("_MWThroat");
        static readonly int Throat2Id = Shader.PropertyToID("_MWThroat2");
        static readonly int Throat3Id = Shader.PropertyToID("_MWThroat3");
        static readonly int ThroatTintId = Shader.PropertyToID("_MWThroatTint");
        static readonly int RingCId = Shader.PropertyToID("_MWRingC");
        static readonly int RingSId = Shader.PropertyToID("_MWRingS");
        static readonly int HullId = Shader.PropertyToID("_MWHull");

        /// <summary>Rings of the ridden tube's wall the shader finds each pixel's place between (the eye to the tube's end).</summary>
        public const int Rings = 16;
        /// <summary>The most riders kept in sight inside the tube a camera rides.</summary>
        public const int MaxHulls = 4;

        /// <summary>The ride eases in and out of the throat over this long (s): no pop as the eye crosses a mouth.</summary>
        public const float RideEaseSeconds = 0.25f;
        /// <summary>The ripple thrown on leaving a tube lasts this long (s).</summary>
        public const float PopSeconds = 0.45f;

        static SolitonTunnelLensPass s_pass;
        static Material s_lens;

        /// <summary>
        /// The march-free wall bend (<c>_MW_STANDIN</c>, SolitonTunnelStandin.hlsl, Docs/UNITY_STANDINS.md): 3-14x cheaper per
        /// pixel than the shell march, a close but approximate bend (mean 5-9 px off at 1280x720, the rims softest). On by
        /// default on phones only, where the march is the cost that matters; any quality setting can flip it. Since
        /// 2026-11-20 it stays on while riding too (the name is from when it could not): the tube the eye is inside draws no
        /// wall in either variant (the throat draws it, <see cref="Throat"/>), and every other tube is seen from outside.
        /// </summary>
        public static bool MarchFreeWhenOutside = Application.isMobilePlatform;
        static bool s_resolved, s_holdingCameras;
        static readonly ProfilerMarker s_marker = new("SolitonTunnel.Maintain");

        static readonly ContourTubes s_tubes = new();
        /// <summary>One tube the contour field may take: a tunnel, or (round 26) a channel of a network.</summary>
        struct Near
        {
            public SolitonTunnel Tunnel;
            public SolitonNetwork Net;
            public int Channel;
            public float D;
            public SolitonTunnelConfigSO Config => Tunnel != null ? Tunnel.Config : Net.Config;
            public Color Tint => Tunnel != null ? Tunnel.Tint : Net.Tint;
        }
        static readonly Near[] s_near = new Near[MaxTubes];
        static int s_nearN;

        // The looks' running phases (wobble, throat flow per kind), advanced once a frame at their comfort-capped speeds.
        static readonly float[] s_phase = new float[4];
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
            public readonly Vector4[] Look = new Vector4[10];          // per kind (tunnel 0..4, network 5..9): see PackLook
            public readonly Vector4[] Tint = new Vector4[MaxTubes];    // rgb the domain tint, a its strength (0 = clear)
            public readonly Vector4[] WellC = new Vector4[MaxWells];   // a mouth on screen: x, y (screen heights), depth, radius
            public readonly Vector4[] WellP = new Vector4[MaxWells];   // its Einstein angle, lens strength, ring, shadow
            public readonly Vector4[] WellT = new Vector4[MaxWells];   // its tube's tint
            public readonly Vector4[] WellF = new Vector4[MaxWells];   // its tube's other mouth on screen (x, y), the far view's amount
            public Vector4 Hole;                                       // mouths, throat stretch, flow phase, twist
            public Vector4 Throat, Throat2, Throat3, ThroatTint;       // riding: see Throat()
            public readonly Vector4[] RingC = new Vector4[Rings];      // riding: a ring of the wall on screen (x, y, its radius)
            public readonly Vector4[] RingS = new Vector4[Rings];      // ...how far along the tube, how far ahead, its frame's normal on screen
            public readonly Vector4[] Hull = new Vector4[MaxHulls];    // a rider in the tube on screen (x, y, its radius, its depth)
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
                System.Array.Copy(Tint, o.Tint, MaxTubes);
                System.Array.Copy(WellC, o.WellC, MaxWells);
                System.Array.Copy(WellP, o.WellP, MaxWells);
                System.Array.Copy(WellT, o.WellT, MaxWells);
                System.Array.Copy(WellF, o.WellF, MaxWells);
                o.Hole = Hole;
                o.Throat = Throat; o.Throat2 = Throat2; o.Throat3 = Throat3; o.ThroatTint = ThroatTint;
                System.Array.Copy(RingC, o.RingC, Rings);
                System.Array.Copy(RingS, o.RingS, Rings);
                System.Array.Copy(Hull, o.Hull, MaxHulls);
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
            public bool standin;
            public readonly ContourTubes tubes = new();
        }

        /// <summary>Turns the march-free keyword on or off on the lens material; returns whether it is on.</summary>
        static bool SetStandin(bool on)
        {
            // A LocalKeyword is bound to the shader's keyword space at the moment it is made. Editing or reimporting the
            // lens shader while playing replaces that space on the SAME Shader object, and a keyword cached from the old
            // one throws "Local keyword _MW_STANDIN comes from a different shader" (Bug Ledger E-8ddade9b04). The lookup
            // is one native name search per camera per frame, so it is made fresh from the material's current shader.
            s_lens.SetKeyword(new LocalKeyword(s_lens.shader, "_MW_STANDIN"), on);
            return on;
        }

        SolitonTunnelLensPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            requiresIntermediateTexture = true;
            profilingSampler = new ProfilingSampler("SolitonTunnel");
        }

        static bool AnythingLive => SolitonTunnel.Live.Count > 0 ||
                                    SolitonNetwork.Live.Count > 0;   // round 26: a network bends space as a tunnel does

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
            s_pass ??= new SolitonTunnelLensPass();
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
            using (var builder = renderGraph.AddRasterRenderPass<LensData>("Soliton Tunnel lens", out var data, profilingSampler))
            {
                data.material = s_lens;
                data.light = LightDir();
                data.view = new Vector4(cam.aspect, s_tubes.Inside, s_tubes.Steady, tubes);
                s_tubes.CopyTo(data.tubes);
                data.standin = SetStandin(MarchFreeWhenOutside);   // riding too: the ridden tube draws no wall (the throat)
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
                    d.block.SetVectorArray(TubeTintId, t.Tint);
                    d.block.SetVectorArray(WellCId, t.WellC);
                    d.block.SetVectorArray(WellPId, t.WellP);
                    d.block.SetVectorArray(WellTId, t.WellT);
                    d.block.SetVector(HoleId, t.Hole);
                    d.block.SetVectorArray(WellFId, t.WellF);
                    d.block.SetVector(ThroatId, t.Throat);
                    d.block.SetVector(Throat2Id, t.Throat2);
                    d.block.SetVector(Throat3Id, t.Throat3);
                    d.block.SetVector(ThroatTintId, t.ThroatTint);
                    d.block.SetVectorArray(RingCId, t.RingC);
                    d.block.SetVectorArray(RingSId, t.RingS);
                    d.block.SetVectorArray(HullId, t.Hull);
                    if (d.standin)   // the shell LUT from the tunnel look's wall widths and rim tightness (_MWLook[0].zw, [2].y)
                        d.block.SetTexture(SolitonTunnelShellLut.LutId,
                            SolitonTunnelShellLut.Get(t.Look[0].z, t.Look[0].w, t.Look[2].y));
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, 0, MeshTopology.Triangles, 3, 1, d.block);
                });
            }
        }

        /// <summary>
        /// The bank for this camera: the <see cref="MaxTubes"/> formed tunnels and network channels nearest it whose look has
        /// anything to draw, each as its world-to-own-frame matrix (<see cref="SolitonTunnelShapeMath.TubeWorldToLocal"/>), its
        /// shape and how far the eye is inside it, its live window (so it forms out of the centre and collapses with it) and
        /// fade, its tint, and a bounding sphere the shader's rays skip to; and both kinds' looks, each from the nearest tube of
        /// that kind's config (the comfort master from the nearest tube's). Returns how many.
        /// </summary>
        public static int ContourTubesFor(Camera cam, ContourTubes bank)
        {
            var eye = cam.transform.position;
            s_nearN = 0;
            var live = SolitonTunnel.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var t = live[i];
                var cfg = t.Config;
                if (!cfg || !Draws(cfg) || t.W.S1 - t.W.S0 < 1f || t.Fade <= 0.004f) continue;
                Offer(new Near { Tunnel = t, D = Vector3.Distance(eye, t.At(0.5f * (t.W.S0 + t.W.S1), out _)) });
            }
            // every channel of a network is a tube; its distance is to the nearest point of its dug window
            var nets = SolitonNetwork.Live;
            var eyeD = SolitonNetwork.D(eye);
            for (int n = 0; n < nets.Count; n++)
            {
                var net = nets[n];
                var cfg = net.Config;
                if (!cfg || !Draws(cfg)) continue;
                for (int i = 0; i < net.Channels.Count; i++)
                {
                    ref var w = ref net.Life[i];
                    if (w.Phase == SolitonTunnelMath.Phase.Gone || w.S1 - w.S0 < 1 || SolitonTunnelView.ChannelFade(w) <= 0.004f) continue;
                    var c = net.Channels[i];
                    double s = System.Math.Max(w.S0, System.Math.Min(w.S1, SolitonTunnelMath.Nearest(c.Path, eyeD)));
                    var at = SolitonTunnelMath.At(c.Path, s, out _);
                    float d = Mathf.Max(0f, (float)at.DistanceTo(eyeD) - (float)SolitonTunnelMath.Radius(c, s, cfg.NetworkTip));
                    Offer(new Near { Net = net, Channel = i, D = d });
                }
            }
            int count = s_nearN;
            bank.Count = count;
            bank.Inside = 0f;
            bank.Steady = 0f;
            int insideAt = -1;
            for (int i = 0; i < MaxTubes; i++)
            {
                for (int j = 0; j < PathPoints; j++) bank.Norm[i * PathPoints + j] = Vector4.zero;
                if (i >= count)
                {
                    bank.W2L[i] = Matrix4x4.identity; bank.Shape[i] = bank.Win[i] = bank.Bound[i] = bank.Tint[i] = Vector4.zero;
                    for (int j = 0; j < PathPoints; j++) bank.Path[i * PathPoints + j] = Vector4.zero;
                    continue;
                }
                if (s_near[i].Tunnel != null) WriteTunnel(bank, i, s_near[i].Tunnel, eye);
                else WriteChannel(bank, i, s_near[i].Net, s_near[i].Channel, eye);
                var tint = s_near[i].Tint;
                bank.Tint[i] = new Vector4(tint.r, tint.g, tint.b, Mathf.Clamp01(tint.a));
                float inside = bank.Shape[i].z;
                if (inside > bank.Inside)
                {
                    bank.Inside = inside;
                    bank.Steady = s_near[i].Config.Ride.SteadyCentre;
                    insideAt = i;
                }
            }
            if (count == 0) return 0;
            var c0 = s_near[0].Config;
            SolitonTunnelConfigSO tunnelCfg = null, networkCfg = null;
            for (int i = 0; i < count; i++)
            {
                if (s_near[i].Tunnel != null) { if (!tunnelCfg) tunnelCfg = s_near[i].Config; }
                else if (!networkCfg) networkCfg = s_near[i].Config;
            }
            if (!tunnelCfg) tunnelCfg = c0;
            if (!networkCfg) networkCfg = c0;
            var comfort = c0.Comfort;
            AdvancePhases(tunnelCfg.Look, networkCfg.Look, comfort);
            PackLook(bank.Look, 0, tunnelCfg.Look, tunnelCfg.Ride, comfort, 0);
            PackLook(bank.Look, 5, networkCfg.Look, networkCfg.Ride, comfort, 2);
            var ct = cam.transform;
            bank.CamPos = new Vector4(eye.x, eye.y, eye.z, Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f));
            bank.CamR = ct.right; bank.CamU = ct.up; bank.CamF = ct.forward;
            int wells = ScreenWells(cam, bank);
            float I = Mathf.Clamp01(comfort.DistortionIntensity), flow = 0f;
            if (insideAt >= 0 && bank.Inside > 0f) flow = s_phase[s_near[insideAt].Tunnel != null ? 1 : 3];
            bank.Hole = new Vector4(wells, 0f, flow, 0f);   // z: the inner wall's ripple phase
            Throat(cam, bank, insideAt, I);
            return count;
        }

        /// <summary>One camera's ride through a tube, kept between frames: how far into the throat it has eased and the
        /// ripple thrown since it left.</summary>
        sealed class RideState
        {
            public int Frame = -1;
            public float Amount, Pop = 1f, PopStrength;
            public bool WasInside;
        }

        static readonly Dictionary<int, RideState> s_rides = new();

        /// <summary>
        /// THE THROAT, what a camera inside a tube sees (Yash, 2026-11-22: "the view camera should give view of inside the
        /// worm hole path, players should not be able to see outer world that time, it's like gliding through the inner
        /// surface of the space-time hole, and exiting to the universe again"). Nothing of the outer world is drawn. The
        /// shader draws the tube's own inner wall from <see cref="Rings"/> rings of it: the centreline from just ahead of
        /// the eye to the tube's end, at distances that grow geometrically (<see cref="RingAhead"/>), each as its place and
        /// radius on screen, how far along the tube it is (the wall's grid stands still in the tube) and the tube's frame
        /// there (so the grid does not turn with the camera). A pixel is on the wall between the last ring it is inside
        /// and the first it is outside; a pixel inside them all looks out of the way out. The riders in this tube stay in
        /// sight (<see cref="HullOnScreen"/>). <c>_MWThroat.w</c> is how far into the ride the camera has eased
        /// (<see cref="RideEaseSeconds"/>); for <see cref="PopSeconds"/> after it leaves, the ripple. No allocation after
        /// a camera's first ride.
        /// </summary>
        static void Throat(Camera cam, ContourTubes bank, int insideAt, float intensity)
        {
            int id = cam.GetInstanceID();
            if (!s_rides.TryGetValue(id, out var ride)) s_rides[id] = ride = new RideState();
            bool inside = insideAt >= 0 && bank.Inside > 0.5f;
            if (ride.Frame != Time.frameCount)
            {
                ride.Frame = Time.frameCount;
                float dt = Time.deltaTime;
                ride.Amount = Mathf.MoveTowards(ride.Amount, inside ? 1f : 0f, dt / RideEaseSeconds);
                if (ride.WasInside && !inside) ride.Pop = 0f;   // it just came out: throw the ripple
                else if (ride.Pop < 1f) ride.Pop = Mathf.Min(1f, ride.Pop + dt / PopSeconds);
                ride.WasInside = inside;
            }
            float f = 0.5f / Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f);
            for (int h = 0; h < MaxHulls; h++) bank.Hull[h] = Vector4.zero;
            if (inside)
            {
                var near = s_near[insideAt];
                var look = near.Config.Look;
                var w2c = cam.worldToCameraMatrix;
                var eye = cam.transform.position;
                // where the eye is along the tube, which way it is going, and how far its end is
                float se, s0, s1;
                bool mouthAhead;
                if (near.Tunnel != null)
                {
                    var t = near.Tunnel;
                    se = SolitonTunnelShapeMath.NearestAlong(t.A, t.T0, t.Side, t.Path, t.L, eye);
                    s0 = t.W.S0; s1 = t.W.S1;
                }
                else
                {
                    ref var w = ref near.Net.Life[near.Channel];
                    se = (float)SolitonTunnelMath.Nearest(near.Net.Channels[near.Channel].Path, SolitonNetwork.D(eye));
                    s0 = (float)w.S0; s1 = (float)w.S1;
                }
                se = Mathf.Clamp(se, s0, s1);
                SampleTube(near, se, out _, out float rHere, out _, out var tangent);
                bool forward = Vector3.Dot(cam.transform.forward, tangent) >= 0f;
                float end = forward ? s1 : s0, remaining = Mathf.Abs(end - se);
                if (near.Tunnel != null) mouthAhead = true;
                else
                {
                    var c = near.Net.Channels[near.Channel];
                    mouthAhead = forward ? c.MouthOut && s1 >= c.L - 0.5 : c.MouthIn && s0 <= 0.5;
                }
                float d0 = Mathf.Max(0.5f, 0.3f * rHere);
                for (int k = 0; k < Rings; k++)
                {
                    float d = RingAhead(d0, remaining, k, Rings), s = forward ? se + d : se - d;
                    SampleTube(near, s, out var at, out float r, out var normal, out _);
                    bank.RingC[k] = RingOnScreen(f, w2c.MultiplyPoint3x4(at), r);
                    var nv = w2c.MultiplyVector(normal);
                    float nm = Mathf.Sqrt(nv.x * nv.x + nv.y * nv.y);
                    bank.RingS[k] = nm > 1e-4f ? new Vector4(s, d, nv.x / nm, nv.y / nm) : new Vector4(s, d, 1f, 0f);
                }
                // the riders in this tube stay in sight
                object tube = near.Tunnel != null ? near.Tunnel : near.Net;
                var riders = SolitonIsolation.Occupants;
                float hullRadius = Mathf.Clamp(look.InsideHullRadius, 2f, 20f);
                for (int i = 0, h = 0; i < riders.Count && h < MaxHulls; i++)
                {
                    if (!ReferenceEquals(riders[i].Tube, tube)) continue;
                    var hull = riders[i].Status?.Transform;
                    if (!hull) continue;
                    var on = HullOnScreen(f, w2c.MultiplyPoint3x4(hull.position), hullRadius);
                    if (on.w > 0f) bank.Hull[h++] = on;
                }
                float spacing = Mathf.Clamp(look.InsideGridSpacing, 2f, 40f);
                bank.Throat = new Vector4(Mathf.Clamp(look.InsideFog, 20f, 600f), spacing, Mathf.Round(Mathf.Clamp(look.InsideGridSpokes, 4f, 48f)), ride.Amount);
                bank.Throat2 = new Vector4(Mathf.Clamp01(look.InsideGrid), Mathf.Clamp01(look.InsideGlow), 0f, 0f);
                bank.Throat3 = new Vector4(Mathf.Clamp(look.InsideStretch, 0f, 0.5f) * intensity,
                    Mathf.Clamp(look.InsideSwirl, 0f, 0.5f) * intensity * SwirlPerUnit * (forward ? 1f : -1f), mouthAhead ? 1f : 0f, f);
                bank.ThroatTint = bank.Tint[insideAt];
                ride.PopStrength = Mathf.Clamp(look.ExitPop, 0f, 0.3f) * intensity;
            }
            else
            {
                // out of the tube (the ride easing away): every ring is passed, so the throat is the world itself
                for (int k = 0; k < Rings; k++) { bank.RingC[k] = new Vector4(0f, 0f, WholeView, 0f); bank.RingS[k] = new Vector4(0f, 0f, 1f, 0f); }
                bank.Throat = new Vector4(100f, 10f, 16f, ride.Amount);
                bank.Throat2 = Vector4.zero;
                bank.Throat3 = new Vector4(0f, 0f, 1f, f);
                bank.ThroatTint = Vector4.zero;
            }
            float pop = ride.Pop < 1f ? ride.PopStrength * (1f - ride.Pop) * (1f - ride.Pop) : 0f;
            bank.Throat2.z = pop;
            bank.Throat2.w = 0.12f + 0.9f * ride.Pop;
        }

        /// <summary>A ring's radius on screen (screen heights) once it is at the eye or behind it: it holds the whole view.</summary>
        public const float WholeView = 8f;
        /// <summary>The grid's twist per unit of <see cref="SolitonTunnelLook.InsideSwirl"/> (radians per u ahead).</summary>
        public const float SwirlPerUnit = 0.05f;

        /// <summary>
        /// How far ahead of the eye ring <paramref name="k"/> of <paramref name="n"/> is (u): from <paramref name="d0"/>
        /// (just ahead, so the first ring holds the whole view) to <paramref name="remaining"/> (the tube's end), growing
        /// geometrically, because the wall near the eye fills most of the screen and needs the rings. With less than
        /// <paramref name="d0"/> left, every ring is the end.
        /// </summary>
        public static float RingAhead(float d0, float remaining, int k, int n)
        {
            if (remaining <= d0 || n < 2) return Mathf.Max(0f, remaining);
            return d0 * Mathf.Pow(remaining / d0, Mathf.Clamp01(k / (float)(n - 1)));
        }

        /// <summary>A ring of the wall, centre at <paramref name="view"/> (camera space) and <paramref name="radius"/>
        /// wide, as its place and radius on screen (focal length <paramref name="f"/>, screen heights). At the eye or
        /// behind it, it holds the whole view.</summary>
        public static Vector4 RingOnScreen(float f, Vector3 view, float radius)
        {
            float depth = -view.z;
            if (depth <= 0.05f) return new Vector4(0f, 0f, WholeView, 0f);
            return new Vector4(f * view.x / depth, f * view.y / depth, Mathf.Min(WholeView, f * Mathf.Max(0f, radius) / depth), 0f);
        }

        /// <summary>A rider at <paramref name="view"/> (camera space) as the disc that stays in sight: its place and
        /// radius on screen and its depth (x, y, radius, depth). Zero when it is behind the eye.</summary>
        public static Vector4 HullOnScreen(float f, Vector3 view, float radius)
        {
            float depth = -view.z;
            if (depth <= 0.2f || radius <= 0f) return Vector4.zero;
            return new Vector4(f * view.x / depth, f * view.y / depth, f * radius / depth, depth);
        }

        /// <summary>The banked tube's centreline at <paramref name="s"/>: the point, the wall's radius there, the tube's
        /// frame normal and its heading (world).</summary>
        static void SampleTube(in Near near, float s, out Vector3 at, out float radius, out Vector3 normal, out Vector3 tangent)
        {
            if (near.Tunnel != null)
            {
                var t = near.Tunnel;
                float sc = Mathf.Clamp(s, 0f, t.L);
                at = t.At(sc, out tangent);
                t.Frame(sc, out normal, out _);
                radius = t.Radius(sc);
                return;
            }
            var c = near.Net.Channels[near.Channel];
            double sd = System.Math.Max(0.0, System.Math.Min(c.L, s));
            at = SolitonNetwork.V(SolitonTunnelMath.At(c.Path, sd, out var tan));
            tangent = SolitonNetwork.V(tan);
            SolitonTunnelMath.Frame(c.Path, sd, out var nd, out _);
            normal = SolitonNetwork.V(nd);
            radius = (float)SolitonTunnelMath.Radius(c, sd, near.Config.NetworkTip);
        }

        /// <summary>
        /// Where a mouth's far view is sampled from: its tube's other mouth at view (<paramref name="x"/>,
        /// <paramref name="y"/>, <paramref name="depth"/>) as a place on screen (screen heights). Behind the eye it is
        /// carried to the opposite side (what is behind you is seen at the edge); never further than 1.2 from the middle
        /// (the scene copy mirrors past its edge).
        /// </summary>
        public static Vector2 FarCentre(float f, float x, float y, float depth)
        {
            const float Far = 1.2f;
            float d = Mathf.Max(Mathf.Abs(depth), 0.5f);
            float px = f * x / d, py = f * y / d;
            if (depth < 0f) { px = -px; py = -py; }
            float m = Mathf.Sqrt(px * px + py * py);
            if (depth < 0f && m < 0.6f) { float k = m > 1e-4f ? 0.6f / m : 0f; px = m > 1e-4f ? px * k : 0.6f; py *= k; }
            else if (m > Far) { px *= Far / m; py *= Far / m; }
            return new Vector2(px, py);
        }

        /// <summary>
        /// The mouths of the banked tubes as gravitational lenses (435ce058c's <c>ScreenWells</c>, a point-mass lens now): each
        /// end of a tunnel's live window, and each true opening of a network channel (<c>MouthIn</c> / <c>MouthOut</c>) its
        /// dug window has reached, in front of the camera. Its Einstein angle is the mouth's angular radius × the look's
        /// <see cref="SolitonTunnelLook.MouthFlare"/>; its strength fades with the tube, as the camera comes right up to the
        /// mouth, and (×0.3) for the tube you are riding. No allocations. Returns how many.
        /// </summary>
        static int ScreenWells(Camera cam, ContourTubes bank)
        {
            int n = 0;
            var worldToCamera = cam.worldToCameraMatrix;
            var eye = cam.transform.position;
            float f = 0.5f / Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float I = Mathf.Clamp01(s_near[0].Config.Comfort.DistortionIntensity);
            for (int i = 0; i < bank.Count && n < MaxWells; i++)
            {
                var near = s_near[i];
                var cfg = near.Config;
                var look = cfg.Look;
                if (look.MouthLens <= 0f && look.EinsteinRing <= 0f && look.MouthShadow <= 0f && look.FarView <= 0f) continue;
                if (bank.Shape[i].z > 0.5f) continue;   // the tube you ride: its way ahead is the throat's clear disk, not a lens
                float ride = 1f - 0.7f * bank.Shape[i].z;
                for (int end = 0; end < 2 && n < MaxWells; end++)
                {
                    Vector3 at, other; float rM, fade, farView = Mathf.Clamp01(look.FarView), flare = 1f;
                    if (near.Tunnel != null)
                    {
                        var t = near.Tunnel;
                        float s = end == 0 ? t.W.S0 : t.W.S1;
                        at = t.At(s, out _); rM = t.Radius(s); fade = t.Fade;
                        other = t.At(end == 0 ? t.W.S1 : t.W.S0, out _);
                        // forming: its mouths' rings flare, settling as it opens
                        if (t.W.Phase == SolitonTunnelShapeMath.Phase.Form)
                            flare += Mathf.Max(0f, look.FormFlare) * (1f - Mathf.Clamp01(t.W.Grow));
                    }
                    else
                    {
                        var c = near.Net.Channels[near.Channel];
                        ref var w = ref near.Net.Life[near.Channel];
                        if (end == 0 ? !c.MouthIn || w.S0 > 0.5 : !c.MouthOut || w.S1 < c.L - 0.5) continue;
                        double s = end == 0 ? w.S0 : w.S1;
                        at = SolitonNetwork.V(SolitonTunnelMath.At(c.Path, s, out _));
                        rM = (float)SolitonTunnelMath.Radius(c, s, cfg.NetworkTip); fade = SolitonTunnelView.ChannelFade(w);
                        other = SolitonNetwork.V(SolitonTunnelMath.At(c.Path, end == 0 ? w.S1 : w.S0, out _));
                    }
                    var view = worldToCamera.MultiplyPoint3x4(at);
                    float depth = -view.z;
                    if (depth <= cam.nearClipPlane + 1f || rM <= 1e-3f) continue;
                    float D = Vector3.Distance(eye, at);
                    float k = fade * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f * rM, 2f * rM, D));
                    if (k <= 0.004f) continue;
                    float thE = BlackHoleLens.Angular(f, rM * Mathf.Max(1f, look.MouthFlare), D);
                    bank.WellC[n] = new Vector4(f * view.x / depth, f * view.y / depth, depth, rM);
                    bank.WellP[n] = new Vector4(thE, Mathf.Clamp01(look.MouthLens) * I * k * ride,
                        Mathf.Clamp01(look.EinsteinRing) * k * flare, Mathf.Clamp01(look.MouthShadow) * k * ride);
                    bank.WellT[n] = bank.Tint[i];
                    var farView3 = worldToCamera.MultiplyPoint3x4(other);
                    var far = FarCentre(f, farView3.x, farView3.y, -farView3.z);
                    bank.WellF[n] = new Vector4(far.x, far.y, farView * k * ride, 0f);
                    n++;
                }
            }
            for (int i = n; i < MaxWells; i++) bank.WellC[i] = bank.WellP[i] = bank.WellT[i] = bank.WellF[i] = Vector4.zero;
            return n;
        }

        /// <summary>Whether a tube of this kind has anything to draw (bending, or light on its wall).</summary>
        static bool Draws(SolitonTunnelConfigSO cfg) => cfg.Look.Visible(cfg.Comfort.DistortionIntensity);

        /// <summary>The main light's direction (toward it) and strength for the glint: the scene's sun, else a soft key.</summary>
        static Vector4 LightDir()
        {
            var sun = RenderSettings.sun;
            if (sun && sun.isActiveAndEnabled)
            {
                var d = -sun.transform.forward;
                return new Vector4(d.x, d.y, d.z, Mathf.Clamp(sun.intensity, 0f, 2f));
            }
            var k = new Vector3(0.3f, 0.8f, 0.4f).normalized;
            return new Vector4(k.x, k.y, k.z, 0.6f);
        }

        /// <summary>
        /// One kind's look as the shader reads it (<c>_MWLook[k..k+4]</c>), with the comfort master folded in: bend, lens and
        /// wobble × Distortion intensity; the phases advance at speeds capped by Comfort (<see cref="AdvancePhases"/>).
        /// </summary>
        static void PackLook(Vector4[] o, int k, in SolitonTunnelLook l, in SolitonTunnelRideView r, in SolitonTunnelComfort c, int ph)
        {
            float I = Mathf.Clamp01(c.DistortionIntensity);
            o[k] = new Vector4(l.Bend * Mathf.Deg2Rad * I, l.Lens * LensScale * I, Mathf.Max(0.02f, l.WallInside), Mathf.Max(0.02f, l.WallOutside));
            o[k + 1] = new Vector4(Mathf.Min(1f, l.Wobble * I), Mathf.Max(0.5f, l.WobbleWaves), s_phase[ph], 0f);
            o[k + 2] = new Vector4(l.Rim, Mathf.Max(1f, l.RimTightness), l.Glint, l.Glow);
            o[k + 3] = Vector4.zero;
            o[k + 4] = new Vector4(Mathf.Clamp01(r.InsideBend), Mathf.Clamp01(r.InsideWobble), Mathf.Max(0f, r.SteadyCentre), 0f);
        }

        /// <summary>The jelly lens's refraction per unit of <see cref="SolitonTunnelLook.Lens"/> (the Tube Lens Lab's glass index).</summary>
        public const float LensScale = 0.05f;

        /// <summary>Once a frame: each kind's wobble and throat-flow phase, at speeds capped by the comfort settings.</summary>
        static void AdvancePhases(in SolitonTunnelLook tunnel, in SolitonTunnelLook network, in SolitonTunnelComfort c)
        {
            if (s_phaseFrame == Time.frameCount) return;
            s_phaseFrame = Time.frameCount;
            float dt = Time.deltaTime;
            for (int kind = 0; kind < 2; kind++)
            {
                var l = kind == 1 ? network : tunnel;
                int b = kind * 2;
                s_phase[b] = Mathf.Repeat(s_phase[b] + dt * Mathf.Min(l.WobbleSpeed, c.MaxWobbleSpeed), 1f);
                s_phase[b + 1] = Mathf.Repeat(s_phase[b + 1] + dt * Mathf.Min(l.InsideFlow, c.MaxFlowSpeed), 1f);
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

        static void WriteTunnel(ContourTubes bank, int i, SolitonTunnel t, Vector3 eye)
        {
            float outer = Mathf.Max(0f, t.Config ? t.Config.Look.WallOutside : 0.15f);
            var mid = t.At(0.5f * (t.W.S0 + t.W.S1), out _);
            bank.W2L[i] = SolitonTunnelShapeMath.TubeWorldToLocal(t.A, t.T0, t.Side);
            float se = SolitonTunnelShapeMath.NearestAlong(t.A, t.T0, t.Side, t.Path, t.L, eye), inside = 0f;
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
        static void WriteChannel(ContourTubes bank, int i, SolitonNetwork net, int ch, Vector3 eye)
        {
            var c = net.Channels[ch];
            var cfg = net.Config;
            ref var w = ref net.Life[ch];
            var a = SolitonNetwork.V(c.A);
            float L = (float)c.L, outer = Mathf.Max(0f, cfg.Look.WallOutside), rMax = 0f;
            bank.W2L[i] = Matrix4x4.Translate(-a);
            var eyeD = SolitonNetwork.D(eye);
            double se = SolitonTunnelMath.Nearest(c.Path, eyeD);
            float inside = se >= w.S0 && se <= w.S1
                ? InsideAmount((float)SolitonTunnelMath.At(c.Path, se, out _).DistanceTo(eyeD), (float)SolitonTunnelMath.Radius(c, se, cfg.NetworkTip))
                : 0f;
            bank.Shape[i] = new Vector4(L, 1f, inside, 0f);
            Vector3 lo = new(float.MaxValue, float.MaxValue, float.MaxValue), hi = -lo;
            for (int j = 0; j < PathPoints; j++)
            {
                double sj = c.L * j / (PathPoints - 1);
                var p = SolitonNetwork.V(SolitonTunnelMath.At(c.Path, sj, out _)) - a;
                SolitonTunnelMath.Frame(c.Path, sj, out var nd, out _);
                float r = (float)SolitonTunnelMath.Radius(c, sj, cfg.NetworkTip);
                bank.Path[i * PathPoints + j] = new Vector4(p.x, p.y, p.z, r);
                var nn = SolitonNetwork.V(nd);
                bank.Norm[i * PathPoints + j] = new Vector4(nn.x, nn.y, nn.z, 0f);
                if (sj < w.S0 - 1e-3 || sj > w.S1 + 1e-3) continue;
                lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); rMax = Mathf.Max(rMax, r);
            }
            if (lo.x > hi.x) { lo = hi = SolitonNetwork.V(SolitonTunnelMath.At(c.Path, 0.5 * (w.S0 + w.S1), out _)) - a; rMax = (float)c.M; }
            var mid = a + 0.5f * (lo + hi);
            bank.Win[i] = new Vector4((float)w.S0, (float)w.S1, SolitonTunnelView.ChannelFade(w), 0f);
            bank.Bound[i] = new Vector4(mid.x, mid.y, mid.z, 0.5f * (hi - lo).magnitude + rMax * (1f + outer) + 4f);
        }

        static bool Resolve()
        {
            if (s_resolved) return s_lens;
            s_resolved = true;
            s_lens = Resources.Load<Material>(LensMaterialPath);
            if (!BlackHoleLens.IsDrawable(s_lens, out string reason))
            {
                CSDebug.LogWarning($"[Soliton] The Soliton Tunnel lens cannot draw: {reason}. Tunnels are not shown.");
                s_lens = null;
            }
            WorldColorTrace.Paint(s_lens);   // loaded after the scene's repaint: born in the worn colour set
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
            s_rides.Clear();
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
            RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
        }
    }
}

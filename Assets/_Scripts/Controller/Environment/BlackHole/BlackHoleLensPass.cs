using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The black hole lens's own render pass (Docs/BLACK_HOLE.md §5.1): after URP has drawn the TRANSPARENTS,
    /// copy the camera's colour, then draw EVERY hole the camera sees in one full-screen triangle from that copy
    /// (<c>BlackHoleLens.shader</c>, the Vessel Studio's lens pass). The holes' screen positions and sizes are
    /// worked out for this camera (<see cref="BlackHoleLens.ScreenWells"/>) and go with the draw in a
    /// <see cref="MaterialPropertyBlock"/>, so two cameras rendering in one frame never read each other's.
    ///
    /// <para><b>Why after the transparents.</b> The lens first drew at Transparent+50 and bent URP's
    /// <c>_CameraOpaqueTexture</c> — a copy taken after the skybox, before any transparent. Everything
    /// alpha-blended — the cytoplasm's snow shards, particles, glows — was in neither that copy nor the depth
    /// texture, so it was never bent. Copying AFTER the transparents puts everything the camera drew into what
    /// the lens bends.</para>
    ///
    /// <para><b>Why one draw (2026-10-10).</b> It used to draw one lens sphere per hole from the same copy; where
    /// a pair's spheres overlapped, the one drawn last painted over its partner. One draw summing every hole is
    /// the studio's pass and has no order.</para>
    ///
    /// <para><b>How it is injected.</b> No renderer feature on the URP renderer asset: the pass is enqueued from
    /// script on every base game camera (and the Scene view) in
    /// <see cref="RenderPipelineManager.beginCameraRendering"/> while at least one lens is live — URP's
    /// documented "inject a pass via scripting" route — so nothing is added to any asset and a scene with no
    /// hole pays nothing. A camera that sees no hole skips the copy too.</para>
    ///
    /// <para><b>Stated limit.</b> A TRANSPARENT between the camera and the hole is in the copy too, so it is bent
    /// with the background. Opaque mass in front is not: the shader skips a hole for every pixel whose opaque
    /// depth is in front of it.</para>
    /// </summary>
    public sealed class BlackHoleLensPass : ScriptableRenderPass
    {
        /// <summary>The LightMode of the lens shader's pass — drawn by this pass and nothing else.</summary>
        public const string LightModeName = "BlackHoleLens";

        static readonly int SceneColorId = Shader.PropertyToID("_BlackHoleSceneColor");

        static BlackHoleLensPass s_pass;
        static int s_users;

        static readonly int WellCId = Shader.PropertyToID("_BHWellC");
        static readonly int WellPId = Shader.PropertyToID("_BHWellP");
        static readonly int WellMId = Shader.PropertyToID("_BHWellM");
        static readonly int WellTId = Shader.PropertyToID("_BHWellT");
        static readonly int WellCountId = Shader.PropertyToID("_BHWellCount");
        static readonly int LookId = Shader.PropertyToID("_BHLook");
        static readonly int Look2Id = Shader.PropertyToID("_BHLook2");

        class CopyData
        {
            public TextureHandle source;
        }

        class LensData
        {
            public Material material;
            public readonly MaterialPropertyBlock block = new();
            public readonly Vector4[] c = new Vector4[BlackHoleLens.MaxWells];
            public readonly Vector4[] p = new Vector4[BlackHoleLens.MaxWells];
            public readonly Vector4[] m = new Vector4[BlackHoleLens.MaxWells];
            public readonly Vector4[] t = new Vector4[BlackHoleLens.MaxWells];
            public int count;
            public Vector4 look, look2;
        }

        // This camera's holes, filled before the graph is built (a camera that sees none skips the pass).
        static readonly Vector4[] s_c = new Vector4[BlackHoleLens.MaxWells];
        static readonly Vector4[] s_p = new Vector4[BlackHoleLens.MaxWells];
        static readonly Vector4[] s_m = new Vector4[BlackHoleLens.MaxWells];
        static readonly Vector4[] s_t = new Vector4[BlackHoleLens.MaxWells];

        BlackHoleLensPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            // The copy reads the camera colour as a texture: the backbuffer cannot be read.
            requiresIntermediateTexture = true;
            profilingSampler = new ProfilingSampler("BlackHoleLens");
        }

        internal static void Acquire() => s_users++;
        internal static void Release() => s_users = Mathf.Max(0, s_users - 1);

        static void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (s_users == 0 || cam == null) return;
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
            s_pass ??= new BlackHoleLensPass();
            renderer.EnqueuePass(s_pass);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;
            var cameraData = frameData.Get<UniversalCameraData>();
            var cam = cameraData.camera;
            // The studio's lens is a perspective lens; an orthographic view (a 2D Scene view) draws no hole.
            if (cam == null || cam.orthographic) return;
            var material = BlackHoleLens.SharedMaterial;
            if (material == null) return;
            var config = BlackHoleRegistry.Config;
            int count = BlackHoleLens.ScreenWells(cam, config, s_c, s_p, s_m, s_t);
            if (count == 0) return;
            TextureHandle colour = resources.activeColorTexture;

            // 1. The scene as drawn so far — opaques, skybox AND transparents — resolved, single-sample.
            var desc = renderGraph.GetTextureDesc(colour);
            desc.name = "_BlackHoleSceneColor";
            desc.msaaSamples = MSAASamples.None;
            desc.bindTextureMS = false;
            desc.clearBuffer = false;
            desc.filterMode = FilterMode.Bilinear;
            desc.wrapMode = TextureWrapMode.Clamp;
            TextureHandle copy = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<CopyData>("BlackHole scene copy", out var data, profilingSampler))
            {
                data.source = colour;
                builder.UseTexture(colour, AccessFlags.Read);
                builder.SetRenderAttachment(copy, 0, AccessFlags.WriteAll);
                builder.SetGlobalTextureAfterPass(copy, SceneColorId);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((CopyData d, RasterGraphContext ctx) =>
                    Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), 0f, false));
            }

            // 2. Every hole at once, bending that copy onto the camera colour.
            using (var builder = renderGraph.AddRasterRenderPass<LensData>("BlackHole lens", out var data, profilingSampler))
            {
                data.material = material;
                data.count = count;
                System.Array.Copy(s_c, data.c, BlackHoleLens.MaxWells);
                System.Array.Copy(s_p, data.p, BlackHoleLens.MaxWells);
                System.Array.Copy(s_m, data.m, BlackHoleLens.MaxWells);
                System.Array.Copy(s_t, data.t, BlackHoleLens.MaxWells);
                data.look = BlackHoleLens.Look(config);
                data.look2 = BlackHoleLens.Look2(config, cam.aspect);
                builder.UseTexture(copy, AccessFlags.Read);
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
                    d.block.SetVectorArray(WellMId, d.m);
                    d.block.SetVectorArray(WellTId, d.t);
                    d.block.SetFloat(WellCountId, d.count);
                    d.block.SetVector(LookId, d.look);
                    d.block.SetVector(Look2Id, d.look2);
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, 0, MeshTopology.Triangles, 3, 1, d.block);
                });
            }
        }

        /// <summary>Play-mode (re)entry: no lens is live yet; subscribe once.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            s_users = 0;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }
    }
}

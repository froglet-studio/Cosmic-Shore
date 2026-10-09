using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The black hole lens's own render pass (Docs/BLACK_HOLE.md §5.1): after URP has drawn the
    /// TRANSPARENTS, copy the camera's colour, then draw every lens from that copy.
    ///
    /// <para><b>Why not in the transparent queue, reading the opaque copy.</b> The lens first drew at
    /// Transparent+50 and bent URP's <c>_CameraOpaqueTexture</c> — a copy taken after the skybox,
    /// before any transparent. Everything alpha-blended — the cytoplasm's snow shards, particles,
    /// glows — was in neither that copy nor the depth texture, so it was never bent; and the lens,
    /// which paints every pixel of its sphere, then covered the shards that had already drawn behind
    /// it. In lava lamp that read as a ball 30 r_s across with no shards in it. Copying AFTER the
    /// transparents puts everything the camera drew behind the hole into what the lens bends.</para>
    ///
    /// <para><b>How it is injected.</b> No renderer feature on the URP renderer asset: the pass is
    /// enqueued from script on every base game camera (and the Scene view) in
    /// <see cref="RenderPipelineManager.beginCameraRendering"/> while at least one lens is live —
    /// URP's documented "inject a pass via scripting" route — so nothing is added to any asset and a
    /// scene with no hole pays nothing. The lens material's pass carries
    /// <c>LightMode = BlackHoleLens</c>, which URP's own passes never draw; only this pass does.</para>
    ///
    /// <para><b>Stated limit.</b> A TRANSPARENT between the camera and the hole is in the copy too, so
    /// inside the lens it is bent with the background (a shard crossing in front of the shadow is
    /// drawn into the bend instead of over the black). Opaque mass in front is not: the shader keeps
    /// every pixel whose opaque depth is nearer than the hole.</para>
    /// </summary>
    public sealed class BlackHoleLensPass : ScriptableRenderPass
    {
        /// <summary>The LightMode of the lens shader's pass — drawn by this pass and nothing else.</summary>
        public const string LightModeName = "BlackHoleLens";

        static readonly ShaderTagId LightMode = new(LightModeName);
        static readonly int SceneColorId = Shader.PropertyToID("_BlackHoleSceneColor");

        static BlackHoleLensPass s_pass;
        static int s_users;

        /// <summary>
        /// Cameras that never draw a lens: the eyes that render what a lens SHOWS (a crystal wormhole's far eye
        /// and panoramas, CrystalWormholeView). Their pictures are composited by the gameplay camera's own lens
        /// pass, so lensing them too would bend the far side twice — and a panorama eye sits inside a throat.
        /// </summary>
        static readonly System.Collections.Generic.HashSet<Camera> s_excluded = new();

        internal static void Exclude(Camera cam) { if (cam) s_excluded.Add(cam); }
        internal static void Include(Camera cam) => s_excluded.Remove(cam);

        class CopyData
        {
            public TextureHandle source;
        }

        class LensData
        {
            public RendererListHandle lenses;
        }

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
            if (s_users == 0 || cam == null || s_excluded.Contains(cam)) return;
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
            var renderingData = frameData.Get<UniversalRenderingData>();
            var lightData = frameData.Get<UniversalLightData>();
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

            // 2. Every lens, back to front, bending that copy onto the camera colour.
            using (var builder = renderGraph.AddRasterRenderPass<LensData>("BlackHole lens", out var data, profilingSampler))
            {
                var drawing = RenderingUtils.CreateDrawingSettings(LightMode, renderingData, cameraData, lightData,
                    SortingCriteria.CommonTransparent);
                var filtering = new FilteringSettings(RenderQueueRange.all);
                data.lenses = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing, filtering));
                builder.UseRendererList(data.lenses);
                builder.UseTexture(copy, AccessFlags.Read);
                if (resources.cameraDepthTexture.IsValid())
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                builder.UseAllGlobalTextures(true);
                builder.SetRenderAttachment(colour, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((LensData d, RasterGraphContext ctx) => ctx.cmd.DrawRendererList(d.lenses));
            }
        }

        /// <summary>Play-mode (re)entry: no lens is live yet; subscribe once.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            s_users = 0;
            s_excluded.Clear();
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }
    }
}

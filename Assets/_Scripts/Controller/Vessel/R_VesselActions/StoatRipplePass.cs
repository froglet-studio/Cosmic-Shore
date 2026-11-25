using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Draws the Stoat's space-time ripples (<see cref="StoatRipple"/>, <c>R_VesselActions/STOAT_DIPOLE.md</c> §1c "The
    /// drawn wave"): after URP has drawn the transparents (and the black-hole lens, which is enqueued first at the same
    /// event), copy the camera colour, then redraw every pixel a wave crosses from that copy, bent, in one full-screen
    /// triangle (<c>CosmicShore/StoatRipple</c>). Pixels no wave reaches are discarded. Nothing is added: no colour, no
    /// glow, so bloom has nothing to blow up.
    ///
    /// <para><b>How it is injected.</b> <see cref="BlackHoleLensPass"/>'s route: no renderer feature on any asset; the pass
    /// is enqueued from <see cref="RenderPipelineManager.beginCameraRendering"/> on every base game camera (and the Scene
    /// view) only while a ripple lives, so a frame without one pays nothing. Unlike the hole's lens it runs in BOTH
    /// black-hole looks (<see cref="ScriptableObjects.HoleLook"/>): a ripple lives about a second and a half, so the copy
    /// it costs is brief.</para>
    ///
    /// <para><b>Stated limit.</b> The URP asset keeps no depth texture, so an opaque in front of the wave inside its band
    /// is bent with what is behind it.</para>
    /// </summary>
    public sealed class StoatRipplePass : ScriptableRenderPass
    {
        static readonly int SceneColorId = Shader.PropertyToID("_StoatRippleSceneColor");
        static readonly int RippleCId = Shader.PropertyToID("_StoatRippleC");
        static readonly int RippleAId = Shader.PropertyToID("_StoatRippleA");
        static readonly int RippleCountId = Shader.PropertyToID("_StoatRippleCount");
        static readonly int RippleViewId = Shader.PropertyToID("_StoatRippleView");

        static StoatRipplePass s_pass;
        static Material s_material;
        static bool s_warned;

        static readonly Vector4[] s_c = new Vector4[StoatRipple.MaxDrawn];
        static readonly Vector4[] s_a = new Vector4[StoatRipple.MaxDrawn];

        class CopyData
        {
            public TextureHandle source;
        }

        class DrawData
        {
            public Material material;
            public readonly MaterialPropertyBlock block = new();
            public readonly Vector4[] c = new Vector4[StoatRipple.MaxDrawn];
            public readonly Vector4[] a = new Vector4[StoatRipple.MaxDrawn];
            public int count;
            public Vector4 view;
        }

        StoatRipplePass()
        {
            // Right after the black-hole lens (AfterRenderingTransparents), so a ripple bends the lensed hole too.
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents + 1;
            // The copy reads the camera colour as a texture: the backbuffer cannot be read.
            requiresIntermediateTexture = true;
            profilingSampler = new ProfilingSampler("StoatRipple");
        }

        static Material SharedMaterial()
        {
            if (s_material) return s_material;
            s_material = Resources.Load<Material>(StoatRipple.MaterialResourcePath);
            if (!s_material && !s_warned)
            {
                s_warned = true;
                CSDebug.LogWarning($"[StoatRipple] Resources/{StoatRipple.MaterialResourcePath}.mat is missing — the Stoat's " +
                                   "annihilation ripple still strips and kills, but nothing draws it.");
            }
            return s_material;
        }

        static void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (cam == null || StoatRipple.Count == 0) return;
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
            s_pass ??= new StoatRipplePass();
            renderer.EnqueuePass(s_pass);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;
            var cam = frameData.Get<UniversalCameraData>().camera;
            if (cam == null || cam.orthographic) return;
            var material = SharedMaterial();
            if (material == null) return;
            int count = StoatRipple.ScreenRipples(cam.worldToCameraMatrix, cam.transform.position, cam.fieldOfView,
                cam.nearClipPlane, StoatRipple.Now, s_c, s_a, out float swell);
            if (count == 0 && Mathf.Abs(swell) < 1e-5f) return;
            TextureHandle colour = resources.activeColorTexture;

            // 1. The scene as drawn so far, resolved and single-sample.
            var desc = renderGraph.GetTextureDesc(colour);
            desc.name = "_StoatRippleSceneColor";
            desc.msaaSamples = MSAASamples.None;
            desc.bindTextureMS = false;
            desc.clearBuffer = false;
            desc.filterMode = FilterMode.Bilinear;
            desc.wrapMode = TextureWrapMode.Clamp;
            TextureHandle copy = renderGraph.CreateTexture(desc);
            using (var builder = renderGraph.AddRasterRenderPass<CopyData>("StoatRipple scene copy", out var data, profilingSampler))
            {
                data.source = colour;
                builder.UseTexture(colour, AccessFlags.Read);
                builder.SetRenderAttachment(copy, 0, AccessFlags.WriteAll);
                builder.SetGlobalTextureAfterPass(copy, SceneColorId);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((CopyData d, RasterGraphContext ctx) =>
                    Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), 0f, false));
            }

            // 2. Every ripple at once, bending that copy back onto the camera colour.
            using (var builder = renderGraph.AddRasterRenderPass<DrawData>("StoatRipple", out var data, profilingSampler))
            {
                data.material = material;
                data.count = count;
                System.Array.Copy(s_c, data.c, StoatRipple.MaxDrawn);
                System.Array.Copy(s_a, data.a, StoatRipple.MaxDrawn);
                data.view = new Vector4(cam.aspect, swell, 0f, 0f);
                builder.UseTexture(copy, AccessFlags.Read);
                builder.UseAllGlobalTextures(true);
                builder.SetRenderAttachment(colour, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((DrawData d, RasterGraphContext ctx) =>
                {
                    d.block.Clear();
                    d.block.SetVectorArray(RippleCId, d.c);
                    d.block.SetVectorArray(RippleAId, d.a);
                    d.block.SetFloat(RippleCountId, d.count);
                    d.block.SetVector(RippleViewId, d.view);
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, 0, MeshTopology.Triangles, 3, 1, d.block);
                });
            }
        }

        /// <summary>Play-mode (re)entry: subscribe once; the material cache describes a scene that is gone.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            s_material = null;
            s_warned = false;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }
    }
}

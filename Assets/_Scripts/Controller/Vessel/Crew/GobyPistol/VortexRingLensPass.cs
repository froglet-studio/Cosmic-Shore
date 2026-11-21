using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The vortex ring lens's render pass (Docs/BLACK_HOLE.md §5.3), the sibling of <see cref="BlackHoleLensPass"/>
    /// with the same plumbing: after URP has drawn the TRANSPARENTS, copy the camera's colour, then draw every ring the
    /// camera sees in one full-screen triangle from that copy (<c>VortexRingLens.shader</c>). The rings' screen
    /// ellipses are worked out for this camera from its real matrices (<see cref="VortexRingLens.ScreenRings"/>) and go
    /// with the draw in a <see cref="MaterialPropertyBlock"/>, so two cameras in one frame (the Goby's chase camera and
    /// the Pistol's seat camera) never read each other's.
    ///
    /// <para><b>Why a sibling and not more rows in the black hole's pass.</b> The black hole lens is ported line for line
    /// from the Vessel Studio and held by goldens (<c>verify_black_hole_lens.py</c>); a toroid needs a different
    /// per-pixel search (the nearest point on an ellipse, not a distance to a point) and different rows. A second pass
    /// leaves that verified code untouched and costs one more copy only while a ring is live.</para>
    ///
    /// <para><b>How it is injected.</b> No renderer feature: enqueued from script on every base game camera (and the
    /// Scene view) in <see cref="RenderPipelineManager.beginCameraRendering"/> while a ring is live, URP's documented
    /// "inject a pass via scripting" route. A camera that sees no ring skips the copy too.</para>
    ///
    /// <para><b>Stated limit.</b> A TRANSPARENT between the camera and the ring is in the copy, so it is bent with the
    /// background. Opaque mass in front is not: the shader skips a ring for every pixel whose opaque depth is in front of
    /// the ring's nearest core point by more than its margin.</para>
    /// </summary>
    public sealed class VortexRingLensPass : ScriptableRenderPass
    {
        /// <summary>The LightMode of the lens shader's pass — drawn by this pass and nothing else.</summary>
        public const string LightModeName = "VortexRingLens";

        static readonly int SceneColorId = Shader.PropertyToID("_VortexRingSceneColor");
        static readonly int RingCId = Shader.PropertyToID("_VRC");
        static readonly int RingAId = Shader.PropertyToID("_VRA");
        static readonly int RingZId = Shader.PropertyToID("_VRZ");
        static readonly int RingLId = Shader.PropertyToID("_VRL");
        static readonly int RingTId = Shader.PropertyToID("_VRT");
        static readonly int RingCountId = Shader.PropertyToID("_VRCount");
        static readonly int ViewId = Shader.PropertyToID("_VRView");

        static VortexRingLensPass s_pass;

        class CopyData
        {
            public TextureHandle source;
        }

        class LensData
        {
            public Material material;
            public readonly MaterialPropertyBlock block = new();
            public readonly Vector4[] c = new Vector4[VortexRingLens.MaxRings];
            public readonly Vector4[] a = new Vector4[VortexRingLens.MaxRings];
            public readonly Vector4[] z = new Vector4[VortexRingLens.MaxRings];
            public readonly Vector4[] l = new Vector4[VortexRingLens.MaxRings];
            public readonly Vector4[] t = new Vector4[VortexRingLens.MaxRings];
            public int count;
            public Vector4 view;
        }

        // This camera's rings, filled before the graph is built (a camera that sees none skips the pass).
        static readonly Vector4[] s_c = new Vector4[VortexRingLens.MaxRings];
        static readonly Vector4[] s_a = new Vector4[VortexRingLens.MaxRings];
        static readonly Vector4[] s_z = new Vector4[VortexRingLens.MaxRings];
        static readonly Vector4[] s_l = new Vector4[VortexRingLens.MaxRings];
        static readonly Vector4[] s_t = new Vector4[VortexRingLens.MaxRings];

        VortexRingLensPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            // The copy reads the camera colour as a texture: the backbuffer cannot be read.
            requiresIntermediateTexture = true;
            profilingSampler = new ProfilingSampler("VortexRingLens");
        }

        static void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (VortexRingLens.Count == 0 || cam == null) return;
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
            s_pass ??= new VortexRingLensPass();
            renderer.EnqueuePass(s_pass);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;
            var cameraData = frameData.Get<UniversalCameraData>();
            var cam = cameraData.camera;
            // A perspective lens: an orthographic view (a 2D Scene view) draws no ring.
            if (cam == null || cam.orthographic) return;
            var material = VortexRingLens.SharedMaterial;
            if (material == null) return;
            int count = VortexRingLens.ScreenRings(cam, s_c, s_a, s_z, s_l, s_t);
            if (count == 0) return;
            TextureHandle colour = resources.activeColorTexture;

            // 1. The scene as drawn so far — opaques, skybox AND transparents — resolved, single-sample.
            var desc = renderGraph.GetTextureDesc(colour);
            desc.name = "_VortexRingSceneColor";
            desc.msaaSamples = MSAASamples.None;
            desc.bindTextureMS = false;
            desc.clearBuffer = false;
            desc.filterMode = FilterMode.Bilinear;
            desc.wrapMode = TextureWrapMode.Clamp;
            TextureHandle copy = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<CopyData>("VortexRing scene copy", out var data, profilingSampler))
            {
                data.source = colour;
                builder.UseTexture(colour, AccessFlags.Read);
                builder.SetRenderAttachment(copy, 0, AccessFlags.WriteAll);
                builder.SetGlobalTextureAfterPass(copy, SceneColorId);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((CopyData d, RasterGraphContext ctx) =>
                    Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), 0f, false));
            }

            // 2. Every ring at once, bending that copy onto the camera colour.
            using (var builder = renderGraph.AddRasterRenderPass<LensData>("VortexRing lens", out var data, profilingSampler))
            {
                data.material = material;
                data.count = count;
                System.Array.Copy(s_c, data.c, VortexRingLens.MaxRings);
                System.Array.Copy(s_a, data.a, VortexRingLens.MaxRings);
                System.Array.Copy(s_z, data.z, VortexRingLens.MaxRings);
                System.Array.Copy(s_l, data.l, VortexRingLens.MaxRings);
                System.Array.Copy(s_t, data.t, VortexRingLens.MaxRings);
                data.view = new Vector4(VortexRingLens.ScreenAspect(cam), Time.time, 0f, 0f);
                builder.UseTexture(copy, AccessFlags.Read);
                if (resources.cameraDepthTexture.IsValid())
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                builder.UseAllGlobalTextures(true);
                builder.SetRenderAttachment(colour, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((LensData d, RasterGraphContext ctx) =>
                {
                    d.block.Clear();
                    d.block.SetVectorArray(RingCId, d.c);
                    d.block.SetVectorArray(RingAId, d.a);
                    d.block.SetVectorArray(RingZId, d.z);
                    d.block.SetVectorArray(RingLId, d.l);
                    d.block.SetVectorArray(RingTId, d.t);
                    d.block.SetFloat(RingCountId, d.count);
                    d.block.SetVector(ViewId, d.view);
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, 0, MeshTopology.Triangles, 3, 1, d.block);
                });
            }
        }

        /// <summary>Play-mode (re)entry: subscribe once.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }
    }
}

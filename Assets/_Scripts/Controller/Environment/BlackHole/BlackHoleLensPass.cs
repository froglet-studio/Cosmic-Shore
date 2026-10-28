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
    /// <para><b>The waves' light goes on AFTER post-processing (2026-10-28).</b> A second instance of this pass,
    /// at <see cref="RenderPassEvent.AfterRenderingPostProcessing"/>, draws the shader's pass 1
    /// (<c>BlackHoleWaveLight</c>): a light shell's glow, a gravitational wave's prism glow and a shell's
    /// screen wash, added to the FINAL picture in display values (sRGB), as the studio adds them to its own
    /// buffer (three r128: an 8-bit target, no bloom, no tonemapping). Added before post-processing they were
    /// linear HDR light: the gameplay profile's bloom (threshold 0.2, intensity 2.5) blew a shell's 0.35 glow and
    /// its ≤0.3 wash into a screen-filling white flash — why annihilation waves were switched off on 2026-10-14.
    /// Pass 0 keeps the bend, the wave's squeeze (trough darkening) and the holes' own light. The two passes
    /// sum to the studio's composite exactly (Tools/Shaders/verify_black_hole_lens.py, 1b).</para>
    ///
    /// <para><b>Stated limit.</b> A TRANSPARENT between the camera and the hole is in the copy too, so it is bent
    /// with the background. Opaque mass in front is not: the shader skips a hole for every pixel whose opaque
    /// depth is in front of it. The holes' own ring glow and core are still drawn before post-processing, so they
    /// still bloom.</para>
    /// </summary>
    public sealed class BlackHoleLensPass : ScriptableRenderPass
    {
        /// <summary>The LightMode of the lens shader's pass — drawn by this pass and nothing else.</summary>
        public const string LightModeName = "BlackHoleLens";

        /// <summary>The LightMode of the shader's second pass: the waves' light, drawn after post-processing.</summary>
        public const string WaveLightModeName = "BlackHoleWaveLight";

        const int LensShaderPass = 0;
        const int WaveLightShaderPass = 1;

        static readonly int SceneColorId = Shader.PropertyToID("_BlackHoleSceneColor");

        static BlackHoleLensPass s_pass;
        static BlackHoleLensPass s_waveLightPass;
        readonly int shaderPass;
        static int s_users;

        static readonly int WellCId = Shader.PropertyToID("_BHWellC");
        static readonly int WellPId = Shader.PropertyToID("_BHWellP");
        static readonly int WellMId = Shader.PropertyToID("_BHWellM");
        static readonly int WellTId = Shader.PropertyToID("_BHWellT");
        static readonly int WellCountId = Shader.PropertyToID("_BHWellCount");
        static readonly int LookId = Shader.PropertyToID("_BHLook");
        static readonly int Look2Id = Shader.PropertyToID("_BHLook2");
        static readonly int WaveCId = Shader.PropertyToID("_BHWaveC");
        static readonly int WavePId = Shader.PropertyToID("_BHWaveP");
        static readonly int WaveQId = Shader.PropertyToID("_BHWaveQ");
        static readonly int WaveAId = Shader.PropertyToID("_BHWaveA");
        static readonly int WaveCountId = Shader.PropertyToID("_BHWaveCount");
        static readonly int WaveId = Shader.PropertyToID("_BHWave");

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
            public readonly Vector4[] wc = new Vector4[BlackHoleWaves.MaxDrawn];
            public readonly Vector4[] wp = new Vector4[BlackHoleWaves.MaxDrawn];
            public readonly Vector4[] wq = new Vector4[BlackHoleWaves.MaxDrawn];
            public readonly Vector4[] wa = new Vector4[BlackHoleWaves.MaxDrawn];
            public int waveCount;
            public Vector4 wave;
            public int shaderPass;
        }

        // This camera's holes, filled before the graph is built (a camera that sees none skips the pass).
        static readonly Vector4[] s_c = new Vector4[BlackHoleLens.MaxWells];
        static readonly Vector4[] s_p = new Vector4[BlackHoleLens.MaxWells];
        static readonly Vector4[] s_m = new Vector4[BlackHoleLens.MaxWells];
        static readonly Vector4[] s_t = new Vector4[BlackHoleLens.MaxWells];
        static readonly Vector4[] s_wc = new Vector4[BlackHoleWaves.MaxDrawn];
        static readonly Vector4[] s_wp = new Vector4[BlackHoleWaves.MaxDrawn];
        static readonly Vector4[] s_wq = new Vector4[BlackHoleWaves.MaxDrawn];
        static readonly Vector4[] s_wa = new Vector4[BlackHoleWaves.MaxDrawn];
        static bool s_waves;

        BlackHoleLensPass(int shaderPass)
        {
            this.shaderPass = shaderPass;
            renderPassEvent = shaderPass == LensShaderPass
                ? RenderPassEvent.AfterRenderingTransparents
                : RenderPassEvent.AfterRenderingPostProcessing;
            // The copy reads the camera colour as a texture: the backbuffer cannot be read.
            requiresIntermediateTexture = true;
            profilingSampler = new ProfilingSampler(shaderPass == LensShaderPass ? "BlackHoleLens" : "BlackHoleWaveLight");
        }

        internal static void Acquire() => s_users++;
        internal static void Release() => s_users = Mathf.Max(0, s_users - 1);

        /// <summary>A wave is alive (BlackHoleWaves): the pass runs even when every hole is gone — a wave outlives its pair.</summary>
        internal static void AcquireWaves() => s_waves = true;
        internal static void ReleaseWaves() => s_waves = false;

        static void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if ((s_users == 0 && !s_waves) || cam == null) return;
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
            s_pass ??= new BlackHoleLensPass(LensShaderPass);
            renderer.EnqueuePass(s_pass);
            if (s_waves)
            {
                s_waveLightPass ??= new BlackHoleLensPass(WaveLightShaderPass);
                renderer.EnqueuePass(s_waveLightPass);
            }
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
            int waveCount = BlackHoleWaves.ScreenWaves(cam.worldToCameraMatrix, cam.transform.position, cam.fieldOfView,
                cam.nearClipPlane, BlackHoleWaves.Now, config, s_wc, s_wp, s_wq, s_wa, out float gwCam, out float shellWash);
            if (count == 0 && waveCount == 0 && gwCam == 0f && shellWash == 0f) return;
            // The waves' light: nothing to add without a wave or a wash.
            if (shaderPass == WaveLightShaderPass && waveCount == 0 && shellWash == 0f) return;
            TextureHandle colour = resources.activeColorTexture;

            // 1. The scene as drawn so far — opaques, skybox AND transparents (pass 1: the post-processed picture) —
            //    resolved, single-sample.
            var desc = renderGraph.GetTextureDesc(colour);
            desc.name = "_BlackHoleSceneColor";
            desc.msaaSamples = MSAASamples.None;
            desc.bindTextureMS = false;
            desc.clearBuffer = false;
            desc.filterMode = FilterMode.Bilinear;
            desc.wrapMode = TextureWrapMode.Clamp;
            TextureHandle copy = renderGraph.CreateTexture(desc);

            string copyName = shaderPass == LensShaderPass ? "BlackHole scene copy" : "BlackHole wave light copy";
            using (var builder = renderGraph.AddRasterRenderPass<CopyData>(copyName, out var data, profilingSampler))
            {
                data.source = colour;
                builder.UseTexture(colour, AccessFlags.Read);
                builder.SetRenderAttachment(copy, 0, AccessFlags.WriteAll);
                builder.SetGlobalTextureAfterPass(copy, SceneColorId);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((CopyData d, RasterGraphContext ctx) =>
                    Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), 0f, false));
            }

            // 2. Every hole at once, bending that copy onto the camera colour (pass 1: every wave's light on it).
            string drawName = shaderPass == LensShaderPass ? "BlackHole lens" : "BlackHole wave light";
            using (var builder = renderGraph.AddRasterRenderPass<LensData>(drawName, out var data, profilingSampler))
            {
                data.material = material;
                data.shaderPass = shaderPass;
                data.count = count;
                System.Array.Copy(s_c, data.c, BlackHoleLens.MaxWells);
                System.Array.Copy(s_p, data.p, BlackHoleLens.MaxWells);
                System.Array.Copy(s_m, data.m, BlackHoleLens.MaxWells);
                System.Array.Copy(s_t, data.t, BlackHoleLens.MaxWells);
                data.look = BlackHoleLens.Look(config);
                data.look2 = BlackHoleLens.Look2(config, cam.aspect);
                data.waveCount = waveCount;
                System.Array.Copy(s_wc, data.wc, BlackHoleWaves.MaxDrawn);
                System.Array.Copy(s_wp, data.wp, BlackHoleWaves.MaxDrawn);
                System.Array.Copy(s_wq, data.wq, BlackHoleWaves.MaxDrawn);
                System.Array.Copy(s_wa, data.wa, BlackHoleWaves.MaxDrawn);
                data.wave = new Vector4(gwCam, shellWash, config.GwGlow, 0f);
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
                    d.block.SetVectorArray(WaveCId, d.wc);
                    d.block.SetVectorArray(WavePId, d.wp);
                    d.block.SetVectorArray(WaveQId, d.wq);
                    d.block.SetVectorArray(WaveAId, d.wa);
                    d.block.SetFloat(WaveCountId, d.waveCount);
                    d.block.SetVector(WaveId, d.wave);
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, d.shaderPass, MeshTopology.Triangles, 3, 1, d.block);
                });
            }
        }

        /// <summary>Play-mode (re)entry: no lens is live yet; subscribe once.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            s_users = 0;
            s_waves = false;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }
    }
}

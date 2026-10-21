using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Draws the silence perimeter (Docs/ElementalAbilitySystem/SILENCE.md §5) on the silenced pilot's own
    /// camera: after URP has drawn the TRANSPARENTS, copy the camera's colour, draw the warp (pass 0 of
    /// <c>SilencePerimeter.shader</c>) as one full-screen triangle from that copy, then draw every shard
    /// (pass 1) back to front, each sampling the copy. The layout is <see cref="SilencePerimeter.Layout"/>,
    /// worked out here for this camera's aspect and field of view.
    ///
    /// <para><b>Injected like <see cref="BlackHoleLensPass"/>.</b> No renderer feature: the pass is enqueued
    /// from <see cref="RenderPipelineManager.beginCameraRendering"/> on the ONE camera
    /// <see cref="SilenceScreenFeedback"/> names (the local pilot's), and only while that pilot is silenced,
    /// so nothing is added to any asset, no other camera pays for it, and no other machine ever draws it.</para>
    ///
    /// <para><b>Space.</b> The shards are posed in the camera's own space about four units out, then the
    /// whole arrangement is scaled about the camera to clear its near plane (which leaves the picture
    /// unchanged), and drawn over everything (ZTest Always): they belong to the screen, not the world.</para>
    /// </summary>
    public sealed class SilencePerimeterPass : ScriptableRenderPass
    {
        /// <summary>The shader's passes, in the order <c>SilencePerimeter.shader</c> declares them
        /// (<c>SilencePerimeterTests.TheShaderDeclaresItsPassesInTheOrderThePassDrawsThem</c> pins it).</summary>
        public const int WarpPass = 0;
        public const int ShardPass = 1;

        static readonly int SceneColorId = Shader.PropertyToID("_SilenceSceneColor");
        static readonly int LookId = Shader.PropertyToID("_SPLook");
        static readonly int Look2Id = Shader.PropertyToID("_SPLook2");
        static readonly int Look3Id = Shader.PropertyToID("_SPLook3");
        static readonly int DangerId = Shader.PropertyToID("_SPDanger");
        static readonly int ShardId = Shader.PropertyToID("_SPShard");
        static readonly int AlphaId = Shader.PropertyToID("_SPAlpha");

        /// <summary>What <see cref="SilenceScreenFeedback"/> hands over every frame. <c>Active</c> false (or no
        /// camera, material or meshes) = the pass is not enqueued at all.</summary>
        internal struct Frame
        {
            public bool Active;
            public Camera Camera;
            public Material Material;
            public Mesh[] Meshes;
            public SilencePerimeter.Settings Settings;
            public float Drive;      // the silence's eased weight 0..1
            public float Pulse;      // the hit flare, already x Drive
            public float Time;       // unscaled seconds
            public float Amount;     // VesselSilenceConfigSO.PerimeterAmount
            public float Opacity;
            public float Glitch;
            public float Warp;
            public Vector4 Look;     // refraction, twist, aberration, tint
            public Vector4 Look2;    // rim glow, edge lines, fill, invert
            public Color Danger;
        }

        internal static Frame Current;

        static SilencePerimeterPass s_pass;
        static readonly SilencePerimeter.Pose[] s_poses = new SilencePerimeter.Pose[SilencePerimeter.MaxShards];
        static readonly int[] s_order = new int[SilencePerimeter.MaxShards];

        class CopyData
        {
            public TextureHandle source;
        }

        class DrawData
        {
            public Material material;
            public int warpPass, shardPass, count;
            public readonly MaterialPropertyBlock warpBlock = new();
            public readonly MaterialPropertyBlock[] blocks = new MaterialPropertyBlock[SilencePerimeter.MaxShards];
            public readonly Mesh[] meshes = new Mesh[SilencePerimeter.MaxShards];
            public readonly Matrix4x4[] matrices = new Matrix4x4[SilencePerimeter.MaxShards];

            public DrawData()
            {
                for (int i = 0; i < blocks.Length; i++) blocks[i] = new MaterialPropertyBlock();
            }
        }

        SilencePerimeterPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            // The copy reads the camera colour as a texture: the backbuffer cannot be read.
            requiresIntermediateTexture = true;
            profilingSampler = new ProfilingSampler("SilencePerimeter");
        }

        static void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            var f = Current;
            if (!f.Active || cam == null || cam != f.Camera || cam.cameraType != CameraType.Game) return;
            if (f.Material == null || f.Meshes == null || f.Meshes.Length == 0) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null || data.renderType != CameraRenderType.Base) return;
            var renderer = data.scriptableRenderer;
            if (renderer == null) return;
            s_pass ??= new SilencePerimeterPass();
            renderer.EnqueuePass(s_pass);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;
            var cam = frameData.Get<UniversalCameraData>().camera;
            var f = Current;
            if (cam == null || cam.orthographic || !f.Active || f.Material == null || f.Meshes == null) return;
            TextureHandle colour = resources.activeColorTexture;

            // 1. The scene as drawn so far — opaques, skybox AND transparents — resolved, single-sample.
            var desc = renderGraph.GetTextureDesc(colour);
            desc.name = "_SilenceSceneColor";
            desc.msaaSamples = MSAASamples.None;
            desc.bindTextureMS = false;
            desc.clearBuffer = false;
            desc.filterMode = FilterMode.Bilinear;
            desc.wrapMode = TextureWrapMode.Clamp;
            TextureHandle copy = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<CopyData>("Silence scene copy", out var data, profilingSampler))
            {
                data.source = colour;
                builder.UseTexture(colour, AccessFlags.Read);
                builder.SetRenderAttachment(copy, 0, AccessFlags.WriteAll);
                builder.SetGlobalTextureAfterPass(copy, SceneColorId);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((CopyData d, RasterGraphContext ctx) =>
                    Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), 0f, false));
            }

            // 2. The warp, then the shards back to front, onto the camera colour.
            using (var builder = renderGraph.AddRasterRenderPass<DrawData>("Silence perimeter", out var data, profilingSampler))
            {
                data.material = f.Material;
                data.warpPass = WarpPass;
                data.shardPass = ShardPass;
                Fill(data, cam, f);
                builder.UseTexture(copy, AccessFlags.Read);
                builder.UseAllGlobalTextures(true);
                builder.SetRenderAttachment(colour, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((DrawData d, RasterGraphContext ctx) =>
                {
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, d.warpPass, MeshTopology.Triangles, 3, 1, d.warpBlock);
                    for (int k = 0; k < d.count; k++)
                        ctx.cmd.DrawMesh(d.meshes[k], d.matrices[k], d.material, 0, d.shardPass, d.blocks[k]);
                });
            }
        }

        /// <summary>This camera's shards: posed for its aspect and field of view, sorted far to near, each with
        /// its world matrix, its centre on screen and its opacity.</summary>
        static void Fill(DrawData d, Camera cam, in Frame f)
        {
            var look3 = new Vector4(f.Glitch * f.Pulse, f.Time, f.Warp * f.Drive * f.Amount, f.Pulse * f.Amount);
            d.warpBlock.Clear();
            d.warpBlock.SetVector(Look3Id, look3);

            int n = Mathf.Min(f.Settings.Count, Mathf.Min(f.Meshes.Length, SilencePerimeter.MaxShards));
            for (int i = 0; i < n; i++)
            {
                s_poses[i] = SilencePerimeter.Layout(i, f.Settings, f.Drive, f.Pulse, f.Time, cam.aspect, cam.fieldOfView);
                s_order[i] = i;
            }
            // Far to near (insertion sort: at most 40, already nearly ordered frame to frame).
            for (int a = 1; a < n; a++)
            {
                int v = s_order[a], b = a - 1;
                while (b >= 0 && s_poses[s_order[b]].Position.z < s_poses[v].Position.z) { s_order[b + 1] = s_order[b]; b--; }
                s_order[b + 1] = v;
            }

            // Scale the arrangement about the camera so its nearest shard clears the near plane.
            float scale = Mathf.Max(1f, cam.nearClipPlane * 4f);
            var cameraToWorld = cam.transform.localToWorldMatrix * Matrix4x4.Scale(new Vector3(scale, scale, scale));
            d.count = 0;
            for (int k = 0; k < n; k++)
            {
                int i = s_order[k];
                var pose = s_poses[i];
                float alpha = Mathf.Clamp01(f.Opacity) * pose.Presence * Mathf.Clamp01(f.Amount);
                if (alpha <= 0.001f || f.Meshes[i] == null) continue;
                var local = new Matrix4x4(pose.Right, pose.Up, pose.Forward,
                                          new Vector4(pose.Position.x, pose.Position.y, pose.Position.z, 1f));
                var world = cameraToWorld * local;
                Vector3 centre = cam.WorldToViewportPoint(world.GetColumn(3));
                var block = d.blocks[d.count];
                block.Clear();
                block.SetVector(LookId, f.Look);
                block.SetVector(Look2Id, f.Look2);
                block.SetVector(Look3Id, look3);
                block.SetColor(DangerId, f.Danger);
                block.SetVector(ShardId, new Vector4(centre.x, centre.y, pose.Seed, 0f));
                block.SetFloat(AlphaId, alpha);
                d.meshes[d.count] = f.Meshes[i];
                d.matrices[d.count] = world;
                d.count++;
            }
        }

        /// <summary>Play-mode (re)entry: nothing is silenced yet; subscribe once.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            Current = default;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }
    }
}

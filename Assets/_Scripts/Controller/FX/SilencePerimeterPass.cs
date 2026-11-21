using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Draws the silence perimeter (Docs/ElementalAbilitySystem/SILENCE.md §5) on the silenced pilot's own
    /// camera: after URP has drawn the TRANSPARENTS, lay the 2D danger edge into the frame (pass 2, worked out
    /// per pixel at this frame's depth), copy the camera's colour, draw the warp (pass 0 of
    /// <c>SilencePerimeter.shader</c>) as one full-screen triangle from that copy, then every shard (pass 1) in
    /// ONE mesh: the shards are posed for this camera (<see cref="SilencePerimeter.Layout"/>), sorted far to
    /// near, and their facets written into a dynamic mesh each frame, so 100-plus overlapping shards cost one
    /// draw call. Each vertex carries its shard's centre on screen, random and opacity (TEXCOORD1).
    ///
    /// <para><b>Injected like <see cref="BlackHoleLensPass"/>.</b> No renderer feature: the pass is enqueued
    /// from <see cref="RenderPipelineManager.beginCameraRendering"/> on the ONE camera
    /// <see cref="SilenceScreenFeedback"/> names (the local pilot's), and only while that pilot is silenced,
    /// so nothing is added to any asset, no other camera pays for it, and no other machine ever draws it.</para>
    ///
    /// <para><b>Space.</b> The shards are posed in the camera's own space a few units out, then the whole
    /// arrangement is scaled about the camera to clear its near plane (which leaves the picture unchanged),
    /// and drawn over everything (ZTest Always): they belong to the screen, not the world.</para>
    /// </summary>
    public sealed class SilencePerimeterPass : ScriptableRenderPass
    {
        /// <summary>The shader's passes, in the order <c>SilencePerimeter.shader</c> declares them
        /// (<c>SilencePerimeterTests.TheShaderDeclaresItsPassesInTheOrderThePassDrawsThem</c> pins it).</summary>
        public const int WarpPass = 0;
        public const int ShardPass = 1;
        public const int EdgePass = 2;

        static readonly int SceneColorId = Shader.PropertyToID("_SilenceSceneColor");
        static readonly int LookId = Shader.PropertyToID("_SPLook");
        static readonly int Look2Id = Shader.PropertyToID("_SPLook2");
        static readonly int Look3Id = Shader.PropertyToID("_SPLook3");
        static readonly int Look4Id = Shader.PropertyToID("_SPLook4");
        static readonly int PullId = Shader.PropertyToID("_SPPull");
        static readonly int DangerId = Shader.PropertyToID("_SPDanger");
        static readonly int EdgeShapeId = Shader.PropertyToID("_SPEdgeShape");
        static readonly int EdgeShape2Id = Shader.PropertyToID("_SPEdgeShape2");
        static readonly int EdgeColorId = Shader.PropertyToID("_SPEdgeColor");

        /// <summary>One shard's facets in its own space (SilencePerimeter.Geometry), kept for the session.</summary>
        internal sealed class ShardGeometry
        {
            public Vector3[] Positions;
            public Vector3[] Normals;
            public Vector3[] Barycentrics;
        }

        /// <summary>What <see cref="SilenceScreenFeedback"/> hands over every frame. <c>Active</c> false (or no
        /// camera, material or shards) = the pass is not enqueued at all.</summary>
        internal struct Frame
        {
            public bool Active;
            public Camera Camera;
            public Material Material;
            public ShardGeometry[] Shards;
            public SilencePerimeter.Settings Settings;
            public float Drive;      // the silence's eased weight 0..1
            public float Depth;      // how much silence is left, 0 (the thin band) .. 1 (the deepest)
            public float Pulse;      // the hit flare, already x Drive
            public float Time;       // unscaled seconds
            public float Amount;     // VesselSilenceConfigSO.PerimeterAmount
            public float Opacity;
            public float Glitch;
            public float Warp;
            public float Pull;
            public float Streak;
            public float TipFade;
            public Vector4 Look;     // refraction, twist, aberration, tint
            public Vector4 Look2;    // rim glow, edge lines, fill, invert
            public Color Danger;
            public SilenceVignetteMask.Shape EdgeShape;   // VesselSilenceConfigSO.VignetteShapeAt(Depth)
            public Color EdgeColor;                       // a = the edge's opacity; 0 = no edge
        }

        internal static Frame Current;

        static SilencePerimeterPass s_pass;
        static readonly SilencePerimeter.Pose[] s_poses = new SilencePerimeter.Pose[SilencePerimeter.MaxShards];
        static readonly int[] s_order = new int[SilencePerimeter.MaxShards];

        // The one mesh every shard is written into, rebuilt each frame the perimeter draws.
        static Mesh s_mesh;
        static readonly List<Vector3> s_positions = new();
        static readonly List<Vector3> s_normals = new();
        static readonly List<Vector4> s_bary = new();
        static readonly List<Vector4> s_shard = new();
        static readonly List<int> s_indices = new();

        class CopyData
        {
            public TextureHandle source;
        }

        class EdgeData
        {
            public Material material;
            public readonly MaterialPropertyBlock block = new();
        }

        class DrawData
        {
            public Material material;
            public Mesh mesh;
            public Matrix4x4 matrix;
            public bool hasShards;
            public readonly MaterialPropertyBlock block = new();
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
            if (f.Material == null || f.Shards == null || f.Shards.Length == 0) return;
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
            if (cam == null || cam.orthographic || !f.Active || f.Material == null || f.Shards == null) return;
            TextureHandle colour = resources.activeColorTexture;

            // 0. The 2D danger edge, laid into the frame first, so the pull drags it and the facets refract it.
            if (f.EdgeColor.a > 0f)
            {
                using var builder = renderGraph.AddRasterRenderPass<EdgeData>("Silence edge", out var data, profilingSampler);
                data.material = f.Material;
                var e = f.EdgeShape;
                data.block.Clear();
                data.block.SetVector(EdgeShapeId, new Vector4(e.Style, e.Facets, e.Inner, e.Feather));
                data.block.SetVector(EdgeShape2Id, new Vector4(e.Roundness, e.Fill, e.RimStrength, e.RimWidth));
                data.block.SetColor(EdgeColorId, f.EdgeColor);
                builder.SetRenderAttachment(colour, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((EdgeData d, RasterGraphContext ctx) =>
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, EdgePass, MeshTopology.Triangles, 3, 1, d.block));
            }

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

            // 2. The warp, then every shard in one mesh, onto the camera colour.
            using (var builder = renderGraph.AddRasterRenderPass<DrawData>("Silence perimeter", out var data, profilingSampler))
            {
                data.material = f.Material;
                Fill(data, cam, f);
                builder.UseTexture(copy, AccessFlags.Read);
                builder.UseAllGlobalTextures(true);
                builder.SetRenderAttachment(colour, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((DrawData d, RasterGraphContext ctx) =>
                {
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, d.material, WarpPass, MeshTopology.Triangles, 3, 1, d.block);
                    if (d.hasShards) ctx.cmd.DrawMesh(d.mesh, d.matrix, d.material, 0, ShardPass, d.block);
                });
            }
        }

        /// <summary>This camera's frame: the look, the pull point, and every visible shard posed for its aspect and
        /// field of view, sorted far to near and written into the one mesh in that order.</summary>
        static void Fill(DrawData d, Camera cam, in Frame f)
        {
            var pull = SilencePerimeter.PullPoint(f.Settings, f.Time);
            d.block.Clear();
            d.block.SetVector(LookId, f.Look);
            d.block.SetVector(Look2Id, f.Look2);
            d.block.SetVector(Look3Id, new Vector4(f.Glitch * f.Pulse, f.Time, f.Warp * f.Drive * f.Amount, f.Pulse * f.Amount));
            d.block.SetVector(Look4Id, new Vector4(f.Pull * f.Drive * f.Amount, f.Pull * f.Drive, f.Streak, f.TipFade));
            d.block.SetVector(PullId, new Vector4(pull.x, pull.y, 0f, 0f));
            d.block.SetColor(DangerId, f.Danger);

            int n = Mathf.Min(f.Settings.Count, Mathf.Min(f.Shards.Length, SilencePerimeter.MaxShards));
            for (int i = 0; i < n; i++)
            {
                s_poses[i] = SilencePerimeter.Layout(i, f.Settings, f.Drive, f.Depth, f.Pulse, f.Time, cam.aspect, cam.fieldOfView);
                s_order[i] = i;
            }
            // Far to near (insertion sort: nearly ordered frame to frame).
            for (int a = 1; a < n; a++)
            {
                int v = s_order[a], b = a - 1;
                while (b >= 0 && s_poses[s_order[b]].Position.z < s_poses[v].Position.z) { s_order[b + 1] = s_order[b]; b--; }
                s_order[b + 1] = v;
            }

            // Scale the arrangement about the camera so its nearest shard clears the near plane.
            float scale = Mathf.Max(1f, cam.nearClipPlane * 4f);
            d.matrix = cam.transform.localToWorldMatrix * Matrix4x4.Scale(new Vector3(scale, scale, scale));
            var projection = cam.projectionMatrix;

            s_positions.Clear(); s_normals.Clear(); s_bary.Clear(); s_shard.Clear(); s_indices.Clear();
            for (int k = 0; k < n; k++)
            {
                int i = s_order[k];
                var pose = s_poses[i];
                var geometry = f.Shards[i];
                float alpha = Mathf.Clamp01(f.Opacity) * pose.Presence * Mathf.Clamp01(f.Amount);
                if (alpha <= 0.001f || geometry == null || s_positions.Count + geometry.Positions.Length > 65000) continue;
                // The shard's centre on screen: the camera's own projection of its centre (camera space has +z
                // forward here; the projection matrix wants the view space's -z).
                var clip = projection * new Vector4(pose.Position.x, pose.Position.y, -pose.Position.z, 1f);
                var shard = new Vector4(clip.x / clip.w * 0.5f + 0.5f, clip.y / clip.w * 0.5f + 0.5f, pose.Seed, alpha);
                float len = pose.Up.magnitude;
                for (int j = 0; j < geometry.Positions.Length; j++)
                {
                    var p = geometry.Positions[j];
                    var m = geometry.Normals[j];
                    var b = geometry.Barycentrics[j];
                    s_indices.Add(s_positions.Count);
                    s_positions.Add(pose.Right * p.x + pose.Up * p.y + pose.Forward * p.z + pose.Position);
                    s_normals.Add((pose.Right * m.x + pose.Up * m.y + pose.Forward * m.z) / Mathf.Max(1e-6f, len));
                    s_bary.Add(new Vector4(b.x, b.y, b.z, p.y));
                    s_shard.Add(shard);
                }
            }

            d.hasShards = s_indices.Count > 0;
            if (!d.hasShards) return;
            if (s_mesh == null)
            {
                s_mesh = new Mesh { name = "SilencePerimeter", hideFlags = HideFlags.DontSave };
                s_mesh.MarkDynamic();
            }
            s_mesh.Clear();
            s_mesh.SetVertices(s_positions);
            s_mesh.SetNormals(s_normals);
            s_mesh.SetUVs(0, s_bary);
            s_mesh.SetUVs(1, s_shard);
            s_mesh.SetIndices(s_indices, MeshTopology.Triangles, 0, false);
            // Drawn straight from a command buffer, so never culled; bounds only need to be sane.
            s_mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
            d.mesh = s_mesh;
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

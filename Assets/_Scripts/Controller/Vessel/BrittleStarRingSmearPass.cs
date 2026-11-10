using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Brittle Star ring's MOTION SMEAR (Garrett, 2026-11-03; lab <c>Docs/Studios/BrittleStarRingLab.html</c>,
    /// "Forward + motion smear", normalized so it adds no brightness). While the ring turns fast, the hull stops
    /// drawing it (<see cref="BrittleStarHullSource.SetRingHidden"/>) and this pass draws it instead, as the AVERAGE of
    /// copies spread back over the angle it turned this frame (a box shutter):
    /// <list type="number">
    /// <item><b>The ring layer.</b> The ring as it stands (baked from the hull, <see cref="BrittleStarAnimation"/>),
    /// drawn with the hull's OWN materials and property blocks into a cleared single-sample RGBA16F texture with its
    /// own depth. An opaque part writes (colour, 1) and the transparent body blends over it, so the layer holds
    /// premultiplied colour with coverage in alpha.</item>
    /// <item><b>The smear.</b> A disc in the ring's plane, depth-tested against the scene (so what is in front of the
    /// ring still hides it), whose every pixel averages the layer at that point turned back onto each copy
    /// (<c>BrittleStarRingSmear.shader</c>; the turn per copy is <see cref="Request.Back"/>), weight 1 / copies, in
    /// float, and blends the premultiplied mean over the frame once (One, OneMinusSrcAlpha). Where every copy
    /// covers a pixel it is exactly the still ring; nowhere is it brighter.</item>
    /// </list>
    ///
    /// <para><b>Why not draw the copies straight into the frame.</b> Copies summed at weight 1 / n into an 8-bit
    /// target round every small contribution and brighten (+9% at 15 copies, +18% at 24, measured in the lab);
    /// copies summed additively also add each copy's own overlapping parts. The average here is taken in the
    /// shader's float registers and written once.</para>
    ///
    /// <para><b>Injected like <see cref="VortexRingLensPass"/>.</b> No renderer feature: enqueued from
    /// <see cref="RenderPipelineManager.beginCameraRendering"/> on every base game camera (and the Scene view) while
    /// any ring smears, and a camera whose culling mask leaves the hull's layer out draws nothing. A request no
    /// camera drew is noticed by <see cref="BrittleStarAnimation"/>, which shows the ring again.</para>
    ///
    /// <para><b>Stated limits.</b> The rotation is applied to points on the ring's PLANE, so parts that stand off
    /// it (the pods' tails, the barrels) smear with a little parallax. A TRANSPARENT drawn in front of the ring is
    /// overdrawn by the smear (it runs after the transparents).</para>
    ///
    /// <para><b>Never an empty layer (2026-11-10).</b> Each material is drawn with its <see cref="ForwardPass"/>, which
    /// must accept an UNTAGGED pass: the hull's Unlit Shader Graph has no LightMode tag, and before this the layer drew
    /// nothing, so the hidden ring vanished while it turned. A request with nothing drawable is now skipped without
    /// being marked drawn, so <see cref="BrittleStarAnimation"/> falls back to the hull's own ring.</para>
    /// </summary>
    public sealed class BrittleStarRingSmearPass : ScriptableRenderPass
    {
        /// <summary>The LightMode of the smear shader's pass: drawn by this pass and nothing else.</summary>
        public const string LightModeName = "BrittleStarRingSmear";
        /// <summary>The most copies one smear averages (the shader's array size).</summary>
        public const int MaxCopies = 32;

        static readonly int LayerId = Shader.PropertyToID("_BrittleStarRingLayer");
        static readonly int BackId = Shader.PropertyToID("_RSBack");
        static readonly int CountId = Shader.PropertyToID("_RSCount");
        static readonly int SizeId = Shader.PropertyToID("_RSLayerSize");
        static readonly ShaderTagId LightModeTag = new("LightMode");
        static readonly ShaderTagId[] ForwardModes =
            { new("UniversalForward"), new("UniversalForwardOnly"), new("SRPDefaultUnlit") };

        /// <summary>One ring's smear this frame, filled by <see cref="BrittleStarAnimation"/> in LateUpdate.</summary>
        public sealed class Request
        {
            public Mesh Ring;                                   // the ring as it stands, in RingMatrix's space
            public Matrix4x4 RingMatrix;
            public readonly List<Material> Materials = new();   // the hull renderer's, one per submesh
            public MaterialPropertyBlock[] Blocks = new MaterialPropertyBlock[0];
            public int Layer;                                   // the hull renderer's layer (camera culling)
            public Matrix4x4 DiscMatrix;                        // a unit disc in XY onto the ring's plane
            public readonly Matrix4x4[] Back = new Matrix4x4[MaxCopies];   // copy k's point -> the ring as drawn
            public int Copies;
            public int SubmittedFrame = -1;
            public int DrawnFrame = -1;
        }

        static readonly List<Request> s_active = new();
        static BrittleStarRingSmearPass s_pass;
        static Material s_material;
        static Shader s_materialShader;
        static Mesh s_disc;
        static readonly Dictionary<Material, int> s_forwardPass = new();
        static readonly List<int> s_order = new();

        class LayerData
        {
            public Request request;
            public readonly List<int> order = new();
            public readonly List<int> passes = new();
        }

        class SmearData
        {
            public Request request;
            public Material material;
            public Mesh disc;
            public Vector4 size;
            public readonly MaterialPropertyBlock block = new();
        }

        BrittleStarRingSmearPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            requiresIntermediateTexture = true;   // draws into the camera colour, which the backbuffer is not
            profilingSampler = new ProfilingSampler("BrittleStarRingSmear");
        }

        /// <summary>Can the smear draw with <paramref name="shader"/> on this pipeline? False = the ring is drawn as it
        /// stands (the caller never hides it).</summary>
        public static bool CanDraw(Shader shader) =>
            shader && shader.isSupported && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;

        /// <summary>Draw <paramref name="request"/> this frame (and every frame until <see cref="Withdraw"/>).</summary>
        public static void Submit(Request request, Shader shader)
        {
            if (request == null) return;
            if (s_material == null || s_materialShader != shader)
            {
                if (s_material) Object.Destroy(s_material);
                s_material = shader ? new Material(shader) { name = "BrittleStarRingSmear", hideFlags = HideFlags.DontSave } : null;
                s_materialShader = shader;
            }
            request.SubmittedFrame = Time.frameCount;
            if (!s_active.Contains(request)) s_active.Add(request);
        }

        public static void Withdraw(Request request)
        {
            if (request != null) s_active.Remove(request);
        }

        static void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (s_active.Count == 0 || cam == null || s_material == null) return;
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
            s_pass ??= new BrittleStarRingSmearPass();
            renderer.EnqueuePass(s_pass);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer || s_material == null) return;
            var cameraData = frameData.Get<UniversalCameraData>();
            var cam = cameraData.camera;
            if (cam == null) return;
            s_disc ??= BuildDisc();

            var target = cameraData.cameraTargetDescriptor;
            var colourDesc = target;
            colourDesc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            colourDesc.depthStencilFormat = GraphicsFormat.None;
            colourDesc.msaaSamples = 1;
            colourDesc.bindMS = false;
            var depthDesc = target;
            depthDesc.graphicsFormat = GraphicsFormat.None;
            depthDesc.depthStencilFormat = GraphicsFormat.D32_SFloat;
            depthDesc.msaaSamples = 1;
            depthDesc.bindMS = false;
            var size = new Vector4(target.width, target.height, 1f / Mathf.Max(1, target.width), 1f / Mathf.Max(1, target.height));

            for (int r = 0; r < s_active.Count; r++)
            {
                var request = s_active[r];
                if (request.Ring == null || request.Copies < 2 || request.SubmittedFrame < Time.frameCount - 1) continue;
                if ((cam.cullingMask & (1 << request.Layer)) == 0) continue;
                // Nothing the layer could draw: the smear would average an EMPTY layer while the hull has hidden its ring,
                // and the ring would vanish whenever it turned (what bleeding-edge did until 2026-11-10, see ForwardPass).
                // DrawnFrame stays behind, so BrittleStarAnimation shows the ring again and warns.
                if (!HasDrawableSubmesh(request)) continue;

                TextureHandle layer = UniversalRenderer.CreateRenderGraphTexture(renderGraph, colourDesc,
                    "_BrittleStarRingLayer", true, FilterMode.Bilinear, TextureWrapMode.Clamp);
                TextureHandle layerDepth = UniversalRenderer.CreateRenderGraphTexture(renderGraph, depthDesc,
                    "_BrittleStarRingLayerDepth", true, FilterMode.Point, TextureWrapMode.Clamp);

                // 1. The ring as it stands, with the hull's own materials: opaque parts first, then the transparent.
                using (var builder = renderGraph.AddRasterRenderPass<LayerData>("BrittleStar ring layer", out var data, profilingSampler))
                {
                    data.request = request;
                    FillOrder(request, data.order, data.passes);
                    builder.SetRenderAttachment(layer, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(layerDepth, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(layer, LayerId);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((LayerData d, RasterGraphContext ctx) =>
                    {
                        var q = d.request;
                        for (int i = 0; i < d.order.Count; i++)
                        {
                            int sub = d.order[i];
                            var block = sub < q.Blocks.Length ? q.Blocks[sub] : null;
                            ctx.cmd.DrawMesh(q.Ring, q.RingMatrix, q.Materials[sub], sub, d.passes[i], block);
                        }
                    });
                }

                // 2. The average of the copies, over the frame.
                using (var builder = renderGraph.AddRasterRenderPass<SmearData>("BrittleStar ring smear", out var data, profilingSampler))
                {
                    data.request = request;
                    data.material = s_material;
                    data.disc = s_disc;
                    data.size = size;
                    builder.UseTexture(layer, AccessFlags.Read);
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((SmearData d, RasterGraphContext ctx) =>
                    {
                        var q = d.request;
                        d.block.Clear();
                        d.block.SetMatrixArray(BackId, q.Back);
                        d.block.SetFloat(CountId, q.Copies);
                        d.block.SetVector(SizeId, d.size);
                        ctx.cmd.DrawMesh(d.disc, q.DiscMatrix, d.material, 0, 0, d.block);
                    });
                }
                request.DrawnFrame = Time.frameCount;
            }
        }

        /// <summary>The submeshes the ring layer draws, in render-queue order (opaque before transparent), with each
        /// material's forward pass. A material without one is skipped.</summary>
        static void FillOrder(Request request, List<int> order, List<int> passes)
        {
            order.Clear();
            passes.Clear();
            s_order.Clear();
            int subMeshes = Mathf.Min(request.Ring.subMeshCount, request.Materials.Count);
            for (int s = 0; s < subMeshes; s++)
                if (request.Materials[s] && request.Ring.GetSubMesh(s).indexCount > 0) s_order.Add(s);
            s_order.Sort((a, b) => request.Materials[a].renderQueue.CompareTo(request.Materials[b].renderQueue));
            foreach (int s in s_order)
            {
                int pass = ForwardPass(request.Materials[s]);
                if (pass < 0) continue;
                order.Add(s);
                passes.Add(pass);
            }
        }

        /// <summary>Does <paramref name="request"/>'s ring have at least one submesh the layer can draw (a material with a
        /// <see cref="ForwardPass"/>)? The same test <see cref="FillOrder"/> applies, asked before anything is recorded.</summary>
        static bool HasDrawableSubmesh(Request request)
        {
            int subMeshes = Mathf.Min(request.Ring.subMeshCount, request.Materials.Count);
            for (int s = 0; s < subMeshes; s++)
                if (request.Materials[s] && request.Ring.GetSubMesh(s).indexCount > 0 && ForwardPass(request.Materials[s]) >= 0)
                    return true;
            return false;
        }

        /// <summary>
        /// The pass URP's forward renderer draws <paramref name="material"/> with: one tagged UniversalForward,
        /// UniversalForwardOnly or SRPDefaultUnlit, else the first pass with NO LightMode tag, which URP draws as
        /// SRPDefaultUnlit. -1 = none.
        /// <para><b>Why the untagged case (2026-11-10).</b> The hull wears <c>VesselGraph</c>, an Unlit Shader Graph, and
        /// URP 17's generator writes an Unlit graph's forward pass with no LightMode tag at all (UniversalUnlitSubTarget
        /// sets none; ShaderGraph's Generator then writes <c>// LightMode: &lt;None&gt;</c>), so
        /// <see cref="Shader.FindPassTagValue"/> answers <see cref="ShaderTagId.none"/>. Matching the tags alone found no
        /// pass on any of the hull's materials: the ring layer came out empty, the smear averaged nothing, and the ring,
        /// which the hull had already hidden, vanished whenever it turned more than <c>ringSmearMinDegrees</c> a frame
        /// (1.5° in BrittleStarConfig.asset). Garrett: "too dark to see".</para>
        /// </summary>
        public static int ForwardPass(Material material)
        {
            if (!material) return -1;
            if (s_forwardPass.TryGetValue(material, out int cached)) return cached;
            int found = -1, untagged = -1;
            var shader = material.shader;
            for (int p = 0; shader && p < shader.passCount && found < 0; p++)
            {
                var mode = shader.FindPassTagValue(p, LightModeTag);
                if (mode == ShaderTagId.none)
                {
                    if (untagged < 0) untagged = p;
                    continue;
                }
                for (int m = 0; m < ForwardModes.Length; m++)
                    if (mode == ForwardModes[m]) { found = p; break; }
            }
            if (found < 0) found = untagged;
            s_forwardPass[material] = found;
            return found;
        }

        /// <summary>A unit disc in the XY plane, facing both ways (the shader culls nothing).</summary>
        static Mesh BuildDisc()
        {
            const int segments = 64;
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }
            var mesh = new Mesh { name = "BrittleStarRingSmearDisc", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(2f, 2f, 0.01f));
            return mesh;
        }

        /// <summary>Play-mode (re)entry: subscribe once, and forget the last session's requests.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            s_active.Clear();
            s_forwardPass.Clear();
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }
    }
}

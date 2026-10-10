using System;
using System.Collections.Generic;

// URP's script-injected render passes and the render graph they record into (Unity 6), and the
// CommandBuffer a script fills by hand. Original contracts, so a game pass COMPILES and runs its
// bookkeeping in the port. What the port does NOT do yet: execute them. A pass enqueued on a renderer is
// counted (ScriptableRenderer.EnqueuedPasses) and dropped at the end of the frame; RecordRenderGraph is
// never called, and a command buffer records nothing. The renderer draws every object through its own
// pipeline instead, so an effect that lives only in such a pass (the black hole's lens, which draws in
// its own LightMode) is not drawn in Prisma until the port's renderer gains a pass-injection point.
namespace CosmicShore.Engine.Rendering
{
    /// <summary>Where in URP's frame an injected pass runs (UnityEngine.Rendering.Universal.RenderPassEvent).</summary>
    public enum RenderPassEvent
    {
        BeforeRendering = 0, BeforeRenderingShadows = 50, AfterRenderingShadows = 100, BeforeRenderingPrePasses = 150,
        AfterRenderingPrePasses = 200, BeforeRenderingGbuffer = 210, AfterRenderingGbuffer = 220,
        BeforeRenderingDeferredLights = 230, AfterRenderingDeferredLights = 240, BeforeRenderingOpaques = 250,
        AfterRenderingOpaques = 300, BeforeRenderingSkybox = 350, AfterRenderingSkybox = 400,
        BeforeRenderingTransparents = 450, AfterRenderingTransparents = 500, BeforeRenderingPostProcessing = 550,
        AfterRenderingPostProcessing = 600, AfterRendering = 1000,
    }

    /// <summary>A named profiler scope for a pass (UnityEngine.Rendering.ProfilingSampler).</summary>
    public class ProfilingSampler
    {
        public string name { get; }
        public ProfilingSampler(string name) { this.name = name; }
        public static ProfilingSampler Get<T>(T marker) where T : Enum => new(marker.ToString());
    }

    /// <summary>A shader pass's LightMode tag value (UnityEngine.Rendering.ShaderTagId).</summary>
    public readonly struct ShaderTagId : IEquatable<ShaderTagId>
    {
        public readonly string name;
        public ShaderTagId(string name) { this.name = name ?? ""; }
        public static readonly ShaderTagId none = new("");
        public bool Equals(ShaderTagId other) => name == other.name;
        public override bool Equals(object obj) => obj is ShaderTagId t && Equals(t);
        public override int GetHashCode() => name?.GetHashCode() ?? 0;
    }

    /// <summary>A script-written URP pass (UnityEngine.Rendering.Universal.ScriptableRenderPass).</summary>
    public abstract class ScriptableRenderPass
    {
        public RenderPassEvent renderPassEvent { get; set; } = RenderPassEvent.AfterRenderingOpaques;
        public bool requiresIntermediateTexture { get; set; }
        public ProfilingSampler profilingSampler { get; set; }
        public virtual void RecordRenderGraph(RenderGraphModule.RenderGraph renderGraph, ContextContainer frameData) { }
        public void ConfigureInput(ScriptableRenderPassInput passInput) { }
    }

    [Flags] public enum ScriptableRenderPassInput { None = 0, Depth = 1, Normal = 2, Color = 4, Motion = 8 }

    /// <summary>A camera's URP renderer (UnityEngine.Rendering.Universal.ScriptableRenderer).</summary>
    public class ScriptableRenderer
    {
        readonly List<ScriptableRenderPass> _queued = new();
        /// <summary>Passes enqueued since the last frame (the port does not execute them; see the file header).</summary>
        public IReadOnlyList<ScriptableRenderPass> EnqueuedPasses => _queued;
        public void EnqueuePass(ScriptableRenderPass pass) { if (pass != null) _queued.Add(pass); }
        /// <summary>Port side: the renderer clears the queue after drawing a camera.</summary>
        public void ClearEnqueuedPasses() => _queued.Clear();
    }

    /// <summary>URP's static entry (UnityEngine.Rendering.Universal.UniversalRenderPipeline).</summary>
    public static class UniversalRenderPipeline
    {
        public static UniversalRenderPipelineAsset asset => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
    }

    /// <summary>The frame's data blocks, keyed by type (UnityEngine.Rendering.ContextContainer).</summary>
    public class ContextContainer
    {
        readonly Dictionary<Type, object> _items = new();
        public T Get<T>() where T : class, new()
        {
            if (!_items.TryGetValue(typeof(T), out var v)) { v = new T(); _items[typeof(T)] = v; }
            return (T)v;
        }
        public bool Contains<T>() where T : class => _items.ContainsKey(typeof(T));
    }

    public class UniversalResourceData
    {
        /// <summary>True in the port: there is no intermediate colour target a pass could read, so a pass that checks this stands down.</summary>
        public bool isActiveTargetBackBuffer { get; set; } = true;
        public RenderGraphModule.TextureHandle activeColorTexture { get; set; }
        public RenderGraphModule.TextureHandle activeDepthTexture { get; set; }
        public RenderGraphModule.TextureHandle cameraColor { get; set; }
        public RenderGraphModule.TextureHandle cameraDepthTexture { get; set; }
        public RenderGraphModule.TextureHandle cameraOpaqueTexture { get; set; }
    }

    public class UniversalCameraData { public Camera camera { get; set; } }
    public class UniversalRenderingData { public CullingResults cullResults { get; set; } }
    public class UniversalLightData { }

    /// <summary>A camera's culled renderers (UnityEngine.Rendering.CullingResults); opaque here.</summary>
    public struct CullingResults { }

    [Flags]
    public enum SortingCriteria
    {
        None = 0, SortingLayer = 1, RenderQueue = 2, BackToFront = 4, QuantizedFrontToBack = 8, OptimizeStateChanges = 16,
        CanvasOrder = 32, RendererPriority = 64,
        CommonOpaque = SortingLayer | RenderQueue | QuantizedFrontToBack | OptimizeStateChanges | CanvasOrder,
        CommonTransparent = SortingLayer | RenderQueue | BackToFront | OptimizeStateChanges,
    }

    public struct DrawingSettings { public ShaderTagId shaderTag; public SortingCriteria sorting; }

    public readonly struct RenderQueueRange
    {
        public readonly int lowerBound, upperBound;
        public RenderQueueRange(int lowerBound, int upperBound) { this.lowerBound = lowerBound; this.upperBound = upperBound; }
        public static RenderQueueRange all => new(0, 5000);
        public static RenderQueueRange opaque => new(0, 2500);
        public static RenderQueueRange transparent => new(2501, 5000);
    }

    public struct FilteringSettings
    {
        public RenderQueueRange renderQueueRange;
        public int layerMask;
        public FilteringSettings(RenderQueueRange? renderQueueRange = null, int layerMask = -1)
        { this.renderQueueRange = renderQueueRange ?? RenderQueueRange.all; this.layerMask = layerMask; }
    }

    public struct RendererListParams
    {
        public CullingResults cullingResults; public DrawingSettings drawSettings; public FilteringSettings filteringSettings;
        public RendererListParams(CullingResults cullingResults, DrawingSettings drawSettings, FilteringSettings filteringSettings)
        { this.cullingResults = cullingResults; this.drawSettings = drawSettings; this.filteringSettings = filteringSettings; }
    }

    public static class RenderingUtils
    {
        public static DrawingSettings CreateDrawingSettings(ShaderTagId shaderTagId, UniversalRenderingData renderingData,
            UniversalCameraData cameraData, UniversalLightData lightData, SortingCriteria sortingCriteria)
            => new() { shaderTag = shaderTagId, sorting = sortingCriteria };
    }

    public enum MSAASamples { None = 1, MSAA2x = 2, MSAA4x = 4, MSAA8x = 8 }
    public enum CubemapFace { Unknown = -1, PositiveX = 0, NegativeX = 1, PositiveY = 2, NegativeY = 3, PositiveZ = 4, NegativeZ = 5 }
    public enum BuiltinRenderTextureType { None = 0, CurrentActive = 1, CameraTarget = 2, Depth = 3 }

    /// <summary>Where a command writes (UnityEngine.Rendering.RenderTargetIdentifier).</summary>
    public readonly struct RenderTargetIdentifier
    {
        public readonly Texture texture; public readonly BuiltinRenderTextureType builtin;
        RenderTargetIdentifier(Texture t, BuiltinRenderTextureType b) { texture = t; builtin = b; }
        public static implicit operator RenderTargetIdentifier(Texture t) => new(t, BuiltinRenderTextureType.None);
        public static implicit operator RenderTargetIdentifier(BuiltinRenderTextureType b) => new(null, b);
    }

    /// <summary>
    /// A list of rendering commands (UnityEngine.Rendering.CommandBuffer). The port records nothing:
    /// <see cref="Graphics.ExecuteCommandBuffer"/> is a no-op, so a script that renders through one (the black
    /// hole's sky capture) leaves its target as it was.
    /// </summary>
    public class CommandBuffer : IDisposable
    {
        public string name { get; set; } = "";
        public int sizeInBytes => 0;
        public void Clear() { }
        public void SetRenderTarget(RenderTargetIdentifier rt) { }
        public void SetRenderTarget(RenderTargetIdentifier rt, int mipLevel) { }
        public void SetRenderTarget(RenderTargetIdentifier rt, int mipLevel, CubemapFace cubemapFace) { }
        public void SetRenderTarget(RenderTargetIdentifier rt, int mipLevel, CubemapFace cubemapFace, int depthSlice) { }
        public void ClearRenderTarget(bool clearDepth, bool clearColor, Color backgroundColor, float depth = 1f) { }
        public void SetViewProjectionMatrices(Matrix4x4 view, Matrix4x4 proj) { }
        public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int submeshIndex = 0, int shaderPass = -1) { }
        public void SetGlobalTexture(int nameID, RenderTargetIdentifier value) { }
        public void SetGlobalFloat(int nameID, float value) { }
        public void SetGlobalVector(int nameID, Vector4 value) { }
        public void Dispose() { }
    }

    /// <summary>The command list a raster render-graph pass records into.</summary>
    public class RasterCommandBuffer
    {
        public void DrawRendererList(RenderGraphModule.RendererListHandle rendererList) { }
        public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int submeshIndex = 0, int shaderPass = -1) { }
        // A procedural draw (the black hole lens's one full-screen triangle). Recorded like the other raster
        // commands, not drawn: the lens shader has no translation yet (Port/docs/ROADMAP.md item 14).
        public void DrawProcedural(Matrix4x4 matrix, Material material, int shaderPass, MeshTopology topology, int vertexCount,
            int instanceCount, MaterialPropertyBlock properties) { }
        public void DrawProcedural(Matrix4x4 matrix, Material material, int shaderPass, MeshTopology topology, int vertexCount,
            int instanceCount = 1) { }
        public void SetGlobalFloat(int nameID, float value) { }
    }

    /// <summary>Full-screen copies (UnityEngine.Rendering.Blitter).</summary>
    public static class Blitter
    {
        public static void BlitTexture(RasterCommandBuffer cmd, RenderGraphModule.TextureHandle source, Vector4 scaleBias, float mipLevel, bool bilinear) { }
        public static void BlitTexture(RasterCommandBuffer cmd, RenderGraphModule.TextureHandle source, Vector4 scaleBias, Material material, int pass) { }
    }
}

namespace CosmicShore.Engine.Rendering.RenderGraphModule
{
    [Flags] public enum AccessFlags { None = 0, Read = 1, Write = 2, Discard = 4, WriteAll = Write | Discard, ReadWrite = Read | Write }

    public readonly struct TextureHandle
    {
        readonly int _id;
        internal TextureHandle(int id) { _id = id; }
        public bool IsValid() => _id != 0;
        public static TextureHandle nullHandle => default;
    }

    public readonly struct RendererListHandle
    {
        readonly int _id;
        internal RendererListHandle(int id) { _id = id; }
        public bool IsValid() => _id != 0;
    }

    public struct TextureDesc
    {
        public string name; public int width, height; public MSAASamples msaaSamples; public bool bindTextureMS, clearBuffer;
        public FilterMode filterMode; public TextureWrapMode wrapMode; public Color clearColor;
    }

    public delegate void BaseRenderFunc<in TPassData, in TRenderGraphContext>(TPassData data, TRenderGraphContext renderGraphContext) where TPassData : class, new();

    public class RasterGraphContext { public RasterCommandBuffer cmd { get; } = new(); }

    public interface IRasterRenderGraphBuilder : IDisposable
    {
        void UseTexture(in TextureHandle input, AccessFlags flags = AccessFlags.Read);
        void UseRendererList(in RendererListHandle input);
        void UseAllGlobalTextures(bool enable);
        void SetRenderAttachment(TextureHandle tex, int index, AccessFlags flags = AccessFlags.Write);
        void SetRenderAttachmentDepth(TextureHandle tex, AccessFlags flags = AccessFlags.Write);
        void SetGlobalTextureAfterPass(in TextureHandle input, int propertyId);
        void AllowPassCulling(bool value);
        void AllowGlobalStateModification(bool value);
        void SetRenderFunc<PassData>(BaseRenderFunc<PassData, RasterGraphContext> renderFunc) where PassData : class, new();
    }

    /// <summary>
    /// The frame graph a pass records into (UnityEngine.Rendering.RenderGraphModule.RenderGraph). Records
    /// handles and builders so a pass's own logic runs; the port never executes a recorded pass (see the
    /// header of this file).
    /// </summary>
    public class RenderGraph
    {
        int _next = 1;
        public TextureDesc GetTextureDesc(in TextureHandle texture) => default;
        public TextureHandle CreateTexture(in TextureDesc desc) => new(_next++);
        public RendererListHandle CreateRendererList(in RendererListParams desc) => new(_next++);
        public IRasterRenderGraphBuilder AddRasterRenderPass<PassData>(string passName, out PassData passData, ProfilingSampler sampler = null) where PassData : class, new()
        { passData = new PassData(); return new Builder(); }

        sealed class Builder : IRasterRenderGraphBuilder
        {
            public void UseTexture(in TextureHandle input, AccessFlags flags = AccessFlags.Read) { }
            public void UseRendererList(in RendererListHandle input) { }
            public void UseAllGlobalTextures(bool enable) { }
            public void SetRenderAttachment(TextureHandle tex, int index, AccessFlags flags = AccessFlags.Write) { }
            public void SetRenderAttachmentDepth(TextureHandle tex, AccessFlags flags = AccessFlags.Write) { }
            public void SetGlobalTextureAfterPass(in TextureHandle input, int propertyId) { }
            public void AllowPassCulling(bool value) { }
            public void AllowGlobalStateModification(bool value) { }
            public void SetRenderFunc<PassData>(BaseRenderFunc<PassData, RasterGraphContext> renderFunc) where PassData : class, new() { }
            public void Dispose() { }
        }
    }
}

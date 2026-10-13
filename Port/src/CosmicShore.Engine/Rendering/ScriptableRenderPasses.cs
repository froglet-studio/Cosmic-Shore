using System;
using System.Collections.Generic;
using CosmicShore.Engine.Rendering.RenderGraphModule;

namespace CosmicShore.Engine.Rendering
{
    // ─────────────────────────────────────────────────────────────────────────
    // URP's script-injected render passes and the command buffers they record (original contract:
    // UnityEngine.Rendering + UnityEngine.Rendering.Universal + the RenderGraph module).
    //
    // A game script enqueues a ScriptableRenderPass on a camera's renderer in beginCameraRendering
    // and records its work in RecordRenderGraph. The port's renderer draws every camera itself and
    // has no render graph: an enqueued pass is kept on the renderer (ScriptableRenderer.QueuedPasses,
    // cleared when the camera starts) for a backend to read, and RecordRenderGraph is never called,
    // so the pass's drawing does not happen. A CommandBuffer records its commands and
    // Graphics.ExecuteCommandBuffer hands it to ExecuteCommandBufferHook, which no backend installs
    // yet. Both are stated gaps (docs/ROADMAP.md), the way texture arrays are.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>A shader pass's LightMode tag value.</summary>
    public readonly struct ShaderTagId : IEquatable<ShaderTagId>
    {
        public static readonly ShaderTagId none = default;
        public string name { get; }
        public ShaderTagId(string name) => this.name = name;
        public bool Equals(ShaderTagId other) => name == other.name;
        public override bool Equals(object obj) => obj is ShaderTagId o && Equals(o);
        public override int GetHashCode() => name?.GetHashCode() ?? 0;
        public static bool operator ==(ShaderTagId a, ShaderTagId b) => a.Equals(b);
        public static bool operator !=(ShaderTagId a, ShaderTagId b) => !a.Equals(b);
        public override string ToString() => name;
    }

    /// <summary>A named profiler scope for a pass.</summary>
    public class ProfilingSampler
    {
        public string name { get; }
        public bool enableRecording { get; set; }
        public ProfilingSampler(string name) => this.name = name;
        public static ProfilingSampler Get<TEnum>(TEnum marker) where TEnum : Enum => new(marker.ToString());
    }

    public enum MSAASamples { None = 1, MSAA2x = 2, MSAA4x = 4, MSAA8x = 8 }

    public enum CubemapFace { Unknown = -1, PositiveX = 0, NegativeX = 1, PositiveY = 2, NegativeY = 3, PositiveZ = 4, NegativeZ = 5 }

    public enum BuiltinRenderTextureType
    {
        PropertyName = -4, BufferPtr = -3, RenderTexture = -2, BindableTexture = -1, None = 0,
        CurrentActive = 1, CameraTarget = 2, Depth = 3, DepthNormals = 4, ResolvedDepth = 5,
        PrepassNormalsSpec = 7, PrepassLight = 8, PrepassLightSpec = 9, GBuffer0 = 10, GBuffer1 = 11,
        GBuffer2 = 12, GBuffer3 = 13, Reflections = 14, MotionVectors = 15, GBuffer4 = 16, GBuffer5 = 17,
        GBuffer6 = 18, GBuffer7 = 19,
    }

    /// <summary>What a command renders into: a built-in target, a texture, or a shader property id.</summary>
    public readonly struct RenderTargetIdentifier : IEquatable<RenderTargetIdentifier>
    {
        public BuiltinRenderTextureType type { get; }
        public Texture texture { get; }
        public int nameID { get; }

        public RenderTargetIdentifier(BuiltinRenderTextureType type) { this.type = type; texture = null; nameID = -1; }
        public RenderTargetIdentifier(Texture texture) { type = BuiltinRenderTextureType.RenderTexture; this.texture = texture; nameID = -1; }
        public RenderTargetIdentifier(int nameID) { type = BuiltinRenderTextureType.PropertyName; texture = null; this.nameID = nameID; }
        public RenderTargetIdentifier(string name) : this(Shader.PropertyToID(name)) { }

        public static implicit operator RenderTargetIdentifier(BuiltinRenderTextureType type) => new(type);
        public static implicit operator RenderTargetIdentifier(Texture texture) => new(texture);
        public static implicit operator RenderTargetIdentifier(RenderTexture texture) => new(texture);
        public static implicit operator RenderTargetIdentifier(int nameID) => new(nameID);
        public static implicit operator RenderTargetIdentifier(string name) => new(name);

        public bool Equals(RenderTargetIdentifier o) => type == o.type && texture == o.texture && nameID == o.nameID;
        public override bool Equals(object obj) => obj is RenderTargetIdentifier o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(type, texture, nameID);
    }

    /// <summary>A render target the pipeline manages (RTHandle); a TextureHandle converts to one.</summary>
    public class RTHandle
    {
        public RenderTexture rt { get; internal set; }
        public RenderTargetIdentifier nameID { get; internal set; }
        public string name { get; internal set; }
        public static implicit operator RenderTargetIdentifier(RTHandle h) => h?.nameID ?? default;
        public static implicit operator Texture(RTHandle h) => h?.rt;
        public static implicit operator RenderTexture(RTHandle h) => h?.rt;
    }

    /// <summary>A render queue interval.</summary>
    public struct RenderQueueRange
    {
        public int lowerBound, upperBound;
        public RenderQueueRange(int lowerBound, int upperBound) { this.lowerBound = lowerBound; this.upperBound = upperBound; }
        public static RenderQueueRange all => new(0, 5000);
        public static RenderQueueRange opaque => new(0, 2500);
        public static RenderQueueRange transparent => new(2501, 5000);
        public static int minimumBound => 0;
        public static int maximumBound => 5000;
    }

    [Flags]
    public enum SortingCriteria
    {
        None = 0, SortingLayer = 1, RenderQueue = 2, BackToFront = 4, QuantizedFrontToBack = 8,
        OptimizeStateChanges = 16, CanvasOrder = 32, RendererPriority = 64,
        CommonOpaque = SortingLayer | RenderQueue | QuantizedFrontToBack | OptimizeStateChanges | CanvasOrder,
        CommonTransparent = SortingLayer | RenderQueue | BackToFront | OptimizeStateChanges,
    }

    /// <summary>Which renderers a draw takes: a queue range, layers and rendering layers.</summary>
    public struct FilteringSettings
    {
        public RenderQueueRange renderQueueRange;
        public int layerMask;
        public uint renderingLayerMask;
        public int excludeMotionVectorObjects;
        public FilteringSettings(RenderQueueRange? renderQueueRange = null, int layerMask = -1, uint renderingLayerMask = uint.MaxValue, int excludeMotionVectorObjects = 0)
        {
            this.renderQueueRange = renderQueueRange ?? RenderQueueRange.all;
            this.layerMask = layerMask;
            this.renderingLayerMask = renderingLayerMask;
            this.excludeMotionVectorObjects = excludeMotionVectorObjects;
        }
        public static FilteringSettings defaultValue => new(RenderQueueRange.all);
    }

    /// <summary>How a draw sorts and which shader passes (LightMode tags) it draws.</summary>
    public struct DrawingSettings
    {
        readonly ShaderTagId[] _passes;
        public SortingCriteria sortingCriteria;
        public Material overrideMaterial;
        public int overrideMaterialPassIndex;
        public bool enableDynamicBatching, enableInstancing;
        public int mainLightIndex;
        public DrawingSettings(ShaderTagId shaderPassName, SortingCriteria sortingCriteria)
        {
            _passes = new ShaderTagId[16];
            _passes[0] = shaderPassName;
            this.sortingCriteria = sortingCriteria;
            overrideMaterial = null; overrideMaterialPassIndex = 0;
            enableDynamicBatching = false; enableInstancing = true; mainLightIndex = -1;
        }
        public ShaderTagId GetShaderPassName(int index) => _passes?[index] ?? default;
        public void SetShaderPassName(int index, ShaderTagId shaderPassName) { if (_passes != null) _passes[index] = shaderPassName; }
    }

    /// <summary>What a camera's culling found (opaque here: the port's renderer culls itself).</summary>
    public struct CullingResults { }

    public struct RendererListParams
    {
        public CullingResults cullingResults;
        public DrawingSettings drawSettings;
        public FilteringSettings filteringSettings;
        public RendererListParams(CullingResults cullingResults, DrawingSettings drawSettings, FilteringSettings filteringSettings)
        {
            this.cullingResults = cullingResults;
            this.drawSettings = drawSettings;
            this.filteringSettings = filteringSettings;
        }
    }

    /// <summary>A list of renderers to draw with one set of drawing and filtering settings.</summary>
    public struct RendererList
    {
        public static readonly RendererList nullRendererList = default;
        internal RendererListParams Params;
        public bool isValid { get; internal set; }
    }

    /// <summary>One recorded command; a backend replays them in order.</summary>
    public abstract record RenderCommand
    {
        public sealed record SetRenderTarget(RenderTargetIdentifier Target, int MipLevel, CubemapFace Face, int DepthSlice) : RenderCommand;
        public sealed record ClearRenderTarget(bool ClearDepth, bool ClearColor, Color BackgroundColor, float Depth) : RenderCommand;
        public sealed record SetViewProjection(Matrix4x4 View, Matrix4x4 Projection) : RenderCommand;
        public sealed record DrawMesh(Mesh Mesh, Matrix4x4 Matrix, Material Material, int SubmeshIndex, int ShaderPass, MaterialPropertyBlock Properties) : RenderCommand;
        public sealed record DrawRenderers(RendererList List) : RenderCommand;
        public sealed record Blit(RenderTargetIdentifier Source, RenderTargetIdentifier Destination, Material Material, int Pass, Vector4 ScaleBias) : RenderCommand;
        public sealed record SetGlobal(int NameID, object Value) : RenderCommand;
    }

    /// <summary>A list of rendering commands to execute later (UnityEngine.Rendering.CommandBuffer).</summary>
    public class CommandBuffer : IDisposable
    {
        readonly List<RenderCommand> _commands = new();

        public string name { get; set; } = "Unnamed command buffer";
        public int sizeInBytes => _commands.Count * 64;
        /// <summary>The commands recorded since the last <see cref="Clear"/>, in order.</summary>
        public IReadOnlyList<RenderCommand> Commands => _commands;

        protected void Add(RenderCommand c) => _commands.Add(c);

        public void Clear() => _commands.Clear();
        public void Release() => _commands.Clear();
        public void Dispose() => _commands.Clear();

        public void SetRenderTarget(RenderTargetIdentifier rt) => Add(new RenderCommand.SetRenderTarget(rt, 0, CubemapFace.Unknown, 0));
        public void SetRenderTarget(RenderTargetIdentifier rt, int mipLevel) => Add(new RenderCommand.SetRenderTarget(rt, mipLevel, CubemapFace.Unknown, 0));
        public void SetRenderTarget(RenderTargetIdentifier rt, int mipLevel, CubemapFace cubemapFace) => Add(new RenderCommand.SetRenderTarget(rt, mipLevel, cubemapFace, 0));
        public void SetRenderTarget(RenderTargetIdentifier rt, int mipLevel, CubemapFace cubemapFace, int depthSlice) => Add(new RenderCommand.SetRenderTarget(rt, mipLevel, cubemapFace, depthSlice));
        public void ClearRenderTarget(bool clearDepth, bool clearColor, Color backgroundColor, float depth = 1f) => Add(new RenderCommand.ClearRenderTarget(clearDepth, clearColor, backgroundColor, depth));
        public void SetViewProjectionMatrices(Matrix4x4 view, Matrix4x4 proj) => Add(new RenderCommand.SetViewProjection(view, proj));

        public void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int submeshIndex = 0, int shaderPass = -1, MaterialPropertyBlock properties = null)
            => Add(new RenderCommand.DrawMesh(mesh, matrix, material, submeshIndex, shaderPass, properties));

        public void DrawRendererList(RendererList rendererList) => Add(new RenderCommand.DrawRenderers(rendererList));

        public void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest) => Add(new RenderCommand.Blit(source, dest, null, -1, new Vector4(1f, 1f, 0f, 0f)));
        public void Blit(RenderTargetIdentifier source, RenderTargetIdentifier dest, Material mat, int pass = -1) => Add(new RenderCommand.Blit(source, dest, mat, pass, new Vector4(1f, 1f, 0f, 0f)));
        public void Blit(Texture source, RenderTargetIdentifier dest) => Blit((RenderTargetIdentifier)source, dest);
        public void Blit(Texture source, RenderTargetIdentifier dest, Material mat, int pass = -1) => Blit((RenderTargetIdentifier)source, dest, mat, pass);

        public void SetGlobalTexture(int nameID, RenderTargetIdentifier value) => Add(new RenderCommand.SetGlobal(nameID, value));
        public void SetGlobalTexture(string name, RenderTargetIdentifier value) => SetGlobalTexture(Shader.PropertyToID(name), value);
        public void SetGlobalFloat(int nameID, float value) => Add(new RenderCommand.SetGlobal(nameID, value));
        public void SetGlobalFloat(string name, float value) => SetGlobalFloat(Shader.PropertyToID(name), value);
        public void SetGlobalVector(int nameID, Vector4 value) => Add(new RenderCommand.SetGlobal(nameID, value));
        public void SetGlobalVector(string name, Vector4 value) => SetGlobalVector(Shader.PropertyToID(name), value);
        public void SetGlobalColor(int nameID, Color value) => Add(new RenderCommand.SetGlobal(nameID, value));
        public void SetGlobalColor(string name, Color value) => SetGlobalColor(Shader.PropertyToID(name), value);
        public void SetGlobalMatrix(int nameID, Matrix4x4 value) => Add(new RenderCommand.SetGlobal(nameID, value));
        public void SetGlobalMatrix(string name, Matrix4x4 value) => SetGlobalMatrix(Shader.PropertyToID(name), value);
    }

    /// <summary>The command buffer a raster render-graph pass records into.</summary>
    public class RasterCommandBuffer : CommandBuffer { }

    /// <summary>Full-screen copies inside a render pass (UnityEngine.Rendering.Blitter).</summary>
    public static class Blitter
    {
        public static void BlitTexture(CommandBuffer cmd, RTHandle source, Vector4 scaleBias, float mipLevel, bool bilinear)
            => cmd.Blit(source, BuiltinRenderTextureType.CurrentActive);
        public static void BlitTexture(CommandBuffer cmd, RTHandle source, Vector4 scaleBias, Material material, int pass)
            => cmd.Blit(source, BuiltinRenderTextureType.CurrentActive, material, pass);
        public static void BlitTexture(CommandBuffer cmd, RenderTargetIdentifier source, Vector4 scaleBias, float mipLevel, bool bilinear)
            => cmd.Blit(source, BuiltinRenderTextureType.CurrentActive);
    }

    // ── URP renderer and passes ──────────────────────────────────────────────

    public enum RenderPassEvent
    {
        BeforeRendering = 0, BeforeRenderingShadows = 50, AfterRenderingShadows = 100,
        BeforeRenderingPrePasses = 150, AfterRenderingPrePasses = 200, BeforeRenderingGbuffer = 210,
        AfterRenderingGbuffer = 220, BeforeRenderingDeferredLights = 230, AfterRenderingDeferredLights = 240,
        BeforeRenderingOpaques = 250, AfterRenderingOpaques = 300, BeforeRenderingSkybox = 350,
        AfterRenderingSkybox = 400, BeforeRenderingTransparents = 450, AfterRenderingTransparents = 500,
        BeforeRenderingPostProcessing = 550, AfterRenderingPostProcessing = 600, AfterRendering = 1000,
    }

    [Flags]
    public enum ScriptableRenderPassInput { None = 0, Depth = 1, Normal = 2, Color = 4, Motion = 8 }

    /// <summary>A pass a script adds to a camera's renderer.</summary>
    public abstract class ScriptableRenderPass
    {
        public RenderPassEvent renderPassEvent { get; set; } = RenderPassEvent.AfterRenderingOpaques;
        public bool requiresIntermediateTexture { get; set; }
        protected internal ProfilingSampler profilingSampler { get; set; }
        public ScriptableRenderPassInput input { get; private set; }

        protected ScriptableRenderPass() => profilingSampler = new ProfilingSampler(GetType().Name);

        public void ConfigureInput(ScriptableRenderPassInput passInput) => input = passInput;

        /// <summary>Records the pass's work into the frame's render graph (never called by the port; see the file header).</summary>
        public virtual void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) { }
    }

    /// <summary>A camera's renderer: the passes enqueued on it this frame.</summary>
    public class ScriptableRenderer
    {
        readonly List<ScriptableRenderPass> _queued = new();

        /// <summary>Passes enqueued since the camera began rendering, in order.</summary>
        public IReadOnlyList<ScriptableRenderPass> QueuedPasses => _queued;

        public void EnqueuePass(ScriptableRenderPass pass)
        {
            if (pass != null) _queued.Add(pass);
        }

        /// <summary>Called by the port's renderer when a camera starts, before beginCameraRendering.</summary>
        public void ClearQueuedPasses() => _queued.Clear();
    }

    /// <summary>The active URP pipeline (UnityEngine.Rendering.Universal.UniversalRenderPipeline).</summary>
    public static class UniversalRenderPipeline
    {
        public static UniversalRenderPipelineAsset asset => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
    }

    // ── frame data ───────────────────────────────────────────────────────────

    /// <summary>One kind of per-frame data in a <see cref="ContextContainer"/>.</summary>
    public abstract class ContextItem
    {
        public abstract void Reset();
    }

    /// <summary>The per-frame data a pass reads (camera, resources, rendering, lights), one of each kind.</summary>
    public class ContextContainer : IDisposable
    {
        readonly Dictionary<Type, ContextItem> _items = new();

        public T Get<T>() where T : ContextItem, new()
            => _items.TryGetValue(typeof(T), out var item) ? (T)item : throw new InvalidOperationException($"No {typeof(T).Name} in this frame's data.");

        public T GetOrCreate<T>() where T : ContextItem, new()
        {
            if (!_items.TryGetValue(typeof(T), out var item)) _items[typeof(T)] = item = new T();
            return (T)item;
        }

        public T Create<T>() where T : ContextItem, new()
        {
            var item = new T();
            _items[typeof(T)] = item;
            return item;
        }

        public bool Contains<T>() where T : ContextItem, new() => _items.ContainsKey(typeof(T));

        public void Dispose()
        {
            foreach (var item in _items.Values) item.Reset();
            _items.Clear();
        }
    }

    public class UniversalResourceData : ContextItem
    {
        public bool isActiveTargetBackBuffer { get; set; }
        public TextureHandle activeColorTexture { get; set; }
        public TextureHandle activeDepthTexture { get; set; }
        public TextureHandle cameraColor { get; set; }
        public TextureHandle cameraDepth { get; set; }
        public TextureHandle cameraDepthTexture { get; set; }
        public TextureHandle cameraOpaqueTexture { get; set; }
        public TextureHandle backBufferColor { get; set; }
        public override void Reset()
        {
            isActiveTargetBackBuffer = false;
            activeColorTexture = activeDepthTexture = cameraColor = cameraDepth = default;
            cameraDepthTexture = cameraOpaqueTexture = backBufferColor = default;
        }
    }

    public class UniversalCameraData : ContextItem
    {
        public Camera camera { get; set; }
        public CameraType cameraType { get; set; }
        public CameraRenderType renderType { get; set; }
        public bool isSceneViewCamera => cameraType == CameraType.SceneView;
        public override void Reset() { camera = null; cameraType = CameraType.Game; renderType = CameraRenderType.Base; }
    }

    public class UniversalRenderingData : ContextItem
    {
        public CullingResults cullResults { get; set; }
        public override void Reset() => cullResults = default;
    }

    public class UniversalLightData : ContextItem
    {
        public int mainLightIndex { get; set; } = -1;
        public int additionalLightsCount { get; set; }
        public override void Reset() { mainLightIndex = -1; additionalLightsCount = 0; }
    }

    /// <summary>URP's drawing helpers (UnityEngine.Rendering.Universal.RenderingUtils).</summary>
    public static class RenderingUtils
    {
        public static DrawingSettings CreateDrawingSettings(ShaderTagId shaderTagId, UniversalRenderingData renderingData,
            UniversalCameraData cameraData, UniversalLightData lightData, SortingCriteria sortingCriteria)
            => new(shaderTagId, sortingCriteria) { mainLightIndex = lightData?.mainLightIndex ?? -1 };

        public static DrawingSettings CreateDrawingSettings(List<ShaderTagId> shaderTagIdList, UniversalRenderingData renderingData,
            UniversalCameraData cameraData, UniversalLightData lightData, SortingCriteria sortingCriteria)
        {
            var s = CreateDrawingSettings(shaderTagIdList.Count > 0 ? shaderTagIdList[0] : ShaderTagId.none, renderingData, cameraData, lightData, sortingCriteria);
            for (int i = 1; i < shaderTagIdList.Count; i++) s.SetShaderPassName(i, shaderTagIdList[i]);
            return s;
        }
    }
}

namespace CosmicShore.Engine.Rendering.RenderGraphModule
{
    /// <summary>A texture inside a render graph.</summary>
    public readonly struct TextureHandle : IEquatable<TextureHandle>
    {
        public static readonly TextureHandle nullHandle = default;
        internal readonly int Id;
        internal readonly RTHandle Target;

        internal TextureHandle(int id, RTHandle target) { Id = id; Target = target; }

        public bool IsValid() => Id > 0;
        public static implicit operator RTHandle(TextureHandle h) => h.Target;
        public static implicit operator RenderTargetIdentifier(TextureHandle h) => h.Target?.nameID ?? default;
        public static implicit operator Texture(TextureHandle h) => h.Target?.rt;
        public bool Equals(TextureHandle o) => Id == o.Id;
        public override bool Equals(object obj) => obj is TextureHandle o && Equals(o);
        public override int GetHashCode() => Id;
    }

    /// <summary>A texture's description in a render graph.</summary>
    public struct TextureDesc
    {
        public string name;
        public int width, height, slices;
        public TextureDimension dimension;
        public MSAASamples msaaSamples;
        public bool bindTextureMS, clearBuffer, useMipMap, autoGenerateMips, enableRandomWrite, isShadowMap;
        public Color clearColor;
        public FilterMode filterMode;
        public TextureWrapMode wrapMode;
        public int depthBufferBits;
        public RenderTextureFormat colorFormat;

        public TextureDesc(int width, int height)
        {
            this = default;
            this.width = width;
            this.height = height;
            slices = 1;
            dimension = TextureDimension.Tex2D;
            msaaSamples = MSAASamples.None;
            filterMode = FilterMode.Bilinear;
            wrapMode = TextureWrapMode.Clamp;
        }
    }

    /// <summary>A renderer list inside a render graph.</summary>
    public readonly struct RendererListHandle
    {
        internal readonly RendererList List;
        internal RendererListHandle(RendererList list) => List = list;
        public bool IsValid() => List.isValid;
        public static implicit operator RendererList(RendererListHandle h) => h.List;
    }

    [Flags]
    public enum AccessFlags { None = 0, Read = 1, Write = 2, Discard = 4, WriteAll = Write | Discard, ReadWrite = Read | Write }

    public delegate void BaseRenderFunc<PassData, ContextType>(PassData data, ContextType renderGraphContext) where PassData : class, new();

    /// <summary>What a raster pass's render function gets.</summary>
    public struct RasterGraphContext
    {
        public RasterCommandBuffer cmd;
    }

    /// <summary>Declares a raster pass's resources and its render function.</summary>
    public interface IRasterRenderGraphBuilder : IDisposable
    {
        TextureHandle UseTexture(in TextureHandle input, AccessFlags flags = AccessFlags.Read);
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
    /// The frame's render graph. The port's renderer does not execute one (see ScriptableRenderPasses.cs):
    /// this records what a pass declares, so a pass's RecordRenderGraph runs to completion when called.
    /// </summary>
    public class RenderGraph
    {
        int _nextTexture = 1;
        readonly Dictionary<int, TextureDesc> _descs = new();

        public string name { get; }
        public RenderGraph(string name = "RenderGraph") => this.name = name;

        public TextureHandle CreateTexture(TextureDesc desc)
        {
            int id = _nextTexture++;
            _descs[id] = desc;
            return new TextureHandle(id, new RTHandle { name = desc.name });
        }

        public TextureHandle CreateTexture(TextureHandle texture) => CreateTexture(GetTextureDesc(texture));

        public TextureDesc GetTextureDesc(TextureHandle texture)
            => _descs.TryGetValue(texture.Id, out var d) ? d : new TextureDesc(1, 1);

        public RendererListHandle CreateRendererList(in RendererListParams desc)
            => new(new RendererList { Params = desc, isValid = true });

        public IRasterRenderGraphBuilder AddRasterRenderPass<PassData>(string passName, out PassData passData, ProfilingSampler sampler = null)
            where PassData : class, new()
        {
            passData = new PassData();
            return new RasterBuilder();
        }

        sealed class RasterBuilder : IRasterRenderGraphBuilder
        {
            public TextureHandle UseTexture(in TextureHandle input, AccessFlags flags = AccessFlags.Read) => input;
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

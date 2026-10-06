using System;

namespace CosmicShore.Engine
{
    public enum ComputeBufferType { Default = 0, Raw = 1, Append = 2, Counter = 4, Constant = 8, Structured = 16, IndirectArguments = 256 }
    public enum ComputeBufferMode { Immutable = 0, Dynamic = 1, SubUpdates = 3 }

    /// <summary>
    /// The CPU copy behind <see cref="ComputeBuffer"/> and <see cref="GraphicsBuffer"/>: SetData/GetData
    /// round-trip byte for byte. Elements are copied from pinned memory, so arrays of structs work
    /// (Buffer.BlockCopy only accepts arrays of primitives).
    /// </summary>
    internal sealed class BufferBytes
    {
        byte[] _data;
        readonly int _stride;

        public BufferBytes(int count, int stride) { _stride = Math.Max(0, stride); _data = new byte[Math.Max(0, count) * _stride]; }

        public bool Valid => _data != null;
        public void Release() => _data = null;

        public void Set(Array data, int managedStart, int bufferStart, int count)
        {
            if (data == null || _data == null || count <= 0) return;
            int elem = System.Runtime.InteropServices.Marshal.SizeOf(data.GetType().GetElementType()!);
            count = Math.Min(count, data.Length - managedStart);
            int dst = bufferStart * _stride, len = Math.Min(count * elem, _data.Length - dst);
            if (managedStart < 0 || dst < 0 || len <= 0) return;
            var pin = System.Runtime.InteropServices.GCHandle.Alloc(data, System.Runtime.InteropServices.GCHandleType.Pinned);
            try { System.Runtime.InteropServices.Marshal.Copy(pin.AddrOfPinnedObject() + managedStart * elem, _data, dst, len); }
            finally { pin.Free(); }
        }

        public void Get(Array data, int managedStart, int bufferStart, int count)
        {
            if (data == null || _data == null || count <= 0) return;
            int elem = System.Runtime.InteropServices.Marshal.SizeOf(data.GetType().GetElementType()!);
            count = Math.Min(count, data.Length - managedStart);
            int src = bufferStart * _stride, len = Math.Min(count * elem, _data.Length - src);
            if (managedStart < 0 || src < 0 || len <= 0) return;
            var pin = System.Runtime.InteropServices.GCHandle.Alloc(data, System.Runtime.InteropServices.GCHandleType.Pinned);
            try { System.Runtime.InteropServices.Marshal.Copy(_data, src, pin.AddrOfPinnedObject() + managedStart * elem, len); }
            finally { pin.Free(); }
        }
    }

    /// <summary>GPU buffer (UnityEngine.ComputeBuffer). CPU-backed in the port so SetData/GetData round-trip.</summary>
    public sealed class ComputeBuffer : IDisposable
    {
        readonly BufferBytes _data;
        public int count { get; }
        public int stride { get; }
        public ComputeBufferType type { get; }
        public string name { get; set; }

        public ComputeBuffer(int count, int stride) : this(count, stride, ComputeBufferType.Default) { }
        public ComputeBuffer(int count, int stride, ComputeBufferType type, ComputeBufferMode usage = ComputeBufferMode.Immutable)
        { this.count = count; this.stride = stride; this.type = type; _data = new BufferBytes(count, stride); }

        public bool IsValid() => _data.Valid;
        public void Release() => _data.Release();
        public void Dispose() => Release();

        public void SetData(Array data) => SetData(data, 0, 0, data?.Length ?? 0);
        public void SetData(Array data, int managedBufferStartIndex, int computeBufferStartIndex, int count)
            => _data.Set(data, managedBufferStartIndex, computeBufferStartIndex, count);
        public void SetData<T>(System.Collections.Generic.List<T> data) where T : struct => SetData(data.ToArray());
        public void GetData(Array data) => _data.Get(data, 0, 0, data?.Length ?? 0);
        public void GetData(Array data, int managedBufferStartIndex, int computeBufferStartIndex, int count)
            => _data.Get(data, managedBufferStartIndex, computeBufferStartIndex, count);
        public void SetCounterValue(uint counterValue) { }
    }

    /// <summary>
    /// GPU buffer (UnityEngine.GraphicsBuffer), CPU-backed like <see cref="ComputeBuffer"/>. Prisma's
    /// GL 3.3 / GL ES 3.0 renderer has no vertex-stage storage buffers, so nothing draws from one
    /// (<see cref="SystemInfo.maxComputeBufferInputsVertex"/> says so, as Unity does on such a device).
    /// </summary>
    public sealed class GraphicsBuffer : IDisposable
    {
        [Flags]
        public enum Target
        {
            Vertex = 1, Index = 2, CopySource = 4, CopyDestination = 8, Structured = 16, Raw = 32,
            Append = 64, Counter = 128, IndirectArguments = 256, Constant = 512,
        }

        [Flags]
        public enum UsageFlags { None = 0, LockBufferForWrite = 1 }

        readonly BufferBytes _data;
        public int count { get; }
        public int stride { get; }
        public Target target { get; }
        public UsageFlags usageFlags { get; }
        public string name { get; set; }

        public GraphicsBuffer(Target target, int count, int stride) : this(target, UsageFlags.None, count, stride) { }
        public GraphicsBuffer(Target target, UsageFlags usageFlags, int count, int stride)
        { this.target = target; this.usageFlags = usageFlags; this.count = count; this.stride = stride; _data = new BufferBytes(count, stride); }

        public bool IsValid() => _data.Valid;
        public void Release() => _data.Release();
        public void Dispose() => Release();

        public void SetData(Array data) => SetData(data, 0, 0, data?.Length ?? 0);
        public void SetData(Array data, int managedBufferStartIndex, int graphicsBufferStartIndex, int count)
            => _data.Set(data, managedBufferStartIndex, graphicsBufferStartIndex, count);
        public void SetData<T>(System.Collections.Generic.List<T> data) where T : struct => SetData(data.ToArray());
        public void GetData(Array data) => _data.Get(data, 0, 0, data?.Length ?? 0);
        public void GetData(Array data, int managedBufferStartIndex, int graphicsBufferStartIndex, int count)
            => _data.Get(data, managedBufferStartIndex, graphicsBufferStartIndex, count);
    }

    /// <summary>Compute program asset. The port has no compute path; dispatches are no-ops and say so once.</summary>
    public sealed class ComputeShader : Object
    {
        static bool s_Warned;
        readonly string[] _kernels;

        /// <summary>A project compute asset: its kernels are not parsed, so every lookup answers kernel 0.</summary>
        public ComputeShader() { _kernels = null; }

        /// <summary>Port: a compute program known by its kernel names (a package resource the port stands in for).</summary>
        public ComputeShader(string name, params string[] kernels) { this.name = name; _kernels = kernels ?? Array.Empty<string>(); }

        /// <summary>Index of a kernel; throws for an unknown one (original contract).</summary>
        public int FindKernel(string name)
        {
            if (_kernels == null) return 0;
            int i = Array.IndexOf(_kernels, name);
            if (i < 0) throw new ArgumentException($"Kernel '{name}' not found.");
            return i;
        }
        public bool HasKernel(string name) => _kernels == null || Array.IndexOf(_kernels, name) >= 0;
        public void SetBuffer(int kernelIndex, string name, ComputeBuffer buffer) { }
        public void SetBuffer(int kernelIndex, int nameID, ComputeBuffer buffer) { }
        public void SetTexture(int kernelIndex, string name, Texture texture) { }
        public void SetFloat(string name, float val) { }
        public void SetInt(string name, int val) { }
        public void SetVector(string name, Vector4 val) { }
        public void SetMatrix(string name, Matrix4x4 val) { }
        public void GetKernelThreadGroupSizes(int kernelIndex, out uint x, out uint y, out uint z) { x = 64; y = 1; z = 1; }
        public void Dispatch(int kernelIndex, int threadGroupsX, int threadGroupsY, int threadGroupsZ)
        {
            if (s_Warned) return;
            s_Warned = true;
            Debug.LogWarning($"[port] ComputeShader '{name}' dispatched — compute has no port backend yet; the dispatch is skipped.");
        }
    }

    /// <summary>Shader warm-up set (UnityEngine.ShaderVariantCollection) — the port compiles on first use.</summary>
    public sealed class ShaderVariantCollection : Object
    {
        public int shaderCount => 0;
        public int variantCount => 0;
        public bool isWarmedUp => true;
        public void WarmUp() { }
        public void Clear() { }
    }

    public enum FullScreenMode { ExclusiveFullScreen = 0, FullScreenWindow = 1, MaximizedWindow = 2, Windowed = 3 }

    /// <summary>One frame's CPU/GPU timings (UnityEngine.FrameTiming).</summary>
    public struct FrameTiming
    {
        public ulong cpuTimePresentCalled, cpuTimeFrameComplete, frameStartTimestamp, firstSubmitTimestamp;
        public double cpuFrameTime, cpuMainThreadFrameTime, cpuMainThreadPresentWaitTime, cpuRenderThreadFrameTime, gpuFrameTime;
        public float heightScale, widthScale;
        public uint syncInterval;
    }

    /// <summary>Frame-timing capture (UnityEngine.FrameTimingManager). Reports the engine's own frame time.</summary>
    public static class FrameTimingManager
    {
        public static bool IsFeatureEnabled() => true;
        public static void CaptureFrameTimings() { }
        public static uint GetLatestTimings(uint numFrames, FrameTiming[] timings)
        {
            if (timings == null || timings.Length == 0 || numFrames == 0) return 0;
            timings[0] = new FrameTiming { cpuFrameTime = Time.unscaledDeltaTime * 1000.0, cpuMainThreadFrameTime = Time.unscaledDeltaTime * 1000.0, gpuFrameTime = 0 };
            return 1;
        }
        public static ulong GetCpuTimerFrequency() => (ulong)System.Diagnostics.Stopwatch.Frequency;
        public static ulong GetGpuTimerFrequency() => 0;
        public static ulong GetVSyncsPerSecond() => 60;
    }

    /// <summary>IMGUI style (UnityEngine.GUIStyle) — the port draws no IMGUI; styles are data.</summary>
    public class GUIStyle
    {
        public GUIStyle() { }
        public GUIStyle(GUIStyle other) { if (other != null) { fontSize = other.fontSize; alignment = other.alignment; richText = other.richText; wordWrap = other.wordWrap; } }
        public int fontSize { get; set; }
        public Font font { get; set; }
        public FontStyle fontStyle { get; set; }
        public TextAnchor alignment { get; set; }
        public bool richText { get; set; }
        public bool wordWrap { get; set; }
        public GUIStyleState normal { get; set; } = new();
        public RectOffset padding { get; set; } = new();
        public RectOffset margin { get; set; } = new();
        public Vector2 CalcSize(GUIContent content) => new((content?.text?.Length ?? 0) * fontSize * 0.6f, fontSize * 1.2f);
    }

    public class GUIStyleState
    {
        public Color textColor { get; set; } = Color.white;
        public Texture2D background { get; set; }
    }

    public class GUIContent
    {
        public string text { get; set; }
        public Texture image { get; set; }
        public string tooltip { get; set; }
        public GUIContent() { }
        public GUIContent(string text) { this.text = text; }
    }

    public enum FontStyle { Normal = 0, Bold = 1, Italic = 2, BoldAndItalic = 3 }
}

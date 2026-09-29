using System;

namespace CosmicShore.Engine
{
    public enum ComputeBufferType { Default = 0, Raw = 1, Append = 2, Counter = 4, Constant = 8, Structured = 16, IndirectArguments = 256 }
    public enum ComputeBufferMode { Immutable = 0, Dynamic = 1, SubUpdates = 3 }

    /// <summary>GPU buffer (UnityEngine.ComputeBuffer). CPU-backed in the port so SetData/GetData round-trip.</summary>
    public sealed class ComputeBuffer : IDisposable
    {
        byte[] _data;
        public int count { get; }
        public int stride { get; }
        public ComputeBufferType type { get; }
        public string name { get; set; }

        public ComputeBuffer(int count, int stride) : this(count, stride, ComputeBufferType.Default) { }
        public ComputeBuffer(int count, int stride, ComputeBufferType type, ComputeBufferMode usage = ComputeBufferMode.Immutable)
        { this.count = count; this.stride = stride; this.type = type; _data = new byte[Math.Max(0, count * stride)]; }

        public bool IsValid() => _data != null;
        public void Release() => _data = null;
        public void Dispose() => Release();

        public void SetData(Array data) => SetData(data, 0, 0, data?.Length ?? 0);
        public void SetData(Array data, int managedBufferStartIndex, int computeBufferStartIndex, int count)
        {
            if (data == null || _data == null) return;
            int elem = System.Runtime.InteropServices.Marshal.SizeOf(data.GetType().GetElementType());
            var bytes = new byte[data.Length * elem];
            Buffer.BlockCopy(data, 0, bytes, 0, Math.Min(bytes.Length, Buffer.ByteLength(data)));
            int src = managedBufferStartIndex * elem, dst = computeBufferStartIndex * stride, len = Math.Min(count * elem, _data.Length - dst);
            if (len > 0) Buffer.BlockCopy(bytes, src, _data, dst, len);
        }
        public void SetData<T>(System.Collections.Generic.List<T> data) where T : struct => SetData(data.ToArray());
        public void GetData(Array data)
        {
            if (data == null || _data == null) return;
            Buffer.BlockCopy(_data, 0, data, 0, Math.Min(_data.Length, Buffer.ByteLength(data)));
        }
        public void SetCounterValue(uint counterValue) { }
    }

    /// <summary>Compute program asset. The port has no compute path; dispatches are no-ops and say so once.</summary>
    public sealed class ComputeShader : Object
    {
        static bool s_Warned;
        public int FindKernel(string name) => 0;
        public bool HasKernel(string name) => false;
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

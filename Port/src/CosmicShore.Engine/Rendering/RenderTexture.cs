using System;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    public enum RenderTextureFormat
    {
        ARGB32 = 0, Depth = 1, ARGBHalf = 2, Shadowmap = 3, RGB565 = 4, ARGB4444 = 5, ARGB1555 = 6,
        Default = 7, ARGB2101010 = 8, DefaultHDR = 9, ARGB64 = 10, ARGBFloat = 11, RGFloat = 12,
        RGHalf = 13, RFloat = 14, RHalf = 15, R8 = 16, ARGBInt = 17, RGInt = 18, RInt = 19,
        BGRA32 = 20, RGB111110Float = 22, RG32 = 23, RGBAUShort = 24, RG16 = 25, BGRA10101010_XR = 26,
        BGR101010_XR = 27, R16 = 28,
    }

    public enum RenderTextureReadWrite { Default = 0, Linear = 1, sRGB = 2 }
    public enum RenderTextureMemoryless { None = 0, Color = 1, Depth = 2, MSAA = 4 }

    public struct RenderTextureDescriptor
    {
        public int width, height, msaaSamples, volumeDepth, mipCount, depthBufferBits;
        public RenderTextureFormat colorFormat;
        public bool sRGB, useMipMap, autoGenerateMips, enableRandomWrite, bindMS;
        public RenderTextureDescriptor(int width, int height) : this(width, height, RenderTextureFormat.Default, 0) { }
        public RenderTextureDescriptor(int width, int height, RenderTextureFormat colorFormat, int depthBufferBits)
        {
            this = default;
            this.width = width; this.height = height; this.colorFormat = colorFormat; this.depthBufferBits = depthBufferBits;
            msaaSamples = 1; volumeDepth = 1; mipCount = -1; sRGB = true; autoGenerateMips = true;
        }
    }

    /// <summary>
    /// A GPU render target (UnityEngine.RenderTexture). Headless it is a sized descriptor;
    /// the GL renderer allocates a framebuffer for it when a camera targets it. A
    /// monotonically increasing <see cref="Version"/> tells the renderer the contents changed.
    /// </summary>
    public class RenderTexture : Texture
    {
        static readonly List<RenderTexture> s_Temporary = new();
        bool _created;

        public int depth { get; set; }
        public RenderTextureFormat format { get; set; } = RenderTextureFormat.Default;
        public int antiAliasing { get; set; } = 1;
        public bool enableRandomWrite { get; set; }
        public bool useMipMap { get; set; }
        public bool autoGenerateMips { get; set; } = true;
        public bool sRGB { get; set; } = true;
        public int volumeDepth { get; set; } = 1;
        public RenderTextureMemoryless memorylessMode { get; set; }
        public bool isPowerOfTwo { get; set; }
        public int Version { get; private set; }
        public RenderTextureDescriptor descriptor
        {
            get => new(width, height, format, depth) { msaaSamples = antiAliasing, sRGB = sRGB, useMipMap = useMipMap, enableRandomWrite = enableRandomWrite };
            set { width = value.width; height = value.height; format = value.colorFormat; depth = value.depthBufferBits; antiAliasing = Math.Max(1, value.msaaSamples); sRGB = value.sRGB; useMipMap = value.useMipMap; enableRandomWrite = value.enableRandomWrite; }
        }

        /// <summary>The target the next Graphics.Blit / ReadPixels reads from.</summary>
        public static RenderTexture active { get; set; }

        public RenderTexture(int width, int height, int depth) : this(width, height, depth, RenderTextureFormat.Default) { }
        public RenderTexture(int width, int height, int depth, RenderTextureFormat format)
        { this.width = width; this.height = height; this.depth = depth; this.format = format; name = "RenderTexture"; }
        public RenderTexture(int width, int height, int depth, RenderTextureFormat format, RenderTextureReadWrite readWrite)
            : this(width, height, depth, format) { sRGB = readWrite != RenderTextureReadWrite.Linear; }
        public RenderTexture(RenderTextureDescriptor desc) { descriptor = desc; name = "RenderTexture"; }
        public RenderTexture(RenderTexture other) : this(other.descriptor) { }

        public bool Create() { _created = true; return true; }
        public void Release() => _created = false;
        public bool IsCreated() => _created;
        public void DiscardContents() { }
        public void MarkRestoreExpected() { }
        public void GenerateMips() { }
        public void MarkModified() => Version++;

        public static RenderTexture GetTemporary(int width, int height, int depthBuffer = 0, RenderTextureFormat format = RenderTextureFormat.Default, RenderTextureReadWrite readWrite = RenderTextureReadWrite.Default, int antiAliasing = 1)
        {
            for (int i = 0; i < s_Temporary.Count; i++)
            {
                var t = s_Temporary[i];
                if (t.width == width && t.height == height && t.depth == depthBuffer && t.format == format && t.antiAliasing == Math.Max(1, antiAliasing))
                { s_Temporary.RemoveAt(i); return t; }
            }
            var rt = new RenderTexture(width, height, depthBuffer, format, readWrite) { antiAliasing = Math.Max(1, antiAliasing) };
            rt.Create();
            return rt;
        }

        public static RenderTexture GetTemporary(RenderTextureDescriptor desc) => GetTemporary(desc.width, desc.height, desc.depthBufferBits, desc.colorFormat, RenderTextureReadWrite.Default, desc.msaaSamples);

        public static void ReleaseTemporary(RenderTexture temp) { if (temp != null && !s_Temporary.Contains(temp)) s_Temporary.Add(temp); }
    }
}

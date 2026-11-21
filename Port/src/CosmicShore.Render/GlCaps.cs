using System;
using System.Collections.Generic;
using System.Text;
using Silk.NET.OpenGL;

namespace CosmicShore.Render
{
    /// <summary>
    /// What the current GL context can do, and the one place the renderer's desktop GLSL becomes
    /// GLSL ES. The renderer is written once against GL 3.3 core; on a phone the same passes run on
    /// OpenGL ES 3.0 (the level every iOS device and every Android device the project targets has),
    /// with three desktop features swapped for ES 3.0 equivalents where the extension is missing:
    ///
    ///   texture buffers (samplerBuffer)   → a 2D RGBA32F texture read with texelFetch (CS_EXT_2D)
    ///   float colour targets              → RGBA16F, else RGBA8 (HDR clips; the frame still draws)
    ///   base-vertex instanced draws       → plain instanced draws (the renderer's base vertex is 0)
    ///
    /// Unity does the same thing at build time — one shader source, per-API variants.
    /// </summary>
    public static class GlCaps
    {
        public static bool IsEs { get; private set; }
        public static int Major { get; private set; } = 3;
        public static int Minor { get; private set; } = 3;
        public static bool TextureBuffer { get; private set; } = true;
        public static bool FloatColorTarget { get; private set; } = true;
        public static bool HalfFloatColorTarget { get; private set; } = true;
        public static string Renderer { get; private set; } = "";
        static readonly HashSet<string> s_extensions = new(StringComparer.Ordinal);

        /// <summary>Reads the context the renderer is about to draw with. Call once, right after GL.GetApi.</summary>
        public static unsafe void Detect(GL gl)
        {
            string version = Str(gl.GetString(StringName.Version));
            Renderer = Str(gl.GetString(StringName.Renderer));
            IsEs = version.StartsWith("OpenGL ES", StringComparison.Ordinal);
            gl.GetInteger(GetPName.MajorVersion, out int major);
            gl.GetInteger(GetPName.MinorVersion, out int minor);
            if (major > 0) { Major = major; Minor = minor; }
            s_extensions.Clear();
            gl.GetInteger(GetPName.NumExtensions, out int count);
            for (uint i = 0; i < count; i++) s_extensions.Add(Str(gl.GetString(StringName.Extensions, i)));

            if (IsEs)
            {
                bool es32 = Major > 3 || (Major == 3 && Minor >= 2);
                // samplerBuffer needs GLSL ES 3.20 or an extension; the 2D path is core ES 3.0,
                // so every ES context takes it and a phone draws the same way on every GPU.
                TextureBuffer = false;
                FloatColorTarget = es32 || Has("GL_EXT_color_buffer_float");
                HalfFloatColorTarget = FloatColorTarget || Has("GL_EXT_color_buffer_half_float");
            }
            else TextureBuffer = FloatColorTarget = HalfFloatColorTarget = true;
            Console.WriteLine($"[gl] {version} — {Renderer}; texture buffers {(TextureBuffer ? "yes" : "no (2D fallback)")}, "
                + $"HDR targets {(FloatColorTarget ? "float" : HalfFloatColorTarget ? "half-float" : "none (RGBA8)")}");
        }

        public static bool Has(string extension) => s_extensions.Contains(extension);

        static unsafe string Str(byte* p) => p == null ? "" : System.Runtime.InteropServices.Marshal.PtrToStringUTF8((IntPtr)p) ?? "";

        /// <summary>
        /// The HDR scene/bloom target formats this context can render into: the desktop's own where
        /// allowed, else the closest ES 3.0 colour-renderable format.
        /// </summary>
        public static (InternalFormat Internal, PixelFormat Format, PixelType Type) HdrTarget(InternalFormat desktop)
        {
            if (!IsEs) return desktop == InternalFormat.R11fG11fB10f
                ? (desktop, PixelFormat.Rgb, PixelType.Float)
                : (desktop, PixelFormat.Rgba, PixelType.HalfFloat);
            if (FloatColorTarget) return desktop == InternalFormat.R11fG11fB10f
                ? (InternalFormat.R11fG11fB10f, PixelFormat.Rgb, PixelType.Float)
                : (InternalFormat.Rgba16f, PixelFormat.Rgba, PixelType.HalfFloat);
            if (HalfFloatColorTarget) return (InternalFormat.Rgba16f, PixelFormat.Rgba, PixelType.HalfFloat);
            return (InternalFormat.Rgba8, PixelFormat.Rgba, PixelType.UnsignedByte);
        }

        /// <summary>
        /// The source a shader stage compiles from on this context. Desktop: unchanged. ES: the GLSL
        /// 3.30 core header becomes GLSL ES 3.00 with explicit precision (desktop GLSL has none to
        /// inherit), and CS_EXT_2D selects the texture-buffer stand-in.
        /// </summary>
        public static string Translate(string source, bool fragment)
        {
            bool es = IsEs || ForceEsTranslation;
            if (!es) return source;
            int start = source.IndexOf("#version", StringComparison.Ordinal);
            if (start < 0) return source;
            int eol = source.IndexOf('\n', start);
            var sb = new StringBuilder(source.Length + 256);
            sb.Append(source, 0, start);
            sb.Append("#version 300 es\n");
            sb.Append("precision highp float;\nprecision highp int;\nprecision highp sampler2D;\nprecision highp samplerCube;\nprecision highp sampler3D;\n");
            sb.Append("#define CS_EXT_2D 1\n");
            sb.Append("#define CS_GLES 1\n");
            sb.Append(source, eol + 1, source.Length - eol - 1);
            return sb.ToString();
        }

        /// <summary>Translate as for a minimal ES 3.0 context even on desktop (offline validation, tests).</summary>
        public static bool ForceEsTranslation;

        /// <summary>Width of the 2D texture standing in for a texture buffer (CS_EXT_2D): texel i is at (i % W, i / W).</summary>
        public const int ExtTextureWidth = 1024;
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CosmicShore.Engine;
using Silk.NET.OpenGL;
using EngineTexture = CosmicShore.Engine.Texture;
using EngineWrap = CosmicShore.Engine.TextureWrapMode;
using EngineFilter = CosmicShore.Engine.FilterMode;
using GlWrap = Silk.NET.OpenGL.TextureWrapMode;

namespace CosmicShore.Render
{
    /// <summary>Shader program helper: compile + link with loud failures and cached uniform locations.</summary>
    public sealed class GlProgram : IDisposable
    {
        readonly GL _gl;
        public readonly uint Handle;
        readonly Dictionary<string, int> _uniforms = new();

        public GlProgram(GL gl, string vertex, string fragment, string name)
        {
            _gl = gl;
            uint vs = Compile(ShaderType.VertexShader, vertex, name + ".vert");
            uint fs = Compile(ShaderType.FragmentShader, fragment, name + ".frag");
            Handle = gl.CreateProgram();
            gl.AttachShader(Handle, vs);
            gl.AttachShader(Handle, fs);
            gl.LinkProgram(Handle);
            gl.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out int ok);
            if (ok == 0) throw new InvalidOperationException($"{name} link: {gl.GetProgramInfoLog(Handle)}");
            gl.DeleteShader(vs);
            gl.DeleteShader(fs);
        }

        uint Compile(ShaderType type, string src, string name)
        {
            // The source is handed over with its CHARACTER count; a multi-byte UTF-8 character
            // makes the driver read that many bytes and cut the tail off ("unexpected end of file").
            for (int i = 0; i < src.Length; i++)
                if (src[i] > 127) throw new InvalidOperationException($"{name}: shader source must be ASCII (U+{(int)src[i]:X4} at char {i}).");
            src = GlCaps.Translate(src, type == ShaderType.FragmentShader);
            DumpSource?.Invoke(name, src);
            uint s = _gl.CreateShader(type);
            _gl.ShaderSource(s, src);
            _gl.CompileShader(s);
            _gl.GetShader(s, ShaderParameterName.CompileStatus, out int ok);
            if (ok == 0) throw new InvalidOperationException($"{name}: {_gl.GetShaderInfoLog(s)}");
            return s;
        }

        /// <summary>Receives every stage's final source (what the driver compiles) — for offline validation.</summary>
        public static Action<string, string> DumpSource;

        public int Loc(string uniform)
        {
            if (!_uniforms.TryGetValue(uniform, out int l)) _uniforms[uniform] = l = _gl.GetUniformLocation(Handle, uniform);
            return l;
        }

        public void Use() => _gl.UseProgram(Handle);
        public void Set(string n, float v) => _gl.Uniform1(Loc(n), v);
        public void Set(string n, int v) => _gl.Uniform1(Loc(n), v);
        public void Set(string n, float x, float y) => _gl.Uniform2(Loc(n), x, y);
        public void Set(string n, float x, float y, float z) => _gl.Uniform3(Loc(n), x, y, z);
        public unsafe void Set4v(string n, float[] values, int count) { fixed (float* p = values) _gl.Uniform4(Loc(n), (uint)count, p); }
        public void Set(string n, float x, float y, float z, float w) => _gl.Uniform4(Loc(n), x, y, z, w);
        public unsafe void Set(string n, System.Numerics.Matrix4x4 m) => _gl.UniformMatrix4(Loc(n), 1, false, (float*)&m);

        public void Dispose() => _gl.DeleteProgram(Handle);
    }

    /// <summary>
    /// Engine <see cref="Texture2D"/> → GL texture, uploaded once per texture instance
    /// (re-uploaded when its pixel buffer is replaced). sRGB-flagged textures upload as
    /// SRGB8_ALPHA8 so sampling returns LINEAR values — Unity's linear color space.
    /// </summary>
    public sealed class TextureCache : IDisposable
    {
        sealed class Entry { public uint Handle; public byte[] Data; }

        readonly GL _gl;
        readonly ConditionalWeakTable<EngineTexture, Entry> _entries = new();
        readonly List<uint> _all = new();
        uint _white;

        public TextureCache(GL gl) { _gl = gl; }

        public uint White
        {
            get
            {
                if (_white != 0) return _white;
                _white = Upload(new byte[] { 255, 255, 255, 255 }, 1, 1, srgb: false, EngineFilter.Point, EngineWrap.Repeat, EngineWrap.Repeat, false);
                return _white;
            }
        }

        readonly Dictionary<uint, uint> _solids = new();

        /// <summary>A 1x1 linear texture of one colour (a shader's black / grey / bump default), made once.</summary>
        public uint Solid(byte r, byte g, byte b, byte a)
        {
            uint key = (uint)(r << 24 | g << 16 | b << 8 | a);
            if (key == 0xFFFFFFFF) return White;
            if (!_solids.TryGetValue(key, out var h))
                _solids[key] = h = Upload(new[] { r, g, b, a }, 1, 1, srgb: false, EngineFilter.Point, EngineWrap.Repeat, EngineWrap.Repeat, false);
            return h;
        }

        /// <summary>Resolves a GPU-produced texture (a camera's RenderTexture) to its GL handle; 0 = not rendered yet.</summary>
        public Func<EngineTexture, uint> External;

        public uint Get(EngineTexture texture)
        {
            if (texture is RenderTexture && External != null)
            {
                uint external = External(texture);
                if (external != 0) return external;
            }
            if (texture is not Texture2D t2 || !t2.HasPixels) return White;
            var data = t2.GetRawTextureData();
            if (_entries.TryGetValue(texture, out var e) && ReferenceEquals(e.Data, data)) return e.Handle;
            if (e != null) _gl.DeleteTexture(e.Handle);
            uint h = Upload(data, t2.width, t2.height, t2.isDataSRGB, t2.filterMode, t2.wrapModeU, t2.wrapModeV, t2.mipmapCount > 1);
            _entries.AddOrUpdate(texture, new Entry { Handle = h, Data = data });
            return h;
        }

        /// <summary>Uploads a single-channel (Alpha8/R8) atlas, e.g. a TMP SDF atlas.</summary>
        public unsafe uint UploadAlpha8(byte[] data, int w, int h, bool linearFilter = true)
        {
            uint tex = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, tex);
            _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            fixed (byte* p = data)
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R8, (uint)w, (uint)h, 0, PixelFormat.Red, PixelType.UnsignedByte, p);
            _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
            var f = linearFilter ? (int)TextureMinFilter.Linear : (int)TextureMinFilter.Nearest;
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, f);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, f);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GlWrap.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GlWrap.ClampToEdge);
            _all.Add(tex);
            return tex;
        }

        unsafe uint Upload(byte[] data, int w, int h, bool srgb, EngineFilter filter, EngineWrap wu, EngineWrap wv, bool mips)
        {
            uint tex = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, tex);
            fixed (byte* p = data)
                _gl.TexImage2D(TextureTarget.Texture2D, 0, srgb ? InternalFormat.Srgb8Alpha8 : InternalFormat.Rgba8,
                    (uint)w, (uint)h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            if (mips) _gl.GenerateMipmap(TextureTarget.Texture2D);
            int mag = filter == EngineFilter.Point ? (int)TextureMagFilter.Nearest : (int)TextureMagFilter.Linear;
            int min = filter == EngineFilter.Point
                ? (mips ? (int)TextureMinFilter.NearestMipmapNearest : (int)TextureMinFilter.Nearest)
                : mips ? (filter == EngineFilter.Trilinear ? (int)TextureMinFilter.LinearMipmapLinear : (int)TextureMinFilter.LinearMipmapNearest)
                       : (int)TextureMinFilter.Linear;
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, min);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, mag);
            if (mips && filter != EngineFilter.Point) RenderQuality.ApplyAnisotropy(_gl);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)Wrap(wu));
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)Wrap(wv));
            _all.Add(tex);
            return tex;
        }

        static GlWrap Wrap(EngineWrap w) => w switch
        {
            EngineWrap.Clamp => GlWrap.ClampToEdge,
            EngineWrap.Mirror => GlWrap.MirroredRepeat,
            EngineWrap.MirrorOnce => GlWrap.MirroredRepeat,
            _ => GlWrap.Repeat,
        };

        public void Dispose()
        {
            foreach (var t in _all) _gl.DeleteTexture(t);
            _all.Clear();
        }
    }

    /// <summary>
    /// An offscreen render target: RGBA16F color (linear HDR) + depth24/stencil8. With samples
    /// above 1 it renders into multisampled renderbuffers (Unity's MSAA) and <see cref="Resolve"/>
    /// averages them into the <see cref="Color"/> texture the post stack reads. A context that
    /// cannot multisample this format falls back to the single-sample target.
    /// </summary>
    public sealed class FrameTarget : IDisposable
    {
        readonly GL _gl;
        public uint Fbo, Color, DepthStencil;
        public int Width, Height;
        /// <summary>The sample count actually in use (0 = single-sample).</summary>
        public int Samples { get; private set; }
        uint _msFbo, _msColor, _msDepth;
        int _requested;

        public FrameTarget(GL gl) { _gl = gl; }

        public unsafe void Ensure(int w, int h, int samples = 0)
        {
            if (samples < 2) samples = 0;
            if (w == Width && h == Height && Fbo != 0 && samples == _requested) return;
            Dispose();
            Width = w; Height = h; _requested = samples;
            Fbo = _gl.GenFramebuffer();
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);
            Color = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, Color);
            var (fmt, layout, type) = GlCaps.HdrTarget(InternalFormat.Rgba16f);
            _gl.TexImage2D(TextureTarget.Texture2D, 0, fmt, (uint)w, (uint)h, 0, layout, type, null);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GlWrap.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GlWrap.ClampToEdge);
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, Color, 0);
            DepthStencil = _gl.GenRenderbuffer();
            _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, DepthStencil);
            _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, (uint)w, (uint)h);
            _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, DepthStencil);
            var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != GLEnum.FramebufferComplete) throw new InvalidOperationException($"framebuffer incomplete: {status}");

            Samples = 0;
            if (samples > 1) TryMultisample(w, h, samples, fmt);
        }

        void TryMultisample(int w, int h, int samples, InternalFormat fmt)
        {
            _gl.GetInteger((GLEnum)0x8D57 /* GL_MAX_SAMPLES */, out int max);
            samples = Math.Min(samples, max);
            if (samples < 2) return;
            while (_gl.GetError() != GLEnum.NoError) { }
            _msFbo = _gl.GenFramebuffer();
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _msFbo);
            _msColor = _gl.GenRenderbuffer();
            _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _msColor);
            _gl.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, (uint)samples, fmt, (uint)w, (uint)h);
            _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, _msColor);
            _msDepth = _gl.GenRenderbuffer();
            _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _msDepth);
            _gl.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, (uint)samples, InternalFormat.Depth24Stencil8, (uint)w, (uint)h);
            _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _msDepth);
            bool ok = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == GLEnum.FramebufferComplete && _gl.GetError() == GLEnum.NoError;
            if (ok) { Samples = samples; return; }
            DisposeMultisample();
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);
        }

        public void Bind()
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Samples > 1 ? _msFbo : Fbo);
            _gl.Viewport(0, 0, (uint)Width, (uint)Height);
        }

        /// <summary>Averages the multisampled color into <see cref="Color"/> (no-op when single-sample).</summary>
        public void Resolve()
        {
            if (Samples < 2) return;
            _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _msFbo);
            _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, Fbo);
            _gl.BlitFramebuffer(0, 0, Width, Height, 0, 0, Width, Height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);
        }

        void DisposeMultisample()
        {
            if (_msFbo != 0) _gl.DeleteFramebuffer(_msFbo);
            if (_msColor != 0) _gl.DeleteRenderbuffer(_msColor);
            if (_msDepth != 0) _gl.DeleteRenderbuffer(_msDepth);
            _msFbo = _msColor = _msDepth = 0;
            Samples = 0;
        }

        public void Dispose()
        {
            DisposeMultisample();
            if (Fbo != 0) _gl.DeleteFramebuffer(Fbo);
            if (Color != 0) _gl.DeleteTexture(Color);
            if (DepthStencil != 0) _gl.DeleteRenderbuffer(DepthStencil);
            Fbo = Color = DepthStencil = 0;
        }
    }

    /// <summary>Color-space helpers matching Unity's (exact sRGB transfer function).</summary>
    public static class ColorSpace
    {
        public static float ToLinear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
        public static System.Numerics.Vector4 Linear(Color c) => new(ToLinear(c.r), ToLinear(c.g), ToLinear(c.b), c.a);
    }
}

using System;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.OpenGL;

namespace CosmicShore.Client
{
    /// <summary>
    /// The Arc-C UI quad pass: one textured shader (alongside the sim's vertex-color
    /// one), one texture (the <see cref="UiFont"/> atlas with its baked solid cell),
    /// one dynamic batch of pos2+uv2+rgba triangles. Coordinates are screen pixels,
    /// y-UP (matching the engine's world-corner space for screen-space canvases and
    /// the sims' existing HUD ortho). Standard alpha blending inside Begin/End; the
    /// sims' additive-blend + depth-test state is restored on End so the UI pass is
    /// a no-op to the 3D pipeline.
    /// </summary>
    public sealed class UiRenderer : IDisposable
    {
        const string VertexSrc = @"#version 330 core
layout(location=0) in vec2 aPos;
layout(location=1) in vec2 aUv;
layout(location=2) in vec4 aColor;
uniform mat4 uProj;
out vec2 vUv;
out vec4 vColor;
void main()
{
    gl_Position = uProj * vec4(aPos, 0.0, 1.0);
    vUv = aUv;
    vColor = aColor;
}";
        const string FragmentSrc = @"#version 330 core
in vec2 vUv;
in vec4 vColor;
out vec4 frag;
uniform sampler2D uTex;
void main() { frag = texture(uTex, vUv) * vColor; }";

        readonly GL _gl;
        readonly uint _program;
        readonly uint _vao;
        readonly uint _vbo;
        readonly uint _atlas;
        readonly int _projLocation;
        readonly List<float> _batch = new();
        float _screenW, _screenH;
        bool _begun;

        public unsafe UiRenderer(GL gl)
        {
            _gl = gl;

            uint vs = _gl.CreateShader(ShaderType.VertexShader);
            _gl.ShaderSource(vs, VertexSrc);
            _gl.CompileShader(vs);
            Check(vs, "ui vertex");
            uint fs = _gl.CreateShader(ShaderType.FragmentShader);
            _gl.ShaderSource(fs, FragmentSrc);
            _gl.CompileShader(fs);
            Check(fs, "ui fragment");
            _program = _gl.CreateProgram();
            _gl.AttachShader(_program, vs);
            _gl.AttachShader(_program, fs);
            _gl.LinkProgram(_program);
            _gl.GetProgram(_program, ProgramPropertyARB.LinkStatus, out int linked);
            if (linked == 0) throw new InvalidOperationException($"ui link: {_gl.GetProgramInfoLog(_program)}");
            _gl.DeleteShader(vs);
            _gl.DeleteShader(fs);
            _projLocation = _gl.GetUniformLocation(_program, "uProj");

            _vao = _gl.GenVertexArray();
            _vbo = _gl.GenBuffer();
            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), (void*)0);
            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), (void*)(2 * sizeof(float)));
            _gl.EnableVertexAttribArray(2);
            _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, 8 * sizeof(float), (void*)(4 * sizeof(float)));

            // Atlas upload — nearest filtering keeps the 8x8 glyphs crisp and the
            // output byte-deterministic across drivers (no filtering variance at
            // integer scales).
            _atlas = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _atlas);
            var pixels = UiFont.BuildAtlas();
            fixed (byte* p = pixels)
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                    UiFont.AtlasWidth, UiFont.AtlasHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        }

        public void Begin(float screenWidth, float screenHeight)
        {
            _screenW = screenWidth;
            _screenH = screenHeight;
            _batch.Clear();
            _begun = true;
        }

        /// <summary>Solid rect at (x, y) size (w, h), pixels, y-up.</summary>
        public void DrawRect(float x, float y, float w, float h, Vector4 rgba)
        {
            var (u, v) = UiFont.SolidUv;
            Quad(x, y, x + w, y + h, u, v, u, v, rgba);
        }

        /// <summary>
        /// Text with its BOTTOM-LEFT corner at (x, y). Monospace: each glyph advances
        /// by <paramref name="pixelHeight"/> × 0.875 — font8x8 glyphs fill 7 of 8
        /// columns, the eighth IS the spacing. Newlines stack downward.
        /// </summary>
        public void DrawText(string text, float x, float y, float pixelHeight, Vector4 rgba)
        {
            if (string.IsNullOrEmpty(text)) return;
            float advance = pixelHeight * 0.875f;
            float penX = x, penY = y;
            foreach (char c in text)
            {
                if (c == '\n') { penX = x; penY -= pixelHeight * 1.25f; continue; }
                if (c != ' ')
                {
                    var (u0, v0, u1, v1) = UiFont.GlyphUv(c);
                    // Atlas v0 is the glyph's TOP row; screen y is up — so the quad's
                    // top edge (y + h) samples v0.
                    Quad(penX, penY, penX + pixelHeight, penY + pixelHeight, u0, v1, u1, v0, rgba);
                }
                penX += advance;
            }
        }

        public static float MeasureText(string text, float pixelHeight)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            int longest = 0, current = 0;
            foreach (char c in text)
            {
                if (c == '\n') { longest = Math.Max(longest, current); current = 0; continue; }
                current++;
            }
            return Math.Max(longest, current) * pixelHeight * 0.875f;
        }

        // Two CCW triangles; (u0,v0) at the BOTTOM-left vertex, (u1,v1) at the TOP-right.
        void Quad(float x0, float y0, float x1, float y1, float u0, float v0, float u1, float v1, Vector4 c)
        {
            if (!_begun) throw new InvalidOperationException("UiRenderer: DrawRect/DrawText outside Begin/End.");
            Vertex(x0, y0, u0, v0, c);
            Vertex(x1, y0, u1, v0, c);
            Vertex(x1, y1, u1, v1, c);
            Vertex(x0, y0, u0, v0, c);
            Vertex(x1, y1, u1, v1, c);
            Vertex(x0, y1, u0, v1, c);
        }

        void Vertex(float x, float y, float u, float v, Vector4 c)
        {
            _batch.Add(x); _batch.Add(y); _batch.Add(u); _batch.Add(v);
            _batch.Add(c.X); _batch.Add(c.Y); _batch.Add(c.Z); _batch.Add(c.W);
        }

        /// <summary>Uploads and draws the batch, then restores the sims' GL state.</summary>
        public unsafe void End()
        {
            _begun = false;
            if (_batch.Count == 0) return;

            _gl.UseProgram(_program);
            var proj = Matrix4x4.CreateOrthographicOffCenter(0f, _screenW, 0f, _screenH, -1f, 1f);
            _gl.UniformMatrix4(_projLocation, 1, false, (float*)&proj);

            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, _atlas);

            _gl.Disable(EnableCap.DepthTest);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha); // standard UI alpha

            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            var array = _batch.ToArray();
            fixed (float* p = array)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(array.Length * sizeof(float)), p, BufferUsageARB.DynamicDraw);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(array.Length / 8));

            // The sim windows run additive neon + depth on — hand their state back.
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            _gl.Enable(EnableCap.DepthTest);
        }

        void Check(uint shader, string stage)
        {
            _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int ok);
            if (ok == 0) throw new InvalidOperationException($"{stage}: {_gl.GetShaderInfoLog(shader)}");
        }

        public void Dispose()
        {
            _gl.DeleteProgram(_program);
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
            _gl.DeleteTexture(_atlas);
        }
    }
}

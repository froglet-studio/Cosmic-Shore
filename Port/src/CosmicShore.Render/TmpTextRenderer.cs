using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;
#if GLES
using Silk.NET.OpenGLES;
using GLNS = Silk.NET.OpenGLES;
#else
using Silk.NET.OpenGL;
using GLNS = Silk.NET.OpenGL;
#endif
using Rect = CosmicShore.Engine.Rect;
using Vector4 = System.Numerics.Vector4;
#if GLES
using PrimitiveType = Silk.NET.OpenGLES.PrimitiveType;
#else
using PrimitiveType = Silk.NET.OpenGL.PrimitiveType;
#endif

namespace CosmicShore.Render
{
    /// <summary>
    /// Draws TextMeshPro components: <see cref="TmpLayout"/> for the geometry (cached per
    /// component until anything that can move a glyph changes), <see cref="TmpSdfShader"/>
    /// for the shading, one draw per contiguous (atlas, material) run so the hierarchy's
    /// draw order is preserved. Called from inside the uGUI walk, so the stencil state of
    /// any enclosing Mask is already bound; RectMask2D and TMP's own Masking overflow mode
    /// clip in window pixels.
    /// </summary>
    public sealed class TmpTextRenderer : IDisposable
    {
        const int Floats = 3 + 2 + 4 + 1;

        sealed class Cached
        {
            public long Signature;
            public TmpLayoutResult Layout;
        }

        readonly GL _gl;
        readonly GlProgram _program;
        readonly uint _vao, _vbo, _ebo;
        readonly Dictionary<(TMP_FontAsset, int), uint> _atlases = new();
        readonly ConditionalWeakTable<TMP_Text, Cached> _cache = new();
        readonly List<float> _verts = new(4096);
        readonly List<uint> _idx = new(4096);
        static readonly Vector3[] s_corners = new Vector3[4];

        public ITmpFontResolver Fonts { get; set; }
        public int DrawCalls { get; private set; }
        public int TextsDrawn { get; private set; }
        public int Layouts { get; private set; }

        public unsafe TmpTextRenderer(GL gl)
        {
            _gl = gl;
            _program = new GlProgram(gl, TmpSdfShader.VertexSource, TmpSdfShader.FragmentSource, "tmp-sdf");
            _vao = gl.GenVertexArray();
            _vbo = gl.GenBuffer();
            _ebo = gl.GenBuffer();
            gl.BindVertexArray(_vao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            int stride = Floats * sizeof(float);
            int[] sizes = { 3, 2, 4, 1 };
            int off = 0;
            for (uint i = 0; i < sizes.Length; i++)
            {
                gl.EnableVertexAttribArray(i);
                gl.VertexAttribPointer(i, sizes[i], VertexAttribPointerType.Float, false, (uint)stride, (void*)(off * sizeof(float)));
                off += sizes[i];
            }
            gl.BindVertexArray(0);
        }

        public void BeginFrame() { DrawCalls = 0; TextsDrawn = 0; Layouts = 0; }

        /// <summary>Lays out (or reuses) and draws <paramref name="text"/>. Leaves this pass's program bound.</summary>
        public void Draw(TMP_Text text, float groupAlpha, Vector4 clip, float screenW, float screenH)
        {
            if (text.transform is not RectTransform rt) return;
            Draw(text, rt, groupAlpha, clip, screenW, screenH);
        }

        /// <summary>Lays out <paramref name="text"/> but places it in <paramref name="rt"/> (legacy Text draws through its TMP twin).</summary>
        public void Draw(TMP_Text text, RectTransform rt, float groupAlpha, Vector4 clip, float screenW, float screenH)
        {
            if (string.IsNullOrEmpty(text.text) || rt == null) return;
            var rect = rt.rect;
            var layout = Layout(text, rect);
            if (layout == null || layout.quads.Count == 0) return;
            TextsDrawn++;

            // TMP's Masking overflow clips to the text rect (in window pixels here).
            if (layout.isMasked)
                clip = Intersect(clip, ScreenRect(rt, layout.clipRect));
            bool clipOn = clip.X > -1e8f || clip.Y > -1e8f || clip.Z < 1e8f || clip.W < 1e8f;
            if (clipOn && (clip.Z <= clip.X || clip.W <= clip.Y)) return;

            _program.Use();
            var mvp = new System.Numerics.Matrix4x4(
                2f / screenW, 0, 0, 0,
                0, 2f / screenH, 0, 0,
                0, 0, 1, 0,
                -1, -1, 0, 1);
            _program.Set("uMvp", mvp);
            _program.Set("uAtlas", 0);
            _program.Set("uClipEnabled", clipOn ? 1 : 0);
            _program.Set("uClipRect", clip.X, clip.Y, clip.Z, clip.W);
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BlendFuncSeparate(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);

            TMP_FontAsset runFont = null;
            Material runMat = null;
            int runAtlas = -1;
            foreach (var q in layout.quads)
            {
                if (q.font == null) continue;
                if (!ReferenceEquals(q.font, runFont) || !ReferenceEquals(q.material, runMat) || q.atlasIndex != runAtlas)
                {
                    Flush(runFont, runMat, runAtlas);
                    runFont = q.font; runMat = q.material; runAtlas = q.atlasIndex;
                }
                uint b = (uint)(_verts.Count / Floats);
                Push(rt, q.bl, groupAlpha);
                Push(rt, q.tl, groupAlpha);
                Push(rt, q.tr, groupAlpha);
                Push(rt, q.br, groupAlpha);
                _idx.Add(b); _idx.Add(b + 1); _idx.Add(b + 2);
                _idx.Add(b + 2); _idx.Add(b + 3); _idx.Add(b);
            }
            Flush(runFont, runMat, runAtlas);
            _gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        }

        TmpLayoutResult Layout(TMP_Text text, Rect rect)
        {
            long sig = Signature(text, rect);
            if (_cache.TryGetValue(text, out var c) && c.Signature == sig) return c.Layout;
            TmpLayoutResult result;
            try { result = TmpLayout.Layout(text, rect, Fonts); }
            catch (Exception e)
            {
                Console.WriteLine($"[tmp] layout failed for '{text.gameObject.name}': {e.Message}");
                result = null;
            }
            Layouts++;
            _cache.AddOrUpdate(text, new Cached { Signature = sig, Layout = result });
            return result;
        }

        /// <summary>Everything TmpLayout reads that a running game plausibly changes frame to frame.</summary>
        static long Signature(TMP_Text t, Rect r)
        {
            var h = new HashCode();
            h.Add(t.text);
            h.Add(r.x); h.Add(r.y); h.Add(r.width); h.Add(r.height);
            h.Add(t.fontSize);
            h.Add(t.enableAutoSizing);
            h.Add(t.color.r); h.Add(t.color.g); h.Add(t.color.b); h.Add(t.color.a);
            h.Add((int)t.alignment);
            h.Add((int)t.fontStyle);
            h.Add((int)t.textWrappingMode);
            h.Add((int)t.overflowMode);
            h.Add(t.characterSpacing);
            h.Add(t.lineSpacing);
            h.Add(t.margin.x); h.Add(t.margin.y); h.Add(t.margin.z); h.Add(t.margin.w);
            h.Add(t.maxVisibleCharacters);
            h.Add(RuntimeHelpers.GetHashCode(t.font ?? (object)string.Empty));
            h.Add(RuntimeHelpers.GetHashCode(t.fontSharedMaterial ?? (object)string.Empty));
            return h.ToHashCode();
        }

        void Push(RectTransform rt, in TmpVertex v, float groupAlpha)
        {
            var p = rt.TransformPoint(v.position);
            Color c = v.color;
            _verts.Add(p.x); _verts.Add(p.y); _verts.Add(0f);
            _verts.Add(v.uv.x); _verts.Add(v.uv.y);
            _verts.Add(ColorSpace.ToLinear(c.r)); _verts.Add(ColorSpace.ToLinear(c.g)); _verts.Add(ColorSpace.ToLinear(c.b));
            _verts.Add(c.a * groupAlpha);
            _verts.Add(v.scale);
        }

        unsafe void Flush(TMP_FontAsset font, Material material, int atlasIndex)
        {
            if (_idx.Count == 0 || font == null) { _verts.Clear(); _idx.Clear(); return; }
            uint tex = Atlas(font, atlasIndex);
            var p = TmpSdfParams.FromMaterial(material ?? font.material, font);
            _program.Set("uAtlasSize", p.textureWidth, p.textureHeight);
            _program.Set("uGradientScale", p.gradientScale);
            _program.Set("uScaleRatioA", p.scaleRatioA);
            _program.Set("uScaleRatioC", p.scaleRatioC);
            _program.Set("uSharpness", p.sharpness);
            _program.Set("uFaceDilate", p.faceDilate);
            _program.Set("uWeightNormal", p.weightNormal);
            _program.Set("uWeightBold", p.weightBold);
            _program.Set("uOutlineWidth", p.outlineWidth);
            _program.Set("uOutlineSoftness", p.outlineSoftness);
            SetColor("uFaceColor", p.faceColor);
            SetColor("uOutlineColor", p.outlineColor);
            SetColor("uUnderlayColor", p.underlayColor);
            _program.Set("uUnderlayDilate", p.underlayDilate);
            _program.Set("uUnderlaySoftness", p.underlaySoftness);
            _program.Set("uOutlineOn", p.outlineOn ? 1 : 0);
            _program.Set("uUnderlayOn", p.underlayOn ? 1 : 0);
            var uo = TmpSdfShader.UnderlayUvOffset(p);
            _program.Set("uUnderlayUvOffset", uo.x, uo.y);

            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            var va = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_verts);
            fixed (float* vp = va)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(va.Length * sizeof(float)), vp, BufferUsageARB.StreamDraw);
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            var ia = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_idx);
            fixed (uint* ip = ia)
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(ia.Length * sizeof(uint)), ip, BufferUsageARB.StreamDraw);
            _gl.BindTexture(TextureTarget.Texture2D, tex);
            _gl.DrawElements(PrimitiveType.Triangles, (uint)ia.Length, DrawElementsType.UnsignedInt, null);
            DrawCalls++;
            _verts.Clear();
            _idx.Clear();
        }

        void SetColor(string n, Color c)
            => _program.Set(n, ColorSpace.ToLinear(c.r), ColorSpace.ToLinear(c.g), ColorSpace.ToLinear(c.b), c.a);

        uint Atlas(TMP_FontAsset font, int index)
        {
            if (_atlases.TryGetValue((font, index), out var h)) return h;
            var pixels = (uint)index < (uint)font.atlasPixels.Length ? font.atlasPixels[index] : null;
            if (pixels == null || pixels.Length < font.atlasWidth * font.atlasHeight)
            {
                Console.WriteLine($"[tmp] font '{font.name}' atlas {index} has no pixels");
                h = UploadEmpty();
            }
            else h = UploadR8(pixels, font.atlasWidth, font.atlasHeight);
            _atlases[(font, index)] = h;
            return h;
        }

        unsafe uint UploadR8(byte[] data, int w, int hgt)
        {
            uint tex = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, tex);
            _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            fixed (byte* p = data)
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R8, (uint)w, (uint)hgt, 0, PixelFormat.Red, PixelType.UnsignedByte, p);
            _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLNS.TextureWrapMode.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLNS.TextureWrapMode.ClampToEdge);
            return tex;
        }

        uint UploadEmpty() => UploadR8(new byte[] { 0 }, 1, 1);

        static Vector4 Intersect(Vector4 a, Vector4 b)
            => new(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y), MathF.Min(a.Z, b.Z), MathF.Min(a.W, b.W));

        static Vector4 ScreenRect(RectTransform rt, Rect local)
        {
            var a = rt.TransformPoint(new Vector3(local.xMin, local.yMin, 0f));
            var b = rt.TransformPoint(new Vector3(local.xMax, local.yMax, 0f));
            return new Vector4(MathF.Min(a.x, b.x), MathF.Min(a.y, b.y), MathF.Max(a.x, b.x), MathF.Max(a.y, b.y));
        }

        public void Dispose()
        {
            _program.Dispose();
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
            _gl.DeleteBuffer(_ebo);
            foreach (var h in _atlases.Values) _gl.DeleteTexture(h);
        }
    }
}

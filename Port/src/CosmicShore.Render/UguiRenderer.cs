using System;
using System.Collections.Generic;
using System.Numerics;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;
using Silk.NET.OpenGL;
using EngineVector3 = CosmicShore.Engine.Vector3;
using EngineColor = CosmicShore.Engine.Color;
using Rect = CosmicShore.Engine.Rect;
using Vector4 = System.Numerics.Vector4;
using PrimitiveType = Silk.NET.OpenGL.PrimitiveType;

namespace CosmicShore.Render
{
    /// <summary>
    /// One glyph/quad batch a text system hands the UI renderer: vertices in the text
    /// component's LOCAL rect space, plus the atlas they sample. Mode 1 = SDF (R8 atlas),
    /// mode 0 = ordinary RGBA texture.
    /// </summary>
    public sealed class UiTextGeometry
    {
        public readonly List<UiTextVertex> Vertices = new();
        public readonly List<int> Indices = new();
        public uint AtlasHandle;
        public bool Sdf = true;
    }

    public struct UiTextVertex
    {
        public float X, Y;          // local rect space (canvas units)
        public float U, V;          // atlas uv (v = 0 bottom)
        public EngineColor Color;   // gamma-space authored color (converted to linear by the renderer)
        /// <summary>SDF params: x = sharpness scale (screen px per SDF unit, pre-scaled by canvas), y = face dilate (0..1 of spread),
        /// z = outline width (0..1 of spread), w = softness.</summary>
        public System.Numerics.Vector4 Sdf;
        public EngineColor Outline;
    }

    /// <summary>Supplies text geometry for TMP/legacy text components (pluggable: the TMP layout system registers here).</summary>
    public interface IUiTextProvider
    {
        /// <summary>Fills <paramref name="geometry"/> for <paramref name="text"/>; false when it cannot draw it.</summary>
        bool Build(Component text, UiTextGeometry geometry);
    }

    /// <summary>
    /// Draws the engine's live uGUI tree the way Unity's canvas renderer does in a
    /// linear-color-space URP project: hierarchy order = draw order within a canvas,
    /// root/override canvases sorted by sortingOrder, CanvasGroup alpha multiplied down,
    /// RectMask2D clip rects (intersected, per vertex, so they never break a batch),
    /// Mask components through the stencil buffer (nested masks increment depth), vertex
    /// colors authored in gamma converted to linear, textures sampled as sRGB → linear,
    /// blending in linear space. Screen-space canvases only here; world-space canvases
    /// are drawn by the 3D pass.
    /// </summary>
    public sealed class UguiRenderer : IDisposable
    {
        const string Vert = @"#version 330 core
layout(location=0) in vec2 aPos;
layout(location=1) in vec2 aUv;
layout(location=2) in vec4 aColor;
layout(location=3) in vec4 aClip;
layout(location=4) in vec4 aSdf;
layout(location=5) in vec4 aOutline;
layout(location=6) in float aMode;
uniform vec2 uScreen;
out vec2 vUv; out vec4 vColor; out vec4 vClip; out vec2 vPos; out vec4 vSdf; out vec4 vOutline; flat out float vMode;
void main(){
  vec2 ndc = aPos / uScreen * 2.0 - 1.0;
  gl_Position = vec4(ndc, 0.0, 1.0);
  vUv = aUv; vColor = aColor; vClip = aClip; vPos = aPos; vSdf = aSdf; vOutline = aOutline; vMode = aMode;
}";

        const string Frag = @"#version 330 core
in vec2 vUv; in vec4 vColor; in vec4 vClip; in vec2 vPos; in vec4 vSdf; in vec4 vOutline; flat in float vMode;
uniform sampler2D uTex;
out vec4 frag;
void main(){
  // RectMask2D: hard clip against the (screen-pixel) clip rect.
  if (vPos.x < vClip.x || vPos.y < vClip.y || vPos.x > vClip.z || vPos.y > vClip.w) discard;
  if (vMode < 0.5) {
    frag = texture(uTex, vUv) * vColor;
  } else {
    // TextMeshPro SDF: distance d in [0,1], 0.5 = glyph edge; vSdf.x converts SDF units to screen pixels.
    float d = texture(uTex, vUv).r;
    float scale = max(vSdf.x, 0.0001);
    float bias = 0.5 - vSdf.y * 0.5;                 // face dilate grows the glyph
    float outline = vSdf.z * 0.5;
    float faceA = clamp((d - bias) * scale + 0.5, 0.0, 1.0);
    vec4 face = vColor * faceA;
    if (outline > 0.0) {
      float outA = clamp((d - (bias - outline)) * scale + 0.5, 0.0, 1.0);
      vec4 o = vec4(vOutline.rgb, vOutline.a * vColor.a) * outA;
      // face over outline (both premultiplied-by-coverage)
      vec4 f = vec4(face.rgb * faceA, face.a);
      frag = vec4(f.rgb + o.rgb * o.a * (1.0 - f.a), f.a + o.a * (1.0 - f.a));
      frag.rgb = frag.a > 0.0 ? frag.rgb / frag.a : vec3(0.0);
    } else {
      frag = vec4(vColor.rgb, vColor.a * faceA);
    }
  }
  if (frag.a <= 0.0) discard;
}";

        const int Floats = 2 + 2 + 4 + 4 + 4 + 4 + 1; // 21

        readonly GL _gl;
        readonly GlProgram _program;
        readonly uint _vao, _vbo, _ebo;
        readonly TextureCache _textures;
        readonly List<float> _verts = new(1 << 16);
        readonly List<uint> _idx = new(1 << 16);
        uint _batchTexture;
        int _stencilDepth;
        float _screenW, _screenH;
        readonly UIMesh _mesh = new();
        readonly UiTextGeometry _text = new();

        public IUiTextProvider Text { get; set; }

        /// <summary>TextMeshPro pass (layout + SDF shading); null = TMP draws nothing.</summary>
        public TmpTextRenderer Tmp { get; set; }

        /// <summary>Draw calls issued in the last frame (diagnostics).</summary>
        public int DrawCalls { get; private set; }
        public int GraphicsDrawn { get; private set; }

        public unsafe UguiRenderer(GL gl, TextureCache textures)
        {
            _gl = gl;
            _textures = textures;
            _program = new GlProgram(gl, Vert, Frag, "ugui");
            _vao = gl.GenVertexArray();
            _vbo = gl.GenBuffer();
            _ebo = gl.GenBuffer();
            gl.BindVertexArray(_vao);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            int stride = Floats * sizeof(float);
            int[] sizes = { 2, 2, 4, 4, 4, 4, 1 };
            int off = 0;
            for (uint i = 0; i < sizes.Length; i++)
            {
                gl.EnableVertexAttribArray(i);
                gl.VertexAttribPointer(i, sizes[i], VertexAttribPointerType.Float, false, (uint)stride, (void*)(off * sizeof(float)));
                off += sizes[i];
            }
            gl.BindVertexArray(0);
        }

        // ── Frame ────────────────────────────────────────────────────────────

        /// <summary>Renders every active screen-space canvas into the currently bound framebuffer.</summary>
        public void Render(int screenWidth, int screenHeight)
        {
            _screenW = screenWidth;
            _screenH = screenHeight;
            DrawCalls = 0;
            GraphicsDrawn = 0;
            Tmp?.BeginFrame();

            var canvases = CosmicShore.Engine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var layers = new List<(Canvas c, int order, long seq)>();
            long seq = 0;
            foreach (var c in canvases)
            {
                seq++;
                if (!c.isActiveAndEnabled || !c.gameObject.activeInHierarchy) continue;
                if (c.renderMode == RenderMode.WorldSpace) continue;
                if (c.isRootCanvas || c.overrideSorting)
                    layers.Add((c, c.sortingOrder, HierarchyOrder(c.transform)));
            }
            layers.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : a.seq.CompareTo(b.seq));

            _gl.Disable(EnableCap.DepthTest);
            _gl.Disable(EnableCap.CullFace);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
            _gl.Enable(EnableCap.StencilTest);
            _gl.ClearStencil(0);
            _gl.Clear(ClearBufferMask.StencilBufferBit);
            _gl.StencilMask(0xFF);
            _stencilDepth = 0;
            ApplyStencilTest();

            _program.Use();
            _program.Set("uScreen", _screenW, _screenH);
            _program.Set("uTex", 0);
            _gl.ActiveTexture(TextureUnit.Texture0);

            foreach (var (canvas, _, _) in layers)
            {
                var clip = new Vector4(-1e9f, -1e9f, 1e9f, 1e9f);
                float alpha = InheritedGroupAlpha(canvas.transform.parent);
                Walk(canvas.transform, alpha, clip, isLayerRoot: true);
                Flush();
            }

            _gl.Disable(EnableCap.StencilTest);
        }

        // Stable key for "hierarchy order" between canvases in different roots.
        static long HierarchyOrder(Transform t)
        {
            var path = new List<int>();
            for (var n = t; n != null; n = n.parent)
            {
                int idx = 0;
                if (n.parent != null)
                    for (int i = 0; i < n.parent.childCount; i++) if (n.parent.GetChild(i) == n) { idx = i; break; }
                path.Add(idx);
            }
            long key = 0;
            for (int i = path.Count - 1; i >= 0 && i >= path.Count - 6; i--) key = key * 1024 + Math.Min(path[i], 1023);
            return key;
        }

        static float InheritedGroupAlpha(Transform parent)
        {
            float a = 1f;
            for (var n = parent; n != null; n = n.parent)
            {
                var g = n.gameObject.GetComponent<CanvasGroup>();
                if (g != null && g.isActiveAndEnabled)
                {
                    a *= g.alpha;
                    if (g.ignoreParentGroups) break;
                }
            }
            return a;
        }

        void Walk(Transform node, float alpha, Vector4 clip, bool isLayerRoot)
        {
            var go = node.gameObject;
            if (!go.activeInHierarchy) return;
            if (!isLayerRoot)
            {
                var c = go.GetComponent<Canvas>();
                if (c != null && c.isActiveAndEnabled && c.overrideSorting) return; // drawn in its own sort layer
            }

            var group = go.GetComponent<CanvasGroup>();
            if (group != null && group.isActiveAndEnabled)
                alpha = group.ignoreParentGroups ? group.alpha : alpha * group.alpha;
            if (alpha <= 0.0005f) return;

            var rectMask = go.GetComponent<RectMask2D>();
            if (rectMask != null && rectMask.isActiveAndEnabled && node is RectTransform rmrt)
                clip = Intersect(clip, ScreenRect(rmrt, rectMask.padding));

            var mask = go.GetComponent<Mask>();
            Graphic maskGraphic = null;
            if (mask != null && mask.isActiveAndEnabled)
            {
                maskGraphic = go.GetComponent<Graphic>();
                if (maskGraphic != null && maskGraphic.isActiveAndEnabled)
                {
                    // Push: draw the mask shape, incrementing stencil where the parent mask passes.
                    Flush();
                    _gl.StencilFunc(StencilFunction.Equal, _stencilDepth, 0xFF);
                    _gl.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Incr);
                    if (!mask.showMaskGraphic) _gl.ColorMask(false, false, false, false);
                    DrawGraphic(maskGraphic, alpha, clip, forceVisible: !mask.showMaskGraphic);
                    Flush();
                    _gl.ColorMask(true, true, true, true);
                    _stencilDepth++;
                    ApplyStencilTest();
                }
                else maskGraphic = null;
            }

            if (maskGraphic == null)
            {
                foreach (var g in go.GetComponents<Graphic>())
                    if (g.isActiveAndEnabled) DrawGraphic(g, alpha, clip, false);
                foreach (var t in go.GetComponents<TMP_Text>())
                    if (t is Behaviour { isActiveAndEnabled: true } && (object)t is not Graphic) DrawText(t, alpha, clip);
            }
            else
            {
                foreach (var t in go.GetComponents<TMP_Text>())
                    if (t is Behaviour { isActiveAndEnabled: true } && (object)t is not Graphic) DrawText(t, alpha, clip);
            }

            for (int i = 0; i < node.childCount; i++)
                Walk(node.GetChild(i), alpha, clip, false);

            if (maskGraphic != null)
            {
                // Pop: decrement where the mask wrote.
                Flush();
                _gl.StencilFunc(StencilFunction.Equal, _stencilDepth, 0xFF);
                _gl.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Decr);
                _gl.ColorMask(false, false, false, false);
                DrawGraphic(maskGraphic, alpha, clip, forceVisible: true);
                Flush();
                _gl.ColorMask(true, true, true, true);
                _stencilDepth--;
                ApplyStencilTest();
            }
        }

        void ApplyStencilTest()
        {
            _gl.StencilFunc(StencilFunction.Equal, _stencilDepth, 0xFF);
            _gl.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
        }

        static Vector4 Intersect(Vector4 a, Vector4 b)
            => new(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y), MathF.Min(a.Z, b.Z), MathF.Min(a.W, b.W));

        static readonly EngineVector3[] s_corners = new EngineVector3[4];

        static Vector4 ScreenRect(RectTransform rt, CosmicShore.Engine.Vector4 padding)
        {
            rt.GetWorldCorners(s_corners);
            float minX = MathF.Min(MathF.Min(s_corners[0].x, s_corners[1].x), MathF.Min(s_corners[2].x, s_corners[3].x));
            float maxX = MathF.Max(MathF.Max(s_corners[0].x, s_corners[1].x), MathF.Max(s_corners[2].x, s_corners[3].x));
            float minY = MathF.Min(MathF.Min(s_corners[0].y, s_corners[1].y), MathF.Min(s_corners[2].y, s_corners[3].y));
            float maxY = MathF.Max(MathF.Max(s_corners[0].y, s_corners[1].y), MathF.Max(s_corners[2].y, s_corners[3].y));
            float scale = rt.lossyScale.x;
            return new Vector4(minX + padding.x * scale, minY + padding.y * scale, maxX - padding.z * scale, maxY - padding.w * scale);
        }

        // ── Graphics ─────────────────────────────────────────────────────────

        void DrawGraphic(Graphic g, float alpha, Vector4 clip, bool forceVisible)
        {
            var color = g.color;
            // The CanvasRenderer colour (Selectable tints, CrossFadeColor/CrossFadeAlpha) is a
            // multiplier over the graphic's vertices. Read it without adding a component.
            var crColor = g.TryGetComponent<CanvasRenderer>(out var cr) ? cr.GetColor() : EngineColor.white;
            alpha *= crColor.a;
            float a = color.a * alpha;
            if (a <= 0.0005f && !forceVisible) return;
            var rt = g.rectTransform;

            _mesh.Clear();
            uint tex;
            switch (g)
            {
                case Image image:
                    ImageMesh.Populate(_mesh, image);
                    tex = image.overrideSprite?.texture != null ? _textures.Get(image.overrideSprite.texture) : _textures.White;
                    break;
                case RawImage raw:
                {
                    var r = rt.rect;
                    var uv = raw.uvRect;
                    _mesh.AddQuad(new CosmicShore.Engine.Vector2(r.xMin, r.yMin), new CosmicShore.Engine.Vector2(r.xMax, r.yMax), color,
                        new CosmicShore.Engine.Vector2(uv.xMin, uv.yMin), new CosmicShore.Engine.Vector2(uv.xMax, uv.yMax));
                    tex = raw.texture != null ? _textures.Get(raw.texture) : _textures.White;
                    break;
                }
                case Text legacy:
                    DrawText(legacy, alpha, clip);
                    return;
                default:
                    if ((object)g is TMP_Text) { DrawText(g, alpha, clip); return; }
                    return; // unknown graphic types draw nothing (like a Graphic with no OnPopulateMesh)
            }
            if (_mesh.vertices.Count == 0) return;
            GraphicsDrawn++;

            if (tex != _batchTexture) { Flush(); _batchTexture = tex; }
            uint baseIndex = (uint)(_verts.Count / Floats);
            var tf = rt;
            foreach (var v in _mesh.vertices)
            {
                var p = tf.TransformPoint(v.position);
                EngineColor vc = v.color;
                vc = new EngineColor(vc.r * crColor.r, vc.g * crColor.g, vc.b * crColor.b, vc.a);
                var lc = ColorSpace.Linear(vc);
                lc.W *= alpha;
                Push(p.x, p.y, v.uv0.x, v.uv0.y, lc, clip, default, default, 0f);
            }
            foreach (var i in _mesh.indices) _idx.Add(baseIndex + (uint)i);
        }

        void DrawText(Component text, float alpha, Vector4 clip)
        {
            if (text is TMP_Text tmp && Tmp != null)
            {
                Flush();
                Tmp.Draw(tmp, alpha, clip, _screenW, _screenH);
                // Back to the uGUI program + state.
                _program.Use();
                _program.Set("uScreen", _screenW, _screenH);
                _program.Set("uTex", 0);
                _gl.ActiveTexture(TextureUnit.Texture0);
                _batchTexture = 0;
                GraphicsDrawn++;
                return;
            }
            if (Text == null) return;
            _text.Vertices.Clear();
            _text.Indices.Clear();
            if (!Text.Build(text, _text) || _text.Vertices.Count == 0) return;
            GraphicsDrawn++;
            if (_text.AtlasHandle != _batchTexture) { Flush(); _batchTexture = _text.AtlasHandle; }
            uint baseIndex = (uint)(_verts.Count / Floats);
            var tf = text.transform;
            float canvasScale = tf.lossyScale.x;
            foreach (var v in _text.Vertices)
            {
                var p = tf.TransformPoint(new EngineVector3(v.X, v.Y, 0f));
                var lc = ColorSpace.Linear(v.Color);
                lc.W *= alpha;
                var sdf = v.Sdf;
                sdf.X *= canvasScale;
                var oc = ColorSpace.Linear(v.Outline);
                Push(p.x, p.y, v.U, v.V, lc, clip, sdf, oc, _text.Sdf ? 1f : 0f);
            }
            foreach (var i in _text.Indices) _idx.Add(baseIndex + (uint)i);
        }

        void Push(float x, float y, float u, float v, Vector4 color, Vector4 clip, Vector4 sdf, Vector4 outline, float mode)
        {
            _verts.Add(x); _verts.Add(y); _verts.Add(u); _verts.Add(v);
            _verts.Add(color.X); _verts.Add(color.Y); _verts.Add(color.Z); _verts.Add(color.W);
            _verts.Add(clip.X); _verts.Add(clip.Y); _verts.Add(clip.Z); _verts.Add(clip.W);
            _verts.Add(sdf.X); _verts.Add(sdf.Y); _verts.Add(sdf.Z); _verts.Add(sdf.W);
            _verts.Add(outline.X); _verts.Add(outline.Y); _verts.Add(outline.Z); _verts.Add(outline.W);
            _verts.Add(mode);
        }

        unsafe void Flush()
        {
            if (_idx.Count == 0) { _verts.Clear(); return; }
            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
            var va = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_verts);
            fixed (float* p = va)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(va.Length * sizeof(float)), p, BufferUsageARB.StreamDraw);
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
            var ia = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_idx);
            fixed (uint* p = ia)
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(ia.Length * sizeof(uint)), p, BufferUsageARB.StreamDraw);
            _gl.BindTexture(TextureTarget.Texture2D, _batchTexture == 0 ? _textures.White : _batchTexture);
            _gl.DrawElements(PrimitiveType.Triangles, (uint)ia.Length, DrawElementsType.UnsignedInt, null);
            DrawCalls++;
            _verts.Clear();
            _idx.Clear();
        }

        public void Dispose()
        {
            _program.Dispose();
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteBuffer(_vbo);
            _gl.DeleteBuffer(_ebo);
        }
    }
}

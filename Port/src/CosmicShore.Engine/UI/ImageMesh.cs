using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.UI
{
    /// <summary>One UI mesh vertex (the subset of the original UIVertex the port draws with).</summary>
    public struct UIVertex
    {
        /// <summary>Position in the graphic's RectTransform local space (canvas units, pivot at origin).</summary>
        public Vector3 position;
        public Vector3 normal;
        public Vector4 tangent;
        public Color32 color;
        /// <summary>Texture coordinate (xy); v = 0 is the BOTTOM of the texture.</summary>
        public Vector4 uv0;
        public Vector4 uv1, uv2, uv3;

        /// <summary>Unity's default vertex: origin, normal -z, tangent +x, white.</summary>
        public static UIVertex simpleVert = new()
        {
            position = Vector3.zero,
            normal = new Vector3(0f, 0f, -1f),
            tangent = new Vector4(1f, 0f, 0f, -1f),
            color = new Color32(255, 255, 255, 255),
        };

        public UIVertex(Vector2 position, Color32 color, Vector2 uv)
        {
            this.position = new Vector3(position.x, position.y, 0f);
            normal = new Vector3(0f, 0f, -1f);
            tangent = new Vector4(1f, 0f, 0f, -1f);
            this.color = color;
            uv0 = new Vector4(uv.x, uv.y, 0f, 0f);
            uv1 = uv2 = uv3 = default;
        }

        public override string ToString() => $"({position.x:0.###}, {position.y:0.###}) uv({uv0.x:0.####}, {uv0.y:0.####})";
    }

    /// <summary>A UI mesh under construction: vertices + triangle indices (the VertexHelper role).</summary>
    public sealed class UIMesh
    {
        public readonly List<UIVertex> vertices = new();
        public readonly List<int> indices = new();

        public int currentVertCount => vertices.Count;
        public int currentIndexCount => indices.Count;

        public void Clear() { vertices.Clear(); indices.Clear(); }

        public void AddVert(Vector2 position, Color32 color, Vector2 uv) => vertices.Add(new UIVertex(position, color, uv));

        public void AddTriangle(int a, int b, int c) { indices.Add(a); indices.Add(b); indices.Add(c); }

        /// <summary>
        /// Axis-aligned quad, vertices in the original order — (min.x,min.y), (min.x,max.y),
        /// (max.x,max.y), (max.x,min.y) — triangles (0,1,2) and (2,3,0).
        /// </summary>
        public void AddQuad(Vector2 posMin, Vector2 posMax, Color32 color, Vector2 uvMin, Vector2 uvMax)
        {
            int s = vertices.Count;
            AddVert(new Vector2(posMin.x, posMin.y), color, new Vector2(uvMin.x, uvMin.y));
            AddVert(new Vector2(posMin.x, posMax.y), color, new Vector2(uvMin.x, uvMax.y));
            AddVert(new Vector2(posMax.x, posMax.y), color, new Vector2(uvMax.x, uvMax.y));
            AddVert(new Vector2(posMax.x, posMin.y), color, new Vector2(uvMax.x, uvMin.y));
            AddTriangle(s, s + 1, s + 2);
            AddTriangle(s + 2, s + 3, s);
        }

        /// <summary>General 4-corner quad (fills); triangles (0,1,2) and (2,3,0).</summary>
        public void AddQuad(ReadOnlySpan<Vector2> positions, Color32 color, ReadOnlySpan<Vector2> uvs)
        {
            int s = vertices.Count;
            for (int i = 0; i < 4; i++) AddVert(positions[i], color, uvs[i]);
            AddTriangle(s, s + 1, s + 2);
            AddTriangle(s + 2, s + 3, s);
        }
    }

    /// <summary>Every Image field the mesh depends on (so the builder is a pure function).</summary>
    public struct ImageMeshSettings
    {
        public Image.Type type;
        public bool preserveAspect;
        public bool fillCenter;
        public Image.FillMethod fillMethod;
        public float fillAmount;
        public int fillOrigin;
        public bool fillClockwise;
        public float pixelsPerUnitMultiplier;

        /// <summary>The original Image defaults (Simple, fill centre, Radial360 clockwise, full, multiplier 1).</summary>
        public static ImageMeshSettings Default => new()
        {
            type = Image.Type.Simple,
            fillCenter = true,
            fillMethod = Image.FillMethod.Radial360,
            fillAmount = 1f,
            fillClockwise = true,
            pixelsPerUnitMultiplier = 1f,
        };

        public static ImageMeshSettings From(Image image) => new()
        {
            type = image.type,
            preserveAspect = image.preserveAspect,
            fillCenter = image.fillCenter,
            fillMethod = image.fillMethod,
            fillAmount = image.fillAmount,
            fillOrigin = image.fillOrigin,
            fillClockwise = image.fillClockwise,
            pixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier,
        };
    }

    /// <summary>
    /// The uGUI Image mesh as a PURE function of (settings, sprite, canvas reference ppu,
    /// rect, pivot, colour). Written from the Image component's documented/observable
    /// behaviour — NOT from the com.unity.ugui source, whose licence only covers use with
    /// Unity.
    ///
    /// Why it lives in the ENGINE rather than the content bridge: it is behaviour of the
    /// Image component itself (the original's OnPopulateMesh), needed by any renderer for
    /// any Image — code-authored or loaded from Unity assets — and it needs nothing but
    /// engine types. The content bridge's job is to produce the <see cref="Sprite"/>.
    ///
    /// Coordinates: positions are in the RectTransform's local space (the rect is
    /// pivot-relative, as <c>RectTransform.rect</c>); UVs have v = 0 at the texture bottom.
    ///
    /// Behaviour reproduced:
    /// • no sprite → one white-UV quad over the rect, whatever the type;
    /// • Simple → one quad over the sprite's outer UVs, optionally aspect-preserved inside
    ///   the rect (the slack distributed by the pivot);
    /// • Sliced → 9 quads (8 without the centre); borders are sprite pixels ÷ (sprite ppu ÷
    ///   reference ppu × multiplier), and shrink proportionally per axis when the rect is
    ///   smaller than the two borders; a borderless sprite falls back to Simple (no aspect);
    /// • Tiled → the centre repeated at the sprite's native size, the last row/column
    ///   clipped in both position and UV; borders drawn as edge strips + corners; a
    ///   borderless full-repeat-wrap sprite becomes ONE quad with scaled UVs; tile count
    ///   capped so the mesh stays under 65,000 vertices;
    /// • Filled → Horizontal/Vertical crop from an origin edge, Radial90/180/360 sweep a
    ///   clock hand from the origin corner/edge/centre (angles measured in the quad's
    ///   normalized space), one quad per swept 90° cell; fillAmount &lt; 0.001 draws nothing.
    /// </summary>
    public static class ImageMesh
    {
        /// <summary>Hard vertex budget for Tiled (the original's 16-bit index ceiling with headroom).</summary>
        public const int MaxTiledVertices = 65000;

        /// <summary>Builds <paramref name="image"/>'s mesh from its live state (sprite, rect, canvas ppu, colour).</summary>
        public static void Populate(UIMesh mesh, Image image)
        {
            var rt = image.rectTransform;
            var canvas = image.canvas;
            Populate(mesh, ImageMeshSettings.From(image), image.overrideSprite,
                canvas != null ? canvas.referencePixelsPerUnit : 100f, rt.rect, rt.pivot, image.color);
        }

        /// <summary>Convenience: builds into a fresh mesh.</summary>
        public static UIMesh Build(in ImageMeshSettings settings, Sprite sprite, float referencePixelsPerUnit,
            Rect rect, Vector2 pivot, Color32 color)
        {
            var mesh = new UIMesh();
            Populate(mesh, settings, sprite, referencePixelsPerUnit, rect, pivot, color);
            return mesh;
        }

        /// <summary>Convenience overload: pivot derived from the pivot-relative rect (pivot = -min / size).</summary>
        public static UIMesh Build(in ImageMeshSettings settings, Sprite sprite, float referencePixelsPerUnit, Rect rect, Color32 color)
            => Build(settings, sprite, referencePixelsPerUnit, rect, PivotOf(rect), color);

        public static Vector2 PivotOf(Rect rect) => new(
            rect.width != 0f ? -rect.x / rect.width : 0.5f,
            rect.height != 0f ? -rect.y / rect.height : 0.5f);

        /// <summary>Clears <paramref name="mesh"/> and fills it with the Image mesh.</summary>
        public static void Populate(UIMesh mesh, in ImageMeshSettings s, Sprite sprite, float referencePixelsPerUnit,
            Rect rect, Vector2 pivot, Color32 color)
        {
            mesh.Clear();
            if (sprite == null)
            {
                mesh.AddQuad(new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMax), color, Vector2.zero, Vector2.one);
                return;
            }

            float refPpu = referencePixelsPerUnit > 0f ? referencePixelsPerUnit : 100f;
            float multiplier = Math.Max(0.01f, s.pixelsPerUnitMultiplier);
            float multipliedPpu = sprite.pixelsPerUnit / refPpu * multiplier;
            if (multipliedPpu <= 0f) multipliedPpu = 1f;

            switch (s.type)
            {
                case Image.Type.Simple:
                    Simple(mesh, sprite, rect, pivot, color, s.preserveAspect);
                    break;
                case Image.Type.Sliced:
                    Sliced(mesh, sprite, rect, pivot, color, s.fillCenter, multipliedPpu);
                    break;
                case Image.Type.Tiled:
                    Tiled(mesh, sprite, rect, color, s.fillCenter, multipliedPpu);
                    break;
                case Image.Type.Filled:
                    Filled(mesh, sprite, rect, pivot, color, s);
                    break;
            }
        }

        static bool HasBorder(Sprite sprite) => sprite.border.sqrMagnitude > 0f;

        // The rect the sprite draws into: the whole rect, or the aspect-fit sub-rect whose
        // slack is distributed by the pivot (a pivot of 0 keeps the image at the min edge).
        static Vector4 DrawingDimensions(Sprite sprite, Rect r, Vector2 pivot, bool preserveAspect)
        {
            Vector2 size = sprite.rect.size;
            if (preserveAspect && size.x > 0f && size.y > 0f && r.width != 0f && r.height != 0f)
            {
                float spriteRatio = size.x / size.y;
                float rectRatio = r.width / r.height;
                if (spriteRatio > rectRatio)
                {
                    float oldH = r.height;
                    r.height = r.width / spriteRatio;
                    r.y += (oldH - r.height) * pivot.y;
                }
                else
                {
                    float oldW = r.width;
                    r.width = r.height * spriteRatio;
                    r.x += (oldW - r.width) * pivot.x;
                }
            }
            return new Vector4(r.xMin, r.yMin, r.xMax, r.yMax);
        }

        static void Simple(UIMesh mesh, Sprite sprite, Rect rect, Vector2 pivot, Color32 color, bool preserveAspect)
        {
            var v = DrawingDimensions(sprite, rect, pivot, preserveAspect);
            var uv = sprite.outerUV;
            mesh.AddQuad(new Vector2(v.x, v.y), new Vector2(v.z, v.w), color, new Vector2(uv.x, uv.y), new Vector2(uv.z, uv.w));
        }

        /// <summary>
        /// Sprite border (pixels, left/bottom/right/top) → canvas units, shrunk per axis
        /// when the two opposite borders do not fit in the rect.
        /// </summary>
        public static Vector4 AdjustedBorders(Vector4 borderPixels, float multipliedPixelsPerUnit, Rect rect)
        {
            var b = borderPixels / multipliedPixelsPerUnit;
            float combinedX = b.x + b.z;
            if (combinedX != 0f && rect.width < combinedX)
            {
                float ratio = rect.width / combinedX;
                b.x *= ratio; b.z *= ratio;
            }
            float combinedY = b.y + b.w;
            if (combinedY != 0f && rect.height < combinedY)
            {
                float ratio = rect.height / combinedY;
                b.y *= ratio; b.w *= ratio;
            }
            return b;
        }

        static void Sliced(UIMesh mesh, Sprite sprite, Rect rect, Vector2 pivot, Color32 color, bool fillCenter, float multipliedPpu)
        {
            if (!HasBorder(sprite))
            {
                Simple(mesh, sprite, rect, pivot, color, false);
                return;
            }

            var outer = sprite.outerUV;
            var inner = sprite.innerUV;
            var b = AdjustedBorders(sprite.border, multipliedPpu, rect);

            Span<float> xs = stackalloc float[4] { rect.xMin, rect.xMin + b.x, rect.xMax - b.z, rect.xMax };
            Span<float> ys = stackalloc float[4] { rect.yMin, rect.yMin + b.y, rect.yMax - b.w, rect.yMax };
            Span<float> us = stackalloc float[4] { outer.x, inner.x, inner.z, outer.z };
            Span<float> vs = stackalloc float[4] { outer.y, inner.y, inner.w, outer.w };

            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    if (!fillCenter && x == 1 && y == 1) continue;
                    mesh.AddQuad(new Vector2(xs[x], ys[y]), new Vector2(xs[x + 1], ys[y + 1]), color,
                        new Vector2(us[x], vs[y]), new Vector2(us[x + 1], vs[y + 1]));
                }
            }
        }

        static void Tiled(UIMesh mesh, Sprite sprite, Rect rect, Color32 color, bool fillCenter, float multipliedPpu)
        {
            var outer = sprite.outerUV;
            var inner = sprite.innerUV;
            var border = sprite.border;
            var spriteSize = sprite.rect.size;
            bool hasBorder = HasBorder(sprite);

            float tileW = (spriteSize.x - border.x - border.z) / multipliedPpu;
            float tileH = (spriteSize.y - border.y - border.w) / multipliedPpu;
            var b = AdjustedBorders(border, multipliedPpu, rect);

            var uvMin = new Vector2(inner.x, inner.y);
            var uvMax = new Vector2(inner.z, inner.w);

            // Local (rect-min-relative) extent of the tiled centre.
            float xMin = b.x, xMax = rect.width - b.z;
            float yMin = b.y, yMax = rect.height - b.w;
            if (tileW <= 0f) tileW = xMax - xMin;
            if (tileH <= 0f) tileH = yMax - yMin;

            var origin = new Vector2(rect.xMin, rect.yMin);
            var tex = sprite.texture;
            bool separateQuads = hasBorder || sprite.packed || tex == null || tex.wrapModeU != TextureWrapMode.Repeat;

            if (!separateQuads)
            {
                // One quad whose UVs run past 1 — the texture's Repeat wrap does the tiling.
                if (!fillCenter) return;
                var scale = new Vector2(
                    tileW > 0f ? (xMax - xMin) / tileW : 0f,
                    tileH > 0f ? (yMax - yMin) / tileH : 0f);
                mesh.AddQuad(origin + new Vector2(xMin, yMin), origin + new Vector2(xMax, yMax), color,
                    Vector2.Scale(uvMin, scale), Vector2.Scale(uvMax, scale));
                return;
            }

            long nW = 0, nH = 0;
            if (tileW > 0f && tileH > 0f && xMax > xMin && yMax > yMin)
            {
                nW = (long)Math.Ceiling((xMax - xMin) / tileW);
                nH = (long)Math.Ceiling((yMax - yMin) / tileH);
                long extra = hasBorder ? 2 : 0;
                double verts = (double)(nW + extra) * (nH + extra) * 4;
                if (verts > MaxTiledVertices)
                {
                    // Keep the tile grid's aspect, shrink the tile COUNT until the mesh fits
                    // (tiles get proportionally larger — the original degrades the same way).
                    double maxTiles = MaxTiledVertices / 4.0;
                    double ratio = (double)(nW + extra) / (nH + extra);
                    double targetH = Math.Sqrt(maxTiles / ratio);
                    double targetW = targetH * ratio;
                    nW = Math.Max(1, (long)Math.Floor(targetW) - extra);
                    nH = Math.Max(1, (long)Math.Floor(targetH) - extra);
                    tileW = (xMax - xMin) / nW;
                    tileH = (yMax - yMin) / nH;
                }
            }

            // Centre tiles, row by row; the last row/column is clipped in position AND uv.
            if (fillCenter)
            {
                for (long j = 0; j < nH; j++)
                {
                    TileSpan(yMin, yMax, tileH, j, uvMin.y, uvMax.y, out float y1, out float y2, out float vTop);
                    for (long i = 0; i < nW; i++)
                    {
                        TileSpan(xMin, xMax, tileW, i, uvMin.x, uvMax.x, out float x1, out float x2, out float uRight);
                        mesh.AddQuad(origin + new Vector2(x1, y1), origin + new Vector2(x2, y2), color,
                            uvMin, new Vector2(uRight, vTop));
                    }
                }
            }

            if (!hasBorder) return;

            // Left/right edge strips, tiled vertically at the centre's tile height.
            for (long j = 0; j < nH; j++)
            {
                TileSpan(yMin, yMax, tileH, j, uvMin.y, uvMax.y, out float y1, out float y2, out float vTop);
                mesh.AddQuad(origin + new Vector2(0f, y1), origin + new Vector2(xMin, y2), color,
                    new Vector2(outer.x, uvMin.y), new Vector2(uvMin.x, vTop));
                mesh.AddQuad(origin + new Vector2(xMax, y1), origin + new Vector2(rect.width, y2), color,
                    new Vector2(uvMax.x, uvMin.y), new Vector2(outer.z, vTop));
            }

            // Bottom/top edge strips, tiled horizontally at the centre's tile width.
            for (long i = 0; i < nW; i++)
            {
                TileSpan(xMin, xMax, tileW, i, uvMin.x, uvMax.x, out float x1, out float x2, out float uRight);
                mesh.AddQuad(origin + new Vector2(x1, 0f), origin + new Vector2(x2, yMin), color,
                    new Vector2(uvMin.x, outer.y), new Vector2(uRight, uvMin.y));
                mesh.AddQuad(origin + new Vector2(x1, yMax), origin + new Vector2(x2, rect.height), color,
                    new Vector2(uvMin.x, uvMax.y), new Vector2(uRight, outer.w));
            }

            // Corners.
            mesh.AddQuad(origin, origin + new Vector2(xMin, yMin), color,
                new Vector2(outer.x, outer.y), new Vector2(uvMin.x, uvMin.y));
            mesh.AddQuad(origin + new Vector2(xMax, 0f), origin + new Vector2(rect.width, yMin), color,
                new Vector2(uvMax.x, outer.y), new Vector2(outer.z, uvMin.y));
            mesh.AddQuad(origin + new Vector2(0f, yMax), origin + new Vector2(xMin, rect.height), color,
                new Vector2(outer.x, uvMax.y), new Vector2(uvMin.x, outer.w));
            mesh.AddQuad(origin + new Vector2(xMax, yMax), origin + new Vector2(rect.width, rect.height), color,
                new Vector2(uvMax.x, uvMax.y), new Vector2(outer.z, outer.w));
        }

        // Tile k of a run from lo toward hi at the given pitch; the tile that crosses hi is
        // cut there and its far uv moved in proportion (tiles never stretch).
        static void TileSpan(float lo, float hi, float pitch, long k, float uvLo, float uvHi,
            out float a, out float b, out float uvFar)
        {
            a = lo + k * pitch;
            b = lo + (k + 1) * pitch;
            uvFar = uvHi;
            if (b > hi)
            {
                uvFar = uvLo + (uvHi - uvLo) * (hi - a) / (b - a);
                b = hi;
            }
        }

        // ── Filled ───────────────────────────────────────────────────

        static void Filled(UIMesh mesh, Sprite sprite, Rect rect, Vector2 pivot, Color32 color, in ImageMeshSettings s)
        {
            float amount = Math.Clamp(s.fillAmount, 0f, 1f);
            if (amount < 0.001f) return;

            var v = DrawingDimensions(sprite, rect, pivot, s.preserveAspect);
            var o = sprite.outerUV;
            float x0 = v.x, y0 = v.y, x1 = v.z, y1 = v.w;
            float u0 = o.x, v0 = o.y, u1 = o.z, v1 = o.w;

            if (s.fillMethod == Image.FillMethod.Horizontal)
            {
                if (s.fillOrigin == (int)Image.OriginHorizontal.Right) { x0 = x1 - (x1 - x0) * amount; u0 = u1 - (u1 - u0) * amount; }
                else { x1 = x0 + (x1 - x0) * amount; u1 = u0 + (u1 - u0) * amount; }
                AddCornerQuad(mesh, color, x0, y0, x1, y1, u0, v0, u1, v1);
                return;
            }
            if (s.fillMethod == Image.FillMethod.Vertical)
            {
                if (s.fillOrigin == (int)Image.OriginVertical.Top) { y0 = y1 - (y1 - y0) * amount; v0 = v1 - (v1 - v0) * amount; }
                else { y1 = y0 + (y1 - y0) * amount; v1 = v0 + (v1 - v0) * amount; }
                AddCornerQuad(mesh, color, x0, y0, x1, y1, u0, v0, u1, v1);
                return;
            }

            if (amount >= 1f)
            {
                AddCornerQuad(mesh, color, x0, y0, x1, y1, u0, v0, u1, v1);
                return;
            }

            // Radial: a clock hand pivots at the origin (corner / edge midpoint / centre) and
            // sweeps from a start direction; the swept area is cut into 90° cells (one per
            // quadrant around the pivot), each drawn as one quad.
            RadialPlan(s.fillMethod, s.fillOrigin, out var pivotN, out var dirs);
            if (!s.fillClockwise) Array.Reverse(dirs);
            int cells = dirs.Length - 1;

            Span<Vector2> pos = stackalloc Vector2[4];
            Span<Vector2> uv = stackalloc Vector2[4];
            Span<Vector2> n = stackalloc Vector2[4];
            for (int k = 0; k < cells; k++)
            {
                float cellAmount = Math.Clamp(amount * cells - k, 0f, 1f);
                if (cellAmount < 0.001f) continue;
                RadialCell(pivotN, dirs[k], dirs[k + 1], cellAmount, n);
                for (int i = 0; i < 4; i++)
                {
                    pos[i] = new Vector2(Lerp(x0, x1, n[i].x), Lerp(y0, y1, n[i].y));
                    uv[i] = new Vector2(Lerp(u0, u1, n[i].x), Lerp(v0, v1, n[i].y));
                }
                mesh.AddQuad(pos, color, uv);
            }
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        static void AddCornerQuad(UIMesh mesh, Color32 color, float x0, float y0, float x1, float y1,
            float u0, float v0, float u1, float v1)
            => mesh.AddQuad(new Vector2(x0, y0), new Vector2(x1, y1), color, new Vector2(u0, v0), new Vector2(u1, v1));

        /// <summary>
        /// Radial geometry in the quad's normalized [0,1]² space: the pivot, and the CLOCKWISE
        /// list of directions from it to the quad boundary (first = where a clockwise fill
        /// starts; the list closes on itself for Radial360).
        /// </summary>
        internal static void RadialPlan(Image.FillMethod method, int origin, out Vector2 pivot, out Vector2[] dirs)
        {
            var up = new Vector2(0f, 1f); var down = new Vector2(0f, -1f);
            var right = new Vector2(1f, 0f); var left = new Vector2(-1f, 0f);
            switch (method)
            {
                case Image.FillMethod.Radial90:
                    switch ((Image.Origin90)(origin & 3))
                    {
                        case Image.Origin90.TopLeft: pivot = new Vector2(0f, 1f); dirs = new[] { right, down }; return;
                        case Image.Origin90.TopRight: pivot = new Vector2(1f, 1f); dirs = new[] { down, left }; return;
                        case Image.Origin90.BottomRight: pivot = new Vector2(1f, 0f); dirs = new[] { left, up }; return;
                        default: pivot = new Vector2(0f, 0f); dirs = new[] { up, right }; return;
                    }
                case Image.FillMethod.Radial180:
                    switch ((Image.Origin180)(origin & 3))
                    {
                        case Image.Origin180.Left: pivot = new Vector2(0f, 0.5f); dirs = new[] { up * 0.5f, right, down * 0.5f }; return;
                        case Image.Origin180.Top: pivot = new Vector2(0.5f, 1f); dirs = new[] { right * 0.5f, down, left * 0.5f }; return;
                        case Image.Origin180.Right: pivot = new Vector2(1f, 0.5f); dirs = new[] { down * 0.5f, left, up * 0.5f }; return;
                        default: pivot = new Vector2(0.5f, 0f); dirs = new[] { left * 0.5f, up, right * 0.5f }; return;
                    }
                default: // Radial360
                {
                    pivot = new Vector2(0.5f, 0.5f);
                    var d = new[] { down * 0.5f, left * 0.5f, up * 0.5f, right * 0.5f };
                    int start = (Image.Origin360)(origin & 3) switch
                    {
                        Image.Origin360.Right => 3,
                        Image.Origin360.Top => 2,
                        Image.Origin360.Left => 1,
                        _ => 0,
                    };
                    dirs = new Vector2[5];
                    for (int i = 0; i < 5; i++) dirs[i] = d[(start + i) % 4];
                    return;
                }
            }
        }

        /// <summary>
        /// One 90° cell: the parallelogram pivot + s·a + t·b, swept from edge a toward edge b
        /// by <paramref name="amount"/> × 90° (angle measured in the cell's own unit square).
        /// Writes 4 corners in normalized quad space, ordered like the original quads
        /// (clockwise on screen, y up); a triangular wedge repeats its pivot corner.
        /// </summary>
        static void RadialCell(Vector2 pivot, Vector2 a, Vector2 b, float amount, Span<Vector2> outCorners)
        {
            var c0 = pivot;
            var c1 = pivot + a;
            Vector2 c2, c3;
            if (amount >= 1f)
            {
                c2 = pivot + a + b;
                c3 = pivot + b;
            }
            else
            {
                double theta = amount * Math.PI * 0.5;
                if (theta <= Math.PI * 0.25)
                {
                    // The hand exits through the far side opposite edge a (the one parallel to b).
                    c2 = pivot + a + b * (float)Math.Tan(theta);
                    c3 = pivot;
                }
                else
                {
                    // The hand exits through the far side opposite edge b; the swept region keeps the far corner.
                    c2 = pivot + a + b;
                    c3 = pivot + b + a * (float)(1.0 / Math.Tan(theta));
                }
            }

            outCorners[0] = c0; outCorners[1] = c1; outCorners[2] = c2; outCorners[3] = c3;
            // Normalise winding to the original quad order (clockwise, y up = negative area).
            float area = 0f;
            for (int i = 0; i < 4; i++)
            {
                var p = outCorners[i]; var q = outCorners[(i + 1) & 3];
                area += p.x * q.y - q.x * p.y;
            }
            if (area > 0f)
            {
                (outCorners[1], outCorners[3]) = (outCorners[3], outCorners[1]);
            }
        }
    }
}

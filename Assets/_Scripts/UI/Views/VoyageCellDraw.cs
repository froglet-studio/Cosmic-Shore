using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.UI
{
    /// <summary>
    /// Draws the HyperSea's cells (<see cref="HyperSeaCells"/>) into a voyage map's mesh, as the Voyage Board's cells.js
    /// draws them (Docs/Voyages/VOYAGE_BOARD.md §4d, the wet foam): the liquid between cells, each cell's rounded section
    /// dark with its own sea and brighter toward its nucleus, a lit rim inside its membrane, the membrane as the cell's
    /// own thin pale wall (so two neighbours show as a pair of lines with the liquid between), and the nucleus as the
    /// game draws it, Nucleus.prefab's white translucent caged sphere with its deep-blue edge and a bluish halo. The
    /// nucleus is a sprite painted once into the graphic's own texture, so a nucleus costs one quad;
    /// <see cref="PaintSprites"/> paints it, <see cref="NucleusQuad"/> places it.
    /// </summary>
    public static class VoyageCellDraw
    {
        /// <summary>The sprites' footprint in a texture: the caged nucleus, then the small one beside it.</summary>
        public const int SpriteRows = BigSprite, SpriteColumns = BigSprite + SmallSprite;
        const int BigSprite = 128, SmallSprite = 32;
        /// <summary>A sprite spans this many nucleus radii from its centre (the halo reaches 1.9).</summary>
        const float Extent = 1.9f;
        /// <summary>A nucleus smaller than this (radius, px) uses the small sprite, which has no cage to alias.</summary>
        const float SmallBelow = 16f;

        static readonly Color MemPaleAuthored = new(214 / 255f, 236 / 255f, 1f, 1f);
        static Color MemPale => ColorTrace.Retrace(ColorTrace.Ui, MemPaleAuthored);
        static readonly Color NucleusLineAuthored = new(190 / 255f, 215 / 255f, 1f, 1f);
        static Color NucleusLine => ColorTrace.Retrace(ColorTrace.Ui, NucleusLineAuthored);
        // The nucleus as Nucleus.prefab draws it: a white translucent body, the cage, the deep-blue edge, a pale rim.
        static readonly Color NucleusWhiteAuthored = new(235 / 255f, 242 / 255f, 1f, 1f);
        static readonly Color NucleusGlowAuthored = new(150 / 255f, 200 / 255f, 1f, 1f);
        static readonly Color NucleusCageAuthored = new(83 / 255f, 89 / 255f, 192 / 255f, 1f);
        static readonly Color NucleusEdgeAuthored = new(0f, 6 / 255f, 191 / 255f, 1f);

        // ── The sprites ──────────────────────────────────────────────────────

        /// <summary>Paint the nucleus sprites into <paramref name="px"/> (a texture <paramref name="texW"/> wide), their
        /// lower-left corner at (x0, y0); <see cref="SpriteColumns"/> by <see cref="SpriteRows"/> texels.</summary>
        public static void PaintSprites(Color32[] px, int texW, int x0, int y0)
        {
            Paint(px, texW, x0, y0, BigSprite, cage: true);
            Paint(px, texW, x0 + BigSprite, y0, SmallSprite, cage: false);
        }

        /// <summary>The uv corners of the sprite a nucleus of radius <paramref name="radiusPx"/> uses, in a texture
        /// <paramref name="texW"/> by <paramref name="texH"/> holding the sprites at (x0, y0).</summary>
        public static (Vector2 lo, Vector2 hi) SpriteUv(float radiusPx, int texW, int texH, int x0, int y0)
        {
            bool small = radiusPx < SmallBelow;
            int sx = small ? x0 + BigSprite : x0, size = small ? SmallSprite : BigSprite;
            return (new Vector2((float)sx / texW, (float)y0 / texH), new Vector2((float)(sx + size) / texW, (float)(y0 + size) / texH));
        }

        static void Paint(Color32[] px, int texW, int x0, int y0, int size, bool cage)
        {
            float r = size / 2f / Extent;
            float aa = 1f / r;
            Color white = ColorTrace.Retrace(ColorTrace.Ui, NucleusWhiteAuthored),
                glow = ColorTrace.Retrace(ColorTrace.Ui, NucleusGlowAuthored),
                cageCol = ColorTrace.Retrace(ColorTrace.Ui, NucleusCageAuthored),
                edge = ColorTrace.Retrace(ColorTrace.Ui, NucleusEdgeAuthored);
            static Color A(Color c, float a) => new(c.r, c.g, c.b, a);
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float x = (i + .5f - size / 2f) / r, y = (j + .5f - size / 2f) / r;
                float d = Mathf.Sqrt(x * x + y * y);
                var c = new Color(0f, 0f, 0f, 0f);
                // the halo in the sea round it, bluish
                if (d >= .8f && d <= Extent) c = Over(c, A(glow, .22f * (1f - (d - .8f) / (Extent - .8f))));
                if (d < 1f)
                {
                    // the body: white and translucent, clearer toward the limb, bluish at the edge
                    c = Over(c, d < .75f ? Color.Lerp(A(white, .55f), A(white, .18f), d / .75f)
                        : Color.Lerp(A(white, .18f), A(glow, .4f), (d - .75f) / .25f));
                    if (cage)
                    {
                        float half = Mathf.Max(.8f, r / 45f) / r * .5f;
                        for (int k = 0; k < 4; k++) c = Over(c, Ring(x, y, 1f, .12f + .22f * k, Mathf.PI / 4f * k + .3f, half, aa, A(cageCol, .75f)));
                        for (int k = 0; k < 3; k++) c = Over(c, Ring(x, y, .28f + .3f * k, 1f, .25f + k * .9f, half, aa, A(cageCol, .75f)));
                    }
                    // a soft highlight where the light falls
                    float lx = x + .42f, ly = y - .42f, dh = Mathf.Sqrt(lx * lx + ly * ly);
                    c = Over(c, A(white, .3f * Mathf.Clamp01(1f - dh / .5f)));
                }
                // the deep-blue edge, then a pale rim just inside it
                float ew = Mathf.Max(1.2f, r / 18f) / r * .5f;
                c = Over(c, A(edge, .9f * Mathf.Clamp01(1f - (Mathf.Abs(d - 1f) - ew) / aa)));
                float rw = Mathf.Max(.8f, r / 40f) / r * .5f;
                c = Over(c, A(glow, .8f * Mathf.Clamp01(1f - (Mathf.Abs(d - .97f) - rw) / aa)));
                px[(y0 + j) * texW + x0 + i] = c;
            }
        }

        /// <summary>One great circle of the cage seen in projection: an ellipse, antialiased.</summary>
        static Color Ring(float x, float y, float rx, float ry, float rot, float half, float aa, Color col)
        {
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            float u = x * cs + y * sn, v = -x * sn + y * cs;
            float f = u * u / (rx * rx) + v * v / (ry * ry);
            float g = 2f * Mathf.Sqrt(u * u / (rx * rx * rx * rx) + v * v / (ry * ry * ry * ry));
            float dist = Mathf.Abs(f - 1f) / Mathf.Max(1e-4f, g);
            return new Color(col.r, col.g, col.b, col.a * Mathf.Clamp01(1f - (dist - half) / aa));
        }

        static Color Over(Color dst, Color src)
        {
            float a = src.a + dst.a * (1f - src.a);
            if (a <= 1e-5f) return new Color(0f, 0f, 0f, 0f);
            float k = dst.a * (1f - src.a);
            return new Color((src.r * src.a + dst.r * k) / a, (src.g * src.a + dst.g * k) / a, (src.b * src.a + dst.b * k) / a, a);
        }

        // ── The mesh ─────────────────────────────────────────────────────────

        /// <summary>A nucleus the slice cuts: its sprite, <paramref name="radiusPx"/> the section's radius on screen.</summary>
        public static void NucleusQuad(VertexHelper vh, Vector2 at, float radiusPx, float alpha, (Vector2 lo, Vector2 hi) uv)
        {
            float h = Mathf.Max(1.5f, radiusPx) * Extent;
            var col = new Color(1f, 1f, 1f, alpha);
            VoyageMapGraphic.Quad(vh, at + new Vector2(-h, -h), at + new Vector2(-h, h), at + new Vector2(h, h), at + new Vector2(h, -h),
                col, uv.lo, new Vector2(uv.lo.x, uv.hi.y), uv.hi, new Vector2(uv.hi.x, uv.lo.y));
        }

        /// <summary>A nucleus the slice passes above or below: its outline, dashed.</summary>
        public static void NucleusOutline(VertexHelper vh, Vector2 at, float radiusPx, float alpha, Vector2 white)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * radiusPx / 7f), 6, 32);
            var c = new Color(NucleusLine.r, NucleusLine.g, NucleusLine.b, alpha);
            for (int k = 0; k < n; k++)
            {
                float t0 = k * Mathf.PI * 2f / n, t1 = t0 + 3f / 7f * Mathf.PI * 2f / n;
                VoyageMapGraphic.Line(vh, at + new Vector2(Mathf.Cos(t0), Mathf.Sin(t0)) * radiusPx,
                    at + new Vector2(Mathf.Cos(t1), Mathf.Sin(t1)) * radiusPx, 1.2f, c, white);
            }
        }

        /// <summary>The liquid between the cells: a pale wash over the whole of <paramref name="r"/>, under the cells,
        /// thicker the wetter the foam (<see cref="HyperSeaCells.Wetness"/>).</summary>
        public static void Liquid(VertexHelper vh, Rect r, float alpha, Vector2 white)
        {
            var c = new Color(MemPale.r, MemPale.g, MemPale.b, (float)(.1 + .1 * HyperSeaCells.Wetness) * alpha);
            VoyageMapGraphic.Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax),
                new Vector2(r.xMax, r.yMin), c, white, white, white, white);
        }

        /// <summary>
        /// One cell's wet section, <paramref name="pts"/> its rounded outline on screen. <paramref name="fill"/> (0-1) fills
        /// it dark with its own sea, <paramref name="sea"/>, brighter toward <paramref name="nucleusAt"/>; with
        /// <paramref name="inner"/> a lit rim runs inside its membrane too. The membrane itself is <see cref="Walls"/>.
        /// </summary>
        public static void Cell(VertexHelper vh, List<Vector2> pts, Vector2 nucleusAt, Color sea, float cellPx, float fill,
            bool inner, Vector2 white)
        {
            int n = pts.Count;
            if (n < 3 || fill <= 0f) return;
            var dark = new Color(sea.r * .34f, sea.g * .36f, sea.b * .44f, fill);
            var bright = new Color(Mathf.Min(1f, sea.r * 1.5f), Mathf.Min(1f, sea.g * 1.5f), Mathf.Min(1f, sea.b * 1.5f), 1f);
            float reach = cellPx * .62f;
            Color At(Vector2 p)
            {
                float t = Vector2.Distance(p, nucleusAt) / reach;
                var c = t < .6f ? Color.Lerp(Color.Lerp(dark, bright, .45f), Color.Lerp(dark, sea, .18f), t / .6f)
                    : Color.Lerp(Color.Lerp(dark, sea, .18f), dark, Mathf.Clamp01((t - .6f) / .4f));
                c.a = fill;
                return c;
            }
            int i0 = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.uv0 = white;
            v.position = nucleusAt;
            v.color = At(nucleusAt);
            vh.AddVert(v);
            // a middle ring, so the glow's fall-off at 60% shows
            for (int k = 0; k < n; k++)
            {
                v.position = Vector2.Lerp(nucleusAt, pts[k], .5f);
                v.color = At(v.position);
                vh.AddVert(v);
            }
            for (int k = 0; k < n; k++)
            {
                v.position = pts[k];
                v.color = At(pts[k]);
                vh.AddVert(v);
            }
            for (int k = 0; k < n; k++)
            {
                int m0 = i0 + 1 + k, m1 = i0 + 1 + (k + 1) % n, o0 = m0 + n, o1 = m1 + n;
                vh.AddTriangle(i0, m0, m1);
                vh.AddTriangle(m0, o0, o1);
                vh.AddTriangle(m0, o1, m1);
            }
            if (!inner) return;
            // the rim inside the membrane: the sea lit through it, then the membrane's own pale
            var lit = new Color(Mathf.Min(1f, sea.r * 1.8f), Mathf.Min(1f, sea.g * 1.8f), Mathf.Min(1f, sea.b * 1.8f), .22f * fill);
            Band(vh, pts, nucleusAt, Mathf.Max(1.5f, cellPx * .08f), lit, white);
            Band(vh, pts, nucleusAt, Mathf.Max(1f, cellPx * .035f), new Color(MemPale.r, MemPale.g, MemPale.b, .22f * fill), white);
        }

        static void Band(VertexHelper vh, List<Vector2> pts, Vector2 centre, float width, Color c, Vector2 white)
        {
            int n = pts.Count;
            for (int k = 0; k < n; k++)
            {
                var p = pts[k];
                var q = pts[(k + 1) % n];
                if ((q - p).sqrMagnitude < .25f) continue;
                var pi = p + (centre - p).normalized * Mathf.Min(width, Vector2.Distance(centre, p));
                var qi = q + (centre - q).normalized * Mathf.Min(width, Vector2.Distance(centre, q));
                VoyageMapGraphic.Quad(vh, p, q, qi, pi, c, white, white, white, white);
            }
        }

        /// <summary>
        /// One cell's membrane: its own thin pale wall round its rounded outline, fading in with <paramref name="alpha"/>.
        /// Every cell draws its own, so two neighbours show as a pair of lines with the liquid between them.
        /// </summary>
        public static void Walls(VertexHelper vh, List<Vector2> pts, float cellPx, float alpha, Vector2 white)
        {
            int n = pts.Count;
            var pale = new Color(MemPale.r, MemPale.g, MemPale.b, .7f * alpha);
            float w = Mathf.Max(1f, cellPx * .008f);
            for (int k = 0; k < n; k++)
            {
                var p = pts[k];
                var q = pts[(k + 1) % n];
                if ((q - p).sqrMagnitude < .04f) continue;
                VoyageMapGraphic.Line(vh, p, q, w, pale, white);
            }
        }
    }
}

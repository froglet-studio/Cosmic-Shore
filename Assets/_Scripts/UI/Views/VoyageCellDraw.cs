using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.UI
{
    /// <summary>
    /// Draws the HyperSea's cells (<see cref="HyperSeaCells"/>) into a voyage map's mesh, as the Voyage Board's cells.js
    /// draws them (Docs/Voyages/VOYAGE_BOARD.md §4c): each cell's section filled with its own sea, the cytoplasm brighter
    /// toward the nucleus and deepening to the membrane's blue at the walls, the membrane as a pale wall with a dark seam
    /// between cells, and the nucleus as the game draws it, a lit translucent caged sphere (Nucleus.prefab's cage shader:
    /// a bright fresnel rim, a halo in the sea). The nucleus is a sprite painted once into the graphic's own texture, so a
    /// nucleus costs one quad; <see cref="PaintSprites"/> paints it, <see cref="NucleusQuad"/> places it.
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
        static readonly Color MemBlueAuthored = new(60 / 255f, 90 / 255f, 210 / 255f, 1f);
        static Color MemBlue => ColorTrace.Retrace(ColorTrace.Ui, MemBlueAuthored);
        static readonly Color SeamAuthored = new(5 / 255f, 9 / 255f, 18 / 255f, 1f);
        static Color Seam => ColorTrace.Retrace(ColorTrace.Ui, SeamAuthored);
        static readonly Color WarmAuthored = new(1f, 248 / 255f, 226 / 255f, 1f);
        static Color Warm => ColorTrace.Retrace(ColorTrace.Ui, WarmAuthored);
        static readonly Color WallBlueAuthored = new(37 / 255f, 54 / 255f, 160 / 255f, 1f);
        static Color WallBlue => ColorTrace.Retrace(ColorTrace.Ui, WallBlueAuthored);
        static readonly Color NucleusLineAuthored = new(1f, 236 / 255f, 190 / 255f, 1f);
        static Color NucleusLine => ColorTrace.Retrace(ColorTrace.Ui, NucleusLineAuthored);

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
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float x = (i + .5f - size / 2f) / r, y = (j + .5f - size / 2f) / r;
                float d = Mathf.Sqrt(x * x + y * y);
                var c = new Color(0f, 0f, 0f, 0f);
                // the halo in the sea round it
                if (d >= 1f && d <= Extent) c = Over(c, new Color(1f, 236 / 255f, 190 / 255f, .32f * (1f - (d - 1f) / (Extent - 1f))));
                if (d < 1f)
                {
                    // the body: translucent, brighter where the light falls (upper left), bright again at the limb
                    float hx = x + .35f, hy = y - .35f;
                    float t = Mathf.Clamp01(.5f * d + .5f * Mathf.Sqrt(hx * hx + hy * hy) / 1.35f);
                    c = Over(c, Body(t));
                    if (cage)
                    {
                        float half = Mathf.Max(.8f, r * .03f) / r * .5f;
                        for (int k = 0; k < 4; k++) c = Over(c, Ring(x, y, 1f, .12f + .22f * k, Mathf.PI / 4f * k + .3f, half, aa, .55f));
                        for (int k = 0; k < 3; k++) c = Over(c, Ring(x, y, .28f + .3f * k, 1f, .25f + k * .9f, half, aa, .55f));
                    }
                    // the fresnel band inside the rim
                    float d0 = 1f - Mathf.Max(1f, r * .04f) / r, bw = Mathf.Max(2f, r * .09f) / r * .5f;
                    float band = Mathf.Clamp01(1f - (Mathf.Abs(d - d0) - bw) / aa);
                    c = Over(c, ColorTrace.Retrace(ColorTrace.Ui, new Color(1f, 236 / 255f, 190 / 255f, .35f * band)));
                    // the highlight
                    float lx = x + .42f, ly = y - .42f, dh = Mathf.Sqrt(lx * lx + ly * ly);
                    c = Over(c, new Color(1f, 1f, 1f, .55f * Mathf.Clamp01(1f - dh / .5f)));
                }
                // the rim, bright and thin
                float rw = Mathf.Max(1f, r * .035f) / r * .5f;
                float rim = Mathf.Clamp01(1f - (Mathf.Abs(d - 1f) - rw) / aa);
                c = Over(c, ColorTrace.Retrace(ColorTrace.Ui, new Color(1f, 250 / 255f, 236 / 255f, .95f * rim)));
                px[(y0 + j) * texW + x0 + i] = c;
            }
        }

        static Color Body(float t)
        {
            var c0 = ColorTrace.Retrace(ColorTrace.Ui, new Color(1f, 252 / 255f, 240 / 255f, .85f));
            var c1 = ColorTrace.Retrace(ColorTrace.Ui, new Color(1f, 238 / 255f, 196 / 255f, .45f));
            var c2 = ColorTrace.Retrace(ColorTrace.Ui, new Color(1f, 224 / 255f, 160 / 255f, .35f));
            var c3 = ColorTrace.Retrace(ColorTrace.Ui, new Color(1f, 246 / 255f, 220 / 255f, .95f));
            if (t < .55f) return Color.Lerp(c0, c1, t / .55f);
            if (t < .92f) return Color.Lerp(c1, c2, (t - .55f) / .37f);
            return Color.Lerp(c2, c3, (t - .92f) / .08f);
        }

        /// <summary>One great circle of the cage seen in projection: an ellipse, antialiased.</summary>
        static Color Ring(float x, float y, float rx, float ry, float rot, float half, float aa, float alpha)
        {
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            float u = x * cs + y * sn, v = -x * sn + y * cs;
            float f = u * u / (rx * rx) + v * v / (ry * ry);
            float g = 2f * Mathf.Sqrt(u * u / (rx * rx * rx * rx) + v * v / (ry * ry * ry * ry));
            float dist = Mathf.Abs(f - 1f) / Mathf.Max(1e-4f, g);
            return new Color(1f, 248 / 255f, 225 / 255f, alpha * Mathf.Clamp01(1f - (dist - half) / aa));
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

        /// <summary>
        /// One cell's section, <paramref name="pts"/> its corners on screen. <paramref name="fill"/> (0-1) fills it with
        /// its own sea, <paramref name="sea"/>; with <paramref name="inner"/> the cytoplasm's gradient round
        /// <paramref name="nucleusAt"/> and the membrane's inner walls are drawn too. The walls between cells are drawn
        /// separately (<see cref="Walls"/>) so a shared one is drawn once.
        /// </summary>
        public static void Cell(VertexHelper vh, List<Vector2> pts, Vector2 nucleusAt, Color sea, float cellPx, float fill,
            bool inner, Vector2 white)
        {
            int n = pts.Count;
            if (n < 3) return;
            var centre = Vector2.zero;
            foreach (var p in pts) centre += p;
            centre /= n;
            if (fill > 0f)
            {
                float reach = cellPx * .62f;
                Color At(Vector2 p)
                {
                    var c = sea;
                    if (inner)
                    {
                        float t = Vector2.Distance(p, nucleusAt) / reach;
                        c = t < .6f ? Color.Lerp(c, Warm, .14f * (1f - t / .6f)) : Color.Lerp(c, WallBlue, .45f * Mathf.Clamp01((t - .6f) / .4f));
                    }
                    c.a = fill;
                    return c;
                }
                int i0 = vh.currentVertCount;
                var v = UIVertex.simpleVert;
                v.uv0 = white;
                v.position = centre;
                v.color = At(centre);
                vh.AddVert(v);
                if (inner)
                {
                    // a middle ring, so the gradient's clear band at 60% shows
                    for (int k = 0; k < n; k++)
                    {
                        v.position = Vector2.Lerp(centre, pts[k], .5f);
                        v.color = At(v.position);
                        vh.AddVert(v);
                    }
                }
                for (int k = 0; k < n; k++)
                {
                    v.position = pts[k];
                    v.color = At(pts[k]);
                    vh.AddVert(v);
                }
                if (!inner)
                    for (int k = 0; k < n; k++) vh.AddTriangle(i0, i0 + 1 + k, i0 + 1 + (k + 1) % n);
                else
                    for (int k = 0; k < n; k++)
                    {
                        int m0 = i0 + 1 + k, m1 = i0 + 1 + (k + 1) % n, o0 = m0 + n, o1 = m1 + n;
                        vh.AddTriangle(i0, m0, m1);
                        vh.AddTriangle(m0, o0, o1);
                        vh.AddTriangle(m0, o1, m1);
                    }
            }
            if (!inner) return;
            // the membrane seen from inside: a blue band and a pale wall along every face
            float mem = Mathf.Clamp(cellPx * .035f, 1f, 14f);
            Band(vh, pts, centre, mem * 3.5f, new Color(MemBlue.r, MemBlue.g, MemBlue.b, .35f), white);
            Band(vh, pts, centre, mem * 1.1f, new Color(MemPale.r, MemPale.g, MemPale.b, .22f), white);
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
        /// The walls of one cell's section: each face once across the map (<paramref name="seen"/> keys a face by its
        /// midpoint), a pale line on a dark seam once a cell is big, a pale line fading in with <paramref name="alpha"/>
        /// while it is small.
        /// </summary>
        public static void Walls(VertexHelper vh, List<Vector2> pts, float cellPx, float alpha, HashSet<long> seen, Vector2 white)
        {
            int n = pts.Count;
            bool inner = cellPx > 90f;
            float mem = Mathf.Clamp(cellPx * .035f, 1f, 14f);
            var pale = new Color(MemPale.r, MemPale.g, MemPale.b, alpha * (inner ? .5f : Mathf.Min(.5f, .14f + cellPx / 200f)));
            var seam = new Color(Seam.r, Seam.g, Seam.b, .85f * alpha);
            for (int k = 0; k < n; k++)
            {
                var p = pts[k];
                var q = pts[(k + 1) % n];
                if ((q - p).sqrMagnitude < .25f) continue;
                var mid = (p + q) * .5f;
                long key = ((long)Mathf.RoundToInt(mid.x * 2f) << 32) ^ (uint)Mathf.RoundToInt(mid.y * 2f);
                if (!seen.Add(key)) continue;
                if (inner)
                {
                    VoyageMapGraphic.Line(vh, p, q, Mathf.Max(1.5f, mem * .7f), pale, white);
                    VoyageMapGraphic.Line(vh, p, q, 1.2f, seam, white);
                }
                else VoyageMapGraphic.Line(vh, p, q, 1f, pale, white);
            }
        }
    }
}

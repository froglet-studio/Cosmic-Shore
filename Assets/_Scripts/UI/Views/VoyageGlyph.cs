using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A small stroked icon drawn as a mesh (Frogma Rev 120 B's chip): a crystal (a diamond with its girdle), the Ark (a
    /// hull with its cabin and mast) or a clock (a ring with two hands). No sprite, so it stays sharp at any size and takes
    /// its colour from <see cref="Graphic.color"/>, which the caller traces through the colour set.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoyageGlyph : MaskableGraphic
    {
        public enum Shape
        {
            Gem = 0,
            Ark = 1,
            Clock = 2,
        }

        const float Stroke = 2f;
        const int ClockSegments = 28;

        Shape _shape;

        public Shape Form
        {
            get => _shape;
            set { _shape = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            // The drawings are authored on the SVG boxes Rev 120 uses (y down); map them into the rect.
            switch (_shape)
            {
                case Shape.Gem:
                    // viewBox 20 x 26: M10 1 l8 12 -8 12 -8 -12 z, and the girdle M2 13 h16 (half strength).
                    Poly(vh, r, 20f, 26f, true, color, (10, 1), (18, 13), (10, 25), (2, 13));
                    Poly(vh, r, 20f, 26f, false, Fade(color, 0.5f), (2, 13), (18, 13));
                    break;
                case Shape.Ark:
                    // viewBox 40 x 20: the hull M1 12 h38 l-6 7 H7 z, the cabin M10 12 V6 h14 l5 6, the mast M15 6 V2.
                    Poly(vh, r, 40f, 20f, true, color, (1, 12), (39, 12), (33, 19), (7, 19));
                    Poly(vh, r, 40f, 20f, false, color, (10, 12), (10, 6), (24, 6), (29, 12));
                    Poly(vh, r, 40f, 20f, false, color, (15, 6), (15, 2));
                    break;
                default:
                    // viewBox 22 x 22: a ring r 9 at (11, 11) and the hands M11 6 v5 l3 2.
                    var ring = new (float, float)[ClockSegments];
                    for (int i = 0; i < ClockSegments; i++)
                    {
                        float a = i * Mathf.PI * 2f / ClockSegments;
                        ring[i] = (11f + 9f * Mathf.Cos(a), 11f + 9f * Mathf.Sin(a));
                    }
                    Poly(vh, r, 22f, 22f, true, color, ring);
                    Poly(vh, r, 22f, 22f, false, color, (11, 6), (11, 11), (14, 13));
                    break;
            }
        }

        static Color Fade(Color c, float a) => new(c.r, c.g, c.b, c.a * a);

        /// <summary>A stroked polyline through <paramref name="points"/> (box units, y down), closed or open.</summary>
        static void Poly(VertexHelper vh, Rect r, float boxW, float boxH, bool closed, Color c, params (float x, float y)[] points)
        {
            int n = points.Length;
            int segments = closed ? n : n - 1;
            for (int i = 0; i < segments; i++)
            {
                var a = Map(r, boxW, boxH, points[i]);
                var b = Map(r, boxW, boxH, points[(i + 1) % n]);
                Segment(vh, a, b, c);
            }
        }

        static Vector2 Map(Rect r, float boxW, float boxH, (float x, float y) p) =>
            new(r.xMin + p.x / boxW * r.width, r.yMax - p.y / boxH * r.height);

        /// <summary>One stroke from <paramref name="a"/> to <paramref name="b"/>, extended half a stroke at each end so
        /// corners meet.</summary>
        static void Segment(VertexHelper vh, Vector2 a, Vector2 b, Color c)
        {
            var d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            d.Normalize();
            var half = Stroke * 0.5f;
            var n = new Vector2(-d.y, d.x) * half;
            a -= d * half;
            b += d * half;
            int i = vh.currentVertCount;
            vh.AddVert(a - n, c, Vector2.zero);
            vh.AddVert(a + n, c, Vector2.zero);
            vh.AddVert(b + n, c, Vector2.zero);
            vh.AddVert(b - n, c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}

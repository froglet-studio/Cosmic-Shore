using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A small stroked icon drawn as a mesh (Frogma Rev 120 B's chip): a crystal (a diamond with its girdle), the Ark (a
    /// hull with its cabin and mast) or a clock (a ring with two hands). No sprite, so it stays sharp at any size and takes
    /// its colour from <see cref="Graphic.color"/>, which the caller traces through the colour set. The three vessel
    /// roles have their glyphs here too, on the 30 x 30 box the Milestone 2 boards draw them on (Frogma Rev 133-138).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoyageGlyph : MaskableGraphic
    {
        public enum Shape
        {
            Gem = 0,
            Ark = 1,
            Clock = 2,
            /// <summary>An event next door (Rev 134 Pause A: a flag).</summary>
            Flag = 3,
            /// <summary>A rival crew (Rev 134 Pause A: a warning triangle).</summary>
            Warn = 4,
            /// <summary>A distress call (Rev 134 WorldEvents: a flare over a still hull).</summary>
            Flare = 5,
            /// <summary>A predator pack (Rev 134 WorldEvents: three bodies gathering).</summary>
            Pack = 6,
            /// <summary>The vote's keycaps (Rev 134 Vote B: D-pad left and right).</summary>
            Left = 7,
            Right = 8,
            Guardian = 9,
            Protector = 10,
            Destroyer = 11,
            Cultivator = 12,
            Hunter = 13,
            Shepherd = 14,
            Pathfinder = 15,
        }

        /// <summary>The glyph of a vessel role (the Gem for none). The board draws a role's mark beside each archetype.</summary>
        public static Shape Of(CosmicShore.Data.VesselRole role) => role switch
        {
            CosmicShore.Data.VesselRole.Cultivator => Shape.Cultivator,
            CosmicShore.Data.VesselRole.Destroyer => Shape.Destroyer,
            CosmicShore.Data.VesselRole.Guardian => Shape.Guardian,
            _ => Shape.Gem,
        };

        /// <summary>The glyph of an archetype: its role's.</summary>
        public static Shape Of(CosmicShore.Data.VesselArchetype a) => Of(CosmicShore.Data.VesselArchetypes.RoleOf(a));

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
                case Shape.Flag:
                    // viewBox 20 x 20: the pole M5 2 V18, the flag M5 3 h10 l-3 3.5 3 3.5 H5.
                    Poly(vh, r, 20f, 20f, false, color, (5, 2), (5, 18));
                    Poly(vh, r, 20f, 20f, false, color, (5, 3), (15, 3), (12, 6.5f), (15, 10), (5, 10));
                    break;
                case Shape.Warn:
                    // viewBox 22 x 20: M11 2 L20 18 H2 z, the mark M11 8 v5, the dot M11 15.5 v0.5.
                    Poly(vh, r, 22f, 20f, true, color, (11, 2), (20, 18), (2, 18));
                    Poly(vh, r, 22f, 20f, false, color, (11, 8), (11, 13));
                    Poly(vh, r, 22f, 20f, false, color, (11, 15.2f), (11, 16));
                    break;
                case Shape.Flare:
                    // viewBox 20 x 20: the trail M10 18 V8, the burst a small diamond round (10, 5).
                    Poly(vh, r, 20f, 20f, false, Fade(color, 0.6f), (10, 18), (10, 8));
                    Poly(vh, r, 20f, 20f, true, color, (10, 2), (13, 5), (10, 8), (7, 5));
                    break;
                case Shape.Pack:
                    // viewBox 22 x 20: three small diamonds.
                    Poly(vh, r, 22f, 20f, true, color, (6, 3), (9, 6), (6, 9), (3, 6));
                    Poly(vh, r, 22f, 20f, true, color, (16, 3), (19, 6), (16, 9), (13, 6));
                    Poly(vh, r, 22f, 20f, true, color, (11, 11), (14, 14), (11, 17), (8, 14));
                    break;
                case Shape.Left:
                    Poly(vh, r, 20f, 20f, true, color, (14, 4), (14, 16), (5, 10));
                    break;
                case Shape.Right:
                    Poly(vh, r, 20f, 20f, true, color, (6, 4), (6, 16), (15, 10));
                    break;
                case Shape.Guardian:
                    // M15 3 L27 8 L25 20 L15 28 L5 20 L3 8 Z: a shield.
                    Poly(vh, r, Box, Box, true, color, (15, 3), (27, 8), (25, 20), (15, 28), (5, 20), (3, 8));
                    break;
                case Shape.Protector:
                    // Two rings about the centre, r 12 and r 6.
                    Poly(vh, r, Box, Box, true, color, Ring(15f, 15f, 12f));
                    Poly(vh, r, Box, Box, true, color, Ring(15f, 15f, 6f));
                    break;
                case Shape.Destroyer:
                    // M15 2 L19 11 L28 15 L19 19 L15 28 L11 19 L2 15 L11 11 Z: a four-point star.
                    Poly(vh, r, Box, Box, true, color, (15, 2), (19, 11), (28, 15), (19, 19), (15, 28), (11, 19), (2, 15), (11, 11));
                    break;
                case Shape.Cultivator:
                    // A stem M15 28 L15 12 and two leaves (M15 16 C9 16 6 11 6 6 C11 6 15 9 15 14; M15 14 C15 9 19 5 25 5
                    // C25 11 21 15 15 15).
                    Poly(vh, r, Box, Box, false, color, (15, 28), (15, 12));
                    Poly(vh, r, Box, Box, false, color, Curve((15, 16), (9, 16), (6, 11), (6, 6), (11, 6), (15, 9), (15, 14)));
                    Poly(vh, r, Box, Box, false, color, Curve((15, 14), (15, 9), (19, 5), (25, 5), (25, 11), (21, 15), (15, 15)));
                    break;
                case Shape.Hunter:
                    // Cross hairs M15 3 L15 27, M3 15 L27 15, and a ring r 7.
                    Poly(vh, r, Box, Box, false, color, (15, 3), (15, 27));
                    Poly(vh, r, Box, Box, false, color, (3, 15), (27, 15));
                    Poly(vh, r, Box, Box, true, color, Ring(15f, 15f, 7f));
                    break;
                case Shape.Shepherd:
                    // A crook's arch M6 22 C6 10 24 10 24 22, its two feet and a mark over it.
                    Poly(vh, r, Box, Box, false, color, Curve((6, 22), (6, 10), (24, 10), (24, 22)));
                    Poly(vh, r, Box, Box, false, color, (10, 22), (10, 26));
                    Poly(vh, r, Box, Box, false, color, (20, 22), (20, 26));
                    Poly(vh, r, Box, Box, false, color, (15, 6), (15, 12));
                    break;
                case Shape.Pathfinder:
                    // A course M3 22 C9 22 9 8 15 8 C21 8 21 22 27 22 and its arrow head M23 18 L27 22 L23 26.
                    Poly(vh, r, Box, Box, false, color, Curve((3, 22), (9, 22), (9, 8), (15, 8), (21, 8), (21, 22), (27, 22)));
                    Poly(vh, r, Box, Box, false, color, (23, 18), (27, 22), (23, 26));
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

        /// <summary>The class glyphs' box (viewBox 0 0 30 30).</summary>
        const float Box = 30f;
        const int CurveSegments = 10;

        /// <summary>A ring of <see cref="ClockSegments"/> points about (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
        static (float, float)[] Ring(float cx, float cy, float radius)
        {
            var ring = new (float, float)[ClockSegments];
            for (int i = 0; i < ClockSegments; i++)
            {
                float a = i * Mathf.PI * 2f / ClockSegments;
                ring[i] = (cx + radius * Mathf.Cos(a), cy + radius * Mathf.Sin(a));
            }
            return ring;
        }

        /// <summary>A run of cubic curves (SVG C): the start, then three points per curve, flattened to a polyline.</summary>
        static (float, float)[] Curve(params (float x, float y)[] p)
        {
            int curves = (p.Length - 1) / 3;
            var points = new (float, float)[curves * CurveSegments + 1];
            points[0] = p[0];
            for (int c = 0; c < curves; c++)
            {
                var a = p[c * 3];
                var b = p[c * 3 + 1];
                var d = p[c * 3 + 2];
                var e = p[c * 3 + 3];
                for (int i = 1; i <= CurveSegments; i++)
                {
                    float t = i / (float)CurveSegments, u = 1f - t;
                    float w0 = u * u * u, w1 = 3f * u * u * t, w2 = 3f * u * t * t, w3 = t * t * t;
                    points[c * CurveSegments + i] = (w0 * a.x + w1 * b.x + w2 * d.x + w3 * e.x, w0 * a.y + w1 * b.y + w2 * d.y + w3 * e.y);
                }
            }
            return points;
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

using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The CHARGE petal's outline — one petal of the element flower — as a normalised convex
    /// pentagon, plus the two derived radii anything drawn inside it needs.
    ///
    /// <para><b>It is a MEASUREMENT of the shipped sprite, not a hand-drawn approximation.</b> The
    /// numbers below were traced off the alpha of <c>Resources/ElementPetals/charge_petal.png</c>
    /// (256², opaque bounding box x 81..174, y 23.5..117): every row's span walks a straight line
    /// to within a pixel, so the shape really is a pentagon and these are its corners. Two
    /// independent facts fall out of the trace and confirm it: the two lower edges meet at
    /// <b>72.3°</b>, which is the inward-pointing 72° apex the element flower is built from (five
    /// petals at 72° each), and the shape is symmetric about its own centre line to half a pixel.
    /// </para>
    ///
    /// <para><b>Normalised over the petal's own bounding box</b>, x right and y UP (UI space, so
    /// the apex that points inward in the flower is at y = 0 here), which makes the coordinates
    /// simultaneously the POSITIONS in a square rect and the UVs into a square picture — the one
    /// property that lets a shaped window sample a render texture with nothing squashed and
    /// nothing thrown away.</para>
    ///
    /// <para><b>One source of truth on purpose.</b> The scope's picture, its opaque backing, its
    /// reticle cap and its recharge ring are all sized from this outline, so the shape the pilot
    /// sees and the radii drawn inside it cannot disagree — and re-tracing the sprite moves all
    /// four at once. <see cref="ScopePetalImage"/> is the renderer.</para>
    /// </summary>
    public static class ScopePetalGeometry
    {
        /// <summary>
        /// The petal's corners, counter-clockwise from the APEX, normalised over its own bounding
        /// box. Convex, so a triangle fan from any interior point is a valid tessellation.
        /// </summary>
        public static readonly Vector2[] Outline =
        {
            new(0.500000f, 0.000000f),   // apex — points at the flower's centre
            new(1.000000f, 0.684356f),   // right shoulder (the widest row)
            new(0.817204f, 1.000000f),   // top-right
            new(0.182796f, 1.000000f),   // top-left
            new(0.000000f, 0.684356f),   // left shoulder
        };

        /// <summary>
        /// The bounding box's centre, which is also the picture's optical centre (UV 0.5, 0.5) —
        /// so this is where the camera's own axis lands and therefore where the reticle belongs.
        /// It is inside the polygon, which is what makes the fan below legal.
        /// </summary>
        public static readonly Vector2 Centre = new(0.5f, 0.5f);

        /// <summary>
        /// The largest circle centred on <see cref="Centre"/> that fits inside the petal, as a
        /// fraction of the bounding box's SIDE. The binding constraint is the pair of long lower
        /// edges running down to the apex, not the top edge — so this is meaningfully tighter than
        /// a half-width, and anything drawn as a circle inside the shape has to respect it or it
        /// pokes out through the taper.
        /// </summary>
        public static readonly float Inradius01;

        /// <summary>
        /// The smallest circle centred on <see cref="Centre"/> that contains the petal, as a
        /// fraction of the side. Recorded because it is what a ring drawn OUTSIDE the shape would
        /// need, and it is <b>1.18 × the half-height</b> — which is why the recharge ring is drawn
        /// inside the petal instead: a circumscribing ring pushes the instrument off the left edge
        /// of the screen at the margins this window is authored at.
        /// </summary>
        public static readonly float Circumradius01;

        /// <summary>How much of its own bounding box the petal fills. Measured 0.600.</summary>
        public static readonly float AreaFraction;

        static ScopePetalGeometry()
        {
            // Derived rather than transcribed: a second copy of a number measured from the same
            // outline is a second thing to keep in step.
            float inner = float.MaxValue, outer = 0f, twiceArea = 0f;

            for (int i = 0; i < Outline.Length; i++)
            {
                Vector2 a = Outline[i];
                Vector2 b = Outline[(i + 1) % Outline.Length];

                Vector2 edge = b - a;
                float len = edge.magnitude;
                if (len > 1e-6f)
                {
                    // Distance from the centre to this edge's line.
                    Vector2 normal = new(edge.y / len, -edge.x / len);
                    inner = Mathf.Min(inner, Mathf.Abs(Vector2.Dot(Centre - a, normal)));
                }

                outer = Mathf.Max(outer, (a - Centre).magnitude);
                twiceArea += a.x * b.y - b.x * a.y;
            }

            Inradius01 = inner;
            Circumradius01 = outer;
            AreaFraction = Mathf.Abs(twiceArea) * 0.5f;
        }

        /// <summary>
        /// The OUTWARD miter direction at corner <paramref name="i"/>, scaled so that offsetting
        /// the corner along it by <c>w</c> moves both adjacent edges out by exactly <c>w</c>. The
        /// miter matters here rather than being pedantry: the apex is a 72° corner, where a plain
        /// bisector offset would produce a feather 41% too thin and the point would read as
        /// harder-edged than the rest of the outline.
        /// </summary>
        public static Vector2 OutwardMiter(int i)
        {
            int n = Outline.Length;
            Vector2 prev = Outline[(i - 1 + n) % n];
            Vector2 here = Outline[i];
            Vector2 next = Outline[(i + 1) % n];

            Vector2 nPrev = OutwardNormal(prev, here);
            Vector2 nNext = OutwardNormal(here, next);

            Vector2 bisector = (nPrev + nNext).normalized;
            // Clamped so a degenerate corner cannot produce an enormous spike.
            float scale = 1f / Mathf.Max(0.35f, Vector2.Dot(bisector, nNext));
            return bisector * scale;
        }

        /// <summary>Unit outward normal of the CCW edge a → b.</summary>
        static Vector2 OutwardNormal(Vector2 a, Vector2 b)
        {
            Vector2 e = b - a;
            float len = e.magnitude;
            return len > 1e-6f ? new Vector2(e.y / len, -e.x / len) : Vector2.up;
        }
    }
}

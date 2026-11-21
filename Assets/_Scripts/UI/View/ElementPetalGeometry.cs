using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The outline of EVERY element's petal, the shapes of the element flower
    /// (<c>Resources/ElementPetals/&lt;element&gt;_petal.png</c>): Charge a five-sided gem, Mass a triangle, Space a kite,
    /// Time a diamond. <see cref="ScopePetalGeometry"/> is the Charge petal alone, the Serpent scope's window; this is the
    /// same measurement for the other three, so a scope can be cut to whichever element is firing (the Shrike's, whose
    /// window is the selected mode's element — Garrett, 2026-10-16).
    ///
    /// <para><b>Traced, not drawn.</b> Each outline is the convex hull of its sprite's alpha (≥ 0.5), reduced to its
    /// corner count, with y UP and the apex (which points at the flower's centre) at the bottom, then symmetrised about
    /// x = 0.5 (each sprite is symmetric to within a pixel). It is fitted into the unit square <b>keeping its true
    /// aspect</b> — its longer side spans 0..1 and the shorter is centred — because the window is square and its picture
    /// is sampled at UV = position: stretching the Time petal (0.74 as wide as tall) into a square would draw a different
    /// shape from the one on the element flower. The same trace reproduces
    /// <see cref="ScopePetalGeometry.Outline"/> to within a pixel, which is how the method was checked — and Charge here
    /// IS that array, so the Serpent's window and this one cannot disagree.</para>
    ///
    /// <para>Every outline is convex, counter-clockwise from the apex, and contains the optical centre (0.5, 0.5), so the
    /// fan <see cref="ScopePetalImage"/> draws is a valid tessellation and the picture's centre is the camera's axis.</para>
    /// </summary>
    public static class ElementPetalGeometry
    {
        public static readonly Vector2 Centre = new(0.5f, 0.5f);

        // 122 x 86 px: wider than tall, so centred vertically.
        static readonly Vector2[] Mass =
        {
            new(0.5f, 0.1475f),   // apex
            new(1f, 0.8525f),
            new(0f, 0.8525f),
        };

        // 98 x 103 px: within 5% of square, so (like the Serpent's Charge) it fills the box.
        static readonly Vector2[] Space =
        {
            new(0.5f, 0f),      // apex
            new(1f, 0.648f),    // right shoulder
            new(0.5f, 1f),      // top
            new(0f, 0.648f),    // left shoulder
        };

        // 76 x 103 px: taller than wide, so centred horizontally.
        static readonly Vector2[] Time =
        {
            new(0.5f, 0f),        // apex
            new(0.869f, 0.49f),   // right
            new(0.5f, 1f),        // top
            new(0.131f, 0.49f),   // left
        };

        /// <summary>The petal of <paramref name="element"/>; anything that is not one of the four gets Charge's.</summary>
        public static Vector2[] Outline(Element element) => element switch
        {
            Element.Mass => Mass,
            Element.Space => Space,
            Element.Time => Time,
            _ => ScopePetalGeometry.Outline,
        };

        /// <summary>The largest circle centred on <see cref="Centre"/> inside the petal, as a fraction of its side.</summary>
        public static float Inradius01(Element element)
        {
            var o = Outline(element);
            float inner = float.MaxValue;
            for (int i = 0; i < o.Length; i++)
            {
                Vector2 a = o[i], b = o[(i + 1) % o.Length];
                Vector2 e = b - a;
                float len = e.magnitude;
                if (len <= 1e-6f) continue;
                inner = Mathf.Min(inner, Mathf.Abs(Vector2.Dot(Centre - a, new Vector2(e.y / len, -e.x / len))));
            }
            return inner;
        }

        /// <summary>The outward miter at corner <paramref name="i"/> (the feather's direction), as
        /// <see cref="ScopePetalGeometry.OutwardMiter"/> computes it for Charge.</summary>
        public static Vector2 OutwardMiter(Element element, int i)
        {
            var o = Outline(element);
            int n = o.Length;
            Vector2 nPrev = OutwardNormal(o[(i - 1 + n) % n], o[i]);
            Vector2 nNext = OutwardNormal(o[i], o[(i + 1) % n]);
            Vector2 bisector = (nPrev + nNext).normalized;
            return bisector / Mathf.Max(0.35f, Vector2.Dot(bisector, nNext));
        }

        static Vector2 OutwardNormal(Vector2 a, Vector2 b)
        {
            Vector2 e = b - a;
            float len = e.magnitude;
            return len > 1e-6f ? new Vector2(e.y / len, -e.x / len) : Vector2.up;
        }
    }
}

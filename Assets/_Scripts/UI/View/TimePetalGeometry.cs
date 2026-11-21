using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The TIME petal's outline — one petal of the element flower — the Thresher launch sight's
    /// eyepiece (<c>R_VesselActions/THRESHER.md</c> §5a). The sibling of
    /// <see cref="ScopePetalGeometry"/> (the Serpent's CHARGE petal), measured the same way.
    ///
    /// <para><b>A MEASUREMENT of the shipped sprite.</b> Traced off the alpha of
    /// <c>Resources/ElementPetals/time_petal.png</c> (256², coverage-weighted row edges, each of the
    /// four edges fitted as a line to within 0.05 px): it is an exact RHOMBUS — outer tip
    /// (127.62, 19.95), shoulders (91.76 / 163.25, 69.12), inner apex (127.62, 118.30) — whose tip
    /// and apex are both <b>72.0°</b>, the element flower's own angle, and whose shoulders sit at
    /// half height. Its bounding box is therefore <b>not square</b>: width / height = tan 36° =
    /// 0.7265 (measured 71.49 / 98.35 = 0.7269).</para>
    ///
    /// <para><b>Normalised over the petal's own bounding box</b>, x right, y UP, the inner apex (the
    /// corner that points at the flower's centre) at y = 0, as <see cref="ScopePetalGeometry"/>. The
    /// coordinates are positions in a rect of aspect <see cref="Aspect"/> AND the UVs into a picture
    /// of that aspect — so the render target behind this window must have the same aspect, or the
    /// picture is squashed (<c>ScopePipView.RenderAspect</c>).</para>
    /// </summary>
    public static class TimePetalGeometry
    {
        /// <summary>The four corners, counter-clockwise from the inner APEX. Convex.</summary>
        public static readonly Vector2[] Outline =
        {
            new(0.5f, 0f),   // inner apex — points at the flower's centre
            new(1f, 0.5f),   // right shoulder
            new(0.5f, 1f),   // outer tip
            new(0f, 0.5f),   // left shoulder
        };

        /// <summary>Bounding-box centre = the picture's optical centre = where the crosshair sits.</summary>
        public static readonly Vector2 Centre = new(0.5f, 0.5f);

        /// <summary>Bounding box width / height: tan(72° / 2), the rhombus's own proportion.</summary>
        public static readonly float Aspect = Mathf.Tan(36f * Mathf.Deg2Rad);

        /// <summary>
        /// The largest circle centred on <see cref="Centre"/> inside the petal, as a fraction of the
        /// bounding box's HEIGHT (measured in true proportions, x scaled by <see cref="Aspect"/>).
        /// Anything round drawn inside the window clamps to this, or it pokes out through the taper.
        /// </summary>
        public static readonly float Inradius01;

        static TimePetalGeometry()
        {
            float inner = float.MaxValue;
            for (int i = 0; i < Outline.Length; i++)
            {
                Vector2 a = Proportional(Outline[i]);
                Vector2 b = Proportional(Outline[(i + 1) % Outline.Length]);
                Vector2 edge = b - a;
                float len = edge.magnitude;
                if (len < 1e-6f) continue;
                Vector2 normal = new(edge.y / len, -edge.x / len);
                inner = Mathf.Min(inner, Mathf.Abs(Vector2.Dot(Proportional(Centre) - a, normal)));
            }
            Inradius01 = inner;
        }

        /// <summary>Normalised → true proportions (height 1, width <see cref="Aspect"/>).</summary>
        static Vector2 Proportional(Vector2 p) => new(p.x * Aspect, p.y);

        /// <summary>
        /// The OUTWARD miter at corner <paramref name="i"/> in TRUE proportions (so in a rect of
        /// this aspect it is a rect-space direction), scaled so offsetting the corner by <c>w</c>
        /// along it moves both adjacent edges out by exactly <c>w</c> — the feather stays even round
        /// the 72° tips, where a bisector would be 41% thin.
        /// </summary>
        public static Vector2 OutwardMiter(int i)
        {
            int n = Outline.Length;
            Vector2 prev = Proportional(Outline[(i - 1 + n) % n]);
            Vector2 here = Proportional(Outline[i]);
            Vector2 next = Proportional(Outline[(i + 1) % n]);
            Vector2 nPrev = OutwardNormal(prev, here);
            Vector2 nNext = OutwardNormal(here, next);
            Vector2 bisector = (nPrev + nNext).normalized;
            float scale = 1f / Mathf.Max(0.35f, Vector2.Dot(bisector, nNext));
            return bisector * scale;
        }

        static Vector2 OutwardNormal(Vector2 a, Vector2 b)
        {
            Vector2 e = b - a;
            float len = e.magnitude;
            return len > 1e-6f ? new Vector2(e.y / len, -e.x / len) : Vector2.up;
        }
    }
}

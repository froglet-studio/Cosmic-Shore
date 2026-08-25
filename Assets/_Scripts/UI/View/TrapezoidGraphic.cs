using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A borderless trapezoid, GENERATED rather than sprited.
    ///
    /// <para>The ability lockup's two plates are trapezoids that meet at their wide edges, and the
    /// slant is a tuning number on one shared style asset. A sprite could not carry that: a
    /// trapezoid has no 9-slice (slanted edges do not tile), so a sprited version would freeze the
    /// slant into the art and force a re-export every time the number moved - and it would need one
    /// asset per direction. Generating it makes the slant a float, keeps one graphic type serving
    /// the plate, the gauge track, the gauge clip and the press flash, and costs no texture at
    /// all.</para>
    ///
    /// <para><b>Widths are FRACTIONS of the rect</b>, so the shape is resolution-independent and a
    /// card can be resized without re-deriving anything. <see cref="FillAmount"/> cuts the shape
    /// off part-way up and interpolates the width at the cut, which is what lets a partially-filled
    /// trapezoid still be a trapezoid rather than a rectangle wearing one.</para>
    ///
    /// <para><b>The slant edge.</b> An optional hairline drawn INSIDE the shape along its two
    /// sloped sides only, solid across the middle of each side and graded to nothing before it
    /// reaches the top or bottom. It is not a border - it never closes - which is the whole point:
    /// it accents the two edges that carry the shape's identity and leaves the horizontals to the
    /// gap and the silhouette. Drawn into the SAME mesh as the fill, so an edged plate is still one
    /// draw call, and its alpha is multiplied by the graphic's own so a fade takes both together.</para>
    ///
    /// <para>Raycasting uses the RECT, not the drawn trapezoid - deliberately. This is a touch
    /// target on a phone: a hit area slightly larger than the mark is the forgiving direction, and
    /// a per-fragment hit test on a slanted edge would make the corners of an ability button
    /// mysteriously dead.</para>
    /// </summary>
    [AddComponentMenu("UI/Trapezoid Graphic", 12)]
    public class TrapezoidGraphic : MaskableGraphic
    {
        [Tooltip("Width of the TOP edge as a fraction of the rect's width.")]
        [SerializeField, Range(0f, 1f)] private float topWidth = 1f;

        [Tooltip("Width of the BOTTOM edge as a fraction of the rect's width.")]
        [SerializeField, Range(0f, 1f)] private float bottomWidth = 1f;

        [Tooltip("How much of the shape is drawn, from the bottom up. The width at the cut is " +
                 "interpolated, so a partial fill is still a trapezoid.")]
        [SerializeField, Range(0f, 1f)] private float fillAmount = 1f;

        public float TopWidth
        {
            get => topWidth;
            set { if (!Mathf.Approximately(topWidth, value)) { topWidth = value; SetVerticesDirty(); } }
        }

        public float BottomWidth
        {
            get => bottomWidth;
            set { if (!Mathf.Approximately(bottomWidth, value)) { bottomWidth = value; SetVerticesDirty(); } }
        }

        [Header("Slant edge (sloped sides only, graded to nothing at both ends)")]
        [Tooltip("Thickness in px, drawn INSIDE the shape. 0 = no edge.")]
        [SerializeField, Min(0f)] private float edgeThickness;

        [Tooltip("Colour of the slant edge. Its alpha is multiplied by the graphic's own.")]
        [SerializeField] private Color edgeColor = Color.white;

        [Tooltip("Fraction of each sloped side spent fading in at the bottom and out at the top. " +
                 "0.5 means the edge only ever reaches full opacity at the exact midpoint.")]
        [SerializeField, Range(0.01f, 0.5f)] private float edgeFade = 0.34f;

        public float EdgeThickness
        {
            get => edgeThickness;
            set { if (!Mathf.Approximately(edgeThickness, value)) { edgeThickness = value; SetVerticesDirty(); } }
        }

        public Color EdgeColor
        {
            get => edgeColor;
            set { if (edgeColor != value) { edgeColor = value; SetVerticesDirty(); } }
        }

        public float EdgeFade
        {
            get => edgeFade;
            set { if (!Mathf.Approximately(edgeFade, value)) { edgeFade = value; SetVerticesDirty(); } }
        }

        public float FillAmount
        {
            get => fillAmount;
            set
            {
                value = Mathf.Clamp01(value);
                if (!Mathf.Approximately(fillAmount, value)) { fillAmount = value; SetVerticesDirty(); }
            }
        }

        /// <summary>Sets both edges at once - the common case, since the pair is mirrored.</summary>
        public void SetEdges(float top, float bottom)
        {
            if (Mathf.Approximately(topWidth, top) && Mathf.Approximately(bottomWidth, bottom)) return;
            topWidth = top;
            bottomWidth = bottom;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float fill = Mathf.Clamp01(fillAmount);
            if (fill <= 0f) return;

            Rect r = rectTransform.rect;
            float cx = r.center.x;
            float yBottom = r.yMin;
            float yTop = Mathf.Lerp(r.yMin, r.yMax, fill);

            // The width at the cut is the honest interpolation between the two edges - this is the
            // whole reason a partial fill still reads as the same shape.
            float halfBottom = r.width * Mathf.Clamp01(bottomWidth) * 0.5f;
            float halfTop = r.width * Mathf.Clamp01(Mathf.Lerp(bottomWidth, topWidth, fill)) * 0.5f;

            Color32 c = color;   // the same conversion BlastProfileGraphic does
            vh.AddVert(new Vector3(cx - halfBottom, yBottom), c, new Vector2(0f, 0f));
            vh.AddVert(new Vector3(cx + halfBottom, yBottom), c, new Vector2(1f, 0f));
            vh.AddVert(new Vector3(cx + halfTop,    yTop),    c, new Vector2(1f, fill));
            vh.AddVert(new Vector3(cx - halfTop,    yTop),    c, new Vector2(0f, fill));

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);

            if (edgeThickness <= 0f || color.a <= 0f) return;

            AddSlantEdge(vh, new Vector2(cx - halfBottom, yBottom), new Vector2(cx - halfTop, yTop));
            AddSlantEdge(vh, new Vector2(cx + halfBottom, yBottom), new Vector2(cx + halfTop, yTop));
        }

        /// <summary>
        /// One sloped side's hairline: a strip laid along the edge and offset INWARD, so it never
        /// grows the silhouette, with per-vertex alpha ramping from nothing at each end to solid
        /// across the middle. Grading it in the mesh rather than with a sprite is what keeps the
        /// whole plate one draw call and lets the fade track a slant that is a live tuning number.
        /// </summary>
        void AddSlantEdge(VertexHelper vh, Vector2 from, Vector2 to)
        {
            Vector2 along = to - from;
            float length = along.magnitude;
            if (length < 0.01f) return;
            along /= length;

            // Inward is the perpendicular pointing at the shape's axis - derived rather than
            // hard-coded per side, so a rectangle (no slant) and either taper direction all work.
            var inward = new Vector2(-along.y, along.x);
            float cx = rectTransform.rect.center.x;
            if (Vector2.Dot(inward, new Vector2(cx, from.y + to.y) * 0.5f - (from + to) * 0.5f) < 0f)
                inward = -inward;
            Vector2 offset = inward * Mathf.Min(edgeThickness, length * 0.5f);

            float fade = Mathf.Clamp(edgeFade, 0.01f, 0.5f);
            int start = vh.currentVertCount;

            for (int i = 0; i <= EdgeSegments; i++)
            {
                float t = i / (float)EdgeSegments;

                // 0 at both ends, 1 across the middle - smoothstepped so the ends dissolve rather
                // than terminate in a visible point.
                float ramp = Mathf.Min(t, 1f - t) / fade;
                float a = Mathf.Clamp01(ramp);
                a = a * a * (3f - 2f * a);

                Color32 ec = new Color(edgeColor.r, edgeColor.g, edgeColor.b,
                                       edgeColor.a * color.a * a);
                Vector2 p = Vector2.Lerp(from, to, t);
                vh.AddVert(new Vector3(p.x, p.y), ec, new Vector2(0f, t));
                vh.AddVert(new Vector3(p.x + offset.x, p.y + offset.y), ec, new Vector2(1f, t));
            }

            for (int i = 0; i < EdgeSegments; i++)
            {
                int a0 = start + i * 2;
                vh.AddTriangle(a0, a0 + 1, a0 + 3);
                vh.AddTriangle(a0 + 3, a0 + 2, a0);
            }
        }

        // Enough to read as a smooth gradient at HUD size; the strip is 2 tris per segment.
        const int EdgeSegments = 12;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}

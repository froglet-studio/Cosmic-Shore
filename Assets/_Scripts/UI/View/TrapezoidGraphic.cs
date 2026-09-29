using System.Collections.Generic;
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
    /// <para><b>The slant edge.</b> An optional band drawn INSIDE the shape, solid for the whole
    /// length of each sloped side and then WRAPPING around both corners onto the horizontals, where
    /// it grades to nothing. It is not a border - it never closes - which is the whole point: it
    /// accents the two edges that carry the shape's identity and lets the horizontals dissolve.
    /// Wrapping is what keeps the corner readable: a gradient that died on the slant left the
    /// corner itself unlit, which reads as the shape being unfinished rather than as an accent.</para>
    ///
    /// <para><b>Antialiasing is baked into the geometry</b>, because a generated diagonal has no
    /// other source of it - UGUI does no MSAA on a canvas and a 2px diagonal strip is pure
    /// stair-steps without help. The band is emitted as three quads across its width: a
    /// zero-alpha feather outside, the solid core, a zero-alpha feather inside. The bilinear
    /// interpolation between those vertex alphas IS the antialiasing, at no texture cost.</para>
    ///
    /// <para>Drawn into the SAME mesh as the fill, so an edged plate is still one draw call, and
    /// its alpha is multiplied by the graphic's own so a fade takes both together.</para>
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

        [Tooltip("How far the band wraps around each corner onto the horizontal edges, in px. The " +
                 "whole grade to nothing happens along this wrap, so the sloped side itself stays " +
                 "solid end to end. 0 makes it stop dead at the corners.")]
        [SerializeField, Min(0f)] private float edgeWrap = 12f;

        [Tooltip("Width of the zero-alpha feather baked onto BOTH sides of the band, in px. This is " +
                 "the antialiasing: without it a generated diagonal is stair-stepped, because a " +
                 "canvas gives it none.")]
        [SerializeField, Min(0f)] private float edgeAntialias = 1f;

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

        public float EdgeWrap
        {
            get => edgeWrap;
            set { if (!Mathf.Approximately(edgeWrap, value)) { edgeWrap = value; SetVerticesDirty(); } }
        }

        public float EdgeAntialias
        {
            get => edgeAntialias;
            set { if (!Mathf.Approximately(edgeAntialias, value)) { edgeAntialias = value; SetVerticesDirty(); } }
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

            var centre = new Vector2(cx, (yBottom + yTop) * 0.5f);
            AddSlantEdge(vh, new Vector2(cx - halfBottom, yBottom), new Vector2(cx - halfTop, yTop),
                         inwardX: 1f, centre);
            AddSlantEdge(vh, new Vector2(cx + halfBottom, yBottom), new Vector2(cx + halfTop, yTop),
                         inwardX: -1f, centre);
        }

        /// <summary>
        /// One sloped side's band: solid along the whole slant, wrapping around both corners onto
        /// the horizontals and grading to nothing there. Emitted as a strip offset INWARD from the
        /// outline, so it never grows the silhouette, with a zero-alpha feather on each side that
        /// bakes in the antialiasing a canvas cannot supply for a generated diagonal.
        /// </summary>
        void AddSlantEdge(VertexHelper vh, Vector2 bottom, Vector2 top, float inwardX, Vector2 centre)
        {
            _path.Clear();

            // Never wrap past the middle of a horizontal edge - on a short plate the two sides
            // would otherwise meet and the band would close into the border this is not.
            float wrap = Mathf.Min(edgeWrap,
                                   Mathf.Min(Mathf.Abs(centre.x - bottom.x), Mathf.Abs(centre.x - top.x)));
            var wrapIn = new Vector2(inwardX, 0f);   // along the horizontal edges, toward the middle

            // The grade lives entirely on the wraps, so the slant itself is solid end to end.
            if (wrap > 0.01f)
                for (int i = 0; i < WrapSamples; i++)
                {
                    float t = i / (float)WrapSamples;                    // 1 at the corner, 0 out on the flat
                    _path.Add((bottom + wrapIn * (wrap * (1f - t)), Smooth(t)));
                }

            _path.Add((bottom, 1f));
            _path.Add((top, 1f));

            if (wrap > 0.01f)
                for (int i = 1; i <= WrapSamples; i++)
                {
                    float t = i / (float)WrapSamples;
                    _path.Add((top + wrapIn * (wrap * t), Smooth(1f - t)));
                }

            EmitBand(vh, centre);
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Lays the band along <see cref="_path"/>. Each point contributes FOUR vertices across the
        /// band's width - feather, core start, core end, feather - and the corner points mitre their
        /// two neighbours' normals so the wrap turns without a gap or an overlap.
        /// </summary>
        void EmitBand(VertexHelper vh, Vector2 centre)
        {
            int n = _path.Count;
            if (n < 2) return;

            int start = vh.currentVertCount;
            float aa = edgeAntialias;

            for (int i = 0; i < n; i++)
            {
                Vector2 p = _path[i].Point;

                // Mitre: average the normals of the segments either side of this point, so a corner
                // gets one normal rather than two fighting ones.
                Vector2 normal = Vector2.zero;
                if (i > 0) normal += Normal(_path[i - 1].Point, p, centre);
                if (i < n - 1) normal += Normal(p, _path[i + 1].Point, centre);
                if (normal.sqrMagnitude < 1e-6f) continue;
                normal = normal.normalized;

                float a = _path[i].Alpha;
                Color32 solid = new Color(edgeColor.r, edgeColor.g, edgeColor.b, edgeColor.a * color.a * a);
                Color32 clear = new Color(edgeColor.r, edgeColor.g, edgeColor.b, 0f);

                vh.AddVert(new Vector3(p.x - normal.x * aa, p.y - normal.y * aa), clear, new Vector2(0f, 0f));
                vh.AddVert(new Vector3(p.x, p.y), solid, new Vector2(0.25f, 0f));
                vh.AddVert(new Vector3(p.x + normal.x * edgeThickness, p.y + normal.y * edgeThickness),
                           solid, new Vector2(0.75f, 0f));
                vh.AddVert(new Vector3(p.x + normal.x * (edgeThickness + aa),
                                       p.y + normal.y * (edgeThickness + aa)), clear, new Vector2(1f, 0f));
            }

            int emitted = (vh.currentVertCount - start) / 4;
            for (int i = 0; i < emitted - 1; i++)
            {
                int a0 = start + i * 4;
                for (int k = 0; k < 3; k++)   // outer feather, core, inner feather
                {
                    vh.AddTriangle(a0 + k, a0 + k + 1, a0 + k + 5);
                    vh.AddTriangle(a0 + k + 5, a0 + k + 4, a0 + k);
                }
            }
        }

        /// <summary>Unit normal of a segment, pointed at the shape's middle.</summary>
        static Vector2 Normal(Vector2 from, Vector2 to, Vector2 centre)
        {
            Vector2 d = to - from;
            if (d.sqrMagnitude < 1e-6f) return Vector2.zero;
            d = d.normalized;
            var n = new Vector2(-d.y, d.x);
            return Vector2.Dot(n, centre - (from + to) * 0.5f) < 0f ? -n : n;
        }

        // Enough to read as a smooth grade around a corner; the wrap is short by design.
        const int WrapSamples = 5;

        readonly List<(Vector2 Point, float Alpha)> _path = new();

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}

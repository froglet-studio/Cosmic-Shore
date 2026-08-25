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
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}

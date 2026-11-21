using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The call-to-action frame: a solid line around the cut-out with a soft glow fading outward,
    /// both mitred at the corners. Requires its CanvasRenderer for the reason on
    /// <see cref="SpotlightDimGraphic"/>.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class SpotlightFrameGraphic : MaskableGraphic
    {
        public Rect Inner;
        public float Thickness = 4f;
        public float Glow = 22f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Inner.width <= 0f || Inner.height <= 0f) return;

            var line = color;
            var glowIn = color; glowIn.a *= 0.55f;
            var glowOut = color; glowOut.a = 0f;

            var mid = Grow(Inner, Thickness);
            Band(vh, Inner, mid, line, line);
            Band(vh, mid, Grow(mid, Glow), glowIn, glowOut);
        }

        static Rect Grow(Rect r, float by) =>
            Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);

        /// <summary>A mitred ring between two nested rectangles.</summary>
        static void Band(VertexHelper vh, Rect a, Rect b, Color ca, Color cb)
        {
            int v = vh.currentVertCount;
            Vector2[] inner = { new(a.xMin, a.yMin), new(a.xMin, a.yMax), new(a.xMax, a.yMax), new(a.xMax, a.yMin) };
            Vector2[] outer = { new(b.xMin, b.yMin), new(b.xMin, b.yMax), new(b.xMax, b.yMax), new(b.xMax, b.yMin) };
            for (int i = 0; i < 4; i++)
            {
                vh.AddVert(inner[i], ca, Vector2.zero);
                vh.AddVert(outer[i], cb, Vector2.zero);
            }
            for (int i = 0; i < 4; i++)
            {
                int i0 = v + i * 2, o0 = i0 + 1;
                int i1 = v + ((i + 1) % 4) * 2, o1 = i1 + 1;
                vh.AddTriangle(i0, o0, o1);
                vh.AddTriangle(i0, o1, i1);
            }
        }
    }
}

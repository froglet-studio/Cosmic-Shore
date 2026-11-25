using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A square outline <see cref="Width"/> wide whose colour runs corner to corner: <see cref="From"/> at the top left,
    /// <see cref="Through"/> half way, <see cref="To"/> at the bottom right (Frogma Rev 108-111's glass edge: light at the
    /// top left, all but gone in the middle, cyan at the bottom right; Docs/MENU_REDESIGN_B.md section 205). One colour
    /// all round with <see cref="Solid"/>. No fill; never takes a press. Each colour is multiplied by
    /// <see cref="Graphic.color"/>.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class VoyageOutline : MaskableGraphic
    {
        public Color From = Color.white, Through = Color.white, To = Color.white;
        public float Width = 1f;

        const int Steps = 12;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void Set(Color from, Color through, Color to)
        {
            From = from;
            Through = through;
            To = to;
            SetVerticesDirty();
        }

        public void Solid(Color c) => Set(c, c, c);

        Color At(Rect r, Vector2 p)
        {
            float t = 0.5f * ((p.x - r.xMin) / r.width + (r.yMax - p.y) / r.height);
            var c = t < 0.5f ? Color.Lerp(From, Through, t * 2f) : Color.Lerp(Through, To, (t - 0.5f) * 2f);
            return c * color;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            if (r.width < 2f || r.height < 2f) return;
            float w = Mathf.Max(0.5f, Width);
            // Top and bottom run the whole width; the sides fit between them.
            Edge(vh, r, new Vector2(r.xMin, r.yMax - w), new Vector2(r.xMax, r.yMax), true);
            Edge(vh, r, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin + w), true);
            Edge(vh, r, new Vector2(r.xMin, r.yMin + w), new Vector2(r.xMin + w, r.yMax - w), false);
            Edge(vh, r, new Vector2(r.xMax - w, r.yMin + w), new Vector2(r.xMax, r.yMax - w), false);
        }

        // A thin box from lo to hi, cut along its length so the colour follows the corners.
        void Edge(VertexHelper vh, Rect r, Vector2 lo, Vector2 hi, bool across)
        {
            int start = vh.currentVertCount;
            for (int s = 0; s <= Steps; s++)
            {
                float t = s / (float)Steps;
                Vector2 a, b;
                if (across)
                {
                    float x = Mathf.Lerp(lo.x, hi.x, t);
                    a = new Vector2(x, lo.y);
                    b = new Vector2(x, hi.y);
                }
                else
                {
                    float y = Mathf.Lerp(lo.y, hi.y, t);
                    a = new Vector2(lo.x, y);
                    b = new Vector2(hi.x, y);
                }
                vh.AddVert(a, At(r, a), Vector2.zero);
                vh.AddVert(b, At(r, b), Vector2.zero);
                if (s == 0) continue;
                int v = start + s * 2;
                vh.AddTriangle(v - 2, v - 1, v + 1);
                vh.AddTriangle(v - 2, v + 1, v);
            }
        }
    }
}

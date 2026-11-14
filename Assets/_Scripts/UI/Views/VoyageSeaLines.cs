using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The sea lines the Voyages pages and their glass cards stand on (Frogma Rev 108, Docs/MENU_REDESIGN_B.md section 202:
    /// "make it interesting with sea lines"): two layers of thin wave lines across the rect, drifting slowly in opposite
    /// directions. No glow and no fill: the lines alone, in <see cref="Graphic.color"/>. <see cref="Speed"/> 0 holds them
    /// still; a card raises it while hovered (Rev 108's "faster on hover").
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class VoyageSeaLines : MaskableGraphic
    {
        /// <summary>Lines in each layer, top to bottom.</summary>
        public int Lines = 7;
        /// <summary>How far a line swells up and down, as a share of the gap between lines.</summary>
        public float Swell = 0.35f;
        /// <summary>Wave lengths across the rect.</summary>
        public float Waves = 2.2f;
        /// <summary>How fast the lines drift, in wave lengths a second (0: still).</summary>
        public float Speed = 0.02f;
        /// <summary>The lines' width.</summary>
        public float Width = 1.2f;

        const int Steps = 64;
        float _phase;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        void Update()
        {
            if (Speed == 0f) return;
            _phase = Mathf.Repeat(_phase + Speed * Time.unscaledDeltaTime, 1f);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            if (r.width < 2f || r.height < 2f || Lines <= 0) return;
            float gap = r.height / (Lines + 1);
            for (int layer = 0; layer < 2; layer++)
            {
                // The far layer is fainter, longer and drifts the other way.
                float dir = layer == 0 ? 1f : -1f;
                float waves = layer == 0 ? Waves : Waves * 0.7f;
                var c = color;
                if (layer == 1) c.a *= 0.55f;
                for (int i = 0; i < Lines; i++)
                {
                    float y0 = r.yMin + gap * (i + 1) + (layer == 1 ? gap * 0.5f : 0f);
                    if (y0 > r.yMax) continue;
                    float offset = i * 0.37f + dir * _phase;
                    Vector2 prev = Point(r, 0, y0, gap, waves, offset);
                    for (int s = 1; s <= Steps; s++)
                    {
                        var next = Point(r, s, y0, gap, waves, offset);
                        Segment(vh, prev, next, Width, c);
                        prev = next;
                    }
                }
            }
        }

        Vector2 Point(Rect r, int step, float y0, float gap, float waves, float offset)
        {
            float t = step / (float)Steps;
            float y = y0 + Mathf.Sin((t * waves + offset) * Mathf.PI * 2f) * gap * Swell;
            return new Vector2(Mathf.Lerp(r.xMin, r.xMax, t), y);
        }

        static void Segment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            var d = b - a;
            if (d.sqrMagnitude < 1e-4f) return;
            var n = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
            int i = vh.currentVertCount;
            vh.AddVert(a - n, c, Vector2.zero);
            vh.AddVert(a + n, c, Vector2.zero);
            vh.AddVert(b + n, c, Vector2.zero);
            vh.AddVert(b - n, c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }
    }
}

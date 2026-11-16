using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Voyages table's rim (Frogma Rev 108, Docs/MENU_REDESIGN_B.md section 202: a square-cornered table): the outline
    /// of the map's rect, laid back at the map's own tilt (<see cref="VoyageTableTilt.Seen"/>) so it sits on the map's edges
    /// at every angle, and drawn flat when the map lies flat in full screen. A thin line in <see cref="Graphic.color"/>; no
    /// glow and no fill. Stretched over the map, it never takes a press.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class VoyageTableRim : MaskableGraphic
    {
        /// <summary>The map whose tilt the rim follows.</summary>
        public VoyageMapGraphic Map;
        /// <summary>The line's width.</summary>
        public float Width = 1.5f;

        const int Steps = 24;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            if (r.width < 2f || r.height < 2f) return;
            float angle = Map && Map.Tilt && Map.Tilt.Active ? Map.Tilt.Angle : 0f;
            var corners = new[]
            {
                new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax),
            };
            for (int side = 0; side < 4; side++)
            {
                var a = corners[side];
                var b = corners[(side + 1) % 4];
                // Each side in short steps: a straight line stays straight under the projection, but the width tapers.
                var prev = See(r, a, angle, out float prevScale);
                for (int s = 1; s <= Steps; s++)
                {
                    var next = See(r, Vector2.Lerp(a, b, s / (float)Steps), angle, out float scale);
                    Segment(vh, prev, next, Width * 0.5f * (prevScale + scale), color);
                    prev = next;
                    prevScale = scale;
                }
            }
        }

        static Vector2 See(Rect r, Vector2 p, float angle, out float scale)
        {
            if (angle <= 0.01f)
            {
                scale = 1f;
                return p;
            }
            return VoyageTableTilt.Seen(r, p, angle, out scale);
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

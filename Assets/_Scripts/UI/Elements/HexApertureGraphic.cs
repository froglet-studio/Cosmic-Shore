using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A cover that fills its rect with a pointy-top hexagon cut out of it - the connecting panel's backdrop on the
    /// Rev 33 cell-gate loading screen (Docs/MENU_REDESIGN_B.md section 99). The arena is revealed THROUGH the hole
    /// when the load gate opens: it starts as the crystal's hex, glides to the centre and grows past the screen.
    /// Radius 0 draws the whole rect, so at rest it is a plain cover.
    ///
    /// <para>UVs span the rect the way a sprite-less Image's do, so a material that reads them (Sea_Window's wave
    /// lines and edge fade) draws exactly as it does on an Image. The cover is built as six convex pieces, each the
    /// rect clipped to the OUTSIDE of one hex edge and the inside of the edges before it: disjoint, gap-free, and
    /// right at every radius - a hole that has grown past the rect's edges leaves only the corners.</para>
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HexApertureGraphic : MaskableGraphic
    {
        // Reused across rebuilds: the hole moves every frame of the gate.
        static readonly List<Vector2> s_a = new(16), s_b = new(16);
        static readonly Vector2[] s_hex = new Vector2[6];

        Vector2 _center;
        float _radius;

        /// <summary>The hole's centre, in this rect's local units from its centre.</summary>
        public Vector2 ApertureCenter => _center;

        /// <summary>The hole's circumradius (centre to corner) in local units; 0 = no hole.</summary>
        public float ApertureRadius => _radius;

        /// <summary>Move and size the hole. Rebuilds only when either changes.</summary>
        public void SetAperture(Vector2 center, float radius)
        {
            radius = Mathf.Max(0f, radius);
            if (center == _center && Mathf.Approximately(radius, _radius)) return;
            _center = center;
            _radius = radius;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f) return;

            if (_radius < 0.5f)
            {
                AddRect(vh, r);
                return;
            }

            var c = r.center + _center;
            for (int k = 0; k < 6; k++)
            {
                float a = (90f + 60f * k) * Mathf.Deg2Rad;
                s_hex[k] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * _radius;
            }

            for (int k = 0; k < 6; k++)
            {
                var piece = s_a;
                var spare = s_b;
                piece.Clear();
                piece.Add(new Vector2(r.xMin, r.yMin));
                piece.Add(new Vector2(r.xMax, r.yMin));
                piece.Add(new Vector2(r.xMax, r.yMax));
                piece.Add(new Vector2(r.xMin, r.yMax));

                Clip(piece, s_hex[k], s_hex[(k + 1) % 6], false, spare);
                (piece, spare) = (spare, piece);
                for (int j = 0; j < k && piece.Count >= 3; j++)
                {
                    Clip(piece, s_hex[j], s_hex[(j + 1) % 6], true, spare);
                    (piece, spare) = (spare, piece);
                }
                AddFan(vh, piece, r);
            }
        }

        // The polygon's part on one side of the line a->b (the hex winds counter-clockwise, so its inside is
        // the left of each edge): one Sutherland-Hodgman pass into `output`.
        static void Clip(List<Vector2> polygon, Vector2 a, Vector2 b, bool keepInside, List<Vector2> output)
        {
            output.Clear();
            int n = polygon.Count;
            for (int i = 0; i < n; i++)
            {
                var p = polygon[i];
                var q = polygon[(i + 1) % n];
                float dp = Side(a, b, p), dq = Side(a, b, q);
                if (!keepInside)
                {
                    dp = -dp;
                    dq = -dq;
                }
                bool pIn = dp >= 0f, qIn = dq >= 0f;
                if (pIn) output.Add(p);
                if (pIn != qIn) output.Add(p + (q - p) * (dp / (dp - dq)));
            }
        }

        static float Side(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

        void AddRect(VertexHelper vh, Rect r)
        {
            int start = vh.currentVertCount;
            Color32 col = color;
            vh.AddVert(new Vector2(r.xMin, r.yMin), col, new Vector2(0f, 0f));
            vh.AddVert(new Vector2(r.xMin, r.yMax), col, new Vector2(0f, 1f));
            vh.AddVert(new Vector2(r.xMax, r.yMax), col, new Vector2(1f, 1f));
            vh.AddVert(new Vector2(r.xMax, r.yMin), col, new Vector2(1f, 0f));
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        void AddFan(VertexHelper vh, List<Vector2> polygon, Rect r)
        {
            if (polygon.Count < 3) return;
            int start = vh.currentVertCount;
            Color32 col = color;
            for (int i = 0; i < polygon.Count; i++)
            {
                var p = polygon[i];
                vh.AddVert(p, col, new Vector2((p.x - r.xMin) / r.width, (p.y - r.yMin) / r.height));
            }
            for (int i = 1; i < polygon.Count - 1; i++)
                vh.AddTriangle(start, start + i, start + i + 1);
        }
    }
}

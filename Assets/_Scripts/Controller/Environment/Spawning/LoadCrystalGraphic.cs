using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The omni crystal on the cell-change loading screen (Rev 7, Docs/MENU_REDESIGN_B.md §47.4): a hexagon outline
    /// with its spokes, filling from the bottom as the cell builds, the fill banded Mass, Time, Charge, Space from the
    /// bottom up. One mesh, rebuilt only when <see cref="Level"/> moves - the build runs the veil at about 10 frames a
    /// second, so the fill is a steady rise rather than an animation that would stutter.
    /// </summary>
    public sealed class LoadCrystalGraphic : MaskableGraphic
    {
        /// <summary>The fill's colours, bottom to top, and where each sits (0-1 of the crystal's height).</summary>
        static readonly float[] Stops = { 0f, 0.35f, 0.7f, 1f };

        // Reused clip buffers: the mesh rebuilds up to ten times a second while the cell builds.
        static readonly List<Vector2> s_hex = new(6), s_above = new(8), s_band = new(10);

        Color[] _bands = { Color.blue, Color.green, Color.yellow, Color.magenta };
        Color _outline = Color.white;
        float _level;

        const float OutlineWidth = 2f;
        const float SpokeWidth = 1f;
        const float SpokeAlpha = 0.35f;
        const float FillAlpha = 0.6f;
        const float RadiusOfHalfSize = 0.8f;

        /// <summary>How full the crystal is, 0-1.</summary>
        public float Level
        {
            get => _level;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Abs(value - _level) < 0.002f) return;
                _level = value;
                SetVerticesDirty();
            }
        }

        /// <summary>The fill's four colours, bottom to top (Mass, Time, Charge, Space), and the outline's.</summary>
        public void SetColours(Color mass, Color time, Color charge, Color space, Color outline)
        {
            _bands = new[] { mass, time, charge, space };
            _outline = outline;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            var centre = r.center;
            float radius = Mathf.Min(r.width, r.height) * 0.5f * RadiusOfHalfSize;
            s_hex.Clear();
            for (int k = 0; k < 6; k++)
            {
                float a = (90f + 60f * k) * Mathf.Deg2Rad;
                s_hex.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }

            float bottom = centre.y - radius, height = 2f * radius;
            if (_level > 0f)
            {
                // Band by band, so the vertex colours (linear in y inside a band) reproduce the gradient exactly.
                float top = bottom + height * _level;
                for (int b = 0; b < Stops.Length - 1; b++)
                {
                    float lo = bottom + height * Stops[b], hi = Mathf.Min(top, bottom + height * Stops[b + 1]);
                    if (hi <= lo) break;
                    Clip(s_hex, lo, true, s_above);
                    Clip(s_above, hi, false, s_band);
                    AddFan(vh, s_band, bottom, height);
                }
            }

            var spoke = _outline;
            spoke.a *= SpokeAlpha;
            for (int k = 0; k < 6; k++)
            {
                AddLine(vh, centre, s_hex[k], SpokeWidth, spoke);
                AddLine(vh, s_hex[k], s_hex[(k + 1) % 6], OutlineWidth, _outline);
            }
        }

        // The convex polygon's part above (or below) the line at y: one Sutherland-Hodgman pass, into `output`.
        static void Clip(List<Vector2> polygon, float y, bool keepAbove, List<Vector2> output)
        {
            output.Clear();
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                bool aIn = keepAbove ? a.y >= y : a.y <= y;
                bool bIn = keepAbove ? b.y >= y : b.y <= y;
                if (aIn) output.Add(a);
                if (aIn != bIn)
                {
                    float t = (y - a.y) / (b.y - a.y);
                    output.Add(new Vector2(Mathf.Lerp(a.x, b.x, t), y));
                }
            }
        }

        void AddFan(VertexHelper vh, List<Vector2> polygon, float bottom, float height)
        {
            if (polygon.Count < 3) return;
            int start = vh.currentVertCount;
            foreach (var p in polygon)
                vh.AddVert(p, Tint(BandColour((p.y - bottom) / height), FillAlpha), Vector2.zero);
            for (int i = 1; i < polygon.Count - 1; i++)
                vh.AddTriangle(start, start + i, start + i + 1);
        }

        Color BandColour(float t)
        {
            t = Mathf.Clamp01(t);
            for (int i = 0; i < Stops.Length - 1; i++)
                if (t <= Stops[i + 1])
                    return Color.Lerp(_bands[i], _bands[i + 1], (t - Stops[i]) / (Stops[i + 1] - Stops[i]));
            return _bands[_bands.Length - 1];
        }

        Color32 Tint(Color c, float alpha)
        {
            c *= color;
            c.a *= alpha;
            return c;
        }

        void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            var dir = (b - a).normalized;
            var n = new Vector2(-dir.y, dir.x) * (width * 0.5f);
            int start = vh.currentVertCount;
            Color32 col = Tint(c, 1f);
            vh.AddVert(a - n, col, Vector2.zero);
            vh.AddVert(a + n, col, Vector2.zero);
            vh.AddVert(b + n, col, Vector2.zero);
            vh.AddVert(b - n, col, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}

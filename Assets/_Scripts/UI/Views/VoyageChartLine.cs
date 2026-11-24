using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A line on the voyage pause chart (Frogma Rev 134 Pause A): the leg sailed (solid, fading in from where it began),
    /// the leg ahead (dashed), or a rival's course (dotted, in the threat red). Points are in the rect's own space, y down
    /// from its top left, as the chart lays them out (<see cref="CosmicShore.Data.VoyageChartLayout"/>). Never takes a press.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoyageChartLine : MaskableGraphic
    {
        readonly List<Vector2> _points = new();
        float _width = 2f, _dash, _gap, _fadeIn;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        /// <summary>Draw through <paramref name="points"/> (y down). <paramref name="dash"/> 0 draws it solid;
        /// <paramref name="fadeIn"/> (0-1) is the share of its length over which it fades in from nothing.</summary>
        public void Set(IReadOnlyList<Vector2> points, float width, float dash, float gap, float fadeIn)
        {
            _points.Clear();
            if (points != null) _points.AddRange(points);
            _width = width;
            _dash = dash;
            _gap = gap;
            _fadeIn = Mathf.Clamp01(fadeIn);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_points.Count < 2) return;
            var r = GetPixelAdjustedRect();
            float total = 0f;
            for (int i = 1; i < _points.Count; i++) total += Vector2.Distance(_points[i - 1], _points[i]);
            if (total <= 1e-3f) return;
            float along = 0f;
            for (int i = 1; i < _points.Count; i++)
            {
                var a = Map(r, _points[i - 1]);
                var b = Map(r, _points[i]);
                float len = Vector2.Distance(a, b);
                if (len <= 1e-4f) continue;
                if (_dash <= 0f)
                {
                    Segment(vh, a, b, Alpha(along / total), Alpha((along + len) / total));
                }
                else
                {
                    // Dashes keep their phase across corners: the pattern runs along the whole line.
                    float period = _dash + Mathf.Max(0f, _gap);
                    float t = 0f;
                    while (t < len)
                    {
                        float phase = (along + t) % period;
                        if (phase < _dash)
                        {
                            float end = Mathf.Min(len, t + (_dash - phase));
                            var p = Vector2.Lerp(a, b, t / len);
                            var q = Vector2.Lerp(a, b, end / len);
                            Segment(vh, p, q, Alpha((along + t) / total), Alpha((along + end) / total));
                            t = end;
                        }
                        else
                        {
                            t += period - phase;
                        }
                    }
                }
                along += len;
            }
        }

        float Alpha(float share) => _fadeIn <= 0f ? 1f : Mathf.Clamp01(share / _fadeIn);

        static Vector2 Map(Rect r, Vector2 p) => new(r.xMin + p.x, r.yMax - p.y);

        void Segment(VertexHelper vh, Vector2 a, Vector2 b, float alphaA, float alphaB)
        {
            var d = (b - a).normalized;
            var n = new Vector2(-d.y, d.x) * (_width * 0.5f);
            var ca = color;
            ca.a *= alphaA;
            var cb = color;
            cb.a *= alphaB;
            int i = vh.currentVertCount;
            vh.AddVert(a - n, ca, Vector2.zero);
            vh.AddVert(a + n, ca, Vector2.zero);
            vh.AddVert(b + n, cb, Vector2.zero);
            vh.AddVert(b - n, cb, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}

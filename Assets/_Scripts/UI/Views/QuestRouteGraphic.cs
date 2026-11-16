using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Quests window's course (Frogma Rev 107 D): a dotted line through every station, and over it a solid line
    /// along each stretch that leads to an open quest. When a claim opens the next quest, its stretch draws in from
    /// the claimed station (the window's one motion; nothing else moves at rest). Stations are in this graphic's own
    /// space, centred; <see cref="QuestsRules.Station"/> places them.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class QuestRouteGraphic : MaskableGraphic
    {
        const int Steps = 32;
        const float DotLength = 3f, DotGap = 9f, DotWidth = 1.5f, LitWidth = 2f;
        const float DrawInSeconds = 1.1f;

        readonly List<Vector2> _stations = new();
        readonly List<float> _lit = new();      // how much of each stretch is drawn, 0..1
        readonly List<bool> _target = new();
        Color _dot = Color.clear;
        bool _moving;

        /// <summary>
        /// The stations, and for each stretch whether it leads to an open quest. A stretch that was dark and is now
        /// lit draws in; one that was already lit, or on the first call, shows at once.
        /// </summary>
        public void Set(IReadOnlyList<Vector2> stations, IReadOnlyList<bool> lit, Color litColor, Color dotColor)
        {
            bool first = _stations.Count != stations.Count;
            _stations.Clear();
            _stations.AddRange(stations);
            int stretches = Mathf.Max(0, stations.Count - 1);
            if (first)
            {
                _lit.Clear();
                _target.Clear();
            }
            while (_lit.Count < stretches) { _lit.Add(0f); _target.Add(false); }
            for (int i = 0; i < stretches; i++)
            {
                bool on = i < lit.Count && lit[i];
                if (first) _lit[i] = on ? 1f : 0f;
                else if (!on) _lit[i] = 0f;
                else if (!_target[i]) _moving = true;
                _target[i] = on;
            }
            color = litColor;
            _dot = dotColor;
            SetVerticesDirty();
        }

        void Update()
        {
            if (!_moving) return;
            bool still = true;
            for (int i = 0; i < _lit.Count; i++)
            {
                if (!_target[i] || _lit[i] >= 1f) continue;
                _lit[i] = Mathf.Min(1f, _lit[i] + Time.unscaledDeltaTime / DrawInSeconds);
                if (_lit[i] < 1f) still = false;
            }
            _moving = !still;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int i = 0; i + 1 < _stations.Count; i++)
            {
                Vector2 a = _stations[i], b = _stations[i + 1];
                Vector2 c = QuestsRules.Control(a, b, QuestsRules.BendOf(i));
                Dots(vh, a, c, b);
                float t = i < _lit.Count ? Ease(_lit[i]) : 0f;
                if (t > 0f) Solid(vh, a, c, b, t);
            }
        }

        static float Ease(float t) => t * t * (3f - 2f * t);

        void Dots(VertexHelper vh, Vector2 a, Vector2 c, Vector2 b)
        {
            float along = 0f;
            Vector2 prev = a;
            for (int s = 1; s <= Steps; s++)
            {
                Vector2 p = QuestsRules.Quadratic(a, c, b, s / (float)Steps);
                float len = Vector2.Distance(prev, p);
                // A dot wherever this step's span crosses the start of a dash.
                float start = Mathf.Ceil(along / (DotLength + DotGap)) * (DotLength + DotGap);
                for (float d = start; d < along + len; d += DotLength + DotGap)
                {
                    float f = (d - along) / Mathf.Max(len, 1e-4f);
                    Vector2 from = Vector2.Lerp(prev, p, f);
                    Vector2 dir = (p - prev).normalized;
                    Quad(vh, from, from + dir * DotLength, DotWidth, _dot);
                }
                along += len;
                prev = p;
            }
        }

        void Solid(VertexHelper vh, Vector2 a, Vector2 c, Vector2 b, float upTo)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Steps * upTo));
            Vector2 prev = a;
            for (int s = 1; s <= steps; s++)
            {
                Vector2 p = QuestsRules.Quadratic(a, c, b, Mathf.Min(upTo, s / (float)Steps));
                Quad(vh, prev, p, LitWidth, color);
                prev = p;
            }
        }

        static void Quad(VertexHelper vh, Vector2 from, Vector2 to, float width, Color32 tint)
        {
            Vector2 d = to - from;
            if (d.sqrMagnitude < 1e-6f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
            int i = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.color = tint;
            v.position = from - n; vh.AddVert(v);
            v.position = from + n; vh.AddVert(v);
            v.position = to + n; vh.AddVert(v);
            v.position = to - n; vh.AddVert(v);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }
    }
}

using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The open voyage card's ground (Rev 49 C, "Route poster", Docs/MENU_REDESIGN_B.md section 119): the real course on
    /// the HyperSea map behind the card. It draws the water the window's own <see cref="VoyageMapGraphic"/> painted
    /// (<see cref="VoyageMapGraphic.TrySeaView"/>, the same texture, never a second paint: the map's atlas is shared),
    /// framed on the course at the card's shape (<see cref="VoyageCardRules.Frame"/>), with the course over it as a lit
    /// line and its two ends marked. With no painted water it draws nothing and the card's own fill shows.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class VoyageRoutePoster : MaskableGraphic
    {
        VoyageMapGraphic _source;
        readonly List<Vector2> _course = new();
        Color _route = new(0.784f, 1f, 0.353f, 1f);
        Color _ark = new(1f, 0.78f, 0.25f, 1f);
        Color _sea = new(0.75f, 0.75f, 0.8f, 1f);
        Texture _texture;
        /// <summary>How thick the course and its ends draw (1: the open card's; the Voyages board's small rows draw thinner).</summary>
        public float LineScale = 1f;

        float _drawn = 1f;
        readonly List<Vector2> _stops = new();

        /// <summary>How much of the course is drawn, from its start (0-1): the voyage page draws it in as it opens
        /// (Frogma Rev 100 P2A, "the route drawing itself").</summary>
        public float Drawn
        {
            get => _drawn;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(value, _drawn)) return;
                _drawn = value;
                SetVerticesDirty();
            }
        }

        /// <summary>The voyage's stops (map points), ringed on the course; a stop the drawing has reached is lit.</summary>
        public void SetStops(IReadOnlyList<Vector2> stops)
        {
            _stops.Clear();
            if (stops != null) _stops.AddRange(stops);
            SetVerticesDirty();
        }

        /// <summary>The stops' ring radius before <see cref="LineScale"/> (9: the open card; the voyage page's 24 unit rings
        /// are 12).</summary>
        public float StopRadius = 9f;
        /// <summary>The fill inside a stop's ring (clear: none; the voyage page's are dark, rgba(0,1,10,.75)).</summary>
        public Color StopFill = new(0f, 0f, 0f, 0f);
        /// <summary>A stop the drawing has not reached yet: its ring in the sea's dim tint, or, set here, this colour.</summary>
        public Color? StopAhead;

        /// <summary>Where map point <paramref name="map"/> is drawn in the poster's rect, as the last mesh framed it; false
        /// before the water is painted (the voyage page names its stops there).</summary>
        public bool TryLocal(Vector2 map, out Vector2 local)
        {
            local = default;
            var r = rectTransform.rect;
            if (!_source || r.width < 1f || r.height < 1f || !_source.TrySeaView(out _, out _, out var lo, out var hi)) return false;
            var (flo, fhi) = VoyageCardRules.Frame(lo, hi, _course, r.width / r.height);
            local = new Vector2(Mathf.LerpUnclamped(r.xMin, r.xMax, Mathf.InverseLerp(flo.x, fhi.x, map.x)),
                Mathf.LerpUnclamped(r.yMin, r.yMax, Mathf.InverseLerp(flo.y, fhi.y, map.y)));
            return true;
        }

        /// <summary>How far along the course (0-1) the stop nearest <paramref name="stop"/> lies.</summary>
        public float ShareAt(Vector2 stop) => VoyageCardRules.ShareAlong(_course, stop);
        // The map's view the mesh was last built for: the poster rebuilds only when the map repaints or moves.
        Vector2 _drawnLo, _drawnHi;
        Rect _drawnRect;

        public override Texture mainTexture => _texture ? _texture : s_WhiteTexture;

        /// <summary>Show <paramref name="course"/> (map points, <see cref="VoyageMapGraphic.MapPoint"/>) on
        /// <paramref name="source"/>'s water. A null or empty course shows the water alone.</summary>
        public void Show(VoyageMapGraphic source, IReadOnlyList<Vector2> course, Color route, Color ark, Color seaTint)
        {
            _source = source;
            _course.Clear();
            if (course != null) _course.AddRange(course);
            _route = route;
            _ark = ark;
            _sea = seaTint;
            raycastTarget = false;
            SetVerticesDirty();
            SetMaterialDirty();
        }

        // The map repaints its water as it is panned and zoomed; the card follows it, rebuilding only on a change.
        void LateUpdate()
        {
            if (!_source) return;
            bool painted = _source.TrySeaView(out var tex, out _, out var lo, out var hi);
            if (!painted) tex = null;
            if (tex != _texture) SetMaterialDirty();
            if (tex != _texture || lo != _drawnLo || hi != _drawnHi || rectTransform.rect != _drawnRect) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            _texture = null;
            var r = rectTransform.rect;
            _drawnRect = r;
            if (!_source || !_source.TrySeaView(out var tex, out var uv, out var lo, out var hi)) return;
            _texture = tex;
            _drawnLo = lo;
            _drawnHi = hi;
            if (r.width < 1f || r.height < 1f) return;
            var (flo, fhi) = VoyageCardRules.Frame(lo, hi, _course, r.width / r.height);
            Vector2 Uv(Vector2 m) => new(uv.x + uv.width * Mathf.InverseLerp(lo.x, hi.x, m.x),
                uv.y + uv.height * Mathf.InverseLerp(lo.y, hi.y, m.y));
            Vector2 At(Vector2 m) => new(Mathf.LerpUnclamped(r.xMin, r.xMax, Mathf.InverseLerp(flo.x, fhi.x, m.x)),
                Mathf.LerpUnclamped(r.yMin, r.yMax, Mathf.InverseLerp(flo.y, fhi.y, m.y)));

            // Painted as the worn colour set has them (ColorTrace, Docs/VesselModels/COLOR_TRACE.md).
            var route = ColorTrace.Retrace(ColorTrace.Ui, _route);
            var ark = ColorTrace.Retrace(ColorTrace.Ui, _ark);
            var sea = ColorTrace.Retrace(ColorTrace.Ui, _sea);

            // The water.
            Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin),
                sea, Uv(flo), Uv(new Vector2(flo.x, fhi.y)), Uv(fhi), Uv(new Vector2(fhi.x, flo.y)));

            if (_course.Count < 2) return;
            var white = _source.WhiteUv;
            // A soft wide line under a thin bright one: the course lit on the water, drawn up to Drawn of its length.
            var glow = new Color(route.r, route.g, route.b, 0.22f);
            float total = VoyageCardRules.Length(_course), left = total * _drawn;
            var dim = new Color(sea.r, sea.g, sea.b, 0.35f);
            for (int i = 1; i < _course.Count; i++)
            {
                var a = _course[i - 1];
                var b = _course[i];
                float len = Vector2.Distance(a, b);
                if (left <= 0f) { Segment(vh, At(a), At(b), 2f * LineScale, dim, white); continue; }
                var end = len > left ? Vector2.Lerp(a, b, left / Mathf.Max(1e-4f, len)) : b;
                Segment(vh, At(a), At(end), 12f * LineScale, glow, white);
                Segment(vh, At(a), At(end), 3f * LineScale, route, white);
                if (len > left) Segment(vh, At(end), At(b), 2f * LineScale, dim, white);
                left -= len;
            }
            var stopFill = ColorTrace.Retrace(ColorTrace.Ui, StopFill);
            var ahead = StopAhead.HasValue ? ColorTrace.Retrace(ColorTrace.Ui, StopAhead.Value) : dim;
            foreach (var stop in _stops)
            {
                var p = At(stop);
                float radius = StopRadius * LineScale;
                if (stopFill.a > 0f) Disc(vh, p, radius, stopFill, white);
                Ring(vh, p, radius, 2f * LineScale, ShareAt(stop) <= _drawn + 1e-3f ? route : ahead, white);
            }
            Diamond(vh, At(_course[0]), 9f * LineScale, ark, white);
            if (_drawn >= 0.999f) Diamond(vh, At(_course[^1]), 9f * LineScale, route, white);
        }

        static void Segment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c, Vector2 uv)
        {
            var d = b - a;
            if (d.sqrMagnitude < 1e-4f) return;
            var n = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
            Quad(vh, a - n, a + n, b + n, b - n, c, uv, uv, uv, uv);
        }

        static void Ring(VertexHelper vh, Vector2 p, float r, float width, Color c, Vector2 uv)
        {
            const int Sides = 20;
            for (int i = 0; i < Sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / Sides, a1 = (i + 1) * Mathf.PI * 2f / Sides;
                var d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
                var d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                Quad(vh, p + d0 * (r - width), p + d0 * r, p + d1 * r, p + d1 * (r - width), c, uv, uv, uv, uv);
            }
        }

        static void Disc(VertexHelper vh, Vector2 p, float r, Color c, Vector2 uv)
        {
            const int Sides = 20;
            for (int i = 0; i < Sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / Sides, a1 = (i + 1) * Mathf.PI * 2f / Sides;
                Quad(vh, p, p + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, p + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r, p,
                    c, uv, uv, uv, uv);
            }
        }

        static void Diamond(VertexHelper vh, Vector2 p, float r, Color c, Vector2 uv) =>
            Quad(vh, p + new Vector2(0, -r), p + new Vector2(-r, 0), p + new Vector2(0, r), p + new Vector2(r, 0), c, uv, uv, uv, uv);

        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col,
            Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, col, ua);
            vh.AddVert(b, col, ub);
            vh.AddVert(c, col, uc);
            vh.AddVert(d, col, ud);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }
    }
}

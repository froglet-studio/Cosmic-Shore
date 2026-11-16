using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The open voyage card's ground (Rev 49 C, "Route poster", Docs/MENU_REDESIGN_B.md section 119): the real course on
    /// the HyperSea map behind the card, framed on the course at the card's shape (<see cref="VoyageCardRules.Frame"/>),
    /// with the course over it as a lit line and its two ends marked.
    ///
    /// <para>It paints its own water for that frame (<see cref="HyperSea.Density"/> coloured by
    /// <see cref="HyperSea.SeaColor"/>, as the map does), once per course and size. It used to crop the map's own painted
    /// water, which is about 190 texels across the whole table's view, so a short course was a few texels blown up: the
    /// voyage page's map read blurred (Shombith, 2026-11-16), and with the table opening at Galaxy view it would have been
    /// one texel. The map still says whether a charted sea is on show (<see cref="VoyageMapGraphic.TrySeaView"/>); without
    /// one it draws nothing and the card's own fill shows.</para>
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
            if (!_source || r.width < 1f || r.height < 1f || !_source.TrySeaView(out _, out _, out _, out _)) return false;
            var (flo, fhi) = OwnFrame(r.width / r.height);
            local = new Vector2(Mathf.LerpUnclamped(r.xMin, r.xMax, Mathf.InverseLerp(flo.x, fhi.x, map.x)),
                Mathf.LerpUnclamped(r.yMin, r.yMax, Mathf.InverseLerp(flo.y, fhi.y, map.y)));
            return true;
        }

        /// <summary>How far along the course (0-1) the stop nearest <paramref name="stop"/> lies.</summary>
        public float ShareAt(Vector2 stop) => VoyageCardRules.ShareAlong(_course, stop);
        Rect _drawnRect;

        // Its own water: the frame painted, the texture holding it (a white row on top for the solid shapes), and the
        // frame and size it was painted for.
        const int MaxTexWidth = 320, MinTexWidth = 64, WhiteRows = 2;
        /// <summary>The least a frame shows across, ly, so a short course keeps its surroundings (the map's focus minimum).</summary>
        const float MinFrameLy = 360f;
        Texture2D _own;
        Vector2 _frameLo, _frameHi, _white;
        int _seaRows;
        string _paintedFor;
        bool _charted;

        public override Texture mainTexture => _texture ? _texture : s_WhiteTexture;


        /// <summary>The frame the poster shows: the course at the card's shape, at least <see cref="MinFrameLy"/> across.</summary>
        (Vector2 lo, Vector2 hi) OwnFrame(float aspect)
        {
            const float half = 50000f;
            if (_course.Count == 0) return (new Vector2(-half, -half), new Vector2(half, half));
            var (lo, hi) = VoyageCardRules.Frame(new Vector2(-half, -half), new Vector2(half, half), _course, aspect);
            var size = hi - lo;
            if (size.x >= MinFrameLy) return (lo, hi);
            var centre = (lo + hi) * 0.5f;
            var want = new Vector2(MinFrameLy, MinFrameLy / Mathf.Max(1e-3f, aspect)) * 0.5f;
            return (centre - want, centre + want);
        }

        /// <summary>Paint the water of the frame for this rect, unless it is already painted.</summary>
        bool PaintOwn(Rect r)
        {
            float aspect = r.width / r.height;
            var (lo, hi) = OwnFrame(aspect);
            int w = Mathf.Clamp(Mathf.RoundToInt(r.width * 0.5f), MinTexWidth, MaxTexWidth);
            int rows = Mathf.Clamp(Mathf.RoundToInt(w / aspect), 16, MaxTexWidth);
            string key = $"{lo.x:F1},{lo.y:F1},{hi.x:F1},{hi.y:F1}:{w}x{rows}";
            if (_own && _paintedFor == key) return true;
            int h = rows + WhiteRows;
            if (!_own || _own.width != w || _own.height != h)
            {
                if (_own) Destroy(_own);
                _own = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                    name = "VoyageRouteSea",
                };
                // Destroyed with the poster (VoyageOwnedTexture).
                if (!TryGetComponent(out VoyageOwnedTexture owner)) owner = gameObject.AddComponent<VoyageOwnedTexture>();
                owner.Texture = _own;
            }
            var px = new Color32[w * h];
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < w; x++)
            {
                // Map points flip y back to the board's frame (VoyageMapGraphic.MapPoint).
                double gx = lo.x + (hi.x - lo.x) * (x + 0.5) / w;
                double gy = -(lo.y + (hi.y - lo.y) * (y + 0.5) / rows);
                var (cr, cg, cb) = HyperSea.SeaColor(HyperSea.Density(gx, gy));
                px[y * w + x] = new Color32((byte)cr, (byte)cg, (byte)cb, 255);
            }
            for (int i = rows * w; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            _own.SetPixels32(px);
            _own.Apply(false);
            _paintedFor = key;
            _frameLo = lo;
            _frameHi = hi;
            _seaRows = rows;
            _white = new Vector2((w - 0.5f) / w, (h - 0.5f) / h);
            return true;
        }

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
            _paintedFor = null;
            raycastTarget = false;
            SetVerticesDirty();
            SetMaterialDirty();
        }

        // The card paints its own water once per course and size; it waits for the map to show a charted sea first.
        void LateUpdate()
        {
            if (!_source) return;
            bool charted = _source.TrySeaView(out _, out _, out _, out _);
            if (charted != _charted || rectTransform.rect != _drawnRect)
            {
                SetMaterialDirty();
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            _texture = null;
            var r = rectTransform.rect;
            _drawnRect = r;
            _charted = _source && _source.TrySeaView(out _, out _, out _, out _);
            if (!_charted || r.width < 1f || r.height < 1f || !PaintOwn(r)) return;
            _texture = _own;
            Vector2 flo = _frameLo, fhi = _frameHi;
            // Texel centres at the edges, so the white row above the water never bleeds into its top.
            float h = _own.height, rows = _seaRows;
            Vector2 Uv(Vector2 m) => new(Mathf.InverseLerp(flo.x, fhi.x, m.x),
                (0.5f + (rows - 1f) * Mathf.InverseLerp(flo.y, fhi.y, m.y)) / h);
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
            var white = _white;
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

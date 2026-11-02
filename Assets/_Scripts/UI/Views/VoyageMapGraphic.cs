using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The galaxy map in the app shell (Garrett, 2026-10-28: "the galaxy map should be in the app shell not the game"):
    /// the HyperSea's locations and lanes seen from above, where the squad is, and the course a job sails. Drawn flat, as
    /// the UI's look is, into one mesh; each location's name is a label whose text is the content's own. Colours are the
    /// voyage harbour's (<see cref="VoyageToyDefinitionSO"/>).
    ///
    /// <para>When the content's locations are charted on the real HyperSea (the Voyage Board,
    /// Docs/Voyages/VOYAGE_BOARD.md), the map is the Milky Way's own sea around them, coloured by how thick the water
    /// is (<see cref="HyperSea.SeaColor"/>), the course is the one an Ark sails cell by cell round the reefs
    /// (<see cref="VoyageChart"/>), a corner shows the whole galaxy with where this water is and Sol, and the caption
    /// under the map is the job's course line (authored, filled by the caller). Uncharted content draws as before.</para>
    ///
    /// <para>It is a galaxy explorer (Garrett, 2026-11-01: "the map divides them as a fun interactive galaxy explorer
    /// deciding where you want your voyages to take you"): drag to pan, pinch or scroll to zoom, tap a place
    /// (<see cref="PlaceTapped"/>). The window's edge view follows it (<see cref="ViewChanged"/>).</para>
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class VoyageMapGraphic : MaskableGraphic, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler,
        IDragHandler, IEndDragHandler, IScrollHandler, IPointerClickHandler
    {
        const float Pad = 0.12f;
        const float LaneWidth = 2f;
        const float RouteWidth = 5f;
        const float CourseWidth = 3.5f;
        const float MarkerSize = 9f;
        const float HereSize = 16f;
        const string LabelName = "Label";
        const string CaptionName = "Course";
        /// <summary>The least a focused map shows across, in light years, so a short course keeps its surroundings.</summary>
        const float MinFocusLy = 360f;

        // The explorer: how far it zooms (the view's width, light years on a charted map, the framed width's share on
        // an uncharted one), how far a wheel notch zooms, how near a tap must land on a place, how often a moving view
        // repaints its sea.
        const float MinViewLy = 120f, MaxViewLy = 20000f;
        const float MinZoom = 0.5f, MaxZoom = 8f;
        const float WheelZoom = 1.15f;
        const float TapReach = 28f;
        const float RepaintEvery = 0.12f;
        // The sea is painted wider than the view, so a pan slides over painted water until the next repaint; while a
        // finger is down it is painted at half resolution (a quarter of the density samples, about 1,900), and in full
        // once the view settles.
        const float PaintMargin = 0.35f;

        // The sea behind a charted map: one texture holding the framed water, the galaxy corner and a white strip the
        // solid shapes sample, so the whole map stays one mesh and one draw.
        const int SeaTexWidth = 192;
        const int InsetTex = 64;
        const int WhiteRows = 2;
        const float InsetShare = 0.3f;
        const float InsetMargin = 6f;
        const double GalaxyHalf = 50000;
        static readonly Color SeaTint = new(1f, 1f, 1f, 0.92f);
        static readonly Color SolColor = new(1f, 1f, 1f, 0.95f);

        VoyageContent _content;
        VoyageToyDefinitionSO _style;
        VoyageChart _chart;
        VoyageChart.Voyage _voyage;
        string _here;
        string _caption;
        bool _focus;
        readonly List<string> _route = new();
        readonly List<TMP_Text> _labels = new();
        readonly List<bool> _labelShown = new();
        TMP_Text _labelTemplate;
        TMP_Text _captionText;
        float _minX, _maxX, _minZ, _maxZ;
        Vector2 _placedFor;

        // The explorer's view over the framed map: an offset of the centre and a zoom of the scale.
        Vector2 _pan;
        float _zoom = 1f;
        string _frameKey;
        readonly Dictionary<int, Vector2> _pointers = new();
        bool _viewDirty;
        float _lastPaint;
        Vector2 _paintLo, _paintHi;
        bool _insetPainted;
        bool _paintedCoarse;

        /// <summary>A place was tapped: its location id.</summary>
        public event Action<string> PlaceTapped;
        /// <summary>The view moved or zoomed, or the map was redrawn (the edge view follows it).</summary>
        public event Action ViewChanged;

        /// <summary>The content on show (null before the first <see cref="Show(VoyageContent, VoyageToyDefinitionSO, string, IReadOnlyList{string}, string, bool)"/>).</summary>
        public VoyageContent Content => _content;
        public VoyageToyDefinitionSO Style => _style;
        /// <summary>The real sea's chart, or null for uncharted content.</summary>
        public VoyageChart Chart => _chart;
        /// <summary>The course on show, or null.</summary>
        public VoyageChart.Voyage Course => _voyage;
        public string Here => _here;
        public IReadOnlyList<string> Route => _route;
        /// <summary>Pixels per map unit (per light year on a charted map).</summary>
        public float PixelsPerUnit => Scale();
        /// <summary>The map point at the centre of the view.</summary>
        public Vector2 ViewCentre => Centre;
        /// <summary>A map x (light years on a charted map) as an x from the rect's centre.</summary>
        public float OffsetX(float mapX) => (mapX - Centre.x) * Scale();

        static Texture2D s_Atlas;
        static string s_AtlasKey;
        Vector2 _white;
        int _seaTexHeight;
        bool _painted;

        public override Texture mainTexture => _painted && _chart != null && s_Atlas ? s_Atlas : s_WhiteTexture;

        /// <summary>The text whose font, size and colour every location's label copies.</summary>
        public void SetLabelTemplate(TMP_Text template) => _labelTemplate = template;

        /// <summary>Draw <paramref name="content"/>'s map with the squad at <paramref name="here"/> and
        /// <paramref name="route"/> (location ids, in order) as the course. Null route: no course.</summary>
        public void Show(VoyageContent content, VoyageToyDefinitionSO style, string here, IReadOnlyList<string> route) =>
            Show(content, style, here, route, null);

        /// <summary>As <see cref="Show(VoyageContent, VoyageToyDefinitionSO, string, IReadOnlyList{string})"/>, with
        /// <paramref name="caption"/> under the map (the course line; empty or null shows nothing). With
        /// <paramref name="focus"/> the map frames the course (Garrett, 2026-11-01: "drilling into a card ... causes the
        /// map to focus on that path, but the map remains"); without it, every place.</summary>
        public void Show(VoyageContent content, VoyageToyDefinitionSO style, string here, IReadOnlyList<string> route,
            string caption, bool focus = false)
        {
            _focus = focus;
            _content = content;
            _style = style;
            _here = here;
            _caption = caption;
            _route.Clear();
            if (route != null) _route.AddRange(route);
            _chart = VoyageChart.For(content);
            _voyage = _chart?.Along(_route);
            raycastTarget = true;
            // A new frame (another course, or none) starts the explorer over; a redraw of the same one keeps where the
            // player has looked.
            string key = (focus ? "f:" : "a:") + string.Join(">", _route);
            if (key != _frameKey)
            {
                _frameKey = key;
                _pan = Vector2.zero;
                _zoom = 1f;
            }
            Bounds();
            Label();
            Caption();
            _placedFor = Vector2.zero;
            Relayout();
        }

        // On a charted map the board's y runs toward Sol and the screen's up, so it flips.
        static Vector2 Pos(VoyageLocation l, bool charted) =>
            charted ? new Vector2(l.GalaxyX, -l.GalaxyY) : new Vector2(l.ChartX, l.ChartZ);

        /// <summary>A charted map draws only the charted locations.</summary>
        bool Drawn(VoyageLocation l) => l != null && (_chart == null || l.Charted);

        void Bounds()
        {
            _minX = _minZ = float.PositiveInfinity;
            _maxX = _maxZ = float.NegativeInfinity;
            if (_content?.Locations == null) return;
            if (_focus && _voyage != null)
            {
                foreach (var p in _voyage.Points) Include(Galaxy(p));
                float half = MinFocusLy * 0.5f;
                var c = FrameCentre;
                Include(c - new Vector2(half, half));
                Include(c + new Vector2(half, half));
                return;
            }
            foreach (var l in _content.Locations)
                if (Drawn(l)) Include(Pos(l, _chart != null));
        }

        void Include(Vector2 p)
        {
            _minX = Mathf.Min(_minX, p.x);
            _maxX = Mathf.Max(_maxX, p.x);
            _minZ = Mathf.Min(_minZ, p.y);
            _maxZ = Mathf.Max(_maxZ, p.y);
        }

        /// <summary>True when a place lies inside the framed map (a focused map leaves out the places beyond it).</summary>
        bool InFrame(Vector2 rectPoint) => rectTransform.rect.Contains(rectPoint);

        /// <summary>The framed map's scale, before the explorer's zoom.</summary>
        float FrameScale()
        {
            var r = rectTransform.rect;
            float w = Mathf.Max(1e-3f, _maxX - _minX), h = Mathf.Max(1e-3f, _maxZ - _minZ);
            return Mathf.Max(1e-6f, Mathf.Min(r.width * (1f - 2f * Pad) / w, r.height * (1f - 2f * Pad) / h));
        }

        float Scale() => FrameScale() * _zoom;

        Vector2 FrameCentre => new((_minX + _maxX) * 0.5f, (_minZ + _maxZ) * 0.5f);
        Vector2 Centre => FrameCentre + _pan;

        /// <summary>A map point (chart units, or light years with y flipped) in the rect.</summary>
        Vector2 ToRect(Vector2 p) => rectTransform.rect.center + (p - Centre) * Scale();

        /// <summary>A point in the rect back on the map.</summary>
        Vector2 FromRect(Vector2 p) => Centre + (p - rectTransform.rect.center) / Scale();

        Vector2 At(VoyageLocation l) => ToRect(Pos(l, _chart != null));

        // The labels and the sea follow the map's size. Watched here rather than by overriding
        // OnRectTransformDimensionsChange, whose access differs between the shipped uGUI (protected) and the compile
        // gate's publicized reference (public).
        void LateUpdate()
        {
            if (rectTransform.rect.size != _placedFor)
            {
                Relayout();
                return;
            }
            if (!_viewDirty) return;
            _viewDirty = false;
            // A moving view slides over the water already painted, and repaints a few times a second at most (until
            // then the view stays dirty, so the repaint still comes once the view has stopped).
            if (NeedsPaint())
            {
                if (Time.unscaledTime - _lastPaint >= RepaintEvery)
                {
                    PaintSea();
                    SetMaterialDirty();
                }
                else _viewDirty = true;
            }
            PlaceLabels();
            SetVerticesDirty();
            ViewChanged?.Invoke();
        }

        void Relayout()
        {
            _placedFor = rectTransform.rect.size;
            _insetPainted = false;
            PaintSea();
            PlaceLabels();
            SetVerticesDirty();
            SetMaterialDirty();
            ViewChanged?.Invoke();
        }

        // ── The explorer: drag, pinch, wheel, tap ────────────────────────────

        Vector2 Local(PointerEventData e, Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, e.pressEventCamera, out var p);
            return p;
        }

        public void OnPointerDown(PointerEventData e) => _pointers[e.pointerId] = Local(e, e.position);
        public void OnPointerUp(PointerEventData e) => _pointers.Remove(e.pointerId);
        public void OnBeginDrag(PointerEventData e) => _pointers[e.pointerId] = Local(e, e.position);

        public void OnDrag(PointerEventData e)
        {
            if (float.IsInfinity(_minX)) return;
            var now = Local(e, e.position);
            if (_pointers.Count >= 2 && _pointers.ContainsKey(e.pointerId))
            {
                // Two fingers: the pinch zooms about their midpoint, and the midpoint's move pans.
                Vector2 other = default;
                foreach (var kv in _pointers)
                    if (kv.Key != e.pointerId) { other = kv.Value; break; }
                var was = _pointers[e.pointerId];
                float d0 = Vector2.Distance(was, other), d1 = Vector2.Distance(now, other);
                if (d0 > 1f) ZoomAbout((now + other) * 0.5f, d1 / d0);
                Pan(((now + other) - (was + other)) * 0.5f);
            }
            else Pan(now - Local(e, e.position - e.delta));
            _pointers[e.pointerId] = now;
        }

        public void OnEndDrag(PointerEventData e)
        {
            _pointers.Remove(e.pointerId);
            // Settled: paint the water the view now shows.
            _lastPaint = float.NegativeInfinity;
            _viewDirty = true;
        }

        public void OnScroll(PointerEventData e)
        {
            if (float.IsInfinity(_minX) || Mathf.Approximately(e.scrollDelta.y, 0f)) return;
            ZoomAbout(Local(e, e.position), Mathf.Pow(WheelZoom, e.scrollDelta.y));
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (_content?.Locations == null || float.IsInfinity(_minX)) return;
            var at = Local(e, e.position);
            string best = null;
            float bestD = TapReach * TapReach;
            foreach (var l in _content.Locations)
            {
                if (!Drawn(l)) continue;
                float d = (At(l) - at).sqrMagnitude;
                if (d < bestD) { bestD = d; best = l.Id; }
            }
            if (best != null) PlaceTapped?.Invoke(best);
        }

        /// <summary>Move the view by <paramref name="delta"/> pixels (the map follows the finger).</summary>
        void Pan(Vector2 delta)
        {
            _pan -= delta / Scale();
            ClampView();
            _viewDirty = true;
        }

        /// <summary>Zoom by <paramref name="factor"/> keeping the map point under <paramref name="at"/> (rect space) still.</summary>
        void ZoomAbout(Vector2 at, float factor)
        {
            var world = FromRect(at);
            float frame = FrameScale();
            float width = Mathf.Max(1f, rectTransform.rect.width);
            float lo = _chart != null ? width / MaxViewLy / frame : MinZoom;
            float hi = _chart != null ? width / MinViewLy / frame : MaxZoom;
            _zoom = Mathf.Clamp(_zoom * factor, Mathf.Min(lo, 1f), Mathf.Max(hi, 1f));
            // The centre that puts world back under the pointer.
            _pan = world - (at - rectTransform.rect.center) / Scale() - FrameCentre;
            ClampView();
            _viewDirty = true;
        }

        /// <summary>A charted map stays over the galaxy; an uncharted one within a frame's width of its places.</summary>
        void ClampView()
        {
            if (_chart != null)
            {
                var c = Centre;
                float r = c.magnitude, max = (float)GalaxyHalf;
                if (r > max) _pan = c * (max / r) - FrameCentre;
                return;
            }
            float w = _maxX - _minX, h = _maxZ - _minZ;
            _pan = new Vector2(Mathf.Clamp(_pan.x, -w, w), Mathf.Clamp(_pan.y, -h, h));
        }

        // ── The sea behind a charted map ─────────────────────────────────────

        /// <summary>True when the view has left the painted water, or zoomed far enough that it reads blurred or coarse.</summary>
        bool NeedsPaint()
        {
            if (!_painted) return _chart != null;
            if (_paintedCoarse && _pointers.Count == 0) return true;
            var r = rectTransform.rect;
            var lo = FromRect(r.min);
            var hi = FromRect(r.max);
            if (lo.x < _paintLo.x || lo.y < _paintLo.y || hi.x > _paintHi.x || hi.y > _paintHi.y) return true;
            float share = (hi.x - lo.x) / Mathf.Max(1e-3f, _paintHi.x - _paintLo.x);
            return share < 0.3f;
        }

        void PaintSea()
        {
            _painted = false;
            if (_chart == null || float.IsInfinity(_minX)) return;
            var r = rectTransform.rect;
            if (r.width < 1f || r.height < 1f) return;
            _lastPaint = Time.unscaledTime;
            _seaTexHeight = Mathf.Clamp(Mathf.RoundToInt(SeaTexWidth * r.height / r.width), 16, 256);
            int w = SeaTexWidth + InsetTex, h = Mathf.Max(_seaTexHeight, InsetTex) + WhiteRows;
            _white = new Vector2((w - 0.5f) / w, (h - 0.5f) / h);

            // The water around the view, half a view wider on every side, so a pan slides over painted sea.
            var lo = FromRect(r.min);
            var hi = FromRect(r.max);
            var span = hi - lo;
            lo -= span * PaintMargin;
            hi += span * PaintMargin;
            _paintLo = lo;
            _paintHi = hi;
            int coarse = _pointers.Count > 0 ? 2 : 1;
            string key = $"{_content.GetHashCode()}:{w}x{h}:{lo.x:F0},{lo.y:F0},{hi.x:F0},{hi.y:F0}:{coarse}";
            _painted = true;
            _paintedCoarse = coarse > 1;
            if (s_Atlas && s_AtlasKey == key) return;

            if (!s_Atlas || s_Atlas.width != w || s_Atlas.height != h)
            {
                _insetPainted = false;
                if (s_Atlas) Destroy(s_Atlas);
                s_Atlas = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                    name = "VoyageSea",
                };
            }
            // The inset and the white strip are painted once per texture; a repaint redraws only the water.
            var px = _insetPainted ? s_Atlas.GetPixels32() : new Color32[w * h];
            // The framed water: each texel the column density at its point (a 2x2 block per sample while dragging).
            int step = _pointers.Count > 0 ? 2 : 1;
            for (int y = 0; y < _seaTexHeight; y += step)
            for (int x = 0; x < SeaTexWidth; x += step)
            {
                double gx = lo.x + (hi.x - lo.x) * (x + 0.5 * step) / SeaTexWidth;
                double gy = -(lo.y + (hi.y - lo.y) * (y + 0.5 * step) / _seaTexHeight);
                var c = Sea(HyperSea.Density(gx, gy), 255);
                for (int by = y; by < Mathf.Min(y + step, _seaTexHeight); by++)
                for (int bx = x; bx < x + step && bx < SeaTexWidth; bx++)
                    px[by * w + bx] = c;
            }
            if (!_insetPainted)
            {
                // The whole galaxy, 100,000 ly across, clear outside the disk.
                for (int y = 0; y < InsetTex; y++)
                for (int x = 0; x < InsetTex; x++)
                {
                    double gx = -GalaxyHalf + 2 * GalaxyHalf * (x + 0.5) / InsetTex;
                    double gy = -(-GalaxyHalf + 2 * GalaxyHalf * (y + 0.5) / InsetTex);
                    double rr = Math.Sqrt(gx * gx + gy * gy) / GalaxyHalf;
                    byte a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01((float)(1.0 - rr) * 12f));
                    px[y * w + SeaTexWidth + x] = Sea(HyperSea.Density(gx, gy), a);
                }
                for (int y = h - WhiteRows; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color32(255, 255, 255, 255);
                _insetPainted = true;
            }
            s_Atlas.SetPixels32(px);
            s_Atlas.Apply(false);
            s_AtlasKey = key;
        }

        static Color32 Sea(double n, byte alpha)
        {
            var (r, g, b) = HyperSea.SeaColor(n);
            return new Color32((byte)r, (byte)g, (byte)b, alpha);
        }

        Vector2 Uv(int x, int y) => new((float)x / s_Atlas.width, (float)y / s_Atlas.height);

        /// <summary>A map point on the painted water, as a uv in the atlas (kept inside the water's own texels).</summary>
        Vector2 SeaUv(Vector2 map)
        {
            float u = Mathf.InverseLerp(_paintLo.x, _paintHi.x, map.x) * SeaTexWidth;
            float v = Mathf.InverseLerp(_paintLo.y, _paintHi.y, map.y) * _seaTexHeight;
            return new Vector2(u / s_Atlas.width, v / s_Atlas.height);
        }

        Rect InsetRect()
        {
            var r = rectTransform.rect;
            float size = Mathf.Min(r.width, r.height) * InsetShare;
            return new Rect(r.xMax - size - InsetMargin, r.yMax - size - InsetMargin, size, size);
        }

        // ── The mesh ─────────────────────────────────────────────────────────

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_content?.Locations == null || _style == null || float.IsInfinity(_minX)) return;
            bool sea = _painted && _chart != null && s_Atlas;
            var white = sea ? _white : Vector2.zero;

            if (sea)
            {
                // The view's corners on the painted water (it is painted wider than the view, see PaintSea).
                var r = rectTransform.rect;
                var lo = SeaUv(FromRect(r.min));
                var hi = SeaUv(FromRect(r.max));
                Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax),
                    new Vector2(r.xMax, r.yMin), SeaTint, lo, new Vector2(lo.x, hi.y), hi, new Vector2(hi.x, lo.y));
                // Under the lanes and places, so a place in the corner still shows over it.
                Inset(vh, white);
            }

            foreach (var lane in _content.Lanes ?? new List<VoyageLane>())
            {
                var a = _content.Location(lane?.From);
                var b = _content.Location(lane?.To);
                if (Drawn(a) && Drawn(b)) Line(vh, At(a), At(b), LaneWidth, _style.LaneColor, white);
            }

            if (_voyage != null)
            {
                // The course an Ark sails: cell by cell, round the reefs.
                for (int i = 1; i < _voyage.Points.Count; i++)
                    Line(vh, ToRect(Galaxy(_voyage.Points[i - 1])), ToRect(Galaxy(_voyage.Points[i])), CourseWidth,
                        _style.RouteColor, white);
            }
            else
            {
                for (int i = 1; i < _route.Count; i++)
                {
                    var a = _content.Location(_route[i - 1]);
                    var b = _content.Location(_route[i]);
                    if (Drawn(a) && Drawn(b)) Line(vh, At(a), At(b), RouteWidth, _style.RouteColor, white);
                }
            }

            foreach (var l in _content.Locations)
            {
                if (!Drawn(l)) continue;
                if (l.Id == _here) Diamond(vh, At(l), HereSize, _style.HereColor, white);
                bool target = _route.Count > 0 && _route[^1] == l.Id;
                Diamond(vh, At(l), target ? MarkerSize * 1.4f : MarkerSize, target ? _style.RouteColor : MarkerColor(l),
                    white);
            }

        }

        static Vector2 Galaxy((double x, double y) p) => new((float)p.x, (float)-p.y);

        /// <summary>The whole galaxy in the corner, with Sol and where this map's water is.</summary>
        void Inset(VertexHelper vh, Vector2 white)
        {
            var box = InsetRect();
            Quad(vh, new Vector2(box.xMin, box.yMin), new Vector2(box.xMin, box.yMax), new Vector2(box.xMax, box.yMax),
                new Vector2(box.xMax, box.yMin), Color.white, Uv(SeaTexWidth, 0), Uv(SeaTexWidth, InsetTex),
                Uv(SeaTexWidth + InsetTex, InsetTex), Uv(SeaTexWidth + InsetTex, 0));
            Vector2 InBox(Vector2 g) => box.center + g / (float)(2 * GalaxyHalf) * box.width;
            Diamond(vh, InBox(new Vector2(0f, -(float)HyperSea.SolRadius)), 5f, SolColor, white);
            Diamond(vh, InBox(Centre), 7f, _style.HereColor, white);
        }

        Color MarkerColor(VoyageLocation l) => MarkerColor(_style, l);

        /// <summary>A place's marker colour: its experience's, from the harbour's style.</summary>
        public static Color MarkerColor(VoyageToyDefinitionSO style, VoyageLocation l) => l.Experience switch
        {
            VoyageExperience.VaultField => style.VaultColor,
            VoyageExperience.Core => style.CoreColor,
            _ => style.PortColor,
        };

        internal static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c, Vector2 uv)
        {
            var d = b - a;
            if (d.sqrMagnitude < 1e-4f) return;
            var n = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
            Quad(vh, a - n, a + n, b + n, b - n, c, uv, uv, uv, uv);
        }

        internal static void Diamond(VertexHelper vh, Vector2 p, float size, Color c, Vector2 uv)
        {
            float s = size * 0.5f;
            Quad(vh, p + new Vector2(0f, s), p + new Vector2(s, 0f), p + new Vector2(0f, -s), p + new Vector2(-s, 0f), c,
                uv, uv, uv, uv);
        }

        internal static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color,
            Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.color = color;
            v.position = a; v.uv0 = ua; vh.AddVert(v);
            v.position = b; v.uv0 = ub; vh.AddVert(v);
            v.position = c; v.uv0 = uc; vh.AddVert(v);
            v.position = d; v.uv0 = ud; vh.AddVert(v);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        // ── Labels: each location's own name, and the course line ─────────────

        TextMeshProUGUI NewText(string name, float sizeShare)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (_labelTemplate)
            {
                t.font = _labelTemplate.font;
                t.fontSharedMaterial = _labelTemplate.fontSharedMaterial;
                t.fontSize = _labelTemplate.fontSize * sizeShare;
                t.color = _labelTemplate.color;
            }
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        void Label()
        {
            int n = _content?.Locations?.Count ?? 0;
            // A copied card brings its labels along; adopt them rather than standing a second set.
            if (_labels.Count == 0)
                foreach (Transform child in transform)
                    if (child.name == LabelName && child.TryGetComponent(out TMP_Text existing)) _labels.Add(existing);
            while (_labels.Count < n)
            {
                var t = NewText(LabelName, 0.6f);
                t.alignment = TextAlignmentOptions.Top;
                ((RectTransform)t.transform).sizeDelta = new Vector2(160f, 24f);
                _labels.Add(t);
            }

            // Places on one spot (the three harbours ride Tumult's rim) show one name: where the squad is, else the
            // course's end, else the first.
            _labelShown.Clear();
            for (int i = 0; i < _labels.Count; i++)
            {
                var l = i < n ? _content.Locations[i] : null;
                _labelShown.Add(Drawn(l));
            }
            string end = _route.Count > 0 ? _route[^1] : null;
            for (int i = 0; i < n; i++)
            {
                if (!_labelShown[i]) continue;
                var a = _content.Locations[i];
                for (int j = i + 1; j < n; j++)
                {
                    if (!_labelShown[j]) continue;
                    var b = _content.Locations[j];
                    if ((Pos(a, _chart != null) - Pos(b, _chart != null)).sqrMagnitude > 1f) continue;
                    bool keepB = b.Id == _here || (b.Id == end && a.Id != _here);
                    if (keepB) { _labelShown[i] = false; break; }
                    _labelShown[j] = false;
                }
            }

            for (int i = 0; i < _labels.Count; i++)
            {
                var l = i < n ? _content.Locations[i] : null;
                _labels[i].gameObject.SetActive(_labelShown[i]);
                if (_labelShown[i]) _labels[i].text = l.Name;
            }
        }

        void Caption()
        {
            if (!_captionText)
            {
                var existing = transform.Find(CaptionName);
                if (existing) existing.TryGetComponent(out _captionText);
            }
            bool show = !string.IsNullOrEmpty(_caption);
            if (!_captionText && !show) return;
            if (!_captionText)
            {
                _captionText = NewText(CaptionName, 0.55f);
                _captionText.alignment = TextAlignmentOptions.BottomLeft;
                var rt = (RectTransform)_captionText.transform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(-2f * InsetMargin, 24f);
                rt.anchoredPosition = new Vector2(InsetMargin, InsetMargin);
            }
            _captionText.gameObject.SetActive(show);
            if (show) _captionText.text = _caption;
        }

        void PlaceLabels()
        {
            if (_content?.Locations == null || float.IsInfinity(_minX)) return;
            for (int i = 0; i < _labels.Count && i < _content.Locations.Count; i++)
            {
                var l = _content.Locations[i];
                if (!Drawn(l) || !_labels[i]) continue;
                var at = At(l);
                _labels[i].gameObject.SetActive(i < _labelShown.Count && _labelShown[i] && InFrame(at));
                ((RectTransform)_labels[i].transform).anchoredPosition =
                    at - rectTransform.rect.center + new Vector2(0f, -MarkerSize);
            }
        }
    }
}

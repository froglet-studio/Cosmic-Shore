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
    ///
    /// <para>It zooms as deep as the Voyage Board does (Garrett, 2026-11-02: "browse the vicinity and preserve all the
    /// ability to zoom into the points of interest"): from the whole galaxy down to a 4 ly star system, with the board's
    /// stars, clusters and landmarks, the seven-tier ladder and the Sol and Tumult dives, and a card for whatever is tapped
    /// (<see cref="VoyageMapSky"/>, Docs/Voyages/VOYAGE_BOARD.md §4b).</para>
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
        const float MinViewLy = (float)HyperSeaSky.MinWidth, MaxViewLy = (float)HyperSeaSky.MaxWidth;
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

        // The deep zoom's sky, and a dive in flight (the board's flyTo: the width eases in log space and, on a long
        // zoom, the centre moves with one over the width, so the target stays steady on screen).
        VoyageMapSky _sky;
        bool _flying;
        Vector2 _flyFromC, _flyToC;
        float _flyFromW, _flyToW, _flyT0, _flyMs;

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
        /// <summary>The deep zoom's sky on a charted map (its stars, landmarks and labels), or null.</summary>
        public VoyageMapSky Sky => _chart != null ? _sky : null;
        VoyageMapSky SkyOf => _sky ??= new VoyageMapSky(this);

        /// <summary>
        /// The water this map last painted, for another view to show the same sea (the open voyage card's ground,
        /// <see cref="VoyageRoutePoster"/>): the texture, the uv rectangle holding the water, and the map points at that
        /// rectangle's lower-left and upper-right corners (<see cref="MapPoint"/>'s space). False before the first paint
        /// or on an uncharted map. Read only: it changes nothing on this map.
        /// </summary>
        public bool TrySeaView(out Texture texture, out Rect uv, out Vector2 lo, out Vector2 hi)
        {
            texture = null;
            uv = default;
            lo = _paintLo;
            hi = _paintHi;
            if (!_painted || _chart == null || !s_Atlas) return false;
            texture = s_Atlas;
            uv = new Rect(0f, 0f, (float)SeaTexWidth / s_Atlas.width, (float)_seaTexHeight / s_Atlas.height);
            return true;
        }

        /// <summary>The uv of a white texel in the painted texture, for lines drawn over the water with it.</summary>
        public Vector2 WhiteUv => _white;

        /// <summary>A course point (light years on the board, <see cref="VoyageChart.Voyage.Points"/>) as a map point.</summary>
        public static Vector2 MapPoint((double x, double y) p) => Galaxy(p);

        static Texture2D s_Atlas;
        static string s_AtlasKey;
        Vector2 _white;
        int _seaTexHeight;
        int _spriteRow;
        bool _painted;

        /// <summary>The uv corners of the nucleus sprite for a nucleus <paramref name="radiusPx"/> across on screen.</summary>
        internal (Vector2 lo, Vector2 hi) NucleusUv(float radiusPx) => s_Atlas
            ? VoyageCellDraw.SpriteUv(radiusPx, s_Atlas.width, s_Atlas.height, 0, _spriteRow)
            : (Vector2.zero, Vector2.zero);

        /// <summary>How far the reader has moved the cells' slice from its focus, ly (the edge view's slice bar).</summary>
        public double SliceOffset { get; private set; }

        /// <summary>Move the cells' slice (and the deep tiers' height slab with it) <paramref name="offset"/> ly from
        /// the focus's layer of nuclei; 0 puts it back.</summary>
        public void SetSliceOffset(double offset)
        {
            double o = Math.Max(-2 * HyperSea.DiskHalf, Math.Min(2 * HyperSea.DiskHalf, offset));
            if (Math.Abs(o - SliceOffset) < 1e-9) return;
            SliceOffset = o;
            _viewDirty = true;
        }

        /// <summary>The content's lines the map writes with, or null before <see cref="SetLines"/>.</summary>
        public VoyageLines Lines => _sky?.Lines;

        public override Texture mainTexture => _painted && _chart != null && s_Atlas ? s_Atlas : s_WhiteTexture;

        /// <summary>The text whose font, size and colour every location's label copies.</summary>
        public void SetLabelTemplate(TMP_Text template)
        {
            _labelTemplate = template;
            SkyOf.SetTemplate(template);
        }

        /// <summary>The content's lines the deep zoom writes with (its ladder, readout and cards).</summary>
        public void SetLines(VoyageLines lines) => SkyOf.SetLines(lines);

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
            if (_chart != null) SkyOf.ContentChanged();
            else _sky?.Hide();
            raycastTarget = true;
            // A new frame (another course, or none) starts the explorer over; a redraw of the same one keeps where the
            // player has looked.
            string key = (focus ? "f:" : "a:") + string.Join(">", _route);
            if (key != _frameKey)
            {
                _frameKey = key;
                _fixWidth = 0f;
                _pan = Vector2.zero;
                _zoom = 1f;
                _flying = false;
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

        /// <summary>Which places this crew can see (Docs/Voyages/DEPTH.md §8): an uncharted place is drawn only once the
        /// crew has found it. Null draws every place. Set by the view with the galaxy's record (VoyageRules.IsVisible).</summary>
        public System.Func<string, bool> Visible;

        /// <summary>
        /// The Voyages board's map (Rev 81 B5, Docs/MENU_REDESIGN_B.md section 165): the course with the sea and its places,
        /// without the explorer's tier ladder and readout. Drag, pinch and wheel still explore.
        /// </summary>
        public bool Plain;

        // A view the board asked for (FrameOn): kept across a resize until the player explores.
        Vector2 _fixCentre;
        float _fixWidth;

        /// <summary>
        /// Frame the view on <paramref name="points"/> (map points, <see cref="MapPoint"/>), <paramref name="margin"/> wider
        /// than they span and at least <paramref name="minWidth"/> ly across, at once. The frame holds when the map is resized
        /// (a first draw before the layout settles) until the player drags, pinches or scrolls.
        /// </summary>
        public void FrameOn(IReadOnlyList<Vector2> points, float margin = 1.35f, float minWidth = 360f)
        {
            if (points == null || points.Count == 0) return;
            Vector2 lo = points[0], hi = points[0];
            foreach (var p in points)
            {
                lo = Vector2.Min(lo, p);
                hi = Vector2.Max(hi, p);
            }
            var r = rectTransform.rect;
            float aspect = r.height > 1f ? r.width / r.height : 1.4f;
            float width = Mathf.Max(minWidth, (hi.x - lo.x) * margin, (hi.y - lo.y) * margin * aspect);
            _fixCentre = (lo + hi) * 0.5f;
            _fixWidth = width;
            _flying = false;
            ApplyFix();
        }

        void ApplyFix()
        {
            if (_fixWidth <= 0f || float.IsInfinity(_minX)) return;
            SetView(_fixCentre, _fixWidth);
        }

        /// <summary>A charted map draws only the charted locations, and only the ones this crew can see.</summary>
        bool Drawn(VoyageLocation l) => l != null && (_chart == null || l.Charted) && (Visible == null || Visible(l.Id));

        /// <summary>True when the map draws <paramref name="l"/> (the edge view and the sky follow the map).</summary>
        public bool IsDrawn(VoyageLocation l) => Drawn(l);

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
        // The colour set the map was last drawn in (ColorTrace.Version): another one worn draws it again, the route,
        // landmark and sky colours through the trace (Docs/VesselModels/COLOR_TRACE.md).
        int _colorVersion;

        void LateUpdate()
        {
            if (_colorVersion != ColorTrace.Version)
            {
                _colorVersion = ColorTrace.Version;
                _viewDirty = true;
            }
            if (rectTransform.rect.size != _placedFor)
            {
                Relayout();
                return;
            }
            if (_chart != null && SkyOf.Tick()) _viewDirty = true;
            if (_flying) StepFlight();
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
            CollectSky();
            SetVerticesDirty();
            ViewChanged?.Invoke();
        }

        /// <summary>Work out the sky the view draws; tiles still being made keep the view dirty for the next frame.</summary>
        void CollectSky()
        {
            if (_chart == null || float.IsInfinity(_minX)) return;
            if (SkyOf.Collect()) _viewDirty = true;
        }

        void Relayout()
        {
            ApplyFix();
            _placedFor = rectTransform.rect.size;
            _insetPainted = false;
            PaintSea();
            PlaceLabels();
            CollectSky();
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
        public void OnBeginDrag(PointerEventData e)
        {
            _fixWidth = 0f;
            _flying = false;
            _pointers[e.pointerId] = Local(e, e.position);
        }

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
            _fixWidth = 0f;
            _flying = false;
            ZoomAbout(Local(e, e.position), Mathf.Pow(WheelZoom, e.scrollDelta.y));
        }

        /// <summary>Zoom by <paramref name="factor"/> about the middle of the view (the Voyages screen's + and -), as the
        /// wheel does about the pointer.</summary>
        public void ZoomStep(float factor)
        {
            if (float.IsInfinity(_minX) || factor <= 0f) return;
            _fixWidth = 0f;
            _flying = false;
            ZoomAbout(rectTransform.rect.center, factor);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (_content?.Locations == null || float.IsInfinity(_minX)) return;
            var at = Local(e, e.position);
            if (_chart != null)
            {
                // The ladder first: a tier, or the Sol and Tumult dives.
                int row = SkyOf.LadderAt(at);
                if (row >= 0)
                {
                    var r = rectTransform.rect;
                    var (centre, width) = SkyOf.Dive(row, r.height / Mathf.Max(1f, r.width));
                    FlyTo(centre ?? Centre, width);
                    return;
                }
            }
            string best = null;
            float bestD = TapReach * TapReach;
            foreach (var l in _content.Locations)
            {
                if (!Drawn(l)) continue;
                float d = (At(l) - at).sqrMagnitude;
                if (d < bestD) { bestD = d; best = l.Id; }
            }
            if (best != null)
            {
                PlaceTapped?.Invoke(best);
                return;
            }
            if (_chart == null) return;
            // A star, a cluster or a landmark opens its card; a tap on open water closes it. The board's plain map has none.
            if (Plain) return;
            var hit = SkyOf.HitAt(at);
            if (hit == null && SkyOf.Selected == null) return;
            SkyOf.Select(hit);
            _viewDirty = true;
        }

        /// <summary>The rects the deep zoom's labels keep clear of: the galaxy corner, the caption and the places' names.</summary>
        internal void ReservedBoxes(List<Rect> boxes)
        {
            if (_chart != null) boxes.Add(InsetRect());
            if (_captionText && _captionText.gameObject.activeSelf)
            {
                var r = rectTransform.rect;
                boxes.Add(new Rect(r.xMin, r.yMin, r.width, VoyageMapText.Caption * 1.4f + InsetMargin));
            }
            foreach (var t in _labels)
            {
                if (!t || !t.gameObject.activeSelf) continue;
                var rt = (RectTransform)t.transform;
                var p = rt.anchoredPosition + rectTransform.rect.center;
                // A place's name is centred under its marker, at the top of its box.
                float w = Mathf.Max(24f, t.text.Length * t.fontSize * .58f), h = rt.sizeDelta.y;
                boxes.Add(new Rect(p.x - w * .5f, p.y - h * .5f, w, h));
            }
        }

        // ── Dives ────────────────────────────────────────────────────────────

        float ViewWidth => Mathf.Max(1f, rectTransform.rect.width) / Scale();

        /// <summary>Fly to a view <paramref name="width"/> ly across centred on <paramref name="centre"/> (map units), as
        /// the board's dive does: longer the further it goes, at most 5.2 s.</summary>
        void FlyTo(Vector2 centre, float width)
        {
            width = Mathf.Clamp(width, MinViewLy, MaxViewLy);
            float w0 = ViewWidth;
            float ratio = Mathf.Abs(Mathf.Log(w0 / width)) + Vector2.Distance(Centre, centre) / w0 * .3f;
            _flyFromC = Centre;
            _flyFromW = w0;
            _flyToC = centre;
            _flyToW = width;
            _flyT0 = Time.unscaledTime;
            _flyMs = Mathf.Min(5200f, 700f + 380f * ratio);
            _flying = true;
        }

        void StepFlight()
        {
            float t = _flyMs > 0f ? Mathf.Min(1f, (Time.unscaledTime - _flyT0) * 1000f / _flyMs) : 1f;
            float e = t < .5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
            float w0 = _flyFromW, w1 = _flyToW;
            float wt = Mathf.Exp(Mathf.Log(w0) + (Mathf.Log(w1) - Mathf.Log(w0)) * e);
            bool far = Mathf.Max(w0 / w1, w1 / w0) > 6f;
            float ce = !far ? e : w1 < w0 ? (1f / wt - 1f / w0) / (1f / w1 - 1f / w0) : (wt - w0) / (w1 - w0);
            SetView(_flyFromC + (_flyToC - _flyFromC) * ce, wt);
            if (t >= 1f)
            {
                _flying = false;
                // Arrived: paint the water the view now shows, in full.
                _lastPaint = float.NegativeInfinity;
            }
        }

        /// <summary>Show a view <paramref name="width"/> ly across centred on <paramref name="centre"/> (map units).</summary>
        void SetView(Vector2 centre, float width)
        {
            float frame = FrameScale();
            _zoom = Mathf.Max(1f, rectTransform.rect.width) / Mathf.Clamp(width, MinViewLy, MaxViewLy) / frame;
            _pan = centre - FrameCentre;
            ClampView();
            _viewDirty = true;
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
            if (_paintedCoarse && _pointers.Count == 0 && !_flying) return true;
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
            // Above the water and the corner, the cells' nucleus sprites (VoyageCellDraw), then the white strip.
            _spriteRow = Mathf.Max(_seaTexHeight, InsetTex);
            int w = Mathf.Max(SeaTexWidth + InsetTex, VoyageCellDraw.SpriteColumns), h = _spriteRow + VoyageCellDraw.SpriteRows + WhiteRows;
            _white = new Vector2((w - 0.5f) / w, (h - 0.5f) / h);

            // The water around the view, half a view wider on every side, so a pan slides over painted sea.
            var lo = FromRect(r.min);
            var hi = FromRect(r.max);
            var span = hi - lo;
            lo -= span * PaintMargin;
            hi += span * PaintMargin;
            _paintLo = lo;
            _paintHi = hi;
            bool moving = _pointers.Count > 0 || _flying;
            int coarse = moving ? 2 : 1;
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
            int step = coarse;
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
                VoyageCellDraw.PaintSprites(px, w, 0, _spriteRow);
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
                // The stars, clusters and landmarks over the water, under the corner, the lanes and the places.
                SkyOf.Populate(vh, white, _style);
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
            VoyageExperience.VaultField or VoyageExperience.Hoard => style.VaultColor,
            VoyageExperience.Core => style.CoreColor,
            VoyageExperience.Lair or VoyageExperience.Pen => style.LairColor,
            VoyageExperience.Nursery => style.NurseryColor,
            VoyageExperience.Shrine or VoyageExperience.Derelict => style.ShrineColor,
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

        TextMeshProUGUI NewText(string name, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (_labelTemplate)
            {
                t.font = _labelTemplate.font;
                t.fontSharedMaterial = _labelTemplate.fontSharedMaterial;
                t.color = _labelTemplate.color;
            }
            // A size of its own, not the template's: the card eyebrow it copies is 14 px, which a share of made
            // unreadable on the map (Garrett, 2026-11-02: "words should be readable at all semantic zoom levels").
            t.fontSize = size;
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
                var t = NewText(LabelName, VoyageMapText.Place);
                t.alignment = TextAlignmentOptions.Top;
                t.fontStyle = FontStyles.Bold;
                ((RectTransform)t.transform).sizeDelta = new Vector2(260f, VoyageMapText.Place * 1.4f);
                _labels.Add(t);
            }

            // Places on one spot (the three harbours ride Tumult's rim) show one name: where the squad is, else the
            // course's end, else the first.
            _labelShown.Clear();
            for (int i = 0; i < _labels.Count; i++)
            {
                var l = i < n ? _content.Locations[i] : null;
                // The board's plain map names only the course's two ends.
                bool named = !Plain || (l != null && _route.Count > 0 && (l.Id == _route[0] || l.Id == _route[^1]));
                _labelShown.Add(Drawn(l) && named);
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
                _labels[i].fontSize = VoyageMapText.Place;
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
                _captionText = NewText(CaptionName, VoyageMapText.Caption);
                _captionText.alignment = TextAlignmentOptions.BottomLeft;
                var rt = (RectTransform)_captionText.transform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(-2f * InsetMargin, VoyageMapText.Caption * 1.4f);
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
                var label = (RectTransform)_labels[i].transform;
                bool shown = i < _labelShown.Count && _labelShown[i] && InFrame(at);
                var below = new Vector2(0f, -MarkerSize * .5f - 2f - label.sizeDelta.y * .5f);
                _labels[i].gameObject.SetActive(shown);
                label.anchoredPosition = at - rectTransform.rect.center + below;
            }
        }
    }
}

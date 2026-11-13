using System;
using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The galaxy map's text sizes, in pixels at the menu canvas's 1920x1080 reference (Garrett, 2026-11-02: "words
    /// should be readable at all semantic zoom levels"). The map copies its font and colour from a voyage card's eyebrow,
    /// which is 14 px, so a share of it was unreadable; every map text has its own size here.
    /// </summary>
    public static class VoyageMapText
    {
        /// <summary>A voyage place's name under its marker.</summary>
        public const float Place = 22f;
        /// <summary>A landmark's name.</summary>
        public const float Landmark = 20f;
        /// <summary>A star's, a cluster's or a cell's name.</summary>
        public const float Small = 18f;
        /// <summary>The tier ladder and the readout.</summary>
        public const float Ladder = 17f;
        /// <summary>The course line under the map, and the edge view's caption.</summary>
        public const float Caption = 18f;
        /// <summary>The card a tapped thing opens.</summary>
        public const float Card = 19f;
    }

    /// <summary>
    /// The galaxy map's deep zoom (Garrett, 2026-11-02: "browse the vicinity and preserve all the ability to zoom into the
    /// points of interest"), as the Voyage Board draws it (Docs/Voyages/VOYAGE_BOARD.md §4a-4b): the stars and clusters of
    /// <see cref="HyperSeaSkyField"/> under the map's places, the board's landmarks, their names where they earn one, the
    /// seven-tier ladder with the Sol and Tumult dives, the readout of the tier on show and the card a tapped star, cluster
    /// or landmark opens. Owned and driven by <see cref="VoyageMapGraphic"/>; every word is the content's
    /// (<see cref="VoyageLines"/>) or the sky table's own.
    /// </summary>
    public sealed class VoyageMapSky
    {
        public const string ResourcePath = "Voyages/HyperSeaSky";
        const int LinesPerFrame = 6000;
        const int MaxLabels = 48;
        const int MaxVerts = 60000;
        const float LadderRow = VoyageMapText.Ladder * 1.45f, LadderGap = 8f, LadderLeft = 8f, LadderTop = 8f;
        // The cells in 3D (Garrett, 2026-11-07: "when we zoom into cells the semantic zoom should see nuclei and membranes
        // and represent the actual cell"; Docs/Voyages/VOYAGE_BOARD.md §4c): truncated octahedra round a BCC lattice of
        // nuclei (HyperSeaCells), drawn as a slice at one height. Their walls fade in as a cell grows from 36 to 58 px
        // across (the board starts at 22 px; the map keeps its one mesh under its vertex budget by starting later), the
        // nuclei from a 4 px radius (full at 8), each cell fills with its own sea from 60 px and shows its cytoplasm and
        // inner walls from 90 px, the cell at the view's centre names its levels from 200 px, and a tap on open water
        // opens a cell's card from 40 px.
        public const float CellsFromPx = 36f;
        const float CellFadeTo = 58f, CellFillFrom = 60f, CellFillTo = 90f, CellInnerFrom = 90f, CellTagsFrom = 200f,
            CellTapFrom = 40f, NucleusOutlineFrom = 50f;
        const int MaxCells = 2500, CellVertBudget = 34000;
        const float Margin = 20f;

        static HyperSeaSkyCatalog s_Catalog;
        static bool s_Loaded;

        static readonly Color32 GlobCore = new(255, 226, 170, 242), GlobMid = new(255, 206, 140, 115), GlobEdge = new(255, 200, 130, 0);
        static readonly Color OpenRingAuthored = new(170 / 255f, 205 / 255f, 1f, .85f);
        static Color OpenRing => ColorTrace.Retrace(ColorTrace.Ui, OpenRingAuthored);
        static readonly Color StarLabelRealAuthored = new(214 / 255f, 226 / 255f, 246 / 255f, .8f);
        static Color StarLabelReal => ColorTrace.Retrace(ColorTrace.Ui, StarLabelRealAuthored);
        static readonly Color StarLabelModelAuthored = new(200 / 255f, 210 / 255f, 230 / 255f, .6f);
        static Color StarLabelModel => ColorTrace.Retrace(ColorTrace.Ui, StarLabelModelAuthored);
        static readonly Color StarLabelTumultAuthored = new(1f, 214 / 255f, 150 / 255f, .82f);
        static Color StarLabelTumult => ColorTrace.Retrace(ColorTrace.Ui, StarLabelTumultAuthored);
        static readonly Color GlobLabelAuthored = new(1f, 220 / 255f, 170 / 255f, .85f);
        static Color GlobLabel => ColorTrace.Retrace(ColorTrace.Ui, GlobLabelAuthored);
        static readonly Color OpenLabelAuthored = new(180 / 255f, 210 / 255f, 1f, .85f);
        static Color OpenLabel => ColorTrace.Retrace(ColorTrace.Ui, OpenLabelAuthored);
        static readonly Color FarColorAuthored = new(159 / 255f, 176 / 255f, 200 / 255f, .9f);
        static Color FarColor => ColorTrace.Retrace(ColorTrace.Ui, FarColorAuthored);
        static readonly Color LadderIdle = new(214 / 255f, 224 / 255f, 240 / 255f, .7f);
        static readonly Color PanelBack = new(.02f, .035f, .07f, .86f);
        static readonly Color LabelBack = new(.02f, .035f, .07f, .62f);
        /// <summary>The Lime of the edge view's slice bar (the style guide's call to action: it is dragged).</summary>
        public static readonly Color SliceLimeAuthored = new(179 / 255f, 1f, 89 / 255f, .95f);
        public static Color SliceLime => ColorTrace.Retrace(ColorTrace.Ui, SliceLimeAuthored);
        static readonly Color CutLineAuthored = new(179 / 255f, 1f, 89 / 255f, .6f);
        static Color CutLine => ColorTrace.Retrace(ColorTrace.Ui, CutLineAuthored);
        static readonly Color TagNucleusAuthored = new(1f, 238 / 255f, 196 / 255f, 1f);
        static Color TagNucleus => ColorTrace.Retrace(ColorTrace.Ui, TagNucleusAuthored);
        static readonly Color TagCytoplasmAuthored = new(207 / 255f, 228 / 255f, 1f, 1f);
        static Color TagCytoplasm => ColorTrace.Retrace(ColorTrace.Ui, TagCytoplasmAuthored);
        static readonly Color TagMembraneAuthored = new(214 / 255f, 236 / 255f, 1f, 1f);
        static Color TagMembrane => ColorTrace.Retrace(ColorTrace.Ui, TagMembraneAuthored);
        static readonly Color TagBodyAuthored = new(214 / 255f, 224 / 255f, 240 / 255f, .8f);
        static Color TagBody => ColorTrace.Retrace(ColorTrace.Ui, TagBodyAuthored);

        readonly VoyageMapGraphic _map;
        readonly HyperSeaSkyField _field = new();
        readonly List<HyperSeaSkyField.Mark> _stars = new(), _clusters = new();
        readonly List<SkyObject> _landmarks = new();
        readonly List<Hit> _hits = new();
        readonly List<Candidate> _candidates = new();
        readonly List<Rect> _boxes = new();
        readonly List<Rect> _backs = new();
        // The cells the view's slice crosses: each cell, its nucleus's middle in the slice (board x, y), how far the
        // nucleus sits off the slice and its radius, and its wet outline (board x, y) in _cellPoly from _cellStart[i] to
        // _cellStart[i + 1].
        readonly List<(double x, double y, double z)> _nuclei = new();
        readonly List<(double x, double y, double z)> _cellNuclei = new();
        readonly List<double> _cellH = new();
        readonly List<double> _cellR = new();
        readonly List<(double x, double y)> _cellMid = new();
        readonly List<(double u, double v)> _wetScratch = new();
        readonly List<int> _cellStart = new();
        readonly List<(double u, double v)> _cellPoly = new();
        readonly List<(double u, double v)> _sectionScratch = new();
        readonly List<Vector2> _screenPoly = new();
        readonly Dictionary<(int, int, int), Color> _cellSea = new();
        float _cellAlpha;
        double _sliceZ;
        readonly List<TMP_Text> _labels = new();
        readonly List<TMP_Text> _ladder = new();
        readonly List<Rect> _ladderRects = new();
        TMP_Text _readout, _cardText;
        Image _card;
        TMP_Text _template;
        VoyageLines _lines;
        HyperSeaSky.View _view;
        bool _hasView;
        int _tier = -1;

        /// <summary>The thing whose card is open, or null.</summary>
        public SkyObject Selected { get; private set; }
        /// <summary>The stars drawn now (the edge view stands them at their heights).</summary>
        public IReadOnlyList<HyperSeaSkyField.Mark> Stars => _stars;
        /// <summary>The landmarks drawn now.</summary>
        public IReadOnlyList<SkyObject> Landmarks => _landmarks;
        public HyperSeaSkyCatalog Catalog => s_Catalog;
        /// <summary>The content's lines the deep zoom writes with.</summary>
        public VoyageLines Lines => _lines;
        /// <summary>The height the map slices the cells at (and the deep tiers' slab is centred on), ly.</summary>
        public double SliceZ => _sliceZ;
        /// <summary>The height the slice would sit at with no offset: the focus's layer of nuclei.</summary>
        public double SliceBase { get; private set; }
        /// <summary>True while the map shows the cells (the edge view then cuts them upright and shows the slice bar).</summary>
        public bool CellZoom => _hasView && CellPx >= CellsFromPx;

        struct Hit
        {
            public SkyObject Object;
            public Vector2 At;
            public float Reach;
        }

        struct Candidate
        {
            public string Text;
            public Vector2 At;
            public float Offset, Priority;
            public Color Color;
            public bool Small;
        }

        public VoyageMapSky(VoyageMapGraphic map) => _map = map;

        public void SetTemplate(TMP_Text template) => _template = template;
        public void SetLines(VoyageLines lines) => _lines = lines;

        /// <summary>Read more of the sky table; true when this frame finished it (the map then redraws).</summary>
        public bool Tick()
        {
            if (!s_Loaded)
            {
                s_Loaded = true;
                var asset = Resources.Load<TextAsset>(ResourcePath);
                if (!asset)
                {
                    CSDebug.LogError($"[VoyageMapSky] Resources/{ResourcePath} is missing: the galaxy map shows the model sky " +
                                   "only. Run node Docs/Voyages/VoyageBoard/export_sky.js.");
                    return false;
                }
                s_Catalog = new HyperSeaSkyCatalog(asset.text);
            }
            if (s_Catalog == null || s_Catalog.Done)
            {
                if (_field.Catalog != s_Catalog && s_Catalog != null) { _field.Catalog = s_Catalog; return Adopt(); }
                return false;
            }
            if (!s_Catalog.Step(LinesPerFrame)) return false;
            _field.Catalog = s_Catalog;
            return Adopt();
        }

        bool Adopt()
        {
            // Tumult's names arrive with the table: make the tiles drawn before it again.
            _field.Clear();
            Landmarks0();
            return true;
        }

        readonly List<SkyObject> _allLandmarks = new();

        /// <summary>The board's places the map does not already draw as the content's own locations.</summary>
        void Landmarks0()
        {
            _allLandmarks.Clear();
            if (s_Catalog == null) return;
            var content = _map.Content;
            foreach (var p in s_Catalog.Landmarks)
            {
                bool drawn = false;
                if (content?.Locations != null)
                    foreach (var l in content.Locations)
                        if (_map.IsDrawn(l) && Math.Abs(l.GalaxyX - p.X) < 2 && Math.Abs(l.GalaxyY - p.Y) < 2)
                        {
                            drawn = true;
                            break;
                        }
                if (!drawn) _allLandmarks.Add(p);
            }
        }

        /// <summary>The content changed: the landmarks the map does not already draw are worked out again.</summary>
        public void ContentChanged() => Landmarks0();

        /// <summary>An uncharted map has no sky: put its names, ladder and card away.</summary>
        public void Hide()
        {
            _hasView = false;
            _stars.Clear();
            _clusters.Clear();
            _landmarks.Clear();
            _hits.Clear();
            _cellNuclei.Clear();
            _backs.Clear();
            _ladderRects.Clear();
            Selected = null;
            foreach (var t in _labels) if (t) t.gameObject.SetActive(false);
            foreach (var t in _ladder) if (t) t.gameObject.SetActive(false);
            if (_readout) _readout.gameObject.SetActive(false);
            if (_card) _card.gameObject.SetActive(false);
        }

        // ── Collect: what the view draws, its labels, its hits ───────────────

        /// <summary>Work out what the map's view draws now. True when tiles are still being made (call again next frame).</summary>
        public bool Collect()
        {
            var r = _map.rectTransform.rect;
            double s = _map.PixelsPerUnit;
            if (r.width < 1f || s <= 0) return false;
            var c = _map.ViewCentre;
            double cx = c.x, cy = -c.y, w = r.width / s;
            _view = HyperSeaSky.ViewOf(cx, cy, w, r.height / r.width,
                s_Catalog != null && s_Catalog.Done ? s_Catalog.Landmarks : null, _map.SliceOffset);
            _hasView = true;
            // The slab's height is the cells' slice; wider views, which have no slab, slice at the squad's place.
            if (_view.Slab)
            {
                _sliceZ = _view.Zf;
                SliceBase = HyperSeaCells.SliceZ(_view.Zf - _map.SliceOffset, 0);
            }
            else
            {
                var here = _map.Content?.Location(_map.Here);
                double hz = here != null && here.Charted ? here.GalaxyZ : HyperSea.SolHeight;
                SliceBase = HyperSeaCells.SliceZ(hz, 0);
                _sliceZ = HyperSeaCells.SliceZ(hz, _map.SliceOffset);
            }
            double halfW = (r.width * .5 + Margin) / s, halfH = (r.height * .5 + Margin) / s;
            _field.Collect(_view, cx - halfW, cy - halfH, cx + halfW, cy + halfH, _stars, _clusters);
            _landmarks.Clear();
            foreach (var p in _allLandmarks)
                if (HyperSeaSky.LandmarkShown(p, w) && Math.Abs(p.X - cx) < halfW + 60 / s && Math.Abs(p.Y - cy) < halfH + 40 / s)
                    _landmarks.Add(p);
            Cells(cx - halfW, cy - halfH, cx + halfW, cy + halfH);
            Hits();
            Ladder();
            Labels();
            return _field.Pending;
        }

        Vector2 At(double x, double y)
        {
            var r = _map.rectTransform.rect;
            double s = _map.PixelsPerUnit;
            var c = _map.ViewCentre;
            return new Vector2((float)(r.center.x + (x - c.x) * s), (float)(r.center.y + (-y - c.y) * s));
        }

        bool SystemTier => _hasView && _view.W < 30;

        /// <summary>A cell's width across its flats, in pixels, at the view's scale.</summary>
        float CellPx => (float)(HyperSea.CellLy * _map.PixelsPerUnit);

        /// <summary>A point of the map's rect in the board's frame (light years).</summary>
        (double x, double y) BoardAt(Vector2 at)
        {
            var r = _map.rectTransform.rect;
            double s = _map.PixelsPerUnit;
            var c = _map.ViewCentre;
            return (c.x + (at.x - r.center.x) / s, -(c.y + (at.y - r.center.y) / s));
        }

        /// <summary>The cells the view's slice crosses, with their sections (none while a cell is too small to see).</summary>
        void Cells(double x0, double y0, double x1, double y1)
        {
            _cellNuclei.Clear();
            _cellH.Clear();
            _cellR.Clear();
            _cellMid.Clear();
            _cellStart.Clear();
            _cellPoly.Clear();
            float px = CellPx;
            _cellAlpha = Mathf.Clamp01((px - CellsFromPx) / (CellFadeTo - CellsFromPx));
            if (px < CellsFromPx) return;
            double m = HyperSeaCells.A;
            HyperSeaCells.NucleiInSlice(_sliceZ, x0 - m, y0 - m, x1 + m, y1 + m, _nuclei, MaxCells);
            foreach (var c in _nuclei)
            {
                if (!HyperSeaCells.TrySection(c, (0, 0, _sliceZ), (1, 0, 0), (0, 1, 0), _sectionScratch, out var sec)) continue;
                // a sliver the slice only grazes is left to the liquid between cells
                if (!HyperSeaCells.TryWetOutline(_sectionScratch, _wetScratch)) continue;
                _cellNuclei.Add(c);
                _cellH.Add(sec.Mh);
                _cellR.Add(sec.NucleusR);
                _cellMid.Add((sec.Mu, sec.Mv));
                _cellStart.Add(_cellPoly.Count);
                _cellPoly.AddRange(_wetScratch);
            }
            _cellStart.Add(_cellPoly.Count);
        }

        /// <summary>A cell's own sea: the colour of the water at its lattice point (a knot's cells share it).</summary>
        Color CellSea((double x, double y, double z) c)
        {
            var key = HyperSeaCells.KeyOf(c);
            if (_cellSea.TryGetValue(key, out var col)) return col;
            if (_cellSea.Count > 50000) _cellSea.Clear();
            var l = HyperSeaCells.LatticePoint(c);
            var (r, g, b) = HyperSea.SeaColor(HyperSea.Density(l.x, l.y, l.z));
            col = new Color((float)r / 255f, (float)g / 255f, (float)b / 255f, 1f);
            _cellSea[key] = col;
            return col;
        }

        /// <summary>The card a cell opens: its nucleus, and the plane cell under it for its berth and name.</summary>
        SkyObject CellObject((double x, double y, double z) c)
        {
            var (q, r) = HyperSea.CellOf(c.x, c.y);
            return new SkyObject { Kind = SkyKind.Cell, X = c.x, Y = c.y, Z = c.z, CellQ = q, CellR = r };
        }

        void Hits()
        {
            _hits.Clear();
            foreach (var m in _stars)
            {
                if (m.Alpha <= .3f) continue;
                float rad = (float)HyperSeaSky.StarRadius(m.Rel);
                _hits.Add(new Hit { Object = m.Object, At = At(m.Object.X, m.Object.Y), Reach = Mathf.Max(7f, rad + 4f) });
            }
            double s = _map.PixelsPerUnit;
            foreach (var m in _clusters)
                _hits.Add(new Hit
                {
                    Object = m.Object, At = At(m.Object.X, m.Object.Y),
                    Reach = Mathf.Max(9f, (float)HyperSeaSky.ClusterRadius(m.Object, s) + 3f),
                });
            foreach (var p in _landmarks)
                _hits.Add(new Hit { Object = p, At = At(p.X, p.Y), Reach = 14f });
        }

        /// <summary>The star, cluster or landmark under a tap (rect space), or null; landmarks win over stars, as places
        /// do on the board.</summary>
        public SkyObject HitAt(Vector2 at)
        {
            SkyObject best = null;
            float bestD = float.PositiveInfinity;
            foreach (var h in _hits)
            {
                float d0 = Vector2.Distance(h.At, at);
                if (d0 >= h.Reach) continue;
                float d = d0 + (h.Object.Kind == SkyKind.Landmark ? 0f : 8f);
                if (d < bestD) { bestD = d; best = h.Object; }
            }
            // Open water at the deep tiers opens the cell under the tap; a second tap on the same cell closes it.
            if (best == null && _hasView && CellPx >= CellTapFrom)
            {
                var (x, y) = BoardAt(at);
                var n = HyperSeaCells.CellAt(x, y, _sliceZ);
                if (Selected is { Kind: SkyKind.Cell } c && c.X == n.x && c.Y == n.y && c.Z == n.z) return null;
                best = CellObject(n);
            }
            return best;
        }

        // ── The mesh ─────────────────────────────────────────────────────────

        /// <summary>Draw the sky into the map's mesh (under the inset, the lanes and the places).</summary>
        public void Populate(VertexHelper vh, Vector2 white, VoyageToyDefinitionSO style)
        {
            if (!_hasView) return;
            var r = _map.rectTransform.rect;
            double s = _map.PixelsPerUnit;
            var lim = new Rect(r.xMin - 10f, r.yMin - 10f, r.width + 20f, r.height + 20f);
            // The cells first, under everything.
            if (_cellAlpha > 0f) PopulateCells(vh, white);
            foreach (var m in _stars)
            {
                if (vh.currentVertCount > MaxVerts) return;
                var o = m.Object;
                var at = At(o.X, o.Y);
                if (!lim.Contains(at)) continue;
                float rad = (float)HyperSeaSky.StarRadius(m.Rel);
                var col = new Color32(o.R, o.G, o.B, (byte)Mathf.RoundToInt(255f * m.Alpha));
                if (rad < 1.1f)
                {
                    VoyageMapGraphic.Quad(vh, at + new Vector2(-rad, -rad), at + new Vector2(-rad, rad), at + new Vector2(rad, rad),
                        at + new Vector2(rad, -rad), col, white, white, white, white);
                    continue;
                }
                if (m.Rel > 3.2f)
                {
                    var glow = new Color32(o.R, o.G, o.B, (byte)Mathf.RoundToInt(255f * m.Alpha * .14f));
                    Disc(vh, at, rad * 2.6f, glow, glow, 10, white);
                }
                Disc(vh, at, rad, col, col, 8, white);
            }
            var clim = new Rect(r.xMin - 30f, r.yMin - 30f, r.width + 60f, r.height + 60f);
            // A globular cluster's glow in the worn colour set (ColorTrace), looked up once per mesh.
            Color32 globCore = (Color32)ColorTrace.Retrace(ColorTrace.Ui, GlobCore),
                globMid = (Color32)ColorTrace.Retrace(ColorTrace.Ui, GlobMid),
                globEdge = (Color32)ColorTrace.Retrace(ColorTrace.Ui, GlobEdge);
            foreach (var m in _clusters)
            {
                if (vh.currentVertCount > MaxVerts) return;
                var c = m.Object;
                var at = At(c.X, c.Y);
                if (!clim.Contains(at)) continue;
                float rpx = (float)HyperSeaSky.ClusterRadius(c, s);
                if (c.Kind == SkyKind.Globular)
                {
                    float a = m.Alpha;
                    Disc(vh, at, rpx * 1.4f * .45f, Fade(globCore, a), Fade(globMid, a), 12, white);
                    Annulus(vh, at, rpx * 1.4f * .45f, rpx * 1.4f, Fade(globMid, a), Fade(globEdge, a), 12, white);
                }
                else DashedRing(vh, at, rpx, 1.1f, new Color(OpenRing.r, OpenRing.g, OpenRing.b, OpenRing.a * m.Alpha), white);
            }
            foreach (var p in _landmarks)
            {
                var at = At(p.X, p.Y);
                if (!clim.Contains(at)) continue;
                VoyageMapGraphic.Diamond(vh, at, LandmarkSize(p), LandmarkColor(p, style), white);
            }
            if (Selected is { Kind: SkyKind.Cell })
            {
                if (HyperSeaCells.TrySection((Selected.X, Selected.Y, Selected.Z), (0, 0, _sliceZ), (1, 0, 0), (0, 1, 0),
                        _sectionScratch, out _) && HyperSeaCells.TryWetOutline(_sectionScratch, _wetScratch))
                {
                    _screenPoly.Clear();
                    foreach (var p in _wetScratch) _screenPoly.Add(At(p.u, p.v));
                    for (int k = 0; k < _screenPoly.Count; k++)
                        VoyageMapGraphic.Line(vh, _screenPoly[k], _screenPoly[(k + 1) % _screenPoly.Count], 2f, Color.white, white);
                }
            }
            else if (Selected != null)
            {
                var at = At(Selected.X, Selected.Y);
                float rad = Selected.IsCluster ? (float)HyperSeaSky.ClusterRadius(Selected, s) + 5f
                    : Selected.Kind == SkyKind.Landmark ? LandmarkSize(Selected) * .5f + 5f : 6f + 2f;
                Ring(vh, at, rad, 1f, Color.white, 24, white);
            }
            // The map's bottom edge is the line the edge view cuts along (Garrett, 2026-11-07: "the bottom edge of the plane
            // [is] the slice for the thickness so they always have a nice lock up"): marked in the slice bar's Lime.
            VoyageMapGraphic.Line(vh, new Vector2(r.xMin, r.yMin + 1f), new Vector2(r.xMax, r.yMin + 1f), 1.5f, CutLine, white);
            // The dark plates under the names and the readout, so they read over bright stars and thick water.
            var labelBack = ColorTrace.Retrace(ColorTrace.Ui, LabelBack);
            foreach (var b in _backs)
                VoyageMapGraphic.Quad(vh, new Vector2(b.xMin, b.yMin), new Vector2(b.xMin, b.yMax), new Vector2(b.xMax, b.yMax),
                    new Vector2(b.xMax, b.yMin), labelBack, white, white, white, white);
            // The ladder's chips, under their text.
            for (int i = 0; i < _ladderRects.Count; i++)
            {
                var b = _ladderRects[i];
                bool on = i == _tier;
                VoyageMapGraphic.Quad(vh, new Vector2(b.xMin, b.yMin), new Vector2(b.xMin, b.yMax), new Vector2(b.xMax, b.yMax),
                    new Vector2(b.xMax, b.yMin),
                    ColorTrace.Retrace(ColorTrace.Ui, on ? new Color(.95f, .97f, 1f, .92f) : new Color(.04f, .067f, .125f, .78f)),
                    white, white, white, white);
            }
        }

        /// <summary>The cells the slice crosses: their seas, cytoplasm and walls, then the nuclei over them.</summary>
        void PopulateCells(VertexHelper vh, Vector2 white)
        {
            var r = _map.rectTransform.rect;
            float px = CellPx, fill = Mathf.Clamp01((px - CellFillFrom) / (CellFillTo - CellFillFrom));
            bool inner = px > CellInnerFrom;
            var lim = new Rect(r.xMin - px, r.yMin - px, r.width + 2f * px, r.height + 2f * px);
            int budget = vh.currentVertCount + CellVertBudget;
            // the liquid between the cells, under them
            float memA = (float)HyperSeaCells.MembraneAlpha(px);
            VoyageCellDraw.Liquid(vh, r, Mathf.Max(.4f, memA) * _cellAlpha, white);
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < _cellNuclei.Count; i++)
                {
                    if (vh.currentVertCount > budget) break;
                    var at = At(_cellMid[i].x, _cellMid[i].y);
                    if (!lim.Contains(at)) continue;
                    _screenPoly.Clear();
                    for (int k = _cellStart[i]; k < _cellStart[i + 1]; k++) _screenPoly.Add(At(_cellPoly[k].u, _cellPoly[k].v));
                    if (pass == 0)
                    {
                        if (fill > 0f) VoyageCellDraw.Cell(vh, _screenPoly, at, CellSea(_cellNuclei[i]), px, fill * _cellAlpha, inner, white);
                    }
                    else VoyageCellDraw.Walls(vh, _screenPoly, px, _cellAlpha, white);
                }
            // The nuclei, each at its cell's middle and as big as its cell: where the slice cuts one, its section; where
            // it passes above or below, its outline, dashed.
            float nucA = (float)HyperSeaCells.NucleusAlpha(px);
            if (nucA <= 0f) return;
            double s = _map.PixelsPerUnit;
            for (int i = 0; i < _cellNuclei.Count; i++)
            {
                if (vh.currentVertCount > budget + 4000) break;
                var at = At(_cellMid[i].x, _cellMid[i].y);
                if (!lim.Contains(at)) continue;
                double R = _cellR[i], h = Math.Abs(_cellH[i]);
                if (h < R)
                {
                    float rad = (float)(Math.Sqrt(R * R - h * h) * s);
                    VoyageCellDraw.NucleusQuad(vh, at, rad, nucA, _map.NucleusUv(rad));
                }
                else if (px > NucleusOutlineFrom)
                    VoyageCellDraw.NucleusOutline(vh, at, (float)(R * s), nucA * (float)(.8 - h / HyperSeaCells.A * .6), white);
            }
        }

        static Color32 Fade(Color32 c, float a) => new(c.r, c.g, c.b, (byte)Mathf.RoundToInt(c.a * a));

        static float LandmarkSize(SkyObject p) =>
            p.Region == "far" ? 7f : p.KindId == "Harbour" || p.KindId == "Guarded" ? 11f : 9f;

        static Color LandmarkColor(SkyObject p, VoyageToyDefinitionSO style) => p.KindId switch
        {
            "Far" => FarColor,
            "VaultField" => style.VaultColor,
            "Core" => style.CoreColor,
            "Harbour" or "Guarded" => style.HereColor,
            _ => style.PortColor,
        };

        static void Disc(VertexHelper vh, Vector2 at, float rad, Color32 centre, Color32 rim, int n, Vector2 uv)
        {
            int i0 = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.uv0 = uv;
            v.color = centre;
            v.position = at;
            vh.AddVert(v);
            v.color = rim;
            for (int k = 0; k < n; k++)
            {
                float t = k * Mathf.PI * 2f / n;
                v.position = at + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * rad;
                vh.AddVert(v);
            }
            for (int k = 0; k < n; k++) vh.AddTriangle(i0, i0 + 1 + k, i0 + 1 + (k + 1) % n);
        }

        static void Annulus(VertexHelper vh, Vector2 at, float r0, float r1, Color32 inner, Color32 outer, int n, Vector2 uv)
        {
            int i0 = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.uv0 = uv;
            for (int k = 0; k < n; k++)
            {
                float t = k * Mathf.PI * 2f / n;
                var d = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
                v.color = inner;
                v.position = at + d * r0;
                vh.AddVert(v);
                v.color = outer;
                v.position = at + d * r1;
                vh.AddVert(v);
            }
            for (int k = 0; k < n; k++)
            {
                int a = i0 + 2 * k, b = i0 + 2 * ((k + 1) % n);
                vh.AddTriangle(a, a + 1, b + 1);
                vh.AddTriangle(a, b + 1, b);
            }
        }

        static void Ring(VertexHelper vh, Vector2 at, float rad, float width, Color c, int n, Vector2 uv)
        {
            for (int k = 0; k < n; k++)
            {
                float t0 = k * Mathf.PI * 2f / n, t1 = (k + 1) * Mathf.PI * 2f / n;
                VoyageMapGraphic.Line(vh, at + new Vector2(Mathf.Cos(t0), Mathf.Sin(t0)) * rad,
                    at + new Vector2(Mathf.Cos(t1), Mathf.Sin(t1)) * rad, width, c, uv);
            }
        }

        /// <summary>The board's open-cluster ring: dashes of 2 px with 2.5 px gaps.</summary>
        static void DashedRing(VertexHelper vh, Vector2 at, float rad, float width, Color c, Vector2 uv)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * rad / 4.5f), 4, 28);
            float share = 2f / 4.5f;
            for (int k = 0; k < n; k++)
            {
                float t0 = k * Mathf.PI * 2f / n, t1 = t0 + share * Mathf.PI * 2f / n;
                VoyageMapGraphic.Line(vh, at + new Vector2(Mathf.Cos(t0), Mathf.Sin(t0)) * rad,
                    at + new Vector2(Mathf.Cos(t1), Mathf.Sin(t1)) * rad, width, c, uv);
            }
        }

        // ── Labels ───────────────────────────────────────────────────────────

        TMP_Text NewText(string name, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_map.transform, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (_template)
            {
                t.font = _template.font;
                t.fontSharedMaterial = _template.fontSharedMaterial;
                t.color = _template.color;
            }
            t.fontSize = size;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.pivot = new Vector2(0f, .5f);
            rt.sizeDelta = new Vector2(400f, size * 1.4f);
            return t;
        }

        static float LabelSize(bool small) => small ? VoyageMapText.Small : VoyageMapText.Landmark;

        void Labels()
        {
            _candidates.Clear();
            if (_map.Plain)
            {
                // The board's plain map: no star, cluster or landmark names, and no card.
                foreach (var t in _labels) if (t && t.gameObject.activeSelf) t.gameObject.SetActive(false);
                _backs.Clear();
                if (Selected != null) Select(null);
                return;
            }
            bool sys = SystemTier;
            foreach (var m in _stars)
            {
                var o = m.Object;
                if (!HyperSeaSky.StarLabelled(o, m.Rel, m.Alpha, sys)) continue;
                _candidates.Add(new Candidate
                {
                    Text = o.Name.Length > 0 ? o.Name : o.Desig, At = At(o.X, o.Y),
                    Offset = (float)HyperSeaSky.StarRadius(m.Rel) + 1f, Priority = -1f + Mathf.Min(.9f, m.Rel / 10f),
                    Color = o.Kind == SkyKind.TumultStar ? StarLabelTumult : o.Kind == SkyKind.Star ? StarLabelReal : StarLabelModel,
                    Small = true,
                });
            }
            double s = _map.PixelsPerUnit;
            foreach (var m in _clusters)
            {
                var c = m.Object;
                if (!HyperSeaSky.ClusterLabelled(c, m.Rel, m.Alpha)) continue;
                _candidates.Add(new Candidate
                {
                    Text = c.Label.Length > 0 ? c.Label : c.Name, At = At(c.X, c.Y),
                    Offset = (float)HyperSeaSky.ClusterRadius(c, s) + 2f, Priority = -.5f + Mathf.Min(.45f, m.Rel / 12f),
                    Color = c.Kind == SkyKind.Globular ? GlobLabel : OpenLabel, Small = true,
                });
            }
            foreach (var p in _landmarks)
            {
                bool big = p.KindId == "Harbour" || p.KindId == "Guarded";
                _candidates.Add(new Candidate
                {
                    Text = p.Name, At = At(p.X, p.Y), Offset = LandmarkSize(p) * .5f + 2f,
                    Priority = big ? 2f : p.Region == "far" ? 0f : 1f, Color = _template ? _template.color : Color.white,
                });
            }
            // Inside the cell at the view's centre, once it is big enough: its levels, named, over everything else's names.
            if (_lines != null && _cellAlpha > 0f && CellPx >= CellTagsFrom) CellTags();
            _candidates.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            var r = _map.rectTransform.rect;
            _boxes.Clear();
            _boxes.AddRange(_ladderRects);
            if (_readout && _readout.gameObject.activeSelf) _boxes.Add(TextBox(_readout));
            if (_card && _card.gameObject.activeSelf) _boxes.Add(TextBox(_card.rectTransform));
            _map.ReservedBoxes(_boxes);

            _backs.Clear();
            if (_readout && _readout.gameObject.activeSelf)
            {
                var rb = TextBox(_readout);
                rb.width = Mathf.Min(rb.width, _readout.preferredWidth) + 8f;
                rb.x -= 4f;
                _backs.Add(rb);
            }
            int used = 0;
            foreach (var cand in _candidates)
            {
                if (used >= MaxLabels) break;
                if (string.IsNullOrEmpty(cand.Text)) continue;
                float size = LabelSize(cand.Small);
                float bw = cand.Text.Length * size * .56f + 8f, bh = size * 1.35f;
                float by = cand.At.y - bh * .5f;
                float bx = cand.At.x + cand.Offset + 4f;
                if (Clash(bx, by, bw, bh, r))
                {
                    float left = cand.At.x - cand.Offset - 4f - bw;
                    if (Clash(left, by, bw, bh, r)) continue;
                    bx = left;
                }
                _boxes.Add(new Rect(bx, by, bw, bh));
                // A dark plate under the name, as the board draws it, so it reads over bright stars and thick water.
                _backs.Add(new Rect(bx, by, bw, bh));
                if (_labels.Count <= used) _labels.Add(NewText("SkyLabel", VoyageMapText.Small));
                var t = _labels[used++];
                t.gameObject.SetActive(true);
                t.fontSize = size;
                t.color = cand.Color;
                if (!ReferenceEquals(t.text, cand.Text)) t.text = cand.Text;
                ((RectTransform)t.transform).anchoredPosition = new Vector2(bx + 3f, cand.At.y) - r.center;
            }
            for (int i = used; i < _labels.Count; i++)
                if (_labels[i].gameObject.activeSelf) _labels[i].gameObject.SetActive(false);
        }

        /// <summary>The levels of the cell nearest the view's centre (cells.js <c>tags</c>): its nucleus below the
        /// nucleus, its cytoplasm between the nucleus and the top wall, its membrane under the top wall.</summary>
        void CellTags()
        {
            var L = _lines;
            var vc = _map.ViewCentre;
            var c = HyperSeaCells.NearestInSlice(vc.x, -vc.y, _sliceZ);
            if (!HyperSeaCells.TrySection(c, (0, 0, _sliceZ), (1, 0, 0), (0, 1, 0), _sectionScratch, out var sec)) return;
            bool heart = HyperSeaCells.IsHeart(c);
            double s = _map.PixelsPerUnit, R = sec.NucleusR, h = sec.Mh, ah = Math.Abs(h);
            var at = At(sec.Mu, sec.Mv);
            var top = _sectionScratch[0];
            foreach (var p in _sectionScratch) if (p.v < top.v) top = p;
            var topAt = At(top.u, top.v);
            float nucR = (float)((ah < R ? Math.Sqrt(R * R - h * h) : R) * s);
            var l = HyperSeaCells.LatticePoint(c);
            double n = HyperSea.Density(l.x, l.y, l.z);
            Tag(heart ? L.MapCellNucleusHeart : L.MapCellNucleus,
                ah < R ? L.MapCellNucleusBody : TemplateText.Fill(h > 0 ? L.MapCellNucleusAbove : L.MapCellNucleusBelow, "ly", Ly(ah)),
                new Vector2(at.x, at.y - nucR - 18f), TagNucleus, 30f);
            Tag(L.MapCellCytoplasm, TemplateText.Fill(L.MapCellCytoplasmBody, "sea", VoyageJobsView.SeaName(L, HyperSea.Sea(n)),
                "density", Density(n)), new Vector2((at.x + topAt.x) * .5f, (at.y + topAt.y) * .5f - 6f), TagCytoplasm, 20f);
            Tag(heart ? L.MapCellMembraneHeart : L.MapCellMembrane, L.MapCellMembraneBody,
                new Vector2(topAt.x, topAt.y - 22f), TagMembrane, 10f);
        }

        /// <summary>A level's tag: its name, and a line under it, centred on <paramref name="at"/>.</summary>
        void Tag(string title, string body, Vector2 at, Color color, float priority)
        {
            float big = LabelSize(false), small = LabelSize(true);
            if (!string.IsNullOrEmpty(title))
                _candidates.Add(new Candidate
                {
                    Text = title, At = new Vector2(at.x - (title.Length * big * .56f + 8f) * .5f - 4f, at.y), Offset = 0f,
                    Priority = 100f + priority, Color = color,
                });
            if (!string.IsNullOrEmpty(body))
                _candidates.Add(new Candidate
                {
                    Text = body, At = new Vector2(at.x - (body.Length * small * .56f + 8f) * .5f - 4f, at.y - big * 1.35f),
                    Offset = 0f, Priority = 99f + priority, Color = TagBody, Small = true,
                });
        }

        static string Density(double n) =>
            n < 10 ? n.ToString(n < .1 ? "0.000" : "0.00", CultureInfo.InvariantCulture) : Fmt(n);

        bool Clash(float x, float y, float w, float h, Rect r)
        {
            if (x < r.xMin + 2f || x + w > r.xMax - 2f || y < r.yMin || y + h > r.yMax) return true;
            var b = new Rect(x, y, w, h);
            foreach (var o in _boxes)
                if (o.Overlaps(b)) return true;
            return false;
        }

        Rect TextBox(TMP_Text t) => TextBox((RectTransform)t.transform);

        Rect TextBox(RectTransform rt)
        {
            var r = _map.rectTransform.rect;
            var p = rt.anchoredPosition + r.center;
            var size = rt.sizeDelta;
            return new Rect(p.x - rt.pivot.x * size.x, p.y - rt.pivot.y * size.y, size.x, size.y);
        }

        // ── The ladder and the readout ───────────────────────────────────────

        string TierName(int i) => _lines == null ? string.Empty : i switch
        {
            0 => _lines.MapTierGalaxy, 1 => _lines.MapTierArm, 2 => _lines.MapTierSector, 3 => _lines.MapTierRegion,
            4 => _lines.MapTierNeighbourhood, 5 => _lines.MapTierCell, _ => _lines.MapTierSystem,
        };

        string TierWhat(int i) => _lines == null ? string.Empty : i switch
        {
            0 => _lines.MapTierGalaxyWhat, 1 => _lines.MapTierArmWhat, 2 => _lines.MapTierSectorWhat,
            3 => _lines.MapTierRegionWhat, 4 => _lines.MapTierNeighbourhoodWhat, 5 => _lines.MapTierCellWhat,
            _ => _lines.MapTierSystemWhat,
        };

        /// <summary>The ladder's rows: the seven tiers, then the Sol and Tumult dives.</summary>
        int Rows => HyperSeaSky.Tiers.Length + 2;

        string RowText(int i) => i < HyperSeaSky.Tiers.Length ? TierName(i)
            : i == HyperSeaSky.Tiers.Length ? _lines?.MapDiveSol : _lines?.MapDiveTumult;

        void Ladder()
        {
            var r = _map.rectTransform.rect;
            _tier = HyperSeaSky.TierOf(_view.W);
            _ladderRects.Clear();
            if (_map.Plain)
            {
                // The Voyages board's map shows no ladder or readout (VoyageMapGraphic.Plain).
                foreach (var t in _ladder) if (t) t.gameObject.SetActive(false);
                if (_readout) _readout.gameObject.SetActive(false);
                return;
            }
            float size = VoyageMapText.Ladder;
            float y = r.yMax - LadderTop;
            for (int i = 0; i < Rows; i++)
            {
                if (i == HyperSeaSky.Tiers.Length) y -= LadderGap;
                string text = RowText(i);
                if (_ladder.Count <= i) _ladder.Add(NewText("Tier", VoyageMapText.Ladder));
                var t = _ladder[i];
                bool show = !string.IsNullOrEmpty(text) && y - LadderRow > r.yMin;
                t.gameObject.SetActive(show);
                if (!show) { _ladderRects.Add(new Rect(-1e6f, -1e6f, 0f, 0f)); continue; }
                bool on = i == _tier;
                t.fontSize = size;
                t.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
                t.color = ColorTrace.Retrace(ColorTrace.Ui, on ? new Color(.02f, .035f, .07f, 1f) : LadderIdle);
                if (!ReferenceEquals(t.text, text)) t.text = text;
                float w = text.Length * size * .66f + 14f;
                var box = new Rect(r.xMin + LadderLeft, y - LadderRow, w, LadderRow - 2f);
                _ladderRects.Add(box);
                ((RectTransform)t.transform).anchoredPosition = new Vector2(box.xMin + 6f, box.center.y) - r.center;
                y -= LadderRow;
            }
            Readout(r);
        }

        void Readout(Rect r)
        {
            if (_lines == null) return;
            if (!_readout)
            {
                _readout = NewText("Readout", VoyageMapText.Ladder);
                _readout.overflowMode = TextOverflowModes.Ellipsis;
                ((RectTransform)_readout.transform).pivot = new Vector2(0f, 1f);
            }
            string slab = _view.Slab
                ? TemplateText.Fill(_lines.MapReadoutSlab, "thick", Ly(_view.Zwin), "height", Height(_view.Zf))
                : string.Empty;
            string text = TemplateText.Fill(_lines.MapReadout, "tier", TierName(_tier), "what", TierWhat(_tier),
                "width", Ly(_view.W), "slab", slab);
            bool show = !string.IsNullOrEmpty(text);
            _readout.gameObject.SetActive(show);
            if (!show) return;
            _readout.fontSize = VoyageMapText.Ladder;
            _readout.color = _template ? _template.color : Color.white;
            if (_readout.text != text) _readout.text = text;
            float left = _ladderRects.Count > 0 ? _ladderRects[0].xMin : r.xMin + LadderLeft;
            float maxW = 0f;
            foreach (var b in _ladderRects) maxW = Mathf.Max(maxW, b.xMax);
            var rt = (RectTransform)_readout.transform;
            rt.sizeDelta = new Vector2(Mathf.Max(80f, r.width - (Mathf.Max(left, maxW + 8f) - r.xMin) - r.width * .32f),
                VoyageMapText.Ladder * 1.4f);
            rt.anchoredPosition = new Vector2(Mathf.Max(left, maxW + 8f), r.yMax - LadderTop) - r.center;
        }

        /// <summary>The ladder row under a tap (rect space): 0-6 a tier, 7 Sol, 8 Tumult; -1 for none.</summary>
        public int LadderAt(Vector2 at)
        {
            for (int i = 0; i < _ladderRects.Count; i++)
                if (_ladderRects[i].Contains(at)) return i;
            return -1;
        }

        /// <summary>Where a ladder row dives: its centre (map units; null keeps the view's) and width.</summary>
        public (Vector2? centre, float width) Dive(int row, float viewAspect)
        {
            if (row < HyperSeaSky.Tiers.Length)
            {
                double w = HyperSeaSky.Tiers[row].width;
                if (w > 50000) w = Math.Max(110000, 110000 / Math.Max(1e-3, viewAspect));
                return (null, (float)w);
            }
            bool sol = row == HyperSeaSky.Tiers.Length;
            return sol ? (new Vector2(0f, -(float)HyperSea.SolRadius), 12f)
                : (new Vector2((float)HyperSeaSky.TumultX, -(float)HyperSeaSky.TumultY), 12f);
        }

        // ── The card a tapped thing opens ────────────────────────────────────

        public void Select(SkyObject o)
        {
            Selected = o;
            Card();
        }

        void Card()
        {
            string text = Selected != null ? Describe(Selected) : string.Empty;
            bool show = !string.IsNullOrEmpty(text);
            if (!_card && !show) return;
            var r = _map.rectTransform.rect;
            if (!_card)
            {
                var go = new GameObject("SkyCard", typeof(RectTransform));
                go.transform.SetParent(_map.transform, false);
                _card = go.AddComponent<Image>();
                _card.color = ColorTrace.Retrace(ColorTrace.Ui, PanelBack);   // a later wear: UIThemeLive retraces it
                _card.raycastTarget = false;
                var crt = _card.rectTransform;
                crt.pivot = new Vector2(1f, 0f);
                _cardText = NewText("Text", VoyageMapText.Card);
                _cardText.transform.SetParent(go.transform, false);
                _cardText.textWrappingMode = TextWrappingModes.Normal;
                _cardText.richText = true;
                _cardText.alignment = TextAlignmentOptions.TopLeft;
                var trt = (RectTransform)_cardText.transform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.pivot = new Vector2(.5f, .5f);
                trt.offsetMin = new Vector2(8f, 6f);
                trt.offsetMax = new Vector2(-8f, -6f);
            }
            _card.gameObject.SetActive(show);
            if (!show) return;
            _cardText.fontSize = VoyageMapText.Card;
            _cardText.text = text;
            float w = Mathf.Min(460f, r.width * .5f);
            float h = _cardText.GetPreferredValues(text, w - 16f, 0f).y + 12f;
            h = Mathf.Min(h, r.height - 16f);
            var box = _card.rectTransform;
            box.anchorMin = box.anchorMax = new Vector2(.5f, .5f);
            box.sizeDelta = new Vector2(w, h);
            box.anchoredPosition = new Vector2(r.xMax - 6f, r.yMin + 30f) - r.center;
        }

        string Describe(SkyObject o)
        {
            var L = _lines;
            if (L == null) return string.Empty;
            string kind, name, body, source;
            var cat = s_Catalog;
            switch (o.Kind)
            {
                case SkyKind.Landmark:
                    kind = cat != null && cat.KindLabels.TryGetValue(o.KindId, out var k) ? k : string.Empty;
                    name = o.Name;
                    body = o.Line;
                    source = o.Tag == "A" ? L.MapSourceProposed : L.MapSourceReal;
                    break;
                case SkyKind.Cell:
                {
                    string qs = o.CellQ.ToString(CultureInfo.InvariantCulture), rs = o.CellR.ToString(CultureInfo.InvariantCulture);
                    kind = L.MapKindCell;
                    name = TemplateText.Fill(L.MapCellName, "q", qs, "r", rs);
                    double n = HyperSea.Density(o.X, o.Y, o.Z);
                    string berth = BerthOf(o.CellQ, o.CellR);
                    body = TemplateText.Fill(L.MapCellBody, "sea", VoyageJobsView.SeaName(L, HyperSea.Sea(n)),
                        "density", Density(n),
                        "danger", HyperSea.Danger(n).ToString("0.0", CultureInfo.InvariantCulture),
                        "berth", berth.Length > 0 ? TemplateText.Fill(L.MapCellBerth, "place", berth) : string.Empty);
                    source = L.MapSourceCell;
                    break;
                }
                case SkyKind.Globular:
                case SkyKind.OpenCluster:
                {
                    bool glob = o.Kind == SkyKind.Globular;
                    kind = glob ? L.MapKindGlobular : L.MapKindOpen;
                    name = o.Name;
                    if (glob) body = TemplateText.Fill(L.MapGlobularBody, "light", Light(o.M));
                    else
                    {
                        string age = o.LogAge > 0 ? TemplateText.Fill(L.MapOpenAge, "age", Age(o.LogAge)) : string.Empty;
                        body = o.Members > 0
                            ? TemplateText.Fill(L.MapOpenBody, "members", Fmt(o.Members), "age", age)
                            : TemplateText.Fill(L.MapOpenBodyYoung, "age", age);
                    }
                    string src = cat != null && cat.Sources.TryGetValue(glob ? "globs" : "ocs", out var sv) ? sv : string.Empty;
                    source = TemplateText.Fill(L.MapSourceCatalog, "source", src);
                    break;
                }
                default:
                {
                    bool real = o.Kind == SkyKind.Star, tumult = o.Kind == SkyKind.TumultStar;
                    kind = real ? L.MapKindStar : tumult ? L.MapKindTumultStar : L.MapKindModelStar;
                    name = o.Name.Length > 0 ? o.Name : o.Desig.Length > 0 ? o.Desig : real ? L.MapUnnamedStar : L.MapUnchartedStar;
                    string type = o.M < -3 && o.Ci > .8f ? L.MapTypeRedSupergiant
                        : o.M < 1.5f && o.Ci > .9f ? L.MapTypeGiant
                        : HyperSeaSky.ClassOf(o.Ci) switch
                        {
                            0 => L.MapTypeOB, 1 => L.MapTypeB, 2 => L.MapTypeA, 3 => L.MapTypeF, 4 => L.MapTypeG,
                            5 => L.MapTypeK, _ => L.MapTypeM,
                        };
                    string also = o.Desig.Length > 0 && o.Name.Length > 0 ? TemplateText.Fill(L.MapStarAlso, "desig", o.Desig) : string.Empty;
                    body = TemplateText.Fill(L.MapStarBody, "type", type, "light", Light(o.M),
                        "mag", o.M.ToString("0.0", CultureInfo.InvariantCulture), "also", also);
                    if (real)
                    {
                        string src = cat != null && cat.Sources.TryGetValue("stars", out var sv) ? sv : string.Empty;
                        source = TemplateText.Fill(L.MapSourceCatalog, "source", src);
                    }
                    else source = tumult ? L.MapSourceTumult : L.MapSourceModel;
                    break;
                }
            }
            return TemplateText.Fill(L.MapInspect, "kind", kind, "name", name, "body", body, "source", source,
                "where", Where(o));
        }

        /// <summary>The name of the charted place whose berth (<see cref="VoyageChart.BerthCells"/>) holds a cell, or empty.</summary>
        string BerthOf(int q, int r)
        {
            var content = _map.Content;
            if (content?.Locations == null) return string.Empty;
            string best = string.Empty;
            int bestSteps = VoyageChart.BerthCells + 1;
            foreach (var l in content.Locations)
            {
                if (!_map.IsDrawn(l)) continue;
                int steps = HyperSea.CellSteps(HyperSea.CellOf(l.GalaxyX, l.GalaxyY), (q, r));
                if (steps < bestSteps) { bestSteps = steps; best = l.Name ?? string.Empty; }
            }
            return best;
        }

        string Where(SkyObject o)
        {
            var L = _lines;
            double D(double x, double y, double z)
            {
                double dx = o.X - x, dy = o.Y - y, dz = o.Z - z;
                return Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }
            double fromSol = D(0, HyperSea.SolRadius, HyperSea.SolHeight);
            double fromTum = D(HyperSeaSky.TumultX, HyperSeaSky.TumultY, HyperSeaSky.TumultZ);
            var here = _map.Content?.Location(_map.Here);
            double fromHere = here != null && here.Charted ? D(here.GalaxyX, here.GalaxyY, here.GalaxyZ) : fromTum;
            double minutes = fromHere / HyperSea.CellLy * HyperSea.MinutesPerCell;
            string time = minutes < 120
                ? TemplateText.Fill(L.MapMinutes, "n", Fmt(minutes))
                : TemplateText.Fill(L.MapHours, "n", minutes / 60 < 10
                    ? (minutes / 60).ToString("0.0", CultureInfo.InvariantCulture)
                    : Fmt(minutes / 60));
            return TemplateText.Fill(L.MapWhere, "height", Height(o.Z), "sol", Ly(fromSol), "tumult", Ly(fromTum),
                "here", Ly(fromHere), "time", time);
        }

        // ── Numbers, as the board writes them ────────────────────────────────

        static string Fmt(double n) => HyperSea.JsRound(n).ToString("N0", CultureInfo.InvariantCulture);

        string Ly(double ly) => TemplateText.Fill(_lines?.MapLy, "ly",
            ly < 9.95 ? ly.ToString("0.0", CultureInfo.InvariantCulture) : Fmt(ly));

        /// <summary>A height as the map writes it (MapAbove, MapBelow or MapMidplane).</summary>
        public string HeightText(double z) => Height(z);

        string Height(double z)
        {
            double a = Math.Abs(HyperSea.JsRound(z));
            if (a < 10) return _lines?.MapMidplane ?? string.Empty;
            return TemplateText.Fill(z > 0 ? _lines?.MapAbove : _lines?.MapBelow, "ly", Ly(a));
        }

        string Light(double M)
        {
            double l = HyperSeaSky.Luminosity(M);
            string x = l >= 100 ? Fmt(l)
                : l >= 1 ? l.ToString("0.0", CultureInfo.InvariantCulture)
                : l.ToString(l < .01 ? "0.0000" : "0.00", CultureInfo.InvariantCulture);
            return TemplateText.Fill(_lines?.MapLight, "x", x);
        }

        string Age(double logAge) => logAge >= 9
            ? TemplateText.Fill(_lines?.MapAgeBillion, "n", Math.Pow(10, logAge - 9).ToString("0.0", CultureInfo.InvariantCulture))
            : TemplateText.Fill(_lines?.MapAgeMillion, "n", Fmt(Math.Pow(10, logAge - 6)));
    }
}

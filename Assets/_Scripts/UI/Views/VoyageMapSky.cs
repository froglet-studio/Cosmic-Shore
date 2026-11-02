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
        // The cells (Garrett, 2026-11-02: "at the deepest levels we should see the hexagons and you should zoom into the
        // hexagons to see cells"): their outline fades in as a cell grows from 16 to 36 px across (about the Region tier
        // into the Neighbourhood), each names itself from 150 px, and a tap on open water opens a cell's card from 40 px.
        const float CellFadeFrom = 16f, CellFadeTo = 36f, CellAlphaMax = .3f, CellNamedFrom = 150f, CellTapFrom = 40f;
        const int MaxCells = 2500;
        const float Margin = 20f;

        static HyperSeaSkyCatalog s_Catalog;
        static bool s_Loaded;

        static readonly Color32 GlobCore = new(255, 226, 170, 242), GlobMid = new(255, 206, 140, 115), GlobEdge = new(255, 200, 130, 0);
        static readonly Color OpenRing = new(170 / 255f, 205 / 255f, 1f, .85f);
        static readonly Color StarLabelReal = new(214 / 255f, 226 / 255f, 246 / 255f, .8f);
        static readonly Color StarLabelModel = new(200 / 255f, 210 / 255f, 230 / 255f, .6f);
        static readonly Color StarLabelTumult = new(1f, 214 / 255f, 150 / 255f, .82f);
        static readonly Color GlobLabel = new(1f, 220 / 255f, 170 / 255f, .85f);
        static readonly Color OpenLabel = new(180 / 255f, 210 / 255f, 1f, .85f);
        static readonly Color FarColor = new(159 / 255f, 176 / 255f, 200 / 255f, .9f);
        static readonly Color LadderIdle = new(214 / 255f, 224 / 255f, 240 / 255f, .7f);
        static readonly Color PanelBack = new(.02f, .035f, .07f, .86f);
        static readonly Color LabelBack = new(.02f, .035f, .07f, .62f);
        static readonly Color CellLine = new(.84f, .88f, .94f, 1f);
        static readonly Color CellLabel = new(.84f, .9f, 1f, .9f);
        static readonly float Sq3 = Mathf.Sqrt(3f);

        readonly VoyageMapGraphic _map;
        readonly HyperSeaSkyField _field = new();
        readonly List<HyperSeaSkyField.Mark> _stars = new(), _clusters = new();
        readonly List<SkyObject> _landmarks = new();
        readonly List<Hit> _hits = new();
        readonly List<Candidate> _candidates = new();
        readonly List<Rect> _boxes = new();
        readonly List<Rect> _backs = new();
        readonly List<(int q, int r)> _cells = new();
        float _cellAlpha;
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
                        if (l != null && l.Charted && Math.Abs(l.GalaxyX - p.X) < 2 && Math.Abs(l.GalaxyY - p.Y) < 2)
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
            _cells.Clear();
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
                s_Catalog != null && s_Catalog.Done ? s_Catalog.Landmarks : null);
            _hasView = true;
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

        /// <summary>The cells the view's outline grid draws (none while a cell is too small to see).</summary>
        void Cells(double x0, double y0, double x1, double y1)
        {
            _cells.Clear();
            float px = CellPx;
            _cellAlpha = CellAlphaMax * Mathf.Clamp01((px - CellFadeFrom) / (CellFadeTo - CellFadeFrom));
            if (_cellAlpha <= 0f) return;
            double row = HyperSea.CellLy * Sq3 / 2;
            int r0 = (int)Math.Floor(y0 / row) - 1, r1 = (int)Math.Ceiling(y1 / row) + 1;
            for (int rr = r0; rr <= r1; rr++)
            {
                int q0 = (int)Math.Floor(x0 / HyperSea.CellLy - rr / 2.0) - 1, q1 = (int)Math.Ceiling(x1 / HyperSea.CellLy - rr / 2.0) + 1;
                for (int q = q0; q <= q1; q++)
                {
                    if (_cells.Count >= MaxCells) return;
                    _cells.Add((q, rr));
                }
            }
        }

        /// <summary>The card a cell opens: its centre in the plane, at the height the view slices.</summary>
        SkyObject CellObject(int q, int r)
        {
            var (x, y) = HyperSea.CellCentre(q, r);
            return new SkyObject { Kind = SkyKind.Cell, X = x, Y = y, Z = _view.Slab ? _view.Zf : HyperSea.SolHeight, CellQ = q, CellR = r };
        }

        /// <summary>A hexagon's corner k (0-5) on the screen: pointy-top, corners at 30 + 60k degrees in the board's frame,
        /// whose y runs down the screen.</summary>
        static Vector2 Corner(Vector2 centre, float circumPx, int k)
        {
            float t = Mathf.PI / 6f + k * Mathf.PI / 3f;
            return centre + new Vector2(Mathf.Cos(t), -Mathf.Sin(t)) * circumPx;
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
                var (q, r) = HyperSea.CellOf(x, y);
                if (Selected is { Kind: SkyKind.Cell } c && c.CellQ == q && c.CellR == r) return null;
                best = CellObject(q, r);
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
            // The cells' outlines first, under everything: each cell draws three of its six edges (the 0, 60 and 120
            // degree sides), so every shared edge is drawn once.
            if (_cellAlpha > 0f)
            {
                float circ = CellPx / Sq3;
                var lineCol = new Color(CellLine.r, CellLine.g, CellLine.b, _cellAlpha);
                var cellLim = new Rect(r.xMin - circ, r.yMin - circ, r.width + 2f * circ, r.height + 2f * circ);
                foreach (var (q, cr) in _cells)
                {
                    if (vh.currentVertCount > MaxVerts / 2) break;
                    var (x, y) = HyperSea.CellCentre(q, cr);
                    var at = At(x, y);
                    if (!cellLim.Contains(at)) continue;
                    var c5 = Corner(at, circ, 5);
                    var c0 = Corner(at, circ, 0);
                    var c1 = Corner(at, circ, 1);
                    var c2 = Corner(at, circ, 2);
                    VoyageMapGraphic.Line(vh, c5, c0, 1f, lineCol, white);
                    VoyageMapGraphic.Line(vh, c0, c1, 1f, lineCol, white);
                    VoyageMapGraphic.Line(vh, c1, c2, 1f, lineCol, white);
                }
            }
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
                    Disc(vh, at, rpx * 1.4f * .45f, Fade(GlobCore, a), Fade(GlobMid, a), 12, white);
                    Annulus(vh, at, rpx * 1.4f * .45f, rpx * 1.4f, Fade(GlobMid, a), Fade(GlobEdge, a), 12, white);
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
                var at = At(Selected.X, Selected.Y);
                float circ = CellPx / Sq3;
                for (int k = 0; k < 6; k++)
                    VoyageMapGraphic.Line(vh, Corner(at, circ, k), Corner(at, circ, (k + 1) % 6), 2f, Color.white, white);
            }
            else if (Selected != null)
            {
                var at = At(Selected.X, Selected.Y);
                float rad = Selected.IsCluster ? (float)HyperSeaSky.ClusterRadius(Selected, s) + 5f
                    : Selected.Kind == SkyKind.Landmark ? LandmarkSize(Selected) * .5f + 5f : 6f + 2f;
                Ring(vh, at, rad, 1f, Color.white, 24, white);
            }
            // The dark plates under the names and the readout, so they read over bright stars and thick water.
            foreach (var b in _backs)
                VoyageMapGraphic.Quad(vh, new Vector2(b.xMin, b.yMin), new Vector2(b.xMin, b.yMax), new Vector2(b.xMax, b.yMax),
                    new Vector2(b.xMax, b.yMin), LabelBack, white, white, white, white);
            // The ladder's chips, under their text.
            for (int i = 0; i < _ladderRects.Count; i++)
            {
                var b = _ladderRects[i];
                bool on = i == _tier;
                VoyageMapGraphic.Quad(vh, new Vector2(b.xMin, b.yMin), new Vector2(b.xMin, b.yMax), new Vector2(b.xMax, b.yMax),
                    new Vector2(b.xMax, b.yMin), on ? new Color(.95f, .97f, 1f, .92f) : new Color(.04f, .067f, .125f, .78f),
                    white, white, white, white);
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
            // A cell big enough to read names itself, under everything else's names.
            if (_lines != null && _cellAlpha > 0f && CellPx >= CellNamedFrom)
            {
                float px = CellPx;
                foreach (var (q, cr) in _cells)
                {
                    var (x, y) = HyperSea.CellCentre(q, cr);
                    string text = TemplateText.Fill(_lines.MapCellLabel, "sea",
                        VoyageJobsView.SeaName(_lines, HyperSea.Sea(HyperSea.Density(x, y))),
                        "q", q.ToString(CultureInfo.InvariantCulture), "r", cr.ToString(CultureInfo.InvariantCulture));
                    // Near the top of the hexagon, starting left of its centre, clear of a star at the centre.
                    _candidates.Add(new Candidate
                    {
                        Text = text, At = At(x, y) + new Vector2(-px * .28f, px * .3f), Offset = 0f, Priority = -3f,
                        Color = CellLabel, Small = true,
                    });
                }
            }
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
                t.color = on ? new Color(.02f, .035f, .07f, 1f) : LadderIdle;
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
                _card.color = PanelBack;
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
                    double n = HyperSea.Density(o.X, o.Y);
                    string berth = BerthOf(o.CellQ, o.CellR);
                    body = TemplateText.Fill(L.MapCellBody, "sea", VoyageJobsView.SeaName(L, HyperSea.Sea(n)),
                        "density", n < 10 ? n.ToString(n < .1 ? "0.000" : "0.00", CultureInfo.InvariantCulture) : Fmt(n),
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
                if (l == null || !l.Charted) continue;
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

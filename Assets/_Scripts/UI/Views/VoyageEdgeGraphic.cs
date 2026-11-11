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
    /// The galaxy map's edge view (Garrett's Voyage window sketch, 2026-11-01: "map top" over "edge view"): the disk seen
    /// edge-on under the map, at the map's own horizontal scale and the same scale upward, so the 1,000 ly thickness is
    /// drawn true, as the Voyage Board's side view does (Docs/Voyages/VoyageBoard/voyage-board.html, "Side view"). The sea
    /// is a slice through the place in focus (the open course's end, else where the squad is); the places and the course
    /// stand at their heights; a gauge on the left shows the whole disk and the window this strip shows of it. It follows
    /// the map as the player pans and zooms (<see cref="VoyageMapGraphic.ViewChanged"/>). Only a charted map has one.
    ///
    /// <para>The sea is cut along the map's bottom edge (Garrett, 2026-11-07: "the bottom edge of the plane [is] the slice
    /// for the thickness so they always have a nice lock up"). Once the map shows the cells
    /// (<see cref="VoyageMapSky.CellZoom"/>), the strip shows the same cells cut upright, with their nuclei, and the map's
    /// slice as a Lime bar: drag it, or scroll over the strip, and the map slices the cells at another height; a tap on its
    /// handle puts it back (Docs/Voyages/VOYAGE_BOARD.md §4c).</para>
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class VoyageEdgeGraphic : MaskableGraphic, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler,
        IPointerClickHandler
    {
        const int TexW = 160, TexH = 24;
        // The texture: the sea's rows, the nucleus sprites over them, then one white row.
        const int SpriteRow = TexH, TexRows = TexH + VoyageCellDraw.SpriteRows + 1;
        const float BarReach = 14f, HandleW = 66f, HandleH = 18f, WheelLy = 2f;
        const string SliceLabelName = "Slice";
        const int MaxSideCells = 1500, SideCellVerts = 30000;
        const float GaugeWidth = 44f;
        const float GaugeRange = 700f;          // the gauge shows heights from -700 to +700 ly
        const float FitsMargin = 120f;
        const float RepaintEvery = 0.12f;
        const float PlaceSize = 7f, HereSize = 12f, CourseWidth = 2.5f, RuleWidth = 1f;
        const string CaptionName = "Caption";
        const int MaxSideStars = 3000;

        static readonly Color SeaTint = new(1f, 1f, 1f, 0.92f);
        static readonly Color Midplane = new(0.84f, 0.88f, 0.94f, 0.3f);
        static readonly Color DiskFace = new(1f, 0.91f, 0.66f, 0.55f);
        static readonly Color GaugeBack = new(0.02f, 0.035f, 0.07f, 0.8f);
        static readonly Color GaugeDisk = new(0.16f, 0.41f, 0.72f, 0.35f);
        static readonly Color GaugeWindow = new(0.89f, 0.92f, 0.96f, 0.9f);
        static readonly Color LandmarkTint = new(0.84f, 0.88f, 0.94f, 0.75f);

        VoyageMapGraphic _map;
        TMP_Text _caption, _sliceLabel, _template;
        string _captionText;
        // One window, one strip: the slice's texture is shared, as the map's sea is.
        static Texture2D _tex;
        static Color32[] _px;
        bool _dirty;
        // The slice bar: where it is drawn, and a drag in flight (the strip's window stays put under the pointer and
        // scrolls only when the bar reaches its edge). A drag that does not start on the bar goes to the strip's parent.
        float _barY = float.NaN;
        bool _dragging, _forwarding;
        double _dragZ0, _dragZc;
        float _dragY0;
        readonly List<(double x, double y, double z)> _nuclei = new();
        readonly List<(double u, double v)> _section = new();
        readonly List<Vector2> _poly = new();
        readonly HashSet<long> _walls = new();
        float _lastPaint = float.NegativeInfinity;
        // The colour set the strip was last painted in (ColorTrace.Version): another one worn paints it again.
        int _colorVersion;

        public override Texture mainTexture => _tex ? _tex : s_WhiteTexture;

        /// <summary>Follow <paramref name="map"/>'s view; <paramref name="caption"/> is the authored line under the strip
        /// (empty shows nothing), <paramref name="labelTemplate"/> the text it copies its look from.</summary>
        public void Follow(VoyageMapGraphic map, string caption, TMP_Text labelTemplate)
        {
            if (_map != map)
            {
                if (_map) _map.ViewChanged -= OnViewChanged;
                _map = map;
                if (_map) _map.ViewChanged += OnViewChanged;
            }
            raycastTarget = false;
            _template = labelTemplate;
            _captionText = caption;
            Caption(caption, labelTemplate);
            OnViewChanged();
        }

        void OnViewChanged()
        {
            // Not by overriding OnDestroy, whose access differs between the shipped uGUI and the compile gate's
            // publicized reference (see VoyageMapGraphic.LateUpdate): a strip that is gone lets go of the map here.
            if (!this)
            {
                if (_map) _map.ViewChanged -= OnViewChanged;
                return;
            }
            _dirty = true;
            // The strip takes pointers only while it shows the cells and their slice bar.
            raycastTarget = CellZoom;
            Caption(CellZoom && _map.Lines != null
                ? TemplateText.Fill(_map.Lines.EdgeCaptionCells, "height", _map.Sky.HeightText(_map.Sky.SliceZ))
                : _captionText, _template);
            SetVerticesDirty();
        }

        bool CellZoom => _map && _map.Sky != null && _map.Sky.CellZoom;

        void LateUpdate()
        {
            // Another colour set worn: the strip is painted again in it (the mesh retraces its colours as it is built).
            if (_colorVersion != ColorTrace.Version)
            {
                _colorVersion = ColorTrace.Version;
                _dirty = true;
            }
            if (!_dirty || Time.unscaledTime - _lastPaint < RepaintEvery) return;
            _dirty = false;
            Paint();
            SetVerticesDirty();
            SetMaterialDirty();
        }

        // ── The frame: the map's x, true-scale heights ───────────────────────

        bool Ready => _map && _map.Chart != null && _map.Content != null && _map.Style;

        /// <summary>The place in focus: the course's end, else where the squad is.</summary>
        VoyageLocation Focus()
        {
            var route = _map.Route;
            string id = route != null && route.Count > 0 ? route[route.Count - 1] : _map.Here;
            return _map.Content.Location(id);
        }

        /// <summary>The height at the strip's middle: the midplane when the whole disk fits, else the focus's height;
        /// with the cells on show, near the focus's layer of nuclei but always with the slice bar in view.</summary>
        float CentreZ(float scale)
        {
            float h = rectTransform.rect.height;
            if (CellZoom)
            {
                if (_dragging) return (float)_dragZc;
                double zs = _map.Sky.SliceZ, half = Math.Max(0, (h * .5 - 30) / scale);
                return (float)Math.Max(zs - half, Math.Min(zs + half, _map.Sky.SliceBase));
            }
            if ((2f * (float)HyperSea.DiskHalf + FitsMargin) * scale <= h - 24f) return 0f;
            var f = Focus();
            return f != null ? f.GalaxyZ : 0f;
        }

        /// <summary>The board's y along the map's bottom edge, where the strip cuts the sea.</summary>
        double SliceY(float scale) => -_map.ViewCentre.y + _map.rectTransform.rect.height * .5 / scale;

        Vector2 ToRect(float galaxyX, float z, float scale, float zc)
        {
            var r = rectTransform.rect;
            return new Vector2(r.center.x + _map.OffsetX(galaxyX), r.center.y + (z - zc) * scale);
        }

        // ── The sea slice ────────────────────────────────────────────────────

        void Paint()
        {
            _lastPaint = Time.unscaledTime;
            if (!Ready) return;
            var r = rectTransform.rect;
            if (r.width < 1f || r.height < 1f) return;
            if (!_tex || _px == null)
            {
                if (!_tex)
                    _tex = new Texture2D(TexW, TexRows, TextureFormat.RGBA32, false)
                    {
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear,
                        hideFlags = HideFlags.HideAndDontSave,
                        name = "VoyageEdge",
                    };
                _px = new Color32[TexW * TexRows];
                VoyageCellDraw.PaintSprites(_px, TexW, 0, SpriteRow);
                for (int i = 0; i < TexW; i++) _px[(TexRows - 1) * TexW + i] = new Color32(255, 255, 255, 255);
            }
            float s = _map.PixelsPerUnit, zc = CentreZ(s);
            float cx = _map.ViewCentre.x;
            double sliceY = SliceY(s);
            var px = _px;
            for (int j = 0; j < TexH; j++)
            {
                double z = zc + (r.height * ((j + 0.5) / TexH - 0.5)) / s;
                for (int i = 0; i < TexW; i++)
                {
                    double x = cx + r.width * ((i + 0.5) / TexW - 0.5) / s;
                    var (cr, cg, cb) = HyperSea.SeaColor(HyperSea.Density(x, sliceY, z));
                    px[j * TexW + i] = new Color32((byte)cr, (byte)cg, (byte)cb, 255);
                }
            }
            _tex.SetPixels32(px);
            _tex.Apply(false);
        }

        // ── The mesh ─────────────────────────────────────────────────────────

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!Ready || !_tex) return;
            var r = rectTransform.rect;
            var white = new Vector2(0.5f / TexW, (TexRows - 0.5f) / TexRows);
            var style = _map.Style;
            float s = _map.PixelsPerUnit, zc = CentreZ(s);
            _barY = float.NaN;

            float seaV = TexH / (float)TexRows;
            VoyageMapGraphic.Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax),
                new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin), SeaTint, new Vector2(0f, 0f),
                new Vector2(0f, seaV), new Vector2(1f, seaV), new Vector2(1f, 0f));
            if (CellZoom) Cells(vh, r, s, zc, white);

            // The disk's faces at +-500 ly and the midplane.
            var diskFace = ColorTrace.Retrace(ColorTrace.Ui, DiskFace);
            Rule(vh, r, r.center.y + ((float)HyperSea.DiskHalf - zc) * s, diskFace, white);
            Rule(vh, r, r.center.y + (-(float)HyperSea.DiskHalf - zc) * s, diskFace, white);
            Rule(vh, r, r.center.y - zc * s, ColorTrace.Retrace(ColorTrace.Ui, Midplane), white);

            // The deep zoom's stars and landmarks at their heights, as the board's side view stands them.
            var sky = _map.Sky;
            if (sky != null)
            {
                int n = 0;
                foreach (var m in sky.Stars)
                {
                    if (m.Alpha <= .2f) continue;
                    if (++n > MaxSideStars) break;
                    var o = m.Object;
                    var at = ToRect((float)o.X, (float)o.Z, s, zc);
                    if (!r.Contains(at)) continue;
                    float rad = Mathf.Max(.5f, (float)HyperSeaSky.StarRadius(m.Rel) * .75f);
                    var col = new Color32(o.R, o.G, o.B, (byte)Mathf.RoundToInt(255f * m.Alpha * .75f));
                    VoyageMapGraphic.Quad(vh, at + new Vector2(-rad, -rad), at + new Vector2(-rad, rad),
                        at + new Vector2(rad, rad), at + new Vector2(rad, -rad), col, white, white, white, white);
                }
                var landmarkTint = ColorTrace.Retrace(ColorTrace.Ui, LandmarkTint);
                foreach (var p in sky.Landmarks)
                {
                    var at = ToRect((float)p.X, (float)p.Z, s, zc);
                    if (r.Contains(at)) VoyageMapGraphic.Diamond(vh, at, PlaceSize * .7f, landmarkTint, white);
                }
            }

            var content = _map.Content;
            foreach (var l in content.Locations)
            {
                if (!_map.IsDrawn(l)) continue;
                var at = ToRect(l.GalaxyX, l.GalaxyZ, s, zc);
                if (!r.Contains(at)) continue;
                VoyageMapGraphic.Diamond(vh, at, PlaceSize, VoyageMapGraphic.MarkerColor(style, l), white);
            }

            // The course, as an elevation.
            var course = _map.Course;
            if (course != null)
                for (int i = 1; i < course.Points.Count && i < course.Heights.Count; i++)
                    VoyageMapGraphic.Line(vh,
                        ToRect((float)course.Points[i - 1].x, (float)course.Heights[i - 1], s, zc),
                        ToRect((float)course.Points[i].x, (float)course.Heights[i], s, zc),
                        CourseWidth, style.RouteColor, white);

            var here = content.Location(_map.Here);
            if (here != null && here.Charted)
                VoyageMapGraphic.Diamond(vh, ToRect(here.GalaxyX, here.GalaxyZ, s, zc), HereSize, style.HereColor, white);

            Gauge(vh, r, s, zc, here, style, white);
            if (CellZoom) Bar(vh, r, s, zc, white);
            else if (_sliceLabel) _sliceLabel.gameObject.SetActive(false);
        }

        // ── The cells, cut upright, and the map's slice ───────────────────────

        /// <summary>The cells the strip's sheet cuts: their seas, walls and nuclei, as on the map.</summary>
        void Cells(VertexHelper vh, Rect r, float s, float zc, Vector2 white)
        {
            double ys = SliceY(s), x0 = _map.ViewCentre.x - r.width * .5 / s, x1 = _map.ViewCentre.x + r.width * .5 / s;
            double z0 = zc - r.height * .5 / s, z1 = zc + r.height * .5 / s;
            HyperSeaCells.NucleiInUpright(ys, x0, x1, z0, z1, _nuclei, MaxSideCells);
            float px = (float)(HyperSeaCells.A * s);
            float alpha = Mathf.Clamp01((px - VoyageMapSky.CellsFromPx) / 22f);
            float fill = Mathf.Clamp01((px - 60f) / 30f);
            bool inner = px > 90f;
            float nucA = (float)HyperSeaCells.NucleusAlpha(px);
            double R = HyperSeaCells.NucleusRadius;
            int budget = vh.currentVertCount + SideCellVerts;
            _walls.Clear();
            for (int pass = 0; pass < 3; pass++)
                foreach (var c in _nuclei)
                {
                    if (vh.currentVertCount > budget) return;
                    if (!HyperSeaCells.TrySection(c, (0, ys, 0), (1, 0, 0), (0, 0, 1), _section, out var sec)) continue;
                    var at = ToRect((float)c.x, (float)c.z, s, zc);
                    if (pass == 2)
                    {
                        double h = Math.Abs(sec.H);
                        if (h >= R || nucA <= 0f) continue;
                        float rad = (float)(Math.Sqrt(R * R - h * h) * s);
                        VoyageCellDraw.NucleusQuad(vh, at, rad, nucA, VoyageCellDraw.SpriteUv(rad, TexW, TexRows, 0, SpriteRow));
                        continue;
                    }
                    _poly.Clear();
                    foreach (var p in _section) _poly.Add(ToRect((float)p.u, (float)p.v, s, zc));
                    if (pass == 0)
                    {
                        if (fill <= 0f) continue;
                        var (cr, cg, cb) = HyperSea.SeaColor(HyperSea.Density(c.x, c.y, c.z));
                        VoyageCellDraw.Cell(vh, _poly, at, new Color((float)cr / 255f, (float)cg / 255f, (float)cb / 255f, 1f),
                            px, fill * alpha, inner, white);
                    }
                    else VoyageCellDraw.Walls(vh, _poly, px, alpha, _walls, white);
                }
        }

        /// <summary>The map's slice: a Lime bar across the strip with a handle at its right end.</summary>
        void Bar(VertexHelper vh, Rect r, float s, float zc, Vector2 white)
        {
            float y = r.center.y + ((float)_map.Sky.SliceZ - zc) * s;
            if (y < r.yMin - 2f || y > r.yMax + 2f)
            {
                if (_sliceLabel) _sliceLabel.gameObject.SetActive(false);
                return;
            }
            _barY = y;
            var a = new Vector2(r.xMin + GaugeWidth, y);
            var b = new Vector2(r.xMax, y);
            VoyageMapGraphic.Line(vh, a, b, 4f, ColorTrace.Retrace(ColorTrace.Ui, new Color(.02f, .035f, .07f, .7f)), white);
            VoyageMapGraphic.Line(vh, a, b, 1.5f, VoyageMapSky.SliceLime, white);
            var handle = Handle(r, y);
            Box(vh, handle, VoyageMapSky.SliceLime, white);
            SliceLabel(handle);
        }

        Rect Handle(Rect r, float y) => new(r.xMax - HandleW - 8f, y - HandleH * .5f, HandleW, HandleH);

        void SliceLabel(Rect handle)
        {
            string text = _map.Lines?.EdgeSlice;
            bool show = !string.IsNullOrEmpty(text);
            if (!_sliceLabel && !show) return;
            if (!_sliceLabel)
            {
                var go = new GameObject(SliceLabelName, typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var t = go.AddComponent<TextMeshProUGUI>();
                if (_template)
                {
                    t.font = _template.font;
                    t.fontSharedMaterial = _template.fontSharedMaterial;
                }
                t.fontSize = VoyageMapText.Ladder * .8f;
                t.fontStyle = FontStyles.Bold;
                t.color = ColorTrace.Retrace(ColorTrace.Ui, new Color(.02f, .035f, .07f, 1f));
                t.alignment = TextAlignmentOptions.Center;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.raycastTarget = false;
                _sliceLabel = t;
            }
            _sliceLabel.gameObject.SetActive(show);
            if (!show) return;
            if (_sliceLabel.text != text) _sliceLabel.text = text;
            var rt = (RectTransform)_sliceLabel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = handle.size;
            rt.anchoredPosition = handle.center - rectTransform.rect.center;
        }

        // ── Dragging the slice ───────────────────────────────────────────────

        Vector2 Local(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, e.position, e.pressEventCamera, out var p);
            return p;
        }

        bool OnBar(Vector2 at) => !float.IsNaN(_barY) && (Mathf.Abs(at.y - _barY) < BarReach ||
            Handle(rectTransform.rect, _barY).Contains(at));

        public void OnBeginDrag(PointerEventData e)
        {
            var at = Local(e);
            if (!CellZoom || !OnBar(at))
            {
                // Not the bar: the drag belongs to whatever holds the strip (a scrolling page, say).
                _forwarding = transform.parent &&
                              ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, e, ExecuteEvents.beginDragHandler);
                return;
            }
            _dragging = true;
            _dragZ0 = _map.Sky.SliceZ;
            _dragY0 = at.y;
            _dragZc = CentreZ(_map.PixelsPerUnit);
        }

        public void OnDrag(PointerEventData e)
        {
            if (_forwarding)
            {
                ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, e, ExecuteEvents.dragHandler);
                return;
            }
            if (!_dragging || !CellZoom) return;
            float s = _map.PixelsPerUnit;
            double limit = HyperSeaCells.SliceLimit;
            double z = Math.Max(-limit, Math.Min(limit, _dragZ0 + (Local(e).y - _dragY0) / s));
            double half = Math.Max(0, (rectTransform.rect.height * .5 - 30) / s);
            if (z > _dragZc + half) _dragZc = z - half;
            else if (z < _dragZc - half) _dragZc = z + half;
            _map.SetSliceOffset(z - _map.Sky.SliceBase);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (_forwarding)
            {
                ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, e, ExecuteEvents.endDragHandler);
                _forwarding = false;
                return;
            }
            if (!_dragging) return;
            _dragging = false;
            _map.SetSliceOffset(Math.Round(_map.SliceOffset * 10) / 10);
            OnViewChanged();
        }

        public void OnScroll(PointerEventData e)
        {
            if (!CellZoom || Mathf.Approximately(e.scrollDelta.y, 0f))
            {
                if (transform.parent) ExecuteEvents.ExecuteHierarchy(transform.parent.gameObject, e, ExecuteEvents.scrollHandler);
                return;
            }
            _map.SetSliceOffset(_map.SliceOffset + Math.Sign(e.scrollDelta.y) * WheelLy);
        }

        /// <summary>A tap on the handle puts the slice back on the focus's layer of nuclei.</summary>
        public void OnPointerClick(PointerEventData e)
        {
            if (e.dragging || !CellZoom || float.IsNaN(_barY)) return;
            if (Handle(rectTransform.rect, _barY).Contains(Local(e))) _map.SetSliceOffset(0);
        }

        static void Rule(VertexHelper vh, Rect r, float y, Color c, Vector2 uv)
        {
            if (y < r.yMin || y > r.yMax) return;
            VoyageMapGraphic.Line(vh, new Vector2(r.xMin + GaugeWidth, y), new Vector2(r.xMax, y), RuleWidth, c, uv);
        }

        /// <summary>The depth gauge: the whole disk, the window this strip shows of it, and where the squad is.</summary>
        void Gauge(VertexHelper vh, Rect r, float s, float zc, VoyageLocation here, VoyageToyDefinitionSO style,
            Vector2 uv)
        {
            Box(vh, new Rect(r.xMin, r.yMin, GaugeWidth, r.height), ColorTrace.Retrace(ColorTrace.Ui, GaugeBack), uv);
            float top = r.yMax - 8f, bottom = r.yMin + 8f, x = r.xMin + GaugeWidth * 0.4f;
            float Gz(float z) => Mathf.Lerp(bottom, top, Mathf.InverseLerp(-GaugeRange, GaugeRange, z));
            float disk = (float)HyperSea.DiskHalf;
            Box(vh, Rect.MinMaxRect(x - 4f, Gz(-disk), x + 4f, Gz(disk)), ColorTrace.Retrace(ColorTrace.Ui, GaugeDisk), uv);
            float zTop = zc + r.height * 0.5f / s, zBottom = zc - r.height * 0.5f / s;
            float wt = Gz(Mathf.Min(GaugeRange, zTop)), wb = Gz(Mathf.Max(-GaugeRange, zBottom));
            if (wt - wb < top - bottom - 2f)
                Outline(vh, Rect.MinMaxRect(x - 8f, wb, x + 8f, Mathf.Max(wb + 2f, wt)), ColorTrace.Retrace(ColorTrace.Ui, GaugeWindow), uv);
            if (here != null && here.Charted)
                VoyageMapGraphic.Diamond(vh, new Vector2(x + 12f, Gz(here.GalaxyZ)), 8f, style.HereColor, uv);
        }

        static void Box(VertexHelper vh, Rect b, Color c, Vector2 uv) =>
            VoyageMapGraphic.Quad(vh, new Vector2(b.xMin, b.yMin), new Vector2(b.xMin, b.yMax), new Vector2(b.xMax, b.yMax),
                new Vector2(b.xMax, b.yMin), c, uv, uv, uv, uv);

        static void Outline(VertexHelper vh, Rect b, Color c, Vector2 uv)
        {
            Box(vh, Rect.MinMaxRect(b.xMin, b.yMin, b.xMax, b.yMin + 1f), c, uv);
            Box(vh, Rect.MinMaxRect(b.xMin, b.yMax - 1f, b.xMax, b.yMax), c, uv);
            Box(vh, Rect.MinMaxRect(b.xMin, b.yMin, b.xMin + 1f, b.yMax), c, uv);
            Box(vh, Rect.MinMaxRect(b.xMax - 1f, b.yMin, b.xMax, b.yMax), c, uv);
        }

        // ── The caption ──────────────────────────────────────────────────────

        void Caption(string caption, TMP_Text template)
        {
            if (!_caption)
            {
                var existing = transform.Find(CaptionName);
                if (existing) existing.TryGetComponent(out _caption);
            }
            bool show = !string.IsNullOrEmpty(caption);
            if (!_caption && !show) return;
            if (!_caption)
            {
                var go = new GameObject(CaptionName, typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var t = go.AddComponent<TextMeshProUGUI>();
                if (template)
                {
                    t.font = template.font;
                    t.fontSharedMaterial = template.fontSharedMaterial;
                    t.fontSize = VoyageMapText.Caption;
                    t.color = template.color;
                }
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.overflowMode = TextOverflowModes.Ellipsis;
                t.alignment = TextAlignmentOptions.TopLeft;
                t.raycastTarget = false;
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(-GaugeWidth - 12f, VoyageMapText.Caption * 1.4f);
                rt.anchoredPosition = new Vector2(GaugeWidth + 6f, -4f);
                _caption = t;
            }
            _caption.gameObject.SetActive(show);
            if (show && _caption.text != caption) _caption.text = caption;
        }
    }
}

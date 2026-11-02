using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
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
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class VoyageEdgeGraphic : MaskableGraphic
    {
        const int TexW = 160, TexH = 24;
        const float GaugeWidth = 44f;
        const float GaugeRange = 700f;          // the gauge shows heights from -700 to +700 ly
        const float FitsMargin = 120f;
        const float RepaintEvery = 0.12f;
        const float PlaceSize = 7f, HereSize = 12f, CourseWidth = 2.5f, RuleWidth = 1f;
        const string CaptionName = "Caption";

        static readonly Color SeaTint = new(1f, 1f, 1f, 0.92f);
        static readonly Color Midplane = new(0.84f, 0.88f, 0.94f, 0.3f);
        static readonly Color DiskFace = new(1f, 0.91f, 0.66f, 0.55f);
        static readonly Color GaugeBack = new(0.02f, 0.035f, 0.07f, 0.8f);
        static readonly Color GaugeDisk = new(0.16f, 0.41f, 0.72f, 0.35f);
        static readonly Color GaugeWindow = new(0.89f, 0.92f, 0.96f, 0.9f);

        VoyageMapGraphic _map;
        TMP_Text _caption;
        // One window, one strip: the slice's texture is shared, as the map's sea is.
        static Texture2D _tex;
        bool _dirty;
        float _lastPaint = float.NegativeInfinity;

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
            SetVerticesDirty();
        }

        void LateUpdate()
        {
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

        /// <summary>The height at the strip's middle: the midplane when the whole disk fits, else the focus's height.</summary>
        float CentreZ(float scale)
        {
            float h = rectTransform.rect.height;
            if ((2f * (float)HyperSea.DiskHalf + FitsMargin) * scale <= h - 24f) return 0f;
            var f = Focus();
            return f != null ? f.GalaxyZ : 0f;
        }

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
            if (!_tex)
                _tex = new Texture2D(TexW, TexH + 1, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                    name = "VoyageEdge",
                };
            float s = _map.PixelsPerUnit, zc = CentreZ(s);
            float cx = _map.ViewCentre.x;
            var focus = Focus();
            double sliceY = focus != null ? focus.GalaxyY : -_map.ViewCentre.y;
            var px = new Color32[TexW * (TexH + 1)];
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
            for (int i = 0; i < TexW; i++) px[TexH * TexW + i] = new Color32(255, 255, 255, 255);
            _tex.SetPixels32(px);
            _tex.Apply(false);
        }

        // ── The mesh ─────────────────────────────────────────────────────────

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!Ready || !_tex) return;
            var r = rectTransform.rect;
            var white = new Vector2(0.5f / TexW, (TexH + 0.5f) / (TexH + 1));
            var style = _map.Style;
            float s = _map.PixelsPerUnit, zc = CentreZ(s);

            VoyageMapGraphic.Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax),
                new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin), SeaTint, new Vector2(0f, 0f),
                new Vector2(0f, TexH / (TexH + 1f)), new Vector2(1f, TexH / (TexH + 1f)), new Vector2(1f, 0f));

            // The disk's faces at +-500 ly and the midplane.
            Rule(vh, r, r.center.y + ((float)HyperSea.DiskHalf - zc) * s, DiskFace, white);
            Rule(vh, r, r.center.y + (-(float)HyperSea.DiskHalf - zc) * s, DiskFace, white);
            Rule(vh, r, r.center.y - zc * s, Midplane, white);

            var content = _map.Content;
            foreach (var l in content.Locations)
            {
                if (l == null || !l.Charted) continue;
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
            Box(vh, new Rect(r.xMin, r.yMin, GaugeWidth, r.height), GaugeBack, uv);
            float top = r.yMax - 8f, bottom = r.yMin + 8f, x = r.xMin + GaugeWidth * 0.4f;
            float Gz(float z) => Mathf.Lerp(bottom, top, Mathf.InverseLerp(-GaugeRange, GaugeRange, z));
            float disk = (float)HyperSea.DiskHalf;
            Box(vh, Rect.MinMaxRect(x - 4f, Gz(-disk), x + 4f, Gz(disk)), GaugeDisk, uv);
            float zTop = zc + r.height * 0.5f / s, zBottom = zc - r.height * 0.5f / s;
            float wt = Gz(Mathf.Min(GaugeRange, zTop)), wb = Gz(Mathf.Max(-GaugeRange, zBottom));
            if (wt - wb < top - bottom - 2f) Outline(vh, Rect.MinMaxRect(x - 8f, wb, x + 8f, Mathf.Max(wb + 2f, wt)), GaugeWindow, uv);
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
                    t.fontSize = template.fontSize * 0.55f;
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
                rt.sizeDelta = new Vector2(-GaugeWidth - 12f, 20f);
                rt.anchoredPosition = new Vector2(GaugeWidth + 6f, -4f);
                _caption = t;
            }
            _caption.gameObject.SetActive(show);
            if (show) _caption.text = caption;
        }
    }
}

using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The galaxy map in the app shell (Garrett, 2026-10-28: "the galaxy map should be in the app shell not the game"):
    /// the HyperSea's locations and lanes seen from above, where the squad is, and the course a job sails. Drawn flat, as
    /// the UI's look is, into one mesh; each location's name is a label whose text is the content's own. Colours are the
    /// voyage harbour's (<see cref="VoyageToyDefinitionSO"/>).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class VoyageMapGraphic : MaskableGraphic
    {
        const float Pad = 0.12f;
        const float LaneWidth = 2f;
        const float RouteWidth = 5f;
        const float MarkerSize = 9f;
        const float HereSize = 16f;

        VoyageContent _content;
        VoyageToyDefinitionSO _style;
        string _here;
        readonly List<string> _route = new();
        readonly List<TMP_Text> _labels = new();
        TMP_Text _labelTemplate;
        float _minX, _maxX, _minZ, _maxZ;
        Vector2 _placedFor;

        /// <summary>The text whose font, size and colour every location's label copies.</summary>
        public void SetLabelTemplate(TMP_Text template) => _labelTemplate = template;

        /// <summary>Draw <paramref name="content"/>'s map with the squad at <paramref name="here"/> and
        /// <paramref name="route"/> (location ids, in order) as the course. Null route: no course.</summary>
        public void Show(VoyageContent content, VoyageToyDefinitionSO style, string here, IReadOnlyList<string> route)
        {
            _content = content;
            _style = style;
            _here = here;
            _route.Clear();
            if (route != null) _route.AddRange(route);
            raycastTarget = false;
            Bounds();
            Label();
            SetVerticesDirty();
        }

        void Bounds()
        {
            _minX = _minZ = float.PositiveInfinity;
            _maxX = _maxZ = float.NegativeInfinity;
            if (_content?.Locations == null) return;
            foreach (var l in _content.Locations)
            {
                if (l == null) continue;
                _minX = Mathf.Min(_minX, l.ChartX);
                _maxX = Mathf.Max(_maxX, l.ChartX);
                _minZ = Mathf.Min(_minZ, l.ChartZ);
                _maxZ = Mathf.Max(_maxZ, l.ChartZ);
            }
        }

        Vector2 At(VoyageLocation l)
        {
            var r = rectTransform.rect;
            float w = Mathf.Max(1e-3f, _maxX - _minX), h = Mathf.Max(1e-3f, _maxZ - _minZ);
            float scale = Mathf.Min(r.width * (1f - 2f * Pad) / w, r.height * (1f - 2f * Pad) / h);
            var centre = new Vector2((_minX + _maxX) * 0.5f, (_minZ + _maxZ) * 0.5f);
            return r.center + new Vector2(l.ChartX - centre.x, l.ChartZ - centre.y) * scale;
        }

        // The labels follow the map's size. Watched here rather than by overriding OnRectTransformDimensionsChange, whose
        // access differs between the shipped uGUI (protected) and the compile gate's publicized reference (public).
        void LateUpdate()
        {
            var size = rectTransform.rect.size;
            if (size == _placedFor) return;
            _placedFor = size;
            PlaceLabels();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_content?.Locations == null || _style == null || float.IsInfinity(_minX)) return;

            foreach (var lane in _content.Lanes ?? new List<VoyageLane>())
            {
                var a = _content.Location(lane?.From);
                var b = _content.Location(lane?.To);
                if (a != null && b != null) Line(vh, At(a), At(b), LaneWidth, _style.LaneColor);
            }
            for (int i = 1; i < _route.Count; i++)
            {
                var a = _content.Location(_route[i - 1]);
                var b = _content.Location(_route[i]);
                if (a != null && b != null) Line(vh, At(a), At(b), RouteWidth, _style.RouteColor);
            }
            foreach (var l in _content.Locations)
            {
                if (l == null) continue;
                if (l.Id == _here) Diamond(vh, At(l), HereSize, _style.HereColor);
                bool target = _route.Count > 0 && _route[^1] == l.Id;
                Diamond(vh, At(l), target ? MarkerSize * 1.4f : MarkerSize, target ? _style.RouteColor : MarkerColor(l));
            }
        }

        Color MarkerColor(VoyageLocation l) => l.Experience switch
        {
            VoyageExperience.VaultField => _style.VaultColor,
            VoyageExperience.Core => _style.CoreColor,
            _ => _style.PortColor,
        };

        static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            var d = b - a;
            if (d.sqrMagnitude < 1e-4f) return;
            var n = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
            Quad(vh, a - n, a + n, b + n, b - n, c);
        }

        static void Diamond(VertexHelper vh, Vector2 p, float size, Color c)
        {
            float s = size * 0.5f;
            Quad(vh, p + new Vector2(0f, s), p + new Vector2(s, 0f), p + new Vector2(0f, -s), p + new Vector2(-s, 0f), c);
        }

        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int i = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.color = color;
            v.position = a; vh.AddVert(v);
            v.position = b; vh.AddVert(v);
            v.position = c; vh.AddVert(v);
            v.position = d; vh.AddVert(v);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        // ── Labels: each location's own name ─────────────────────────────────

        void Label()
        {
            int n = _content?.Locations?.Count ?? 0;
            // A copied card brings its labels along; adopt them rather than standing a second set.
            if (_labels.Count == 0)
                foreach (Transform child in transform)
                    if (child.TryGetComponent(out TMP_Text existing)) _labels.Add(existing);
            while (_labels.Count < n)
            {
                var go = new GameObject("Label", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var t = go.AddComponent<TextMeshProUGUI>();
                if (_labelTemplate)
                {
                    t.font = _labelTemplate.font;
                    t.fontSharedMaterial = _labelTemplate.fontSharedMaterial;
                    t.fontSize = _labelTemplate.fontSize * 0.6f;
                    t.color = _labelTemplate.color;
                }
                t.alignment = TextAlignmentOptions.Top;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.raycastTarget = false;
                ((RectTransform)go.transform).sizeDelta = new Vector2(160f, 24f);
                _labels.Add(t);
            }
            for (int i = 0; i < _labels.Count; i++)
            {
                var l = i < n ? _content.Locations[i] : null;
                _labels[i].gameObject.SetActive(l != null);
                if (l != null) _labels[i].text = l.Name;
            }
            PlaceLabels();
        }

        void PlaceLabels()
        {
            if (_content?.Locations == null || float.IsInfinity(_minX)) return;
            for (int i = 0; i < _labels.Count && i < _content.Locations.Count; i++)
            {
                var l = _content.Locations[i];
                if (l == null || !_labels[i]) continue;
                ((RectTransform)_labels[i].transform).anchoredPosition =
                    At(l) - rectTransform.rect.center + new Vector2(0f, -MarkerSize);
            }
        }
    }
}

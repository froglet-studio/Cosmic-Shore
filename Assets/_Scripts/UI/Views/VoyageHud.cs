using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The voyage's colours beyond the Voyage window's kit (Frogma Rev 113: Hud, Run, Compare and Score), as authored and
    /// read through the worn colour set (Docs/VesselModels/COLOR_TRACE.md).
    /// </summary>
    static class VoyageHudColors
    {
        static readonly Color SecondAuthored = new(0.722f, 0.753f, 0.941f, 1f);   // #B8C0F0
        static readonly Color ThreatAuthored = new(1f, 0.541f, 0.486f, 1f);       // #FF8A7C
        static readonly Color BreachAuthored = new(1f, 0.294f, 0.227f, 1f);       // #FF4B3A
        static readonly Color ThreatBandAuthored = new(0.314f, 0.039f, 0.078f, 0.88f);
        static readonly Color GoldAuthored = new(1f, 0.682f, 0f, 1f);             // #FFAE00

        public static Color Second => ColorTrace.Retrace(ColorTrace.Ui, SecondAuthored);
        public static Color Threat => ColorTrace.Retrace(ColorTrace.Ui, ThreatAuthored);
        public static Color Breach => ColorTrace.Retrace(ColorTrace.Ui, BreachAuthored);
        public static Color ThreatBand => ColorTrace.Retrace(ColorTrace.Ui, ThreatBandAuthored);
        public static Color Gold => ColorTrace.Retrace(ColorTrace.Ui, GoldAuthored);

        /// <summary>A screen-space overlay canvas on <paramref name="go"/>, laid out on Frogma's 1920 x 1080 frame.</summary>
        public static RectTransform Overlay(GameObject go, int order, bool hits)
        {
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (hits) go.AddComponent<GraphicRaycaster>();
            return (RectTransform)go.transform;
        }

        /// <summary>A bar: a track, a fill <see cref="SetFill"/> sizes, and an optional breach mark.</summary>
        public static (RectTransform fill, RectTransform breach) Bar(VoyageKit kit, Transform parent, float height, bool breach)
        {
            var track = VoyageKit.Node("Bar", parent);
            var le = VoyageKit.Le(track);
            le.minHeight = le.preferredHeight = height;
            VoyageKit.Block(track, VoyageKit.Fade(kit.Outline, 0.45f));
            var fill = VoyageKit.Node("Fill", track);
            VoyageKit.Block(fill, kit.Light);
            SetFill(fill, 0f);
            RectTransform mark = null;
            if (breach)
            {
                mark = VoyageKit.Node("Breach", track);
                VoyageKit.Block(mark, Breach);
                mark.anchorMin = new Vector2(0f, 0f);
                mark.anchorMax = new Vector2(0f, 1f);
                mark.pivot = new Vector2(0.5f, 0.5f);
                mark.sizeDelta = new Vector2(2f, 10f);
                mark.gameObject.SetActive(false);
            }
            return (fill, mark);
        }

        public static void SetFill(RectTransform fill, float share)
        {
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(Mathf.Clamp01(share), 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
        }

        public static void SetBreach(RectTransform mark, float share)
        {
            if (!mark) return;
            mark.gameObject.SetActive(share > 0f);
            mark.anchorMin = new Vector2(Mathf.Clamp01(share), 0f);
            mark.anchorMax = new Vector2(Mathf.Clamp01(share), 1f);
            mark.anchoredPosition = Vector2.zero;
        }

        public static string ElementName(VoyageLines lines, Element e) => e switch
        {
            Element.Charge => lines.ElementCharge,
            Element.Mass => lines.ElementMass,
            Element.Space => lines.ElementSpace,
            Element.Time => lines.ElementTime,
            _ => string.Empty,
        };

        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }
    }

    /// <summary>
    /// The in-voyage HUD (Frogma Rev 116 A, Marquee, picked by Shombith 2026-11-17: "build A"; it replaces Rev 114 O1's
    /// strip). Arcade top centre: the goal's figure is the score (a Crystal Run's count with its goal small beside it,
    /// else the Ark's hull), the route a line of stops under it with the Ark's place on it, then where the Ark is and
    /// whether it is hostile, and the raid clock. The toast spot is the band right under this (<see cref="ArkwayVoyageHud"/>
    /// moves its banner there while a voyage shows this HUD). Down the left edge the Ark's hull stands as a tall bar with
    /// its breach line (red) and leak line (dashed), the figure over it, its caption under it, both in the threat colour
    /// while the hull is losing plates or the hold leaks. Under the Ark, a line of what you carry unbanked. In the last
    /// <see cref="RingFrom"/> seconds at anchor a ring and the seconds stand in the centre. Every word is
    /// <see cref="VoyageLines"/>', chosen by <see cref="VoyageObjectiveText"/>; it reads <see cref="VoyageObjective.Source"/>
    /// ten times a second and never takes a tap.
    /// </summary>
    public sealed class VoyageObjectiveBox : MonoBehaviour
    {
        const float RefreshSeconds = 0.1f;
        // Rev 116 A, in canvas units (1920 x 1080 reference, matched half width half height), inside the safe area. Each
        // piece is anchored to its own edge or the centre, so a phone's wider screen moves them out, never off screen.
        // Made smaller and clearer after the first playtest (Shombith, 2026-11-17): a dark plate behind the words, the
        // count smaller, the lines under it larger and brighter.
        const float TopY = 18f, RouteWidth = 360f, RouteDot = 12f, MarqueeMinWidth = 460f;
        const float RailX = 20f, RailWidth = 92f, RailHeight = 340f, BarWidth = 16f, BarTop = 52f, BarHeight = 240f;
        const float CarryY = -120f;
        const float RingSize = 220f, RingY = 40f, RingFrom = 5f;
        const float SubSize = 20f;

        VoyageHarbour _harbour;
        VoyageKit _kit;
        VoyageLines _lines;

        CanvasGroup _group, _ringGroup;
        TMP_Text _count, _place, _clock, _action, _railFigure, _railLabel, _carry, _ringNumber;
        RectTransform _route, _routeFill, _routeArk, _barFill, _breach, _leak, _carryChip, _flowers, _marquee, _safe;
        Image _barFillImage;
        VoyageCountdownRing _ring;
        readonly Dictionary<Element, TMP_Text> _split = new();
        int _routeCells = -1;
        readonly List<Image> _dots = new();
        float _nextRefresh;

        public void Configure(VoyageHarbour harbour)
        {
            _harbour = harbour;
            var content = harbour && harbour.Content ? harbour.Content : VoyageContentSO.Load();
            _lines = content ? content.Lines : new VoyageLines();
            _kit = new VoyageKit(VoyageBoardStyleSO.Load(), _lines, null);
            Build();
        }

        // ── Build ───────────────────────────────────────────────────────────

        void Build()
        {
            var go = new GameObject("VoyageHud", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var root = VoyageHudColors.Overlay(go, 20001, hits: false);
            var frame = VoyageKit.Node("Frame", root);
            VoyageKit.Stretch(frame);
            frame.gameObject.AddComponent<SafeAreaFitter>();
            _safe = frame;
            _group = frame.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = _group.interactable = false;

            BuildMarquee(frame);
            BuildRail(frame);
            BuildCarry(frame);
            BuildRing(frame);
        }

        /// <summary>Place <paramref name="rt"/> by an anchor and pivot of its own, <paramref name="at"/> from that anchor.</summary>
        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 at, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = at;
            rt.sizeDelta = size;
        }

        /// <summary>The score at top centre on a dark plate, the route under it, then the place and the raid clock, then
        /// one job line.</summary>
        void BuildMarquee(RectTransform frame)
        {
            _marquee = VoyageKit.Node("Marquee", frame);
            Anchor(_marquee, new Vector2(0.5f, 1f), new Vector2(0f, -TopY), new Vector2(MarqueeMinWidth, 150f));
            VoyageKit.Block(_marquee, VoyageKit.Fade(_kit.Popup, 0.6f));
            var col = VoyageKit.Column(_marquee, 6f, TextAnchor.UpperCenter);
            col.childForceExpandWidth = false;
            col.padding = new RectOffset(26, 26, 8, 12);
            var fit = _marquee.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            VoyageKit.Le(_marquee).minWidth = MarqueeMinWidth;

            _count = _kit.Text(_marquee, 54f, _kit.Light, TextAlignmentOptions.Center);
            _count.richText = true;
            VoyageKit.Le(_count).minHeight = 60f;

            _route = VoyageKit.Node("Route", _marquee);
            var rle = VoyageKit.Le(_route);
            rle.minWidth = rle.preferredWidth = RouteWidth;
            rle.minHeight = rle.preferredHeight = RouteDot + 4f;

            var sub = VoyageKit.Node("Where", _marquee);
            VoyageKit.Row(sub, 20f, TextAnchor.MiddleCenter);
            _flowers = VoyageKit.Node("Flowers", sub);
            VoyageKit.Row(_flowers, 12f);
            foreach (var e in VoyageRaid.Elements)
            {
                var cell = VoyageKit.Node(e.ToString(), _flowers);
                VoyageKit.Row(cell, 4f);
                var flower = VoyageKit.Flower(cell, e, _kit.Light);
                var fle = VoyageKit.Le(flower);
                fle.minWidth = fle.preferredWidth = fle.minHeight = fle.preferredHeight = 20f;
                _split[e] = _kit.Text(cell, SubSize, _kit.Light, TextAlignmentOptions.Left);
            }
            _place = _kit.Text(sub, SubSize, _kit.Light, TextAlignmentOptions.Center);
            _clock = _kit.Text(sub, SubSize, _kit.Light, TextAlignmentOptions.Center);

            _action = _kit.Text(_marquee, SubSize, _kit.Light, TextAlignmentOptions.Center);
        }

        /// <summary>The Ark's hull at the left edge, on a dark plate: its figure, a bar with the breach and leak lines, its
        /// caption.</summary>
        void BuildRail(RectTransform frame)
        {
            var rail = VoyageKit.Node("Rail", frame);
            Anchor(rail, new Vector2(0f, 0.5f), new Vector2(RailX, 0f), new Vector2(RailWidth, RailHeight));
            VoyageKit.Block(rail, VoyageKit.Fade(_kit.Popup, 0.6f));

            var head = VoyageKit.Node("RailFigure", rail);
            VoyageKit.At(head, 0f, 8f, RailWidth, 38f);
            _railFigure = _kit.Text(head, 28f, _kit.Light, TextAlignmentOptions.Center);
            VoyageKit.Stretch(_railFigure.rectTransform);

            var track = VoyageKit.Node("HullBar", rail);
            VoyageKit.At(track, (RailWidth - BarWidth) * 0.5f, BarTop, BarWidth, BarHeight);
            VoyageKit.Block(track, VoyageKit.Fade(_kit.Outline, 0.5f));
            _barFill = VoyageKit.Node("Fill", track);
            _barFillImage = VoyageKit.Block(_barFill, _kit.Light);
            SetVertical(_barFill, 0f);

            // The breach line, red, a little wider than the bar.
            _breach = VoyageKit.Node("Breach", track);
            VoyageKit.Block(_breach, VoyageHudColors.Breach);
            _breach.anchorMin = new Vector2(0f, 0f);
            _breach.anchorMax = new Vector2(1f, 0f);
            _breach.pivot = new Vector2(0.5f, 0.5f);
            _breach.offsetMin = new Vector2(-8f, -1.5f);
            _breach.offsetMax = new Vector2(8f, 1.5f);

            // The leak line, dashed: four short marks across the bar.
            _leak = VoyageKit.Node("Leak", track);
            _leak.anchorMin = new Vector2(0f, 0f);
            _leak.anchorMax = new Vector2(1f, 0f);
            _leak.pivot = new Vector2(0.5f, 0.5f);
            _leak.offsetMin = new Vector2(-8f, -1.5f);
            _leak.offsetMax = new Vector2(8f, 1.5f);
            for (int i = 0; i < 4; i++)
            {
                var dash = VoyageKit.Node("Dash", _leak);
                dash.anchorMin = new Vector2(i / 4f, 0f);
                dash.anchorMax = new Vector2((i + 0.6f) / 4f, 1f);
                dash.offsetMin = dash.offsetMax = Vector2.zero;
                VoyageKit.Block(dash, _kit.Light);
            }

            var foot = VoyageKit.Node("RailLabel", rail);
            VoyageKit.At(foot, 0f, BarTop + BarHeight + 10f, RailWidth, 30f);
            _railLabel = _kit.Label(foot, 14f, _kit.Light, TextAlignmentOptions.Center);
            VoyageKit.Stretch(_railLabel.rectTransform);
        }

        /// <summary>What you carry unbanked, a chip under the middle of the screen.</summary>
        void BuildCarry(RectTransform frame)
        {
            _carryChip = VoyageKit.Node("Carrying", frame);
            Anchor(_carryChip, new Vector2(0.5f, 0.5f), new Vector2(0f, CarryY), new Vector2(800f, 44f));
            VoyageKit.Row(_carryChip, 0f, TextAnchor.MiddleCenter);
            var chip = VoyageKit.Node("Chip", _carryChip);
            VoyageKit.Block(chip, VoyageKit.Fade(_kit.Popup, 0.7f));
            var h = VoyageKit.Row(chip, 0f, TextAnchor.MiddleCenter);
            h.padding = new RectOffset(18, 18, 8, 8);
            chip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            _carry = _kit.Text(chip, SubSize, _kit.Light, TextAlignmentOptions.Center);
        }

        /// <summary>The last seconds at anchor: a ring that empties and the seconds in it, a little above the centre.</summary>
        void BuildRing(RectTransform frame)
        {
            var holder = VoyageKit.Node("Countdown", frame);
            Anchor(holder, new Vector2(0.5f, 0.5f), new Vector2(0f, RingY), new Vector2(RingSize, RingSize));
            _ringGroup = holder.gameObject.AddComponent<CanvasGroup>();
            _ringGroup.alpha = 0f;
            var disc = VoyageKit.Node("Disc", holder);
            VoyageKit.Stretch(disc);
            var back = disc.gameObject.AddComponent<VoyageCountdownRing>();
            back.Thickness = 7f;
            back.Fill = 1f;
            back.Inner = VoyageKit.Fade(_kit.Popup, 0.55f);
            back.color = VoyageKit.Fade(_kit.Outline, 0.6f);
            var arc = VoyageKit.Node("Arc", holder);
            VoyageKit.Stretch(arc);
            _ring = arc.gameObject.AddComponent<VoyageCountdownRing>();
            _ring.Thickness = 7f;
            _ring.color = _kit.Light;
            _ringNumber = _kit.Text(holder, 110f, _kit.Light, TextAlignmentOptions.Center);
            VoyageKit.Stretch(_ringNumber.rectTransform);
        }

        static void SetVertical(RectTransform fill, float share)
        {
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(1f, Mathf.Clamp01(share));
            fill.offsetMin = fill.offsetMax = Vector2.zero;
        }

        static void SetMark(RectTransform mark, float share)
        {
            mark.gameObject.SetActive(share > 0f);
            mark.anchorMin = new Vector2(0f, Mathf.Clamp01(share));
            mark.anchorMax = new Vector2(1f, Mathf.Clamp01(share));
        }

        /// <summary>The route as stops on a line (Rev 116 A): done ones lit, the current one ringed, the destination ringed
        /// in the call colour, the line lit up to the Ark.</summary>
        void BuildRoute(int cells)
        {
            _routeCells = cells;
            _dots.Clear();
            for (int i = _route.childCount - 1; i >= 0; i--) Destroy(_route.GetChild(i).gameObject);
            if (cells <= 0) return;
            var line = VoyageKit.Node("Line", _route);
            line.anchorMin = new Vector2(0f, 0.5f);
            line.anchorMax = new Vector2(1f, 0.5f);
            line.offsetMin = new Vector2(RouteDot * 0.5f, -1f);
            line.offsetMax = new Vector2(-RouteDot * 0.5f, 1f);
            VoyageKit.Block(line, VoyageKit.Fade(_kit.Outline, 0.8f));
            _routeFill = VoyageKit.Node("Done", line);
            VoyageKit.Block(_routeFill, _kit.Light);
            for (int i = 0; i < cells; i++)
            {
                float x = cells == 1 ? 0.5f : (float)i / (cells - 1);
                var dot = VoyageKit.Node("Stop" + i, _route);
                dot.anchorMin = dot.anchorMax = new Vector2(x, 0.5f);
                dot.pivot = new Vector2(0.5f, 0.5f);
                dot.sizeDelta = new Vector2(RouteDot, RouteDot);
                _dots.Add(VoyageKit.Block(dot, _kit.Light));
            }
        }

        // ── Each frame ──────────────────────────────────────────────────────

        void Update()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            var source = VoyageObjective.Source;
            VoyageObjective o = default;
            bool shown = source != null && !VoyageScoreboard.IsOpen && !VoyageIntro.IsPlaying &&
                         source.TryGetObjective(out o) && Show(o);
            _group.alpha = Mathf.MoveTowards(_group.alpha, shown ? 1f : 0f, 0.5f);
            if (shown && _marquee && _safe)
            {
                var canvas = (RectTransform)_safe.parent;
                float safeTop = canvas.rect.height * (1f - _safe.anchorMax.y);
                Bottom = safeTop + TopY + _marquee.rect.height;
            }
            bool ring = shown && o.Goal == VoyageObjective.Kind.CrystalRun && o.Anchored && o.AnchorSeconds > 0f &&
                        o.AnchorSeconds <= RingFrom;
            if (ring)
            {
                VoyageKit.Set(_ringNumber, Mathf.CeilToInt(o.AnchorSeconds).ToString());
                _ring.Fill = Mathf.Clamp01(o.AnchorSeconds / RingFrom);
            }
            _ringGroup.alpha = Mathf.MoveTowards(_ringGroup.alpha, ring ? 1f : 0f, 0.5f);
        }

        /// <summary>While this HUD is up the toasts take the spot under its marquee (Rev 116 A).</summary>
        public static bool Showing { get; private set; }

        /// <summary>Where the marquee ends, in canvas units down from the top of the screen (the 1920 x 1080 reference):
        /// the toast spot starts just under it.</summary>
        public static float Bottom { get; private set; } = 200f;

        void OnDisable() => Showing = false;

        bool Show(in VoyageObjective o)
        {
            Showing = false;
            if (o.Goal == VoyageObjective.Kind.None) return false;
            Showing = true;
            bool raid = o.Goal == VoyageObjective.Kind.CrystalRun;

            // The score: a Crystal Run's count with its goal small and quiet beside it; else the Ark's hull.
            string goalTag = "<size=50%><color=#" + ColorUtility.ToHtmlStringRGB(_kit.Sub) + ">";
            VoyageKit.Set(_count, raid
                ? TemplateText.Fill(_lines.ObjectiveCount, "done", o.Done + goalTag, "goal", o.Target + "</color></size>")
                : VoyageObjectiveText.Figure(_lines, o));

            ShowRoute(o);

            VoyageKit.Set(_place, VoyageObjectiveText.Place(_lines, o));
            _place.color = o.Hostile ? VoyageHudColors.Threat : _kit.Light;
            _flowers.gameObject.SetActive(raid && o.Crystals != null);
            if (raid && o.Crystals != null)
                foreach (var e in VoyageRaid.Elements)
                    VoyageKit.Set(_split[e], TemplateText.Fill(_lines.ObjectiveStripElement, "count", o.Crystals[(int)e].ToString()));
            string clock = raid && o.Anchored && !string.IsNullOrEmpty(_lines.ObjectiveStripAnchor)
                ? TemplateText.Fill(_lines.ObjectiveStripAnchor, "time", VoyageHudColors.Clock(o.AnchorSeconds))
                : string.Empty;
            _clock.gameObject.SetActive(!string.IsNullOrEmpty(clock));
            VoyageKit.Set(_clock, clock);
            _clock.color = o.AnchorClosing ? VoyageHudColors.Threat : _kit.Light;

            // One job line under it, unless it only repeats the clock or the carried line drawn elsewhere.
            var (action, threat) = VoyageObjectiveText.StripAction(_lines, o);
            if (action == clock || (raid && o.Carried > 0 && !threat && !o.CanComplete)) action = string.Empty;
            _action.gameObject.SetActive(!string.IsNullOrEmpty(action));
            VoyageKit.Set(_action, action);
            _action.color = threat ? VoyageHudColors.Threat : _kit.Light;

            // The hull rail.
            bool hot = o.Leaking || o.UnderAttack || o.Assault == ArkAssaultPhase.Active;
            var railInk = hot ? VoyageHudColors.Threat : _kit.Light;
            VoyageKit.Set(_railFigure, TemplateText.Fill(_lines.ObjectiveStripHull, "hull", Mathf.RoundToInt(o.Hull * 100f).ToString()));
            _railFigure.color = railInk;
            SetVertical(_barFill, o.Hull);
            _barFillImage.color = railInk;
            SetMark(_breach, o.Breach);
            SetMark(_leak, o.LeakLine);
            VoyageKit.Set(_railLabel, _lines.ObjectiveRailLabel);
            _railLabel.color = railInk;

            // What you carry.
            bool carry = raid && o.Carried > 0 && !string.IsNullOrEmpty(_lines.ObjectiveStripCarried);
            _carryChip.gameObject.SetActive(carry);
            if (carry) VoyageKit.Set(_carry, TemplateText.Fill(_lines.ObjectiveStripCarried, "count", o.Carried.ToString()));
            return true;
        }

        void ShowRoute(in VoyageObjective o)
        {
            _route.gameObject.SetActive(o.Cells > 0);
            if (o.Cells <= 0) return;
            if (o.Cells != _routeCells) BuildRoute(o.Cells);
            for (int i = 0; i < _dots.Count; i++)
            {
                bool last = i == _dots.Count - 1;
                bool done = i < o.Cell - 1;
                bool now = i == o.Cell - 1;
                _dots[i].color = done || now ? _kit.Light : last ? _kit.CtaFill : VoyageKit.Fade(_kit.Sub, 0.6f);
                _dots[i].rectTransform.sizeDelta = Vector2.one * (now ? RouteDot + 4f : RouteDot);
            }
            if (_routeFill)
            {
                float share = _dots.Count <= 1 ? 0f : Mathf.Clamp01((o.Cell - 1) / (float)(_dots.Count - 1));
                _routeFill.anchorMin = Vector2.zero;
                _routeFill.anchorMax = new Vector2(share, 1f);
                _routeFill.offsetMin = _routeFill.offsetMax = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// A ring drawn as a mesh (Frogma Rev 116 A's countdown): an annulus <see cref="Thickness"/> wide, lit clockwise from
    /// twelve o'clock for <see cref="Fill"/> of the way round, the inside filled with <see cref="Inner"/> (clear: none).
    /// No sprite, so it stays sharp at any size.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoyageCountdownRing : MaskableGraphic
    {
        const int Segments = 96;
        float _fill = 1f, _thickness = 8f;
        Color _inner = Color.clear;

        public float Fill { get => _fill; set { if (Mathf.Approximately(_fill, value)) return; _fill = Mathf.Clamp01(value); SetVerticesDirty(); } }
        public float Thickness { get => _thickness; set { _thickness = value; SetVerticesDirty(); } }
        public Color Inner { get => _inner; set { _inner = value; SetVerticesDirty(); } }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            var c = r.center;
            float outer = Mathf.Min(r.width, r.height) * 0.5f;
            float inner = Mathf.Max(0f, outer - _thickness);
            if (_inner.a > 0f)
            {
                int centre = vh.currentVertCount;
                vh.AddVert(c, _inner, Vector4.zero);
                for (int i = 0; i <= Segments; i++)
                {
                    float a = i / (float)Segments * Mathf.PI * 2f;
                    vh.AddVert(c + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * inner, _inner, Vector4.zero);
                    if (i > 0) vh.AddTriangle(centre, centre + i, centre + i + 1);
                }
            }
            int steps = Mathf.CeilToInt(Segments * _fill);
            if (steps <= 0) return;
            int start = vh.currentVertCount;
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Min(i / (float)Segments, _fill) * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                vh.AddVert(c + dir * inner, color, Vector4.zero);
                vh.AddVert(c + dir * outer, color, Vector4.zero);
                if (i > 0)
                {
                    int k = start + i * 2;
                    vh.AddTriangle(k - 2, k - 1, k + 1);
                    vh.AddTriangle(k - 2, k + 1, k);
                }
            }
        }
    }

    /// <summary>
    /// Which authored line the objective box shows for a <see cref="VoyageObjective"/> (Frogma Rev 113, Hud and Run): pure,
    /// so an edit-mode test reads every choice. An empty authored line shows nothing.
    /// </summary>
    public static class VoyageObjectiveText
    {
        static string Percent(float share) => Mathf.RoundToInt(share * 100f).ToString();

        public static string Kicker(VoyageLines lines, in VoyageObjective o) => o.Goal switch
        {
            VoyageObjective.Kind.CrystalRun => lines.ObjectiveRaidKicker,
            VoyageObjective.Kind.SailingHome => lines.ObjectiveHomeKicker,
            VoyageObjective.Kind.Escort => TemplateText.Fill(lines.ObjectiveEscortKicker, "difficulty", o.Difficulty.ToString()),
            _ => string.Empty,
        };

        public static string GoalLine(VoyageLines lines, in VoyageObjective o) => o.Goal switch
        {
            VoyageObjective.Kind.CrystalRun => TemplateText.Fill(lines.ObjectiveRaidGoal, "goal", o.Target.ToString(), "to", o.To),
            VoyageObjective.Kind.SailingHome => TemplateText.Fill(lines.ObjectiveHomeGoal, "to", o.To),
            VoyageObjective.Kind.Escort => o.Breach > 0f
                ? TemplateText.Fill(lines.ObjectiveEscortGoal, "breach", Percent(o.Breach), "to", o.To)
                : TemplateText.Fill(lines.ObjectiveEscortGoalNoBreach, "to", o.To),
            _ => string.Empty,
        };

        /// <summary>The strip's first line (Rev 114 O1): the goal's name.</summary>
        public static string Title(VoyageLines lines, in VoyageObjective o) => o.Goal switch
        {
            VoyageObjective.Kind.CrystalRun => lines.ObjectiveRaidKicker,
            VoyageObjective.Kind.SailingHome => lines.ObjectiveHomeKicker,
            VoyageObjective.Kind.Escort => lines.ObjectiveStripEscort,
            _ => string.Empty,
        };

        /// <summary>The figure on the strip's first line: a Crystal Run's count, else the Ark's hull.</summary>
        public static string Figure(VoyageLines lines, in VoyageObjective o) => o.Goal switch
        {
            VoyageObjective.Kind.None => string.Empty,
            VoyageObjective.Kind.CrystalRun => TemplateText.Fill(lines.ObjectiveCount, "done", o.Done.ToString(),
                "goal", o.Target.ToString()),
            _ => TemplateText.Fill(lines.ObjectiveStripHull, "hull", Percent(o.Hull)),
        };

        /// <summary>The hull line under the bar: a Crystal Run's (its figure is the count; every other strip's figure is
        /// the hull already).</summary>
        public static string HullLine(VoyageLines lines, in VoyageObjective o) =>
            o.Goal == VoyageObjective.Kind.CrystalRun
                ? TemplateText.Fill(lines.ObjectiveStripArkHull, "hull", Percent(o.Hull))
                : string.Empty;

        /// <summary>The hold is leaking: the line that says so, or empty.</summary>
        static string LeakLine(VoyageLines lines, in VoyageObjective o) =>
            o.Leaking ? TemplateText.Fill(lines.ObjectiveStripLeaking, "line", Percent(o.LeakLine)) : string.Empty;

        /// <summary>Where the Ark is and whether it is hostile ("Scarab Reef · hostile", "Cell 2 · hostile").</summary>
        public static string Place(VoyageLines lines, in VoyageObjective o)
        {
            if (o.Goal == VoyageObjective.Kind.None) return string.Empty;
            string place = !string.IsNullOrEmpty(o.World) ? o.World
                : o.Cell > 0 ? StopName(lines, o, o.Cell - 1) : string.Empty;
            if (string.IsNullOrEmpty(place)) return string.Empty;
            return TemplateText.Fill(lines.ObjectiveStripPlace, "place", place,
                "state", o.Hostile ? lines.ObjectiveStripHostile : lines.ObjectiveStripCalm);
        }

        /// <summary>The strip's one job line: an escort's action line; on the way home what is at stake (else the action);
        /// a Crystal Run only when the Ark is threatened or the goal is met (its flowers, count and place say the rest).</summary>
        public static (string line, bool threat) StripAction(VoyageLines lines, in VoyageObjective o)
        {
            if (o.Goal == VoyageObjective.Kind.None) return (string.Empty, false);
            var action = Action(lines, o);
            if (o.Goal == VoyageObjective.Kind.CrystalRun)
            {
                // An assault on the Ark comes first; then the raid window while the Ark is anchored; then what the pilot
                // carries unbanked. The place line already says hostile or calm; otherwise only the Ark's danger or a met
                // goal earns a line.
                if (o.Assault != ArkAssaultPhase.None) return action;
                string leak = LeakLine(lines, o);
                if (!string.IsNullOrEmpty(leak)) return (leak, true);
                if (o.Anchored && !string.IsNullOrEmpty(lines.ObjectiveStripAnchor))
                    return (TemplateText.Fill(lines.ObjectiveStripAnchor, "time", VoyageHudColors.Clock(o.AnchorSeconds)),
                        o.AnchorClosing);
                if (o.Carried > 0 && !string.IsNullOrEmpty(lines.ObjectiveStripCarried))
                    return (TemplateText.Fill(lines.ObjectiveStripCarried, "count", o.Carried.ToString()), false);
                if (o.CanComplete && !string.IsNullOrEmpty(lines.ObjectiveStripComplete))
                    return (lines.ObjectiveStripComplete, false);
                return action.line == lines.ObjectiveHostile || action.line == lines.ObjectiveCalm
                    ? (string.Empty, false) : action;
            }
            if (o.Goal == VoyageObjective.Kind.SailingHome && o.Assault != ArkAssaultPhase.Active)
            {
                string leak = LeakLine(lines, o);
                if (!string.IsNullOrEmpty(leak)) return (leak, true);
            }
            if (o.Goal == VoyageObjective.Kind.SailingHome && !action.threat)
            {
                string stake = UnderHull(lines, o);
                if (!string.IsNullOrEmpty(stake)) return (stake, false);
            }
            return action;
        }

        /// <summary>A stop's name on the route: its own name (an escort's worlds), the destination's for the last stop of
        /// a run, else "Cell n".</summary>
        public static string StopName(VoyageLines lines, in VoyageObjective o, int index)
        {
            if (o.Stops != null && index >= 0 && index < o.Stops.Length && !string.IsNullOrEmpty(o.Stops[index]))
                return o.Stops[index];
            if (index == o.Cells - 1 && !string.IsNullOrEmpty(o.To)) return o.To;
            return TemplateText.Fill(lines.ObjectiveCellName, "cell", (index + 1).ToString());
        }

        /// <summary>The line under the hull: where the Ark is in an escort, what is at stake on the way home.</summary>
        public static string UnderHull(VoyageLines lines, in VoyageObjective o)
        {
            if (o.Goal == VoyageObjective.Kind.Escort)
                return string.IsNullOrEmpty(o.World) ? string.Empty
                    : TemplateText.Fill(o.Inside ? lines.ObjectiveArkInside : lines.ObjectiveArkBound, "world", o.World);
            if (o.Goal == VoyageObjective.Kind.SailingHome && o.AtStake > 0)
                return TemplateText.Fill(lines.ObjectiveAtStake, "count", o.AtStake.ToString());
            return string.Empty;
        }

        /// <summary>The one action line, and whether it is a threat: an assault under way, then one announced, then a
        /// hull losing plates, then a healing Ark, then a met goal, then the stop calm or hostile.</summary>
        public static (string line, bool threat) Action(VoyageLines lines, in VoyageObjective o)
        {
            bool escort = o.Goal == VoyageObjective.Kind.Escort;
            if (o.Assault == ArkAssaultPhase.Active)
                return (TemplateText.Fill(lines.ObjectiveActionAssault, "kills", o.AssaultKills.ToString(),
                    "needed", o.AssaultKillsNeeded.ToString()), true);
            if (o.Assault == ArkAssaultPhase.Incoming)
                return (TemplateText.Fill(lines.ObjectiveActionIncoming, "seconds",
                    Mathf.CeilToInt(Mathf.Max(0f, o.AssaultSeconds)).ToString()), true);
            if (o.UnderAttack) return (lines.ObjectiveActionUnderAttack, true);
            if (o.Healing) return (lines.ObjectiveActionHealing, false);
            if (o.Goal == VoyageObjective.Kind.CrystalRun && o.Target > 0 && o.Done >= o.Target)
                return (lines.ObjectiveGoalMet, false);
            if (o.Hostile) return (escort ? lines.ObjectiveEscortHostile : lines.ObjectiveHostile, true);
            return (escort ? lines.ObjectiveEscortCalm : lines.ObjectiveCalm, false);
        }
    }

    /// <summary>
    /// The end-of-voyage scoreboard (Frogma Rev 113, Score): over the dimmed sea, the kicker and the title, the Ark's hull
    /// against its breach line, a row per seat, the rewards, and the ways out: Finish (B, the quiet button) and Venture
    /// further (A, the call to action), or one button once the voyage is over. It shows
    /// <see cref="VoyageScoreboard.Current"/>; a press is <see cref="VoyageScoreboard.Choose"/>. While it is up the
    /// pointer is free and the voyage scene waits.
    /// </summary>
    public sealed class VoyageScoreboardView : MonoBehaviour
    {
        VoyageKit _kit;
        VoyageLines _lines;
        RectTransform _root, _page;
        VoyageScoreboard _shown;
        float _pressFrom;
        CursorLockMode _cursorLock;
        bool _cursorVisible, _cursorHeld;

        public void Configure(VoyageHarbour harbour)
        {
            var content = harbour && harbour.Content ? harbour.Content : VoyageContentSO.Load();
            _lines = content ? content.Lines : new VoyageLines();
            _kit = new VoyageKit(VoyageBoardStyleSO.Load(), _lines, null);
            var go = new GameObject("VoyageScoreboard", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _root = VoyageHudColors.Overlay(go, 25000, hits: true);
            go.SetActive(false);
            VoyageScoreboard.Changed += OnChanged;
            if (VoyageScoreboard.Current != null) OnChanged(VoyageScoreboard.Current);
        }

        void OnDestroy()
        {
            VoyageScoreboard.Changed -= OnChanged;
            ReleaseCursor();
        }

        void OnChanged(VoyageScoreboard board)
        {
            _shown = board;
            if (_page) Destroy(_page.gameObject);
            _page = null;
            _root.gameObject.SetActive(board != null);
            if (board == null)
            {
                ReleaseCursor();
                return;
            }
            HoldCursor();
            // A press already under way as the board opens (a boost, a fire) is not a choice.
            _pressFrom = Time.unscaledTime + 0.6f;
            Build(board);
        }

        void HoldCursor()
        {
            if (_cursorHeld) return;
            _cursorHeld = true;
            _cursorLock = Cursor.lockState;
            _cursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void ReleaseCursor()
        {
            if (!_cursorHeld) return;
            _cursorHeld = false;
            Cursor.lockState = _cursorLock;
            Cursor.visible = _cursorVisible;
        }

        /// <summary>The pad and the keyboard press the drawn keycaps: A or Enter for the call to action, B or Escape for
        /// Finish.</summary>
        void Update()
        {
            var b = _shown;
            if (b == null || Time.unscaledTime < _pressFrom) return;
            var pad = Gamepad.current;
            var keys = Keyboard.current;
            bool a = pad != null && pad.buttonSouth.wasPressedThisFrame || keys != null && keys.enterKey.wasPressedThisFrame;
            bool back = pad != null && pad.buttonEast.wasPressedThisFrame || keys != null && keys.escapeKey.wasPressedThisFrame;
            bool x = pad != null && pad.buttonWest.wasPressedThisFrame || keys != null && keys.xKey.wasPressedThisFrame;
            var backTo = b.Finish ?? (b.TryAgain != null ? b.Close : null);
            if (a) VoyageScoreboard.Choose(b.Venture ?? b.TryAgain ?? b.Close ?? b.Finish);
            else if (back && backTo != null) VoyageScoreboard.Choose(backTo);
            else if (x && b.FinishHere != null) VoyageScoreboard.Choose(b.FinishHere);
        }

        // ── The page ────────────────────────────────────────────────────────

        // Frogma Rev 116 A (Marquee), the end screen: one centred column. The head (what happened, the big count and the
        // count by element), the seats table under it, a row of reward tiles, then Finish and the call to action.
        const float ColumnX = 510f, ColumnWidth = 900f;
        const float HeadY = 64f, SeatsY = 470f, TilesY = 780f, ButtonsY = 924f, ButtonWidth = 380f, ButtonGap = 40f;

        void Build(VoyageScoreboard b)
        {
            _page = VoyageKit.Node("Page", _root);
            VoyageKit.Stretch(_page);
            VoyageKit.Block(_page, VoyageKit.Fade(_kit.Popup, 0.62f), hit: true);
            var frame = VoyageKit.Node("Frame", _page);
            VoyageKit.Fit(frame, new Vector2(1920f, 1080f));

            Head(frame, b);
            Seats(frame, b);
            Rewards(frame, b);
            Buttons(frame, b);
        }

        void Head(RectTransform frame, VoyageScoreboard b)
        {
            var head = VoyageKit.Node("Head", frame);
            VoyageKit.At(head, 0f, HeadY, 1920f, SeatsY - HeadY - 16f);
            VoyageKit.Column(head, 10f, TextAnchor.UpperCenter);
            var (kicker, color) = b.Board switch
            {
                VoyageScoreboard.Kind.Lost => (_lines.ScoreKickerLost, VoyageHudColors.Threat),
                VoyageScoreboard.Kind.Docked => (_lines.ScoreKickerDocked, _kit.CtaFill),
                _ => b.Succeeded ? (_lines.ScoreKickerDone, _kit.CtaFill) : (_lines.ScoreKickerFailed, VoyageHudColors.Threat),
            };
            _kit.Label(head, 16, color, TextAlignmentOptions.Center).text = kicker;
            string time = VoyageHudColors.Clock(b.Seconds);
            string title = b.Board switch
            {
                VoyageScoreboard.Kind.CrystalRun => TemplateText.Fill(_lines.ScoreRaidTitle, "done", b.Done.ToString(), "goal", b.Goal.ToString()),
                VoyageScoreboard.Kind.Escort => TemplateText.Fill(_lines.ScoreEscortTitle, "to", b.To),
                VoyageScoreboard.Kind.Docked => TemplateText.Fill(_lines.ScoreDockedTitle, "to", b.To),
                _ => _lines.ScoreLostTitle,
            };
            string stats = b.Board switch
            {
                VoyageScoreboard.Kind.CrystalRun => TemplateText.Fill(_lines.ScoreRaidStats, "to", b.To, "cells", b.Cells.ToString(), "time", time),
                VoyageScoreboard.Kind.Escort => TemplateText.Fill(_lines.ScoreEscortStats, "difficulty", b.Difficulty.ToString(),
                    "worlds", b.Worlds.ToString(), "legs", b.Legs.ToString(), "time", time),
                VoyageScoreboard.Kind.Docked => _lines.ScoreDockedStats,
                _ => string.Empty,
            };
            _kit.Text(head, 60, _kit.Light, TextAlignmentOptions.Center).text = title;

            // The big count: every crystal the crew took, and beside it the count by element (Rev 116 A, Score).
            if (b.Board != VoyageScoreboard.Kind.Escort)
            {
                var count = VoyageKit.Node("Count", head);
                VoyageKit.Row(count, 16f, TextAnchor.LowerCenter);
                _kit.Text(count, 96, _kit.Light, TextAlignmentOptions.Center).text = b.CrystalTotal.ToString();
                var label = _kit.Text(count, 22, _kit.Sub, TextAlignmentOptions.BottomLeft);
                label.text = _lines.ScoreStolen;
                label.margin = new Vector4(0f, 0f, 0f, 16f);

                var split = VoyageKit.Node("ByElement", head);
                VoyageKit.Row(split, 26f, TextAnchor.MiddleCenter);
                foreach (var e in VoyageRaid.Elements)
                {
                    if (b.Crystals[(int)e] <= 0) continue;
                    var cell = VoyageKit.Node(e.ToString(), split);
                    VoyageKit.Row(cell, 6f);
                    var flower = VoyageKit.Flower(cell, e, VoyageHudColors.Second);
                    var fle = VoyageKit.Le(flower);
                    fle.minWidth = fle.preferredWidth = fle.minHeight = fle.preferredHeight = 22f;
                    _kit.Text(cell, 19, _kit.Light, TextAlignmentOptions.Left).text = b.Crystals[(int)e].ToString();
                }
            }
            if (!string.IsNullOrEmpty(stats)) _kit.Text(head, 17, _kit.Sub, TextAlignmentOptions.Center).text = stats;
        }

        void Seats(RectTransform frame, VoyageScoreboard b)
        {
            var panel = VoyageKit.Node("Seats", frame);
            VoyageKit.At(panel, ColumnX, SeatsY, ColumnWidth, TilesY - SeatsY - 18f);
            var col = VoyageKit.Column(panel, 0f);
            col.padding = new RectOffset(0, 0, 0, 0);

            bool crystals = b.Board != VoyageScoreboard.Kind.Escort;
            Line(panel, 30f, _kit.Sub, true, _lines.ScoreColSeat, crystals ? _lines.ScoreColCrystals : string.Empty,
                _lines.ScoreColKills, _lines.ScoreColGuarded, _lines.ScoreColScore);
            foreach (var r in b.Rows)
            {
                string name = TemplateText.Fill(_lines.ScoreRowSeat, "name", r.You && !string.IsNullOrEmpty(_lines.ScoreRowYou) ? _lines.ScoreRowYou : r.Name,
                    "vessel", r.Vessel.ToString());
                var row = Line(panel, 56f, _kit.Light, false, name, crystals ? r.Crystals.ToString() : string.Empty,
                    r.Kills.ToString(), r.Guarded.ToString(), r.Score.ToString());
                // One Image per row: a second AddComponent<Image> on the same object returns null, which threw here on
                // your own row and left every board without its buttons (Shombith, 2026-11-17: "no button to sail home").
                // Your row is the same fill lifted a touch toward the light.
                var fill = VoyageKit.Fade(_kit.Popup, r.You ? 0.85f : 0.6f);
                if (r.You) fill = Color.Lerp(fill, VoyageKit.Fade(_kit.Light, fill.a), 0.06f);
                VoyageKit.Block(row, fill);
                VoyageKit.Hairline(row, true, VoyageKit.Fade(_kit.Outline, 0.5f));
            }
        }

        /// <summary>A row of the seats table: the seat, then its crystals, kills, creatures felled near the Ark and score
        /// (Frogma Rev 113, Score; laid out to Rev 116 A).</summary>
        RectTransform Line(RectTransform parent, float height, Color color, bool head, string seat, params string[] numbers)
        {
            var row = VoyageKit.Node(head ? "Columns" : "Row", parent);
            var le = VoyageKit.Le(row);
            le.minHeight = le.preferredHeight = height;
            var h = VoyageKit.Row(row, 0f, TextAnchor.MiddleLeft, expandWidth: false);
            h.padding = new RectOffset(18, 18, 0, 0);
            TMP_Text Cell(string words, float width, TextAlignmentOptions align, bool flex)
            {
                var t = head ? _kit.Label(row, 14, color, align) : _kit.Text(row, 20, color, align);
                t.text = words ?? string.Empty;
                var cle = VoyageKit.Le(t);
                cle.preferredWidth = width;
                cle.flexibleWidth = flex ? 1f : 0f;
                return t;
            }
            Cell(seat, 360f, TextAlignmentOptions.Left, true);
            foreach (var n in numbers) Cell(n, 120f, TextAlignmentOptions.Right, false);
            return row;
        }

        /// <summary>The reward tiles under the table (Rev 116 A): the Ark's hull and the pay, side by side, with whether
        /// the haul is banked or still at stake under them.</summary>
        void Rewards(RectTransform frame, VoyageScoreboard b)
        {
            var tiles = VoyageKit.Node("Rewards", frame);
            VoyageKit.At(tiles, ColumnX, TilesY, ColumnWidth, 84f);
            VoyageKit.Row(tiles, 12f, TextAnchor.UpperLeft, expandWidth: true);

            var hull = Tile(tiles, _lines.ScoreHull,
                TemplateText.Fill(_lines.ScoreHullValue, "hull", Mathf.RoundToInt(b.Hull * 100f).ToString()), _kit.Light);
            var (fill, breach) = VoyageHudColors.Bar(_kit, hull, 6f, breach: true);
            VoyageHudColors.SetFill(fill, b.Hull);
            VoyageHudColors.SetBreach(breach, b.Breach);

            if (b.Pay > 0 || b.Board is VoyageScoreboard.Kind.CrystalRun or VoyageScoreboard.Kind.Escort)
                Tile(tiles, b.Board == VoyageScoreboard.Kind.Escort
                        ? TemplateText.Fill(_lines.ScoreEscortPay, "difficulty", b.Difficulty.ToString())
                        : _lines.ScorePay,
                    TemplateText.Fill(_lines.ScoreRewardValue, "count", b.Pay.ToString()), VoyageHudColors.Gold);
            if (b.Board == VoyageScoreboard.Kind.Escort && b.CrystalTotal > 0)
                Tile(tiles, _lines.ScoreStolen, TemplateText.Fill(_lines.ScoreRewardValue, "count", b.CrystalTotal.ToString()),
                    VoyageHudColors.Gold);

            if (b.Board != VoyageScoreboard.Kind.Lost)
            {
                var noteRt = VoyageKit.Node("Note", frame);
                VoyageKit.At(noteRt, ColumnX, TilesY + 90f, ColumnWidth, 28f);
                var note = _kit.Text(noteRt, 15, b.Banked ? _kit.CtaFill : _kit.Sub, TextAlignmentOptions.Center);
                VoyageKit.Stretch(note.rectTransform);
                note.text = b.Banked ? _lines.ScoreBanked : _lines.ScoreAtStake;
            }
        }

        RectTransform Tile(RectTransform row, string label, string value, Color valueColor)
        {
            var tile = VoyageKit.Node("Tile", row);
            VoyageKit.Block(tile, VoyageKit.Fade(_kit.Popup, 0.7f));
            var col = VoyageKit.Column(tile, 4f);
            col.padding = new RectOffset(16, 16, 12, 12);
            VoyageKit.Le(tile).flexibleWidth = 1f;
            _kit.Label(tile, 13, VoyageHudColors.Second).text = label;
            _kit.Text(tile, 26, valueColor, TextAlignmentOptions.Left).text = value;
            return tile;
        }

        /// <summary>The two buttons, centred under the column: the quiet one (Finish, Back) left, the call to action
        /// right, each with its caption under it.</summary>
        void Buttons(RectTransform frame, VoyageScoreboard b)
        {
            const float left = 960f - ButtonGap / 2f - ButtonWidth, right = 960f + ButtonGap / 2f;
            const float captionY = ButtonsY + 70f;
            Button primary = null;
            if (b.FinishHere != null)
            {
                ThreeWays(frame, b, captionY);
                return;
            }
            if (b.Venture == null && b.Finish == null && b.TryAgain != null)
            {
                // A lost Ark: back to Voyages (B, quiet) or the same voyage again (A, the call to action).
                var back = VoyageKit.Node("Back", frame);
                VoyageKit.At(back, left, ButtonsY, ButtonWidth, 64f);
                _kit.Quiet(back, _lines.ScoreFinishKey, _lines.ScoreDone, () => VoyageScoreboard.Choose(b.Close));
                Caption(frame, left - 60f, captionY, ButtonWidth + 60f, _lines.ScoreDoneLine, TextAlignmentOptions.Right);
                var again = VoyageKit.Node("TryAgain", frame);
                VoyageKit.At(again, right, ButtonsY, ButtonWidth, 64f);
                primary = _kit.Cta(again, _lines.ScoreVentureKey, _lines.ScoreTryAgain, () => VoyageScoreboard.Choose(b.TryAgain)).button;
                Caption(frame, right, captionY, ButtonWidth + 60f, _lines.ScoreTryAgainLine, TextAlignmentOptions.Left);
            }
            else if (b.Venture == null && b.Finish == null)
            {
                var done = VoyageKit.Node("Done", frame);
                VoyageKit.At(done, 960f - ButtonWidth / 2f, ButtonsY, ButtonWidth, 64f);
                primary = _kit.Cta(done, _lines.ScoreVentureKey, b.Following ? _lines.ScoreKeepFlying : _lines.ScoreDone,
                    () => VoyageScoreboard.Choose(b.Close)).button;
                // A party friend's board: the host chooses what next, and the choice reaches every friend's voyage.
                Caption(frame, 0f, captionY, 1920f, b.Following ? _lines.ScoreHostChooses : _lines.ScoreDoneLine, TextAlignmentOptions.Center);
            }
            // A missed Crystal Run: Finish (B, quiet) or Try again (A), the same goal from here.
            bool retry = b.Venture == null && b.TryAgain != null && b.Finish != null;
            bool both = b.Finish != null && (b.Venture != null || retry);
            if (b.Finish != null)
            {
                float x = both ? left : 960f - ButtonWidth / 2f;
                var finish = VoyageKit.Node("Finish", frame);
                VoyageKit.At(finish, x, ButtonsY, ButtonWidth, 64f);
                var quiet = _kit.Quiet(finish, _lines.ScoreFinishKey, _lines.ScoreFinish, () => VoyageScoreboard.Choose(b.Finish));
                primary ??= quiet;
                Caption(frame, both ? x - 60f : 0f, captionY, both ? ButtonWidth + 60f : 1920f, _lines.ScoreFinishLine,
                    both ? TextAlignmentOptions.Right : TextAlignmentOptions.Center);
            }
            if (b.Venture != null || retry)
            {
                float x = both ? right : 960f - ButtonWidth / 2f;
                var venture = VoyageKit.Node(retry ? "TryAgain" : "Venture", frame);
                VoyageKit.At(venture, x, ButtonsY, ButtonWidth, 64f);
                var choice = retry ? b.TryAgain : b.Venture;
                primary = _kit.Cta(venture, _lines.ScoreVentureKey, retry ? _lines.ScoreTryAgain : _lines.ScoreVenture,
                    () => VoyageScoreboard.Choose(choice)).button;
                string line = retry
                    ? TemplateText.Fill(_lines.ScoreRetryRaid, "goal", b.NextGoal.ToString(), "cells", b.NextCells.ToString())
                    : b.Board == VoyageScoreboard.Kind.Escort
                    ? TemplateText.Fill(_lines.ScoreVentureEscort, "difficulty", b.NextDifficulty.ToString())
                    : TemplateText.Fill(_lines.ScoreVentureRaid, "goal", b.NextGoal.ToString(), "cells", b.NextCells.ToString());
                Caption(frame, both ? x : 0f, captionY, both ? ButtonWidth + 60f : 1920f, line,
                    both ? TextAlignmentOptions.Left : TextAlignmentOptions.Center);
            }
            if (primary && UnityEngine.EventSystems.EventSystem.current)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(primary.gameObject);
        }

        /// <summary>A met Crystal Run (Shombith, 2026-11-17): Finish here (X, bank now), Sail home (B, the escort home pays
        /// more) and Venture further (A, the call to action), left to right; without a further run, the first two.</summary>
        void ThreeWays(RectTransform frame, VoyageScoreboard b, float captionY)
        {
            int count = b.Venture != null ? 3 : 2;
            float total = count * ButtonWidth + (count - 1) * ButtonGap;
            float x = 960f - total / 2f;
            float Next() { float at = x; x += ButtonWidth + ButtonGap; return at; }

            float hereX = Next();
            var here = VoyageKit.Node("FinishHere", frame);
            VoyageKit.At(here, hereX, ButtonsY, ButtonWidth, 64f);
            Button primary = _kit.Quiet(here, _lines.ScoreFinishHereKey, _lines.ScoreFinishHere, () => VoyageScoreboard.Choose(b.FinishHere));
            Caption(frame, hereX, captionY, ButtonWidth, _lines.ScoreFinishHereLine, TextAlignmentOptions.Center);

            float homeX = Next();
            var home = VoyageKit.Node("SailHome", frame);
            VoyageKit.At(home, homeX, ButtonsY, ButtonWidth, 64f);
            _kit.Quiet(home, _lines.ScoreFinishKey, _lines.ScoreSailHome, () => VoyageScoreboard.Choose(b.Finish));
            Caption(frame, homeX, captionY, ButtonWidth, TemplateText.Fill(_lines.ScoreSailHomeLine, "scale",
                b.HomeRewardScale.ToString("0.#")), TextAlignmentOptions.Center);

            if (b.Venture != null)
            {
                float ventureX = Next();
                var venture = VoyageKit.Node("Venture", frame);
                VoyageKit.At(venture, ventureX, ButtonsY, ButtonWidth, 64f);
                primary = _kit.Cta(venture, _lines.ScoreVentureKey, _lines.ScoreVenture, () => VoyageScoreboard.Choose(b.Venture)).button;
                Caption(frame, ventureX, captionY, ButtonWidth, TemplateText.Fill(_lines.ScoreVentureRaid, "goal",
                    b.NextGoal.ToString(), "cells", b.NextCells.ToString()), TextAlignmentOptions.Center);
            }
            if (primary && UnityEngine.EventSystems.EventSystem.current)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(primary.gameObject);
        }

        void Caption(RectTransform frame, float x, float y, float w, string words, TextAlignmentOptions align)
        {
            var rt = VoyageKit.Node("Caption", frame);
            VoyageKit.At(rt, x, y, w, 64f);
            var t = _kit.Text(rt, 15, _kit.Sub, align);
            VoyageKit.Stretch(t.rectTransform);
            t.text = words ?? string.Empty;
        }
    }
}

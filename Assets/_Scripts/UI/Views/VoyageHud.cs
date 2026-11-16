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
    /// The in-voyage HUD's objective strip (Frogma Rev 114 O1, picked by Shombith 2026-11-16: "the objective on the left
    /// was too big"; it was Rev 113's 600 x 330 box): a 420 x 120 strip 40 in from the top left, one thin left line, no
    /// glow. Line 1 is the goal's name and its figure (the hull for an escort and the way home, the count for a Crystal
    /// Run); then the hull bar with its breach line; then where the Ark is and whether it is hostile, with the route as
    /// dots (a Crystal Run shows its four element flowers with their counts there instead); then one job line, in the
    /// threat colour when it is one. The goal sentence, the time to the next world and the route's names are gone: the
    /// intro and the first toast say them. Toasts are the run's banner (<see cref="VoyageToasts"/>). It reads
    /// <see cref="VoyageObjective.Source"/> a few times a second; every word is <see cref="VoyageLines"/>', chosen by
    /// <see cref="VoyageObjectiveText"/>. It never takes a tap.
    /// </summary>
    public sealed class VoyageObjectiveBox : MonoBehaviour
    {
        const float RefreshSeconds = 0.25f;
        // Rev 114 O1, on the 1920 x 1080 frame.
        const float StripX = 40f, StripY = 40f, StripWidth = 420f;
        const float RouteWidth = 110f, RouteDot = 12f;

        VoyageHarbour _harbour;
        VoyageKit _kit;
        VoyageLines _lines;

        CanvasGroup _boxGroup;
        TMP_Text _title, _figure, _place, _placeRight, _action;
        RectTransform _hullBar, _breach, _route, _flowers;
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

            var box = VoyageKit.Node("ObjectiveStrip", root);
            VoyageKit.At(box, StripX, StripY, StripWidth, 0f);
            VoyageKit.Block(box, VoyageKit.Fade(_kit.Popup, 0.82f));
            var edge = VoyageKit.Node("Edge", box);
            VoyageKit.Le(edge).ignoreLayout = true;
            edge.anchorMin = Vector2.zero;
            edge.anchorMax = new Vector2(0f, 1f);
            edge.pivot = new Vector2(0f, 0.5f);
            edge.sizeDelta = new Vector2(2f, 0f);
            VoyageKit.Block(edge, _kit.Light);
            var col = VoyageKit.Column(box, 8f);
            col.padding = new RectOffset(18, 18, 14, 14);
            box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _boxGroup = box.gameObject.AddComponent<CanvasGroup>();
            _boxGroup.alpha = 0f;
            _boxGroup.blocksRaycasts = _boxGroup.interactable = false;

            // Line 1: the goal's name, and its figure on the right.
            var top = VoyageKit.Node("Top", box);
            VoyageKit.Row(top, 8f, TextAnchor.LowerLeft, expandWidth: true);
            _title = _kit.Label(top, 13, _kit.Light);
            _figure = _kit.Text(top, 18, _kit.Light, TextAlignmentOptions.BottomRight);

            // The hull bar and its breach line.
            (_hullBar, _breach) = VoyageHudColors.Bar(_kit, box, 8f, breach: true);

            // Line 3: where the Ark is, then the route's dots (or a Crystal Run's flowers, then where it is).
            var mid = VoyageKit.Node("Where", box);
            VoyageKit.Row(mid, 14f, TextAnchor.MiddleLeft, expandWidth: true);
            _flowers = VoyageKit.Node("Flowers", mid);
            VoyageKit.Row(_flowers, 14f);
            foreach (var e in VoyageRaid.Elements)
            {
                var cell = VoyageKit.Node(e.ToString(), _flowers);
                VoyageKit.Row(cell, 4f);
                var flower = VoyageKit.Flower(cell, e, VoyageHudColors.Second);
                var fle = VoyageKit.Le(flower);
                fle.minWidth = fle.preferredWidth = fle.minHeight = fle.preferredHeight = 16f;
                _split[e] = _kit.Text(cell, 13, VoyageHudColors.Second, TextAlignmentOptions.Left);
            }
            _place = _kit.Text(mid, 14, VoyageHudColors.Second, TextAlignmentOptions.Left);
            _placeRight = _kit.Text(mid, 13, VoyageHudColors.Second, TextAlignmentOptions.Right);
            _route = VoyageKit.Node("Route", mid);
            var rle = VoyageKit.Le(_route);
            rle.minWidth = rle.preferredWidth = RouteWidth;
            rle.minHeight = rle.preferredHeight = RouteDot;

            // Line 4: the one job line.
            _action = _kit.Text(box, 15, VoyageHudColors.Second, TextAlignmentOptions.TopLeft);
            _action.textWrappingMode = TextWrappingModes.Normal;
            _action.overflowMode = TextOverflowModes.Overflow;
        }

        /// <summary>The route as dots on a line (Rev 114 O1): done and current lit, the destination ringed in the call
        /// colour, the rest ringed.</summary>
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
            bool shown = source != null && !VoyageScoreboard.IsOpen && !VoyageIntro.IsPlaying &&
                         source.TryGetObjective(out var o) && Show(o);
            _boxGroup.alpha = Mathf.MoveTowards(_boxGroup.alpha, shown ? 1f : 0f, 1f);
        }

        bool Show(in VoyageObjective o)
        {
            if (o.Goal == VoyageObjective.Kind.None) return false;
            bool raid = o.Goal == VoyageObjective.Kind.CrystalRun;
            VoyageKit.Set(_title, VoyageObjectiveText.Title(_lines, o));
            VoyageKit.Set(_figure, VoyageObjectiveText.Figure(_lines, o));

            VoyageHudColors.SetFill(_hullBar, o.Hull);
            VoyageHudColors.SetBreach(_breach, o.Breach);

            string where = VoyageObjectiveText.Place(_lines, o);
            _flowers.gameObject.SetActive(raid);
            _placeRight.gameObject.SetActive(raid);
            _place.gameObject.SetActive(!raid);
            _route.gameObject.SetActive(!raid && o.Cells > 0);
            if (raid)
            {
                foreach (var e in VoyageRaid.Elements)
                    VoyageKit.Set(_split[e], TemplateText.Fill(_lines.ObjectiveStripElement, "count",
                        (o.Crystals != null ? o.Crystals[(int)e] : 0).ToString()));
                VoyageKit.Set(_placeRight, where);
            }
            else
            {
                VoyageKit.Set(_place, where);
                ShowRoute(o);
            }

            var (action, threat) = VoyageObjectiveText.StripAction(_lines, o);
            _action.gameObject.SetActive(!string.IsNullOrEmpty(action));
            VoyageKit.Set(_action, action);
            _action.color = threat ? VoyageHudColors.Threat : VoyageHudColors.Second;
            return true;
        }

        void ShowRoute(in VoyageObjective o)
        {
            if (o.Cells <= 0) return;
            if (o.Cells != _routeCells) BuildRoute(o.Cells);
            for (int i = 0; i < _dots.Count; i++)
            {
                bool last = i == _dots.Count - 1;
                bool reached = i <= o.Cell - 1;
                _dots[i].color = reached ? _kit.Light : last ? _kit.CtaFill : VoyageKit.Fade(_kit.Sub, 0.6f);
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
                if (o.Anchored && !string.IsNullOrEmpty(lines.ObjectiveStripAnchor))
                    return (TemplateText.Fill(lines.ObjectiveStripAnchor, "time", VoyageHudColors.Clock(o.AnchorSeconds)),
                        o.AnchorClosing);
                if (o.Carried > 0 && !string.IsNullOrEmpty(lines.ObjectiveStripCarried))
                    return (TemplateText.Fill(lines.ObjectiveStripCarried, "count", o.Carried.ToString()), false);
                return action.line == lines.ObjectiveHostile || action.line == lines.ObjectiveCalm
                    ? (string.Empty, false) : action;
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
            var backTo = b.Finish ?? (b.TryAgain != null ? b.Close : null);
            if (a) VoyageScoreboard.Choose(b.Venture ?? b.TryAgain ?? b.Close ?? b.Finish);
            else if (back && backTo != null) VoyageScoreboard.Choose(backTo);
        }

        // ── The page ────────────────────────────────────────────────────────

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
            VoyageKit.At(head, 0f, 110f, 1920f, 160f);
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
            _kit.Text(head, 56, _kit.Light, TextAlignmentOptions.Center).text = title;
            if (!string.IsNullOrEmpty(stats)) _kit.Text(head, 17, _kit.Sub, TextAlignmentOptions.Center).text = stats;
        }

        RectTransform Panel(RectTransform frame, string name, float x, float w)
        {
            var panel = VoyageKit.Node(name, frame);
            VoyageKit.At(panel, x, 300f, w, 470f);
            VoyageKit.Sliced(panel, _kit.Style.Sliver10Fill, _kit.RowPicked);
            var col = VoyageKit.Column(panel, 0f);
            col.padding = new RectOffset(32, 32, 30, 30);
            return panel;
        }

        void Seats(RectTransform frame, VoyageScoreboard b)
        {
            var panel = Panel(frame, "Seats", 120f, 1080f);
            var top = VoyageKit.Node("Hull", panel);
            VoyageKit.Row(top, 14f, TextAnchor.LowerLeft, expandWidth: true);
            _kit.Label(top, 15, VoyageHudColors.Second).text = _lines.ScoreHull;
            _kit.Text(top, 32, _kit.Light, TextAlignmentOptions.BottomRight).text =
                TemplateText.Fill(_lines.ScoreHullValue, "hull", Mathf.RoundToInt(b.Hull * 100f).ToString());
            var gap = VoyageKit.Node("Gap", panel);
            VoyageKit.Le(gap).preferredHeight = 14f;
            var (fill, breach) = VoyageHudColors.Bar(_kit, panel, 12f, breach: true);
            VoyageHudColors.SetFill(fill, b.Hull);
            VoyageHudColors.SetBreach(breach, b.Breach);
            var gap2 = VoyageKit.Node("Gap", panel);
            VoyageKit.Le(gap2).preferredHeight = 28f;

            bool crystals = b.Board != VoyageScoreboard.Kind.Escort;
            Line(panel, 30f, _kit.Sub, true, _lines.ScoreColSeat, crystals ? _lines.ScoreColCrystals : string.Empty,
                _lines.ScoreColKills, _lines.ScoreColGuarded, _lines.ScoreColScore);
            foreach (var r in b.Rows)
            {
                string name = TemplateText.Fill(_lines.ScoreRowSeat, "name", r.You && !string.IsNullOrEmpty(_lines.ScoreRowYou) ? _lines.ScoreRowYou : r.Name,
                    "vessel", r.Vessel.ToString());
                var row = Line(panel, 64f, _kit.Light, false, name, crystals ? r.Crystals.ToString() : string.Empty,
                    r.Kills.ToString(), r.Guarded.ToString(), r.Score.ToString());
                if (r.You) VoyageKit.Block(row, VoyageKit.Fade(_kit.Light, 0.05f));
                VoyageKit.Hairline(row, true, VoyageKit.Fade(_kit.Outline, 0.5f));
            }
        }

        /// <summary>A row of the seats table: the seat, then its crystals, kills, creatures felled near the Ark and score
        /// (Frogma Rev 113, Score).</summary>
        RectTransform Line(RectTransform parent, float height, Color color, bool head, string seat, params string[] numbers)
        {
            var row = VoyageKit.Node(head ? "Columns" : "Row", parent);
            var le = VoyageKit.Le(row);
            le.minHeight = le.preferredHeight = height;
            var h = VoyageKit.Row(row, 0f, TextAnchor.MiddleLeft, expandWidth: false);
            h.padding = new RectOffset(16, 16, 0, 0);
            TMP_Text Cell(string words, float width, TextAlignmentOptions align, bool flex)
            {
                var t = head ? _kit.Label(row, 14, color, align) : _kit.Text(row, 20, color, align);
                t.text = words ?? string.Empty;
                var cle = VoyageKit.Le(t);
                cle.preferredWidth = width;
                cle.flexibleWidth = flex ? 1f : 0f;
                return t;
            }
            Cell(seat, 420f, TextAlignmentOptions.Left, true);
            foreach (var n in numbers) Cell(n, 135f, TextAlignmentOptions.Right, false);
            return row;
        }

        void Rewards(RectTransform frame, VoyageScoreboard b)
        {
            var panel = Panel(frame, "Rewards", 1230f, 570f);
            ((VerticalLayoutGroup)panel.GetComponent<LayoutGroup>()).spacing = 18f;
            _kit.Label(panel, 15, VoyageHudColors.Second).text = _lines.ScoreRewards;

            int crystals = b.CrystalTotal;
            if (b.Board != VoyageScoreboard.Kind.Escort)
            {
                Reward(panel, _lines.ScoreStolen, crystals, VoyageHudColors.Gold);
                var parts = new List<string>();
                foreach (var e in VoyageRaid.Elements)
                    if (b.Crystals[(int)e] > 0)
                        parts.Add(TemplateText.Fill(_lines.ScoreStolenBy, "element", VoyageHudColors.ElementName(_lines, e),
                            "count", b.Crystals[(int)e].ToString()));
                if (parts.Count > 0)
                    _kit.Text(panel, 15, _kit.Sub, TextAlignmentOptions.Left).text = string.Join(_lines.ScoreStolenSeparator, parts);
            }
            if (b.Pay > 0 || b.Board is VoyageScoreboard.Kind.CrystalRun or VoyageScoreboard.Kind.Escort)
                Reward(panel, b.Board == VoyageScoreboard.Kind.Escort
                    ? TemplateText.Fill(_lines.ScoreEscortPay, "difficulty", b.Difficulty.ToString())
                    : _lines.ScorePay, b.Pay, VoyageHudColors.Gold);
            if (b.Board != VoyageScoreboard.Kind.Lost)
            {
                var note = _kit.Text(panel, 15, b.Banked ? _kit.CtaFill : _kit.Sub, TextAlignmentOptions.Left);
                note.textWrappingMode = TextWrappingModes.Normal;
                note.text = b.Banked ? _lines.ScoreBanked : _lines.ScoreAtStake;
            }
        }

        void Reward(RectTransform panel, string label, int count, Color color)
        {
            var row = VoyageKit.Node("Reward", panel);
            VoyageKit.Row(row, 12f, TextAnchor.MiddleLeft, expandWidth: true);
            _kit.Text(row, 21, _kit.Light, TextAlignmentOptions.Left).text = label;
            _kit.Text(row, 21, color, TextAlignmentOptions.Right).text =
                TemplateText.Fill(_lines.ScoreRewardValue, "count", count.ToString());
        }

        void Buttons(RectTransform frame, VoyageScoreboard b)
        {
            Button primary = null;
            if (b.Venture == null && b.Finish == null && b.TryAgain != null)
            {
                // A lost Ark: back to Voyages (B, quiet) or the same voyage again (A, the call to action).
                var back = VoyageKit.Node("Back", frame);
                VoyageKit.At(back, 120f, 820f, 380f, 64f);
                _kit.Quiet(back, _lines.ScoreFinishKey, _lines.ScoreDone, () => VoyageScoreboard.Choose(b.Close));
                Caption(frame, 524f, 820f, 460f, _lines.ScoreDoneLine, TextAlignmentOptions.Left);
                var again = VoyageKit.Node("TryAgain", frame);
                VoyageKit.At(again, 1420f, 820f, 380f, 64f);
                primary = _kit.Cta(again, _lines.ScoreVentureKey, _lines.ScoreTryAgain, () => VoyageScoreboard.Choose(b.TryAgain)).button;
                Caption(frame, 920f, 820f, 476f, _lines.ScoreTryAgainLine, TextAlignmentOptions.Right);
            }
            else if (b.Venture == null && b.Finish == null)
            {
                var done = VoyageKit.Node("Done", frame);
                VoyageKit.At(done, 770f, 820f, 380f, 64f);
                primary = _kit.Cta(done, _lines.ScoreVentureKey, _lines.ScoreDone, () => VoyageScoreboard.Choose(b.Close)).button;
                Caption(frame, 0f, 900f, 1920f, _lines.ScoreDoneLine, TextAlignmentOptions.Center);
            }
            if (b.Finish != null)
            {
                var finish = VoyageKit.Node("Finish", frame);
                VoyageKit.At(finish, 120f, 820f, 360f, 64f);
                var quiet = _kit.Quiet(finish, _lines.ScoreFinishKey, _lines.ScoreFinish, () => VoyageScoreboard.Choose(b.Finish));
                primary ??= quiet;
                Caption(frame, 504f, 820f, 480f, _lines.ScoreFinishLine, TextAlignmentOptions.Left);
            }
            if (b.Venture != null)
            {
                var venture = VoyageKit.Node("Venture", frame);
                VoyageKit.At(venture, 1420f, 820f, 380f, 64f);
                primary = _kit.Cta(venture, _lines.ScoreVentureKey, _lines.ScoreVenture, () => VoyageScoreboard.Choose(b.Venture)).button;
                string line = b.Board == VoyageScoreboard.Kind.Escort
                    ? TemplateText.Fill(_lines.ScoreVentureEscort, "difficulty", b.NextDifficulty.ToString())
                    : TemplateText.Fill(_lines.ScoreVentureRaid, "goal", b.NextGoal.ToString(), "cells", b.NextCells.ToString());
                Caption(frame, 920f, 820f, 476f, line, TextAlignmentOptions.Right);
            }
            if (primary && UnityEngine.EventSystems.EventSystem.current)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(primary.gameObject);
        }

        void Caption(RectTransform frame, float x, float y, float w, string words, TextAlignmentOptions align)
        {
            var rt = VoyageKit.Node("Caption", frame);
            VoyageKit.At(rt, x, y, w, 64f);
            var t = _kit.Text(rt, 16, _kit.Sub, align);
            VoyageKit.Stretch(t.rectTransform);
            t.text = words ?? string.Empty;
        }
    }
}

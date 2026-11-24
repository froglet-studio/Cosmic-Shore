using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The voyage's pause (Frogma Rev 134 Pause A, picked by Shombith 2026-11-24, "Pause A"): the chart framed on the lane
    /// in place of the pause window. Left, what is paused (the voyage's name, the crystals aboard against the goal, the cell
    /// and the sea) over Resume, the run's own choice (Complete and set sail), Settings and Leave voyage; right, the leg on
    /// the chart: the place it left, its cells along the lane with the one the Ark is in lit, the line sailed solid and the
    /// line ahead dashed, the Ark live on it with the time to the next cell, the events next door with their timers, the
    /// rival crews in the threat red with their courses, the stop; and a legend. In a party the pilot looks while the
    /// others sail on (the window's own rule: time is frozen only alone), so it redraws twice a second.
    /// <para>The shared <see cref="PauseMenu"/> opens it (<see cref="IPauseMenuStage"/>): its buttons call the menu's own
    /// Resume, Settings and Leave, so pausing and leaving behave exactly as they do everywhere else. Every word is
    /// <see cref="VoyageLines"/>' Pause* and Chart* lines.</para>
    /// </summary>
    public sealed class VoyagePauseChart : MonoBehaviour, IPauseMenuStage
    {
        const int SortingOrder = 24000;     // above the voyage HUD (20000-20001), below the intro and the scoreboard (25000)
        const float RefreshSeconds = 0.5f;

        // Rev 134 Pause, on the 1920 x 1080 frame.
        const float SideX = 60f, SideW = 360f, KickerY = 300f, ButtonsY = 480f, ButtonH = 56f, ButtonGap = 12f;
        const float ChartX = 520f, ChartY = 200f, ChartW = 1300f, ChartH = 720f;
        const float CellR = 110f, HereR = 130f, NeighbourR = 100f, NeighbourOff = 245f, RivalOff = 330f, ArkR = 18f;
        const float PlaceMark = 18f, LegendY = 1008f;

        VoyageKit _kit;
        VoyageLines _lines;
        RectTransform _root, _frame, _chart;
        TMP_Text _name, _count, _goal, _where, _party;
        Button _resume, _action, _settings, _leave;
        TMP_Text _actionLabel, _leaveLabel;
        PauseMenu _menu;
        bool _open, _covered;
        float _nextRefresh;

        readonly List<VoyageNeighbourFix> _neighbours = new();
        readonly List<VoyageRivalFix> _rivals = new();
        readonly List<Vector2> _scratch = new();

        public void Configure(VoyageHarbour harbour)
        {
            var content = harbour && harbour.Content ? harbour.Content : VoyageContentSO.Load();
            _lines = content ? content.Lines : new VoyageLines();
            _kit = new VoyageKit(VoyageBoardStyleSO.Load(), _lines, null);
            Build();
            _root.gameObject.SetActive(false);
            PauseMenuStage.Current = this;
        }

        void OnDestroy() => PauseMenuStage.Release(this);

        /// <summary>Only while a voyage is at sea; between voyages the pause is the window.</summary>
        public bool Wanted => VoyageScreenFeed.Source != null;

        // ── IPauseMenuStage ─────────────────────────────────────────────────

        public void Open(PauseMenu menu)
        {
            _menu = menu;
            _open = true;
            _covered = false;
            Show();
        }

        public void Close()
        {
            _open = false;
            _menu = null;
            if (_root) _root.gameObject.SetActive(false);
        }

        public void Cover(bool covered)
        {
            if (!_open) return;
            _covered = covered;
            _root.gameObject.SetActive(!covered);
            if (!covered) Focus(_settings ? _settings : _resume);
        }

        void Show()
        {
            _root.gameObject.SetActive(true);
            _nextRefresh = 0f;
            Refresh();
            Focus(_resume);
        }

        static void Focus(Selectable s)
        {
            var events = EventSystem.current;
            if (events && s && s.gameObject.activeInHierarchy) events.SetSelectedGameObject(s.gameObject);
        }

        void Update()
        {
            if (!_open || _covered) return;
            // B resumes (Rev 134's keycap). Start and Escape already toggle the pause through the HUD's pause button.
            var pad = Gamepad.current;
            if (pad != null && pad.buttonEast.wasPressedThisFrame && _menu)
            {
                _menu.OnClickResumeGameButton();
                return;
            }
            if (Time.unscaledTime >= _nextRefresh) Refresh();
        }

        // ── Build ───────────────────────────────────────────────────────────

        void Build()
        {
            var go = new GameObject("VoyagePauseChart", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _root = VoyageHudColors.Overlay(go, SortingOrder, hits: true);
            var dim = VoyageKit.Node("Dim", _root);
            VoyageKit.Stretch(dim);
            VoyageKit.Block(dim, VoyageKit.Fade(_kit.Popup, 0.97f), hit: true);
            var safe = VoyageKit.Node("Safe", _root);
            VoyageKit.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>().Apply();
            _frame = VoyageKit.Node("Frame", safe);
            VoyageKit.Fit(_frame, new Vector2(1920f, 1080f));

            _chart = VoyageKit.Node("Chart", _frame);
            VoyageKit.At(_chart, 0f, 0f, 1920f, 1080f);

            float y = KickerY;
            Place(_kit.Label(_frame, 20, VoyageHudColors.Second), SideX, y, SideW, 26f).text = _lines.PauseKicker;
            _name = Place(_kit.Text(_frame, 26, _kit.Light, TextAlignmentOptions.Left), SideX, y + 32f, SideW + 80f, 36f);
            var gem = Glyph(_frame, VoyageGlyph.Shape.Gem, _kit.Light);
            VoyageKit.At(gem.rectTransform, SideX, y + 80f, 16f, 20f);
            _count = Place(_kit.Text(_frame, 22, _kit.Light, TextAlignmentOptions.Left), SideX + 24f, y + 76f, 60f, 28f);
            _goal = Place(_kit.Text(_frame, 18, VoyageHudColors.Second, TextAlignmentOptions.Left), SideX + 66f, y + 78f, 260f, 26f);
            _where = Place(_kit.Text(_frame, 15, VoyageHudColors.Second, TextAlignmentOptions.Left), SideX, y + 116f, SideW + 80f, 22f);

            float by = ButtonsY;
            _resume = Button(by, _lines.PauseResumeKey, _lines.PauseResume, () => _menu?.OnClickResumeGameButton(), out _);
            by += ButtonH + ButtonGap;
            _action = Button(by, string.Empty, string.Empty, OnAction, out _actionLabel);
            by += ButtonH + ButtonGap;
            _settings = Button(by, string.Empty, _lines.PauseSettings, () => _menu?.OnClickSettings(), out _);
            by += ButtonH + ButtonGap;
            _leave = Button(by, string.Empty, _lines.PauseLeave, () => _menu?.OnClickMainMenu(), out _leaveLabel);
            _party = Place(_kit.Text(_frame, 15, VoyageHudColors.Second, TextAlignmentOptions.Left), SideX, by + ButtonH + 30f, SideW + 80f, 22f);

            Legend();
        }

        Button Button(float y, string key, string words, System.Action onClick, out TMP_Text label)
        {
            var rt = VoyageKit.Node("Button", _frame);
            VoyageKit.At(rt, SideX, y, SideW, ButtonH);
            var b = _kit.Quiet(rt, key, words, onClick, out label);
            // Rev 134's pause buttons read from the left, the keycap first.
            if (label && label.transform.parent.TryGetComponent<HorizontalLayoutGroup>(out var row))
            {
                row.childAlignment = TextAnchor.MiddleLeft;
                row.padding = new RectOffset(26, 18, 0, 0);
            }
            return b;
        }

        void OnAction()
        {
            var action = PauseMenuAction.Current;
            _menu?.OnClickResumeGameButton();
            action?.OnPauseAction();
        }

        /// <summary>Bottom right: what the chart's three marks mean.</summary>
        void Legend()
        {
            var row = VoyageKit.Node("Legend", _frame);
            VoyageKit.At(row, 960f, LegendY, 900f, 24f);
            VoyageKit.Row(row, 10f, TextAnchor.MiddleRight, expandWidth: false);
            void Item(VoyageGlyph.Shape shape, Color color, string words)
            {
                if (string.IsNullOrEmpty(words)) return;
                var g = Glyph(row, shape, color);
                var le = VoyageKit.Le(g);
                le.minWidth = le.preferredWidth = shape == VoyageGlyph.Shape.Ark ? 22f : 16f;
                le.minHeight = le.preferredHeight = shape == VoyageGlyph.Shape.Ark ? 11f : 16f;
                var t = _kit.Text(row, 15, VoyageHudColors.Second, TextAlignmentOptions.Left);
                t.text = words;
                t.margin = new Vector4(0f, 0f, 18f, 0f);
                VoyageKit.Le(t).preferredWidth = t.GetPreferredValues(words).x + 18f;
            }
            Item(VoyageGlyph.Shape.Ark, VoyageHudColors.Second, _lines.ChartLegendArk);
            Item(VoyageGlyph.Shape.Flag, VoyageHudColors.Second, _lines.ChartLegendEvent);
            Item(VoyageGlyph.Shape.Warn, VoyageHudColors.Threat, _lines.ChartLegendRival);
        }

        // ── Refresh ─────────────────────────────────────────────────────────

        void Refresh()
        {
            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            var source = VoyageScreenFeed.Source;
            var objectiveSource = VoyageObjective.Source;
            VoyageObjective o = default;
            bool haveObjective = objectiveSource != null && objectiveSource.TryGetObjective(out o);
            VoyageCourseFix fix = default;
            bool haveFix = source != null && source.TryGetCourseFix(out fix);

            VoyageKit.Set(_name, haveObjective ? o.Name : string.Empty);
            int done = haveObjective ? Mathf.Max(o.Done, o.AtStake) : 0;
            bool counts = haveObjective && o.Target > 0;
            _count.gameObject.SetActive(counts);
            _goal.gameObject.SetActive(counts);
            if (counts)
            {
                _count.text = TemplateText.Fill(_lines.PauseCount, "done", done.ToString());
                _goal.text = TemplateText.Fill(_lines.PauseGoal, "goal", o.Target.ToString());
                _goal.rectTransform.anchoredPosition = new Vector2(SideX + 30f + _count.GetPreferredValues(_count.text).x,
                    _goal.rectTransform.anchoredPosition.y);
            }
            int difficulty = source?.SeaDifficulty ?? 0;
            VoyageKit.Set(_where, haveFix && fix.Cells > 0
                ? TemplateText.Fill(difficulty > 0 ? _lines.PauseWhere : _lines.PauseWhereCalm, "cell", Mathf.Max(1, fix.Cell).ToString(),
                    "cells", fix.Cells.ToString(), "sea", SeaName(difficulty))
                : string.Empty);

            string action = PauseMenuAction.Label;
            _action.gameObject.SetActive(!string.IsNullOrEmpty(action));
            if (_actionLabel) _actionLabel.text = action;
            if (_leaveLabel && _menu) _leaveLabel.text = _menu.LeavesParty ? _lines.PauseLeaveParty : _lines.PauseLeave;
            VoyageKit.Set(_party, PauseMenu.SharedSession ? _lines.PauseParty : string.Empty);
            LinkButtons();

            for (int i = _chart.childCount - 1; i >= 0; i--) Destroy(_chart.GetChild(i).gameObject);
            if (!haveFix) return;
            _neighbours.Clear();
            _rivals.Clear();
            source.GetNeighbours(_neighbours);
            source.GetRivals(_rivals);
            DrawChart(fix, haveObjective ? o : default, source.CellSeconds);
        }

        void LinkButtons()
        {
            var chain = new List<Selectable>(4);
            foreach (var b in new Selectable[] { _resume, _action, _settings, _leave })
                if (b && b.gameObject.activeSelf) chain.Add(b);
            for (int i = 0; i < chain.Count; i++)
                chain[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = chain[(i + chain.Count - 1) % chain.Count],
                    selectOnDown = chain[(i + 1) % chain.Count],
                };
        }

        string SeaName(int difficulty) => VoyageScreenToasts.SeaName(_lines, difficulty);

        // ── The chart ───────────────────────────────────────────────────────

        void DrawChart(in VoyageCourseFix fix, in VoyageObjective o, float cellSeconds)
        {
            var content = VoyageContentSO.Load();
            var places = new List<VoyageChartLayout.Point>();
            var names = new List<string>();
            if (fix.Route != null && content != null && content.Content != null)
                foreach (var id in fix.Route)
                {
                    var at = content.Content.Location(id);
                    if (at == null) continue;
                    places.Add(new VoyageChartLayout.Point(at.ChartX, at.ChartY));
                    names.Add(at.Name);
                }
            // A leg the chart cannot place (no route, one place) still reads as a lane: a straight line across.
            if (places.Count < 2)
            {
                places.Clear();
                places.Add(new VoyageChartLayout.Point(0f, 0f));
                places.Add(new VoyageChartLayout.Point(1f, 0.15f));
                if (names.Count == 0) names.Add(string.Empty);
                names.Add(o.To ?? string.Empty);
            }
            // Fit inside the chart, leaving room round the lane for the cells and their neighbours.
            var line = VoyageChartLayout.Fit(places, ChartX + HereR + 40f, ChartY + NeighbourOff + 40f,
                ChartW - 2f * (HereR + 40f), ChartH - 2f * (NeighbourOff + 40f));
            int cells = Mathf.Max(1, fix.Cells);
            int here = Mathf.Clamp(fix.Cell, 1, cells);

            // The cells along the lane: sailed ones faint, the one the Ark is in lit, the ones ahead in outline.
            for (int c = 1; c <= cells; c++)
            {
                var at = ToV(VoyageChartLayout.Along(line, VoyageChartLayout.CellShare(c, cells)));
                bool lit = c == here;
                float r = lit ? HereR : CellR;
                if (lit)
                {
                    var glow = VoyageKit.Node("Glow", _chart);
                    Centre(glow, at, r * 2.4f);
                    VoyageKit.Glow(glow, VoyageKit.Fade(_kit.Light, 0.18f));
                }
                var ring = Ring(_chart, at, r, lit ? _kit.Light : VoyageKit.Fade(_kit.Sub, c < here ? 0.35f : 0.6f),
                    VoyageKit.Fade(_kit.Light, lit ? 0.07f : c < here ? 0.05f : 0.03f), lit ? 3f : 1.5f);
                ring.name = "Cell" + c;
                string cellName = VoyageObjectiveText.StopName(_lines, o, c - 1);
                Caption(at.x - 160f, at.y + r + 10f, 320f, cellName, c < here ? VoyageKit.Fade(_kit.Sub, 0.7f) : VoyageHudColors.Second,
                    TextAlignmentOptions.Center);
            }

            // The places the leg sails between: where it began (faint) and the stop.
            Diamond(line[0], names.Count > 0 ? names[0] : string.Empty, true);
            Diamond(line[line.Count - 1], TemplateText.Fill(_lines.ChartStop, "place", names.Count > 0 ? names[names.Count - 1] : o.To),
                false);

            // The line sailed (solid, fading in from the start) and the line ahead (dashed).
            float progress = Mathf.Clamp01(fix.Progress);
            var ark = VoyageChartLayout.Along(line, progress);
            Path(line, 0f, progress, VoyageKit.Fade(_kit.Light, 0.9f), 2.5f, 0f, 0f, 0.6f);
            Path(line, progress, 1f, VoyageKit.Fade(_kit.Light, 0.55f), 1.5f, 8f, 7f, 0f);

            // The Ark, live, with the time to the next cell.
            var arkPos = new Vector2(ark.X, ark.Y);
            Ring(_chart, arkPos, ArkR, _kit.Light, VoyageKit.Fade(_kit.Popup, 0.95f), 2f);
            var glyph = Glyph(_chart, VoyageGlyph.Shape.Ark, _kit.Light);
            Centre(glyph.rectTransform, arkPos, 22f, 11f);
            string you = _lines.ChartYou;
            if (cellSeconds > 0f && fix.Cells > 0)
            {
                float boundary = Mathf.Min(1f, Mathf.Floor(progress * fix.Cells + 1f) / fix.Cells);
                float left = (boundary - progress) * fix.Cells * cellSeconds;
                if (left > 0.5f) you = TemplateText.Fill(_lines.ChartYouNext, "time", VoyageHudColors.Clock(left));
            }
            Caption(arkPos.x + 22f, arkPos.y + 18f, 340f, you, _kit.Light, TextAlignmentOptions.Left);

            // The events next door, each in its own cell beside the lane, with its timer.
            foreach (var n in _neighbours)
            {
                if (n.Kind == VoyageNeighbourKind.Rival) continue;
                var p = VoyageChartLayout.Beside(line, VoyageChartLayout.CellShare(n.Cell, cells), n.Bearing, NeighbourOff);
                var at = new Vector2(p.X, p.Y);
                Ring(_chart, at, NeighbourR, VoyageKit.Fade(_kit.Sub, 0.55f), VoyageKit.Fade(_kit.Light, 0.03f), 1.5f);
                Marked(at + new Vector2(-46f, -16f), ShapeOf(n.Kind), _kit.Light, string.IsNullOrEmpty(n.Title) ? KindName(n.Kind) : n.Title,
                    n.SecondsLeft > 0f
                        ? TemplateText.Fill(_lines.ChartTimer, "time", VoyageHudColors.Clock(n.SecondsLeft), "verb", VerbName(n.Verb))
                        : VerbName(n.Verb), VoyageHudColors.Second);
            }

            // The rival crews, in the threat red, with the line of their course.
            foreach (var r in _rivals)
            {
                var p = VoyageChartLayout.Beside(line, VoyageChartLayout.CellShare(Mathf.Clamp(r.Cell, 1, cells), cells), r.Bearing, RivalOff);
                var at = new Vector2(p.X, p.Y);
                var toward = r.Act is VoyageRivalAct.Racing or VoyageRivalAct.Attacking ? arkPos : (Vector2?)null;
                if (toward is { } aim) Path(new List<VoyageChartLayout.Point> { new(at.x, at.y), new(aim.x, aim.y) }, 0f, 0.55f,
                    VoyageKit.Fade(VoyageHudColors.Threat, 0.8f), 1.5f, 4f, 5f, 0f);
                if (r.Kind == VoyageRivalKind.Ark)
                {
                    var stop = line[line.Count - 1];
                    Path(new List<VoyageChartLayout.Point> { new(at.x, at.y), stop }, 0f, 1f,
                        VoyageKit.Fade(VoyageHudColors.Threat, 0.7f), 1.5f, 6f, 6f, 0f);
                }
                Marked(at, r.Kind == VoyageRivalKind.Ark ? VoyageGlyph.Shape.Ark : VoyageGlyph.Shape.Warn, VoyageHudColors.Threat,
                    TemplateText.Fill(r.Kind == VoyageRivalKind.Ark ? _lines.ChartRivalArk : _lines.ChartRunaway, "name", r.Name),
                    RivalLine(r), VoyageHudColors.Threat);
            }
        }

        string RivalLine(VoyageRivalFix r) => r.Act switch
        {
            VoyageRivalAct.Racing => TemplateText.Fill(_lines.ChartRivalRacing, "place", r.BoundFor),
            VoyageRivalAct.Attacking => _lines.ChartRivalAttacking,
            VoyageRivalAct.Routed => _lines.ChartRivalRouted,
            VoyageRivalAct.Broken => _lines.ChartRivalBroken,
            _ => r.Kind == VoyageRivalKind.Ark
                ? TemplateText.Fill(_lines.ChartRivalArkLine, "place", r.BoundFor, "hold", r.Hold.ToString())
                : _lines.ChartRunawayLine,
        };

        static VoyageGlyph.Shape ShapeOf(VoyageNeighbourKind kind) => kind switch
        {
            VoyageNeighbourKind.Bloom => VoyageGlyph.Shape.Gem,
            VoyageNeighbourKind.Distress => VoyageGlyph.Shape.Flare,
            VoyageNeighbourKind.Predators => VoyageGlyph.Shape.Pack,
            VoyageNeighbourKind.Rival => VoyageGlyph.Shape.Warn,
            _ => VoyageGlyph.Shape.Flag,
        };

        string KindName(VoyageNeighbourKind kind) => kind switch
        {
            VoyageNeighbourKind.Wreck => _lines.ChartWreck,
            VoyageNeighbourKind.Bloom => _lines.ChartBloom,
            VoyageNeighbourKind.Distress => _lines.ChartDistress,
            VoyageNeighbourKind.Predators => _lines.ChartPredators,
            _ => _lines.ChartRivalCrew,
        };

        public static string VerbName(VoyageLines lines, WorkVerb verb) => verb switch
        {
            WorkVerb.Arrive => lines.VerbArrive,
            WorkVerb.Stow => lines.VerbStow,
            WorkVerb.Hold => lines.VerbHold,
            WorkVerb.Gather => lines.VerbGather,
            WorkVerb.Cull => lines.VerbCull,
            WorkVerb.Lay => lines.VerbLay,
            WorkVerb.Raze => lines.VerbRaze,
            WorkVerb.Observe => lines.VerbObserve,
            WorkVerb.Meet => lines.VerbMeet,
            _ => string.Empty,
        };

        string VerbName(WorkVerb verb) => VerbName(_lines, verb);

        // ── Drawing helpers (chart units: the 1920 x 1080 frame, y down) ─────

        static TMP_Text Place(TMP_Text t, float x, float y, float w, float h)
        {
            VoyageKit.At(t.rectTransform, x, y, w, h);
            return t;
        }

        static void Centre(RectTransform rt, Vector2 at, float size) => Centre(rt, at, size, size);

        static void Centre(RectTransform rt, Vector2 at, float w, float h) => VoyageKit.At(rt, at.x - w * 0.5f, at.y - h * 0.5f, w, h);

        VoyageRing Ring(RectTransform parent, Vector2 at, float radius, Color line, Color fill, float width)
        {
            var rt = VoyageKit.Node("Ring", parent);
            Centre(rt, at, radius * 2f);
            var ring = rt.gameObject.AddComponent<VoyageRing>();
            ring.Width = width;
            ring.Set(line, fill);
            return ring;
        }

        void Diamond(VoyageChartLayout.Point p, string name, bool faint)
        {
            var at = new Vector2(p.X, p.Y);
            var rt = VoyageKit.Node("Place", _chart);
            Centre(rt, at, PlaceMark);
            var mark = rt.gameObject.AddComponent<VoyageRing>();
            mark.Diamond = true;
            mark.Width = 2f;
            var c = faint ? VoyageKit.Fade(_kit.Sub, 0.5f) : _kit.Light;
            mark.Set(c, faint ? VoyageKit.Fade(c, 0.6f) : VoyageKit.Clear);
            Caption(at.x + 16f, at.y - 12f, 320f, name, faint ? VoyageKit.Fade(_kit.Sub, 0.6f) : _kit.Light, TextAlignmentOptions.Left);
        }

        static VoyageGlyph Glyph(Transform parent, VoyageGlyph.Shape shape, Color color)
        {
            var rt = VoyageKit.Node(shape.ToString(), parent);
            var g = rt.gameObject.AddComponent<VoyageGlyph>();
            g.Form = shape;
            g.color = color;
            g.raycastTarget = false;
            return g;
        }

        /// <summary>A mark in a small square with its two lines beside it (Rev 134 Pause: "Bloom / 0:31 left · Gather").</summary>
        void Marked(Vector2 at, VoyageGlyph.Shape shape, Color color, string title, string line, Color lineColor)
        {
            var box = VoyageKit.Node("Mark", _chart);
            Centre(box, at, 28f);
            var fill = VoyageKit.Node("Fill", box);
            VoyageKit.Stretch(fill);
            VoyageKit.Block(fill, VoyageKit.Fade(_kit.Popup, 0.9f));
            var frame = VoyageKit.Node("Edge", box);
            VoyageKit.Stretch(frame);
            var outline = frame.gameObject.AddComponent<VoyageOutline>();
            outline.raycastTarget = false;
            outline.Solid(color);
            var g = Glyph(box, shape, color);
            VoyageKit.At(g.rectTransform, 6f, 6f, 16f, 16f);
            Caption(at.x + 24f, at.y - 18f, 360f, title, color, TextAlignmentOptions.Left, 16);
            Caption(at.x + 24f, at.y + 1f, 360f, line, lineColor, TextAlignmentOptions.Left, 14);
        }

        void Caption(float x, float y, float w, string words, Color color, TextAlignmentOptions align, float size = 15f)
        {
            if (string.IsNullOrEmpty(words)) return;
            var t = _kit.Text(_chart, size, color, align);
            VoyageKit.At(t.rectTransform, x, y, w, size + 8f);
            t.text = words;
        }

        void Path(IReadOnlyList<VoyageChartLayout.Point> line, float from, float to, Color color, float width, float dash, float gap, float fadeIn)
        {
            if (to - from <= 1e-3f) return;
            _scratch.Clear();
            _scratch.Add(ToV(VoyageChartLayout.Along(line, from)));
            float total = VoyageChartLayout.Length(line), walked = 0f;
            for (int i = 1; i < line.Count; i++)
            {
                walked += Vector2.Distance(ToV(line[i - 1]), ToV(line[i]));
                float share = total <= 0f ? 1f : walked / total;
                if (share > from && share < to) _scratch.Add(ToV(line[i]));
            }
            _scratch.Add(ToV(VoyageChartLayout.Along(line, to)));
            var rt = VoyageKit.Node("Line", _chart);
            VoyageKit.At(rt, 0f, 0f, 1920f, 1080f);
            var g = rt.gameObject.AddComponent<VoyageChartLine>();
            g.color = color;
            g.Set(_scratch, width, dash, gap, fadeIn);
        }

        static Vector2 ToV(VoyageChartLayout.Point p) => new(p.X, p.Y);
    }
}

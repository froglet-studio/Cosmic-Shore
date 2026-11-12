using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The open voyage (Shombith, 2026-11-02, Docs/MENU_REDESIGN_B.md section 119): a voyage card on the Voyage window
    /// slides this open over the voyage cards, and so does the voyage's place pressed on the HyperSea map. It is the
    /// picked design, Rev 44 I (the squad leads the card, its presets beside its formation) in the Rev 49 C look (the
    /// real course on the HyperSea map behind the card, <see cref="VoyageRoutePoster"/>), with three levels:
    /// <list type="number">
    /// <item>the voyage: where it goes, who found it, three numbers, the course's two ends lit, the squad with its presets
    /// (the Ark's squads, <see cref="SquadBook"/>), Set sail;</item>
    /// <item>Ark crew: the squad's three seats and four settings each (vessel, pilot, element, artifact), pressed to change
    /// under the Ark's own rules (<c>SquadsPage</c>: your own seat, and the AI or open seats of a squad you made), and
    /// Save squad;</item>
    /// <item>the story: one beat at a time (the news, the place, who brought it, the water).</item>
    /// </list>
    /// <see cref="VoyageJobsView"/> builds it once and fills it; every word is the voyage content's
    /// (<see cref="VoyageLines"/>) or the game's data, never this class's.
    ///
    /// <para><b>Pilots are people, artifacts one of a kind</b> (Garrett 2026-11-07, Docs/Pilots/INVENTORY.md). Pressing a
    /// seat's pilot cycles the people in the player's crew whose callsign can fly the hull, and its artifact cycles the
    /// hoard's artifacts the hull can carry (and none); each writes the record's own id into the slot, as the Ark's
    /// picker does. A seat whose ids are the player's shows the person's name (its callsign word on the setting's label)
    /// and the artifact's name; a joined squad's other seats carry the owner's ids, which this crew does not hold, so they
    /// show the callsign and kind from the catalog. A legacy slot (a bare callsign or kind id saved before 2026-11-07)
    /// cycles on from the person or artifact it resolves to.</para>
    /// </summary>
    public class VoyageOpenCard : MonoBehaviour
    {
        /// <summary>What one open voyage shows (filled by <see cref="VoyageJobsView"/> from the job and its course).</summary>
        public sealed class Model
        {
            public string Pager, Kicker, Title, CourseChip, FoundBy;
            public string CourseValue, SailValue, IntensityValue, Cells, From, To;
            /// <summary>The raw numbers (no template), for the Voyages board's rows.</summary>
            public string LightYears, Minutes, Intensity;
            public readonly List<Vector2> Course = new();
            public readonly List<VoyageCardRules.Beat> Beats = new();
            public Action SetSail;
        }

        // The style foundation's colours (Docs/STYLE_FOUNDATION.md v0.3.3) and the map's course colour, each painted as
        // the worn colour set has it (ColorTrace, Docs/VesselModels/COLOR_TRACE.md).
        static readonly Color LightAuthored = new(0.902f, 0.914f, 1f, 1f);
        static Color Light => ColorTrace.Retrace(ColorTrace.Ui, LightAuthored);
        static readonly Color SubAuthored = new(0.604f, 0.627f, 0.8f, 1f);
        static Color Sub => ColorTrace.Retrace(ColorTrace.Ui, SubAuthored);
        static readonly Color LabAuthored = new(0.549f, 0.576f, 0.78f, 1f);
        static Color Lab => ColorTrace.Retrace(ColorTrace.Ui, LabAuthored);
        static readonly Color RouteAuthored = new(0.784f, 1f, 0.353f, 1f);
        static Color Route => ColorTrace.Retrace(ColorTrace.Ui, RouteAuthored);
        static readonly Color ArkAuthored = new(1f, 0.78f, 0.25f, 1f);
        static Color Ark => ColorTrace.Retrace(ColorTrace.Ui, ArkAuthored);
        static readonly Color GroundAuthored = new(0.024f, 0.039f, 0.149f, 1f);
        static Color Ground => ColorTrace.Retrace(ColorTrace.Ui, GroundAuthored);
        static readonly Color PlateAuthored = new(0f, 0.004f, 0.039f, 0.6f);
        static Color Plate => ColorTrace.Retrace(ColorTrace.Ui, PlateAuthored);
        static readonly Color EdgeAuthored = new(0.169f, 0.196f, 0.4f, 1f);
        static Color Edge => ColorTrace.Retrace(ColorTrace.Ui, EdgeAuthored);
        static readonly Color PickedAuthored = new(0.902f, 0.914f, 1f, 0.12f);
        static Color Picked => ColorTrace.Retrace(ColorTrace.Ui, PickedAuthored);
        static readonly Color IdleAuthored = new(0f, 0.004f, 0.039f, 0.35f);
        static Color Idle => ColorTrace.Retrace(ColorTrace.Ui, IdleAuthored);
        static readonly Color SeaAuthored = new(0.75f, 0.75f, 0.8f, 1f);
        static Color Sea => ColorTrace.Retrace(ColorTrace.Ui, SeaAuthored);
        static readonly Element[] Elements = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        const float Slide = 0.32f;

        public event Action Closed;
        public event Action<int> Step;
        public event Action SquadChanged;

        VoyageLines _lines;
        VoyageMapGraphic _map;
        SO_VesselList _vessels;
        PilotArtifactCatalogSO _catalog;
        TMP_Text _font;
        Button _buttonFrom;
        RectTransform _root, _panel;
        CanvasGroup _group;
        VoyageRoutePoster _poster;
        Model _model;
        int _level = 1, _beat;
        bool _open;
        // Ark crew edits a copy of the active squad, as the Ark does (SquadsPage's draft): Save squad writes it back, and
        // closing the card or picking another squad drops it.
        SavedSquad _draft;
        int _draftIndex = -1;
        bool Drafting => _draft != null;
        float _t, _from, _to;

        // Level parts.
        GameObject _l1, _l2, _l3;
        TMP_Text _pager, _chipCourse, _chipFound, _kicker, _title, _course, _sail, _intensity, _cells, _from_, _toName;
        TMP_Text _squadName, _crewName, _crewNote, _beatLabel, _beatLine, _beatBy, _saveLabel;
        readonly TMP_Text[] _crumbs = new TMP_Text[3];
        readonly Image[] _formation = new Image[3];
        readonly List<(Button b, TMP_Text t, Image fill)> _presets = new();
        TMP_Text _noSquad;
        readonly Seat[] _seats = new Seat[3];
        readonly List<Image> _dots = new();
        RectTransform _sweep, _routeLine;
        Image _endNode;
        Button _sail1, _sail2, _sail3, _save;
        // Read on each press of a pilot or artifact setting (reused, not allocated per press).
        readonly List<PilotRecord> _crew = new();
        readonly List<ArtifactRecord> _hoard = new();
        readonly List<string> _ids = new();

        sealed class Seat
        {
            public TMP_Text Role, Who;
            public Image Hull;
            public readonly (Button b, TMP_Text label, TMP_Text value)[] Settings = new (Button, TMP_Text, TMP_Text)[4];
        }

        // ── Building ─────────────────────────────────────────────────────────

        /// <summary>The card, built once over <paramref name="column"/> (it covers that column while open), its words
        /// from <paramref name="lines"/>, its text in <paramref name="font"/>'s face, its big buttons copies of
        /// <paramref name="button"/>, and its ground <paramref name="map"/>'s water.</summary>
        public static VoyageOpenCard Create(RectTransform column, TMP_Text font, Button button, VoyageMapGraphic map,
            SO_VesselList vessels, VoyageLines lines)
        {
            var go = new GameObject("OpenVoyage", typeof(RectTransform));
            go.transform.SetParent(column.parent, false);
            go.transform.SetAsLastSibling();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = column.anchorMin;
            rt.anchorMax = column.anchorMax;
            rt.pivot = column.pivot;
            rt.anchoredPosition = column.anchoredPosition;
            rt.sizeDelta = column.sizeDelta;
            go.AddComponent<RectMask2D>();
            var card = go.AddComponent<VoyageOpenCard>();
            card._root = rt;
            card._font = font;
            card._buttonFrom = button;
            card._map = map;
            card._vessels = vessels;
            card._lines = lines;
            card._catalog = PilotArtifactCatalogSO.Load();
            card.Build();
            return card;
        }

        /// <summary>The map whose water the card's ground shows (it is made after the card on a first draw).</summary>
        public void SetMap(VoyageMapGraphic map) => _map = map;

        void Build()
        {
            _panel = Node("Panel", _root);
            Stretch(_panel);
            _group = _panel.gameObject.AddComponent<CanvasGroup>();
            Fill(_panel, Ground);
            var posterGo = Node("Course", _panel);
            Stretch(posterGo);
            _poster = posterGo.gameObject.AddComponent<VoyageRoutePoster>();
            // The course shows through at the top and darkens toward the words below (Rev 49 C).
            var shade = Node("Shade", _panel);
            Stretch(shade);
            Fill(shade, new Color(Ground.r, Ground.g, Ground.b, 0.35f));
            var foot = Node("Foot", _panel);
            foot.anchorMin = Vector2.zero;
            foot.anchorMax = new Vector2(1f, 0.58f);
            foot.offsetMin = foot.offsetMax = Vector2.zero;
            Fill(foot, new Color(Ground.r, Ground.g, Ground.b, 0.9f));
            var border = Node("Border", _panel);
            Stretch(border);
            var outline = border.gameObject.AddComponent<Outline>();
            Fill(border, new Color(0, 0, 0, 0)).raycastTarget = false;
            outline.effectColor = Edge;
            outline.effectDistance = new Vector2(1, -1);

            var content = Node("Content", _panel);
            Stretch(content);
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(24, 24, 18, 22);
            v.spacing = 10;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            // Shared head: back, the pager, the three levels.
            var head = Row(content, 40, 10);
            MakeButton(head, _lines.CardBack, 15, Plate, () => Closed?.Invoke(), 150);
            Spacer(head);
            MakeButton(head, _lines.CardPrevious, 18, Plate, () => Step?.Invoke(-1), 44);
            _pager = Text(head, 14, Sub, TextAlignmentOptions.Center, 190);
            MakeButton(head, _lines.CardNext, 18, Plate, () => Step?.Invoke(+1), 44);
            var crumbs = Row(content, 26, 22);
            string[] names = { _lines.CardLevelVoyage, _lines.CardLevelCrew, _lines.CardLevelStory };
            for (int i = 0; i < 3; i++)
            {
                int level = i + 1;
                _crumbs[i] = MakeButton(crumbs, names[i], 13, new Color(0, 0, 0, 0), () => ShowLevel(level), 130).t;
            }
            Spacer(crumbs);

            _l1 = Level(content, "Voyage");
            _l2 = Level(content, "ArkCrew");
            _l3 = Level(content, "Story");
            BuildVoyage(_l1.transform);
            BuildCrew(_l2.transform);
            BuildStory(_l3.transform);
            _group.alpha = 0f;
            _group.interactable = _group.blocksRaycasts = false;
            _panel.anchoredPosition = new Vector2(PanelWidth + 40f, 0f);
        }

        void BuildVoyage(Transform l)
        {
            var chips = Row(l, 30, 10);
            _chipCourse = Chip(chips, Route);
            Spacer(chips);
            _chipFound = Chip(chips, Light);
            Spacer(l, true);
            _kicker = Text(l, 13, Lab, TextAlignmentOptions.Left);
            Height(_kicker, 18);
            _title = Text(l, 32, Light, TextAlignmentOptions.BottomLeft);
            _title.textWrappingMode = TextWrappingModes.Normal;
            Height(_title, 80);

            var nums = Row(l, 58, 34);
            _course = Number(nums, _lines.CardCourseLabel);
            _sail = Number(nums, _lines.CardSailLabel);
            _intensity = Number(nums, _lines.CardIntensityLabel);
            Spacer(nums);

            // The course's two ends, lit, with a light sweeping from the Ark to where it goes.
            var route = Node("Route", l);
            Height(route, 62);
            _routeLine = Node("Line", route);
            _routeLine.anchorMin = new Vector2(0f, 1f);
            _routeLine.anchorMax = new Vector2(1f, 1f);
            _routeLine.offsetMin = new Vector2(8f, -9f);
            _routeLine.offsetMax = new Vector2(-8f, -7f);
            Fill(_routeLine, new Color(Route.r, Route.g, Route.b, 0.5f));
            _sweep = Node("Sweep", _routeLine);
            _sweep.anchorMin = new Vector2(0f, 0.5f);
            _sweep.anchorMax = new Vector2(0f, 0.5f);
            _sweep.sizeDelta = new Vector2(90f, 8f);
            Fill(_sweep, new Color(Route.r, Route.g, Route.b, 0.85f));
            Node4(route, Ark, false);
            _endNode = Node4(route, Route, true);
            _from_ = EndLabel(route, false, _lines.CardYourArk, Ark);
            _toName = EndLabel(route, true, _lines.CardDestination, Route);
            _cells = Text(route, 12, Sub, TextAlignmentOptions.Center);
            var crt = _cells.rectTransform;
            crt.anchorMin = new Vector2(0.5f, 1f);
            crt.anchorMax = new Vector2(0.5f, 1f);
            crt.sizeDelta = new Vector2(260f, 20f);
            crt.anchoredPosition = new Vector2(0f, -28f);

            // The squad: its name and formation, its presets beside it.
            var squad = Row(l, 168, 16);
            var left = Column(squad, 4);
            Flex(left);
            var label = Text(left, 12, Lab, TextAlignmentOptions.Left);
            label.text = _lines.CardSquadLabel ?? string.Empty;
            Height(label, 16);
            _squadName = Text(left, 22, Light, TextAlignmentOptions.Left);
            Height(_squadName, 30);
            var form = Node("Formation", left);
            Height(form, 110);
            Vector2[] at = { new(0.5f, 0.78f), new(0.2f, 0.32f), new(0.8f, 0.32f) };
            for (int i = 0; i < 3; i++)
            {
                var h = Node("Hull" + i, form);
                h.anchorMin = h.anchorMax = at[i];
                h.sizeDelta = i == 0 ? new Vector2(170f, 58f) : new Vector2(140f, 48f);
                _formation[i] = h.gameObject.AddComponent<Image>();
                _formation[i].preserveAspect = true;
                _formation[i].raycastTarget = false;
            }
            var right = Column(squad, 6);
            Width(right, 230);
            var plabel = Text(right, 12, Light, TextAlignmentOptions.Left);
            plabel.text = _lines.CardPresetsLabel ?? string.Empty;
            Height(plabel, 16);
            for (int i = 0; i < SquadBook.MaxSquads; i++)
            {
                int index = i;
                var p = MakeButton(right, string.Empty, 14, Idle, () => PickSquad(index), 230);
                Height(p.b.transform, 26);
                _presets.Add(p);
            }
            _noSquad = Text(right, 13, Sub, TextAlignmentOptions.TopLeft);
            _noSquad.textWrappingMode = TextWrappingModes.Normal;
            _noSquad.text = _lines.CardNoSquad ?? string.Empty;
            Height(_noSquad, 40);
            var cfg = MakeButton(right, _lines.CardConfigure, 12, Plate, () => ShowLevel(2), 230);
            Height(cfg.b.transform, 30);

            var buttons = Row(l, 48, 10);
            _sail1 = BigButton(buttons, _lines.CardSetSail, 2f, () => _model?.SetSail?.Invoke());
            BigButton(buttons, _lines.CardCrew, 1f, () => ShowLevel(2));
            BigButton(buttons, _lines.CardStory, 1f, () => ShowLevel(3));
        }

        void BuildCrew(Transform l)
        {
            var head = Text(l, 13, Lab, TextAlignmentOptions.Left);
            head.text = _lines.CardCrewHead ?? string.Empty;
            Height(head, 18);
            _crewName = Text(l, 28, Light, TextAlignmentOptions.Left);
            Height(_crewName, 36);
            _crewNote = Text(l, 13, Sub, TextAlignmentOptions.Left);
            _crewNote.textWrappingMode = TextWrappingModes.Normal;
            Height(_crewNote, 34);
            var seats = Row(l, 380, 12);
            string[] settings = { _lines.CardSettingVessel, _lines.CardSettingPilot, _lines.CardSettingElement, _lines.CardSettingArtifact };
            for (int s = 0; s < 3; s++)
            {
                var col = Column(seats, 6);
                Flex(col);
                var seat = _seats[s] = new Seat();
                seat.Role = Text(col, 11, Lab, TextAlignmentOptions.Left);
                Height(seat.Role, 14);
                seat.Who = Text(col, 17, Light, TextAlignmentOptions.Left);
                Height(seat.Who, 22);
                var hull = Node("Hull", col);
                Height(hull, 84);
                seat.Hull = hull.gameObject.AddComponent<Image>();
                seat.Hull.preserveAspect = true;
                seat.Hull.raycastTarget = false;
                for (int k = 0; k < 4; k++)
                {
                    int seatIndex = s, setting = k;
                    var b = MakeButton(col, string.Empty, 15, Idle, () => ChangeSetting(seatIndex, setting), 0);
                    Height(b.b.transform, 50);
                    b.t.alignment = TextAlignmentOptions.BottomLeft;
                    b.t.margin = new Vector4(10, 0, 10, 6);
                    var label = Text(b.b.transform, 10, Lab, TextAlignmentOptions.TopLeft);
                    label.text = settings[k] ?? string.Empty;
                    label.margin = new Vector4(10, 6, 10, 0);
                    Stretch(label.rectTransform);
                    seat.Settings[k] = (b.b, label, b.t);
                }
            }
            Spacer(l, true);
            var buttons = Row(l, 48, 10);
            _sail2 = BigButton(buttons, _lines.CardSetSail, 2f, () => _model?.SetSail?.Invoke());
            _save = BigButton(buttons, _lines.CardSaveSquad, 1.2f, SaveSquad);
            _saveLabel = _save.GetComponentInChildren<TMP_Text>(true);
            BigButton(buttons, _lines.CardVoyage, 1f, () => ShowLevel(1));
        }

        void BuildStory(Transform l)
        {
            var head = Row(l, 24, 10);
            var h = Text(head, 13, Lab, TextAlignmentOptions.Left, 80);
            h.text = _lines.CardStoryHead ?? string.Empty;
            var opt = Text(head, 11, Sub, TextAlignmentOptions.Left, 100);
            opt.text = _lines.CardStoryOptional ?? string.Empty;
            Spacer(head);
            Spacer(l, true);
            _beatLabel = Text(l, 13, Route, TextAlignmentOptions.Left);
            Height(_beatLabel, 18);
            _beatLine = Text(l, 32, Light, TextAlignmentOptions.TopLeft);
            _beatLine.textWrappingMode = TextWrappingModes.Normal;
            Height(_beatLine, 170);
            _beatBy = Text(l, 13, Sub, TextAlignmentOptions.Left);
            Height(_beatBy, 18);
            Spacer(l, true);
            var nav = Row(l, 32, 12);
            Spacer(nav);
            MakeButton(nav, _lines.CardPrevious, 18, Plate, () => StepBeat(-1), 44);
            for (int i = 0; i < 4; i++)
            {
                var d = Node("Dot" + i, nav);
                Width(d, 8);
                Height(d, 4);
                _dots.Add(Fill(d, Sub));
            }
            MakeButton(nav, _lines.CardNext, 18, Plate, () => StepBeat(+1), 44);
            Spacer(nav);
            var buttons = Row(l, 48, 10);
            _sail3 = BigButton(buttons, _lines.CardSetSail, 2f, () => _model?.SetSail?.Invoke());
            BigButton(buttons, _lines.CardVoyage, 1f, () => ShowLevel(1));
        }

        // ── Showing ──────────────────────────────────────────────────────────

        public bool IsOpen => _open;

        float PanelWidth => _root ? Mathf.Max(1f, _root.rect.width) : 760f;

        /// <summary>Slide the card open on <paramref name="model"/> (or redraw it, if it is open), on its first level
        /// unless <paramref name="keepLevel"/>.</summary>
        public void Show(Model model, bool keepLevel = false)
        {
            bool sameVoyage = _model != null && model != null && _model.Title == model.Title;
            _model = model;
            if (!keepLevel || !sameVoyage)
            {
                _level = 1;
                _beat = 0;
            }
            Draw();
            if (!_open)
            {
                _open = true;
                Animate(true);
            }
        }

        /// <summary>Slide the card shut.</summary>
        public void Hide()
        {
            if (!_open) return;
            _open = false;
            DropDraft();
            Animate(false);
        }

        void Animate(bool open)
        {
            _from = _panel.anchoredPosition.x;
            _to = open ? 0f : PanelWidth + 40f;
            _t = 0f;
            _group.interactable = _group.blocksRaycasts = open;
        }

        void Update()
        {
            if (!_panel) return;
            if (_t < 1f)
            {
                _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / Slide);
                // OutQuint, the style foundation's panel motion.
                float e = 1f - Mathf.Pow(1f - _t, 5f);
                _panel.anchoredPosition = new Vector2(Mathf.Lerp(_from, _to, e), 0f);
                _group.alpha = Mathf.Lerp(_open ? 0f : 1f, _open ? 1f : 0f, e);
            }
            if (_open && _level == 1 && _sweep && _routeLine)
            {
                // The light sweeps the course every 4.2 s; the destination breathes as it arrives.
                float cycle = Mathf.Repeat(Time.unscaledTime, 4.2f) / 2.9f;
                float w = _routeLine.rect.width;
                _sweep.anchoredPosition = new Vector2(Mathf.Clamp01(cycle) * w, 0f);
                _sweep.gameObject.SetActive(cycle <= 1f);
                if (_endNode) _endNode.transform.localScale = Vector3.one * (1f + 0.25f * Mathf.Max(0f, 1f - Mathf.Abs(cycle - 1f) * 4f));
            }
        }

        void ShowLevel(int level)
        {
            _level = Mathf.Clamp(level, 1, 3);
            Draw();
        }

        void StepBeat(int step)
        {
            if (_model == null) return;
            _beat = VoyageCardRules.Wrap(_beat, step, _model.Beats.Count);
            Draw();
        }

        void Draw()
        {
            if (_model == null) return;
            _l1.SetActive(_level == 1);
            _l2.SetActive(_level == 2);
            _l3.SetActive(_level == 3);
            for (int i = 0; i < 3; i++) if (_crumbs[i]) _crumbs[i].color = i + 1 == _level ? Light : Lab;
            Set(_pager, _model.Pager);
            bool sail = _model.SetSail != null;
            foreach (var b in new[] { _sail1, _sail2, _sail3 }) if (b) b.interactable = sail;
            if (_map) _poster.Show(_map, _model.Course, Route, Ark, Sea);

            Set(_chipCourse, _model.CourseChip);
            Set(_chipFound, _model.FoundBy);
            Set(_kicker, _model.Kicker);
            Set(_title, _model.Title);
            Set(_course, _model.CourseValue);
            Set(_sail, _model.SailValue);
            Set(_intensity, _model.IntensityValue);
            Set(_cells, _model.Cells);
            Set(_from_, _model.From);
            Set(_toName, _model.To);
            DrawSquad();

            int beats = _model.Beats.Count;
            if (beats > 0)
            {
                _beat = Mathf.Clamp(_beat, 0, beats - 1);
                var b = _model.Beats[_beat];
                Set(_beatLabel, b.Label);
                Set(_beatLine, b.Line);
                Set(_beatBy, b.By);
            }
            for (int i = 0; i < _dots.Count; i++)
            {
                _dots[i].gameObject.SetActive(i < beats);
                _dots[i].color = i == _beat ? Route : Sub;
                var le = _dots[i].GetComponent<LayoutElement>();
                if (le) le.preferredWidth = i == _beat ? 28f : 8f;
            }
        }

        static void Set(TMP_Text t, string value)
        {
            if (t) t.text = value ?? string.Empty;
        }

        // ── The squad (the Ark's squads) ─────────────────────────────────────

        static SquadBook Book => UGSDataService.Instance?.SquadRepo?.Data?.Book;

        static void SaveBook() => UGSDataService.Instance?.SquadRepo?.MarkDirty();

        void DrawSquad()
        {
            var book = Book;
            var active = Editing(book);
            int count = book?.Squads?.Count ?? 0;
            for (int i = 0; i < _presets.Count; i++)
            {
                bool show = i < count;
                _presets[i].b.gameObject.SetActive(show);
                if (!show) continue;
                var s = book.Squads[i];
                _presets[i].t.text = s?.Name ?? string.Empty;
                bool on = i == book.ActiveIndex;
                _presets[i].fill.color = on ? Picked : Idle;
                _presets[i].t.color = on ? Light : Sub;
            }
            if (_noSquad) _noSquad.gameObject.SetActive(count == 0);
            Set(_squadName, active?.Name);
            Set(_crewName, active?.Name);
            Set(_crewNote, active != null && active.IsJoined()
                ? TemplateText.Fill(_lines.CardCrewJoined, "owner", active.OwnerName)
                : _lines.CardCrewNote);
            if (_saveLabel) _saveLabel.text = (Drafting ? _lines.CardSaveSquad : _lines.CardSaved) ?? string.Empty;
            if (_save) _save.interactable = Drafting;

            string[] roles = { _lines.CardSeatLeader, _lines.CardSeatRogueOne, _lines.CardSeatRogueTwo };
            for (int seat = 0; seat < 3; seat++)
            {
                var slot = active != null ? active.GetSlot(seat) : default;
                var vessel = active != null ? Vessel(slot.Vessel) : null;
                var sprite = Hull(vessel);
                _formation[seat].sprite = sprite;
                _formation[seat].enabled = sprite;
                var view = _seats[seat];
                Set(view.Role, roles[seat]);
                Set(view.Who, active == null ? string.Empty : Who(active, seat, slot));
                view.Hull.sprite = sprite;
                view.Hull.enabled = sprite;
                bool editable = active != null && CanEdit(active, seat);
                var keywords = vessel ? vessel.Keywords : null;
                var pilot = active != null && _catalog ? _catalog.ResolvePilot(slot, keywords) : null;
                var artifact = active != null && _catalog ? _catalog.ResolveArtifact(slot, keywords) : null;
                // The catalog gives the callsign and kind for anyone's ids; only the player's own name a person and an
                // artifact this inventory holds (SquadsPage.IdsAreMine).
                PilotRecord person = null;
                ArtifactRecord held = null;
                if (editable && pilot && PilotLoadoutStore.TryGetPilot(slot.PilotId, out var p) && p.CallsignOrTemplate() == pilot.Id) person = p;
                if (editable && artifact && PilotLoadoutStore.TryGetArtifact(slot.ArtifactId, out var a) && a.Kind() == artifact.Id) held = a;
                string word = pilot ? Callsigns.WordOf(pilot.Vision) : string.Empty;
                Set(view.Settings[1].label, string.IsNullOrEmpty(word) ? _lines.CardSettingPilot : _lines.CardSettingPilot + " - " + word);
                string[] values =
                {
                    vessel ? vessel.CardName : string.Empty,
                    person != null ? person.DisplayName() : pilot ? pilot.DisplayName : string.Empty,
                    ElementName(slot.Element),
                    held != null ? held.Name : artifact ? artifact.DisplayName : _lines.CardNoArtifact,
                };
                for (int k = 0; k < 4; k++)
                {
                    Set(view.Settings[k].value, active == null ? string.Empty : values[k]);
                    view.Settings[k].b.interactable = editable;
                }
            }
        }

        void PickSquad(int index)
        {
            var book = Book;
            if (book == null || index == book.ActiveIndex) return;
            if (!book.TrySetActive(index)) return;
            DropDraft();
            SaveBook();
            // The pilots seated in the squad decide which voyages are offered: the window redraws its cards.
            SquadChanged?.Invoke();
            Draw();
        }

        // The Ark's rules (SquadsPage.IsMySeat / CanEdit): your own card, and the AI or open cards of a squad you made.
        internal static bool IsMySeat(SavedSquad squad, int seat) => squad.IsJoined() ? seat == squad.MySeat : seat == 0;

        internal static bool CanEdit(SavedSquad squad, int seat) =>
            squad != null && (IsMySeat(squad, seat) || (!squad.IsJoined() && seat > 0 && !squad.GetSlot(seat).IsPlayer()));

        string Who(SavedSquad squad, int seat, SquadSlot slot)
        {
            if (IsMySeat(squad, seat)) return _lines.CardSeatYou;
            if (slot.IsOpen()) return _lines.CardSeatOpen;
            if (slot.IsPlayer()) return slot.MemberName;
            return _lines.CardSeatAi;
        }

        void ChangeSetting(int seat, int setting)
        {
            var book = Book;
            var active = book?.GetActive();
            if (active == null || !CanEdit(active, seat)) return;
            if (_draft == null)
            {
                _draft = SquadMembership.Copy(active);
                _draftIndex = book.ActiveIndex;
            }
            var squad = _draft;
            var slot = squad.GetSlot(seat);
            string previousPilot = slot.PilotId;
            bool mine = IsMySeat(squad, seat);
            switch (setting)
            {
                case 0:
                {
                    var offered = new List<VesselClassType>();
                    if (_vessels && _vessels.VesselList != null)
                        foreach (var v in _vessels.VesselList)
                            if (v && !v.IsLocked) offered.Add(v.Class);
                    var next = VoyageCardRules.Cycle(offered, slot.Vessel);
                    if (offered.Count == 0 || next == slot.Vessel) return;
                    slot = slot.WithVessel(next);
                    // The player's own card starts on the person they keep in that vessel's Hangar seat (a crew id).
                    if (mine) slot.PilotId = PilotLoadoutStore.GetPilot(next);
                    break;
                }
                case 1:
                {
                    // The crew's people whose callsign can fly the hull, in crew order.
                    var vessel = Vessel(slot.Vessel);
                    _ids.Clear();
                    if (_catalog && vessel)
                    {
                        PilotLoadoutStore.GetCrew(_crew);
                        foreach (var person in _crew)
                            if (_catalog.TryGetPilot(person.Id, out var callsign) && callsign && callsign.Requirement != null
                                && callsign.Requirement.IsMetBy(vessel.Class, vessel.Keywords))
                                _ids.Add(person.Id);
                    }
                    if (_ids.Count == 0) return;
                    // A legacy callsign id cycles on from the person it resolves to.
                    string current = PilotLoadoutStore.TryGetPilot(slot.PilotId, out var seated) ? seated.Id : slot.PilotId;
                    slot.PilotId = VoyageCardRules.Cycle(_ids, current);
                    break;
                }
                case 2:
                    slot.Element = VoyageCardRules.Cycle(Elements, slot.Element);
                    break;
                default:
                {
                    // The hoard's artifacts the hull can carry: each is one of a kind, so every seat this card edits (the
                    // player's own, or an AI or open seat of a squad they made) carries only what is held. Null is none.
                    var vessel = Vessel(slot.Vessel);
                    _ids.Clear();
                    _ids.Add(null);
                    if (_catalog && vessel)
                    {
                        PilotLoadoutStore.GetHoard(_hoard);
                        foreach (var artifact in _hoard)
                            if (_catalog.TryGetArtifact(artifact.Id, out var kind) && kind && kind.Requirement != null
                                && kind.Requirement.IsMetBy(vessel.Class, vessel.Keywords))
                                _ids.Add(artifact.Id);
                    }
                    // A legacy kind id cycles on from the artifact it resolves to.
                    string current = PilotLoadoutStore.TryGetArtifact(slot.ArtifactId, out var carried) ? carried.Id : null;
                    slot.ArtifactId = VoyageCardRules.Cycle(_ids, current);
                    break;
                }
            }
            squad.SetSlot(seat, slot);
            // Each vessel has its own pilot and artifact: one moved onto this vessel leaves the others (the pilot swaps).
            squad.KeepCardsOwn(seat, previousPilot);
            // Every vessel needs a pilot: an empty or doubled seat takes the next free person of the crew.
            PilotLoadoutStore.GetCrew(_crew);
            _ids.Clear();
            foreach (var p in _crew) if (!string.IsNullOrEmpty(p.Id)) _ids.Add(p.Id);
            squad.FillPilots(_ids);
            Draw();
        }

        /// <summary>The squad Ark crew shows: the unsaved copy while one is being edited, else the active squad.</summary>
        SavedSquad Editing(SquadBook book) =>
            _draft != null && book != null && _draftIndex == book.ActiveIndex ? _draft : book?.GetActive();

        void DropDraft()
        {
            _draft = null;
            _draftIndex = -1;
        }

        void SaveSquad()
        {
            var book = Book;
            if (_draft == null || book?.Squads == null) return;
            if (_draftIndex == book.ActiveIndex && _draftIndex >= 0 && _draftIndex < book.Squads.Count)
                book.Squads[_draftIndex] = _draft;
            DropDraft();
            SaveBook();
            SquadChanged?.Invoke();
            Draw();
        }

        SO_Vessel Vessel(VesselClassType cls) =>
            _vessels && _vessels.TryGetVesselByClass(cls, out var v, reportMissing: false) ? v : null;

        static Sprite Hull(SO_Vessel v) =>
            !v ? null : v.CardSilohoutteActive ? v.CardSilohoutteActive : v.SquadImage ? v.SquadImage : v.IconActive;

        string ElementName(Element e) => e switch
        {
            Element.Charge => _lines.ElementCharge,
            Element.Mass => _lines.ElementMass,
            Element.Space => _lines.ElementSpace,
            Element.Time => _lines.ElementTime,
            _ => string.Empty,
        };

        // ── Small builders ───────────────────────────────────────────────────

        static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Image Fill(RectTransform rt, Color c)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        static LayoutElement Le(Component c) =>
            c.TryGetComponent(out LayoutElement le) ? le : c.gameObject.AddComponent<LayoutElement>();

        static void Height(Component c, float h)
        {
            var le = Le(c);
            le.minHeight = le.preferredHeight = h;
        }

        static void Width(Component c, float w)
        {
            var le = Le(c);
            le.minWidth = le.preferredWidth = w;
        }

        static void Flex(Component c) => Le(c).flexibleWidth = 1f;

        static void Spacer(Transform parent, bool vertical = false)
        {
            var s = Node("Space", parent);
            var le = s.gameObject.AddComponent<LayoutElement>();
            if (vertical) le.flexibleHeight = 1f;
            else le.flexibleWidth = 1f;
        }

        static RectTransform Row(Transform parent, float height, float spacing)
        {
            var r = Node("Row", parent);
            var h = r.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            Height(r, height);
            return r;
        }

        static RectTransform Column(Transform parent, float spacing)
        {
            var c = Node("Column", parent);
            var v = c.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return c;
        }

        static GameObject Level(Transform parent, string name)
        {
            var l = Column(parent, 10);
            l.name = name;
            Le(l).flexibleHeight = 1f;
            return l.gameObject;
        }

        TMP_Text Text(Transform parent, float size, Color color, TextAlignmentOptions align, float width = -1f)
        {
            var rt = Node("Text", parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font)
            {
                t.font = _font.font;
                t.fontSharedMaterial = _font.fontSharedMaterial;
            }
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            if (width > 0f) Width(t, width);
            return t;
        }

        TMP_Text Chip(Transform parent, Color color)
        {
            // A plate as wide as its words: the row asks the plate for its width, the plate asks its text.
            var plate = Node("Chip", parent);
            Fill(plate, Plate);
            var h = plate.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(10, 10, 4, 4);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            return Text(plate, 12, color, TextAlignmentOptions.Center);
        }

        TMP_Text Number(Transform parent, string label)
        {
            var col = Column(parent, 2);
            var value = Text(col, 28, Light, TextAlignmentOptions.Left);
            Height(value, 32);
            var l = Text(col, 10, Lab, TextAlignmentOptions.Left);
            l.text = label ?? string.Empty;
            Height(l, 14);
            return value;
        }

        Image Node4(RectTransform route, Color c, bool right)
        {
            var n = Node(right ? "Destination" : "Ark", route);
            n.anchorMin = n.anchorMax = new Vector2(right ? 1f : 0f, 1f);
            n.sizeDelta = new Vector2(13f, 13f);
            n.anchoredPosition = new Vector2(right ? -8f : 8f, -8f);
            n.localRotation = Quaternion.Euler(0, 0, 45f);
            return Fill(n, c);
        }

        TMP_Text EndLabel(RectTransform route, bool right, string under, Color underColor)
        {
            var name = Text(route, 13, Light, right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
            var rt = name.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(right ? 1f : 0f, 1f);
            rt.pivot = new Vector2(right ? 1f : 0f, 1f);
            rt.sizeDelta = new Vector2(220f, 18f);
            rt.anchoredPosition = new Vector2(0f, -20f);
            var tag = Text(route, 10, underColor, right ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
            tag.text = under ?? string.Empty;
            var tr = tag.rectTransform;
            tr.anchorMin = tr.anchorMax = rt.anchorMin;
            tr.pivot = rt.pivot;
            tr.sizeDelta = new Vector2(220f, 14f);
            tr.anchoredPosition = new Vector2(0f, -40f);
            return name;
        }

        (Button b, TMP_Text t, Image fill) MakeButton(Transform parent, string label, float size, Color fill, Action onClick, float width)
        {
            var rt = Node("Button", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = fill;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick());
            var t = Text(rt, size, Light, TextAlignmentOptions.Center);
            Stretch(t.rectTransform);
            t.text = label ?? string.Empty;
            if (width > 0f) Width(rt, width);
            return (b, t, img);
        }

        Button BigButton(Transform parent, string label, float weight, Action onClick)
        {
            Button b;
            if (_buttonFrom)
            {
                b = Instantiate(_buttonFrom, parent);
                b.name = "Action";
                b.onClick.RemoveAllListeners();
                b.gameObject.SetActive(true);
                var rt = (RectTransform)b.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                b.interactable = true;
                var t = b.GetComponentInChildren<TMP_Text>(true);
                if (t) t.text = label ?? string.Empty;
            }
            else b = MakeButton(parent, label, 15, Plate, onClick, 0).b;
            if (_buttonFrom) b.onClick.AddListener(() => onClick());
            var le = Le(b);
            le.flexibleWidth = weight;
            le.minHeight = le.preferredHeight = 48f;
            return b;
        }
    }
}

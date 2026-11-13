using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Voyages tab as two screens, as Shombith picked them on Frogma (Rev 98 1A and 2D, Docs/MENU_REDESIGN_B.md
    /// sections 180-184; they replace the Sea table of section 172, whose lying map could not be explored).
    /// <list type="bullet">
    /// <item><b>The voyage screen</b>: the voyages on offer as chips (left); the window's own HyperSea map, flat, with
    /// its explorer (the tier ladder, drag, pinch, wheel, the + and - here, a press on a place) over its edge-on view
    /// (middle); the open voyage's name and numbers, the squads as formations, Configure squad and Set sail (right).
    /// <see cref="VoyageJobsView"/> stands the map and the edge view in <see cref="CourseColumn"/> and
    /// <see cref="FitTable"/> shapes them.</item>
    /// <item><b>The squad screen</b> (Configure squad): the voyage on one line with the way back, the squads' names,
    /// <see cref="VoyageSquadDeck"/> (the three seats and the picked seat's choices), Save as preset and Set sail. The
    /// voyage's map stands dimmed behind it.</item>
    /// </list>
    /// Every word is the voyage content's (<see cref="VoyageLines"/>) or the game's data; every colour, sprite and
    /// proportion is <see cref="VoyageBoardStyleSO"/>'s. The spacing is one rule (section 183): lines of one group 10 or
    /// more apart, groups 28 or more apart, no text wrapping inside a tile.
    /// </summary>
    public class VoyageBoard : MonoBehaviour
    {
        /// <summary>One voyage on the list.</summary>
        public sealed class Row
        {
            public string Title, By, Meta;
            public readonly List<Vector2> Course = new();
        }

        /// <summary>The open voyage: its name beside the map, and Set sail.</summary>
        public sealed class Voyage
        {
            /// <summary>The small line over the name (which voyage it is and who brought it).</summary>
            public string Kicker;
            /// <summary>Where it goes: the name. Empty: <see cref="Title"/> is the name and <see cref="Line"/> stands
            /// under it.</summary>
            public string Place;
            public string Title, Line, Distance, Sail, Intensity;
            /// <summary>Set sail's label; empty: the content's own.</summary>
            public string SailLabel;
            public Action SetSail;
            /// <summary>A stepper under the numbers (the Ark escort's difficulty); null hides it.</summary>
            public string LowerLabel, RaiseLabel;
            public Action Lower, Raise;
        }

        /// <summary>The squad changed (another squad picked, a seat changed, a squad saved): the pilots bring the
        /// voyages, so the window redraws.</summary>
        public event Action SquadChanged;

        /// <summary>The screen on show changed (the voyage screen, the squad screen): the map is refitted.</summary>
        public event Action ScreenChanged;

        /// <summary>Where the window's galaxy map and edge view stand on the voyage screen.</summary>
        public RectTransform CourseColumn => _courseColumn;

        /// <summary>The squad screen is on show.</summary>
        public bool OnSquadScreen => _squadScreen;

        /// <summary>The squad screen's seats and choices.</summary>
        public VoyageSquadDeck Deck => _deck;

        /// <summary>The edge-on view's share of the table's height, under the map (the board: 204 of 796).</summary>
        public const float EdgeShare = 204f / 796f;

        const float FootHeight = 56f, FootGap = 18f, ZoomStep = 1.6f;

        static readonly Color RouteAuthored = new(0.784f, 1f, 0.353f, 1f);
        static readonly Color ArkAuthored = new(1f, 0.78f, 0.25f, 1f);
        static readonly Color SeaAuthored = new(0.75f, 0.75f, 0.8f, 1f);

        // Every colour is painted as the worn colour set has it (ColorTrace, Docs/VesselModels/COLOR_TRACE.md).
        static Color Route => ColorTrace.Retrace(ColorTrace.Ui, RouteAuthored);
        static Color Ark => ColorTrace.Retrace(ColorTrace.Ui, ArkAuthored);
        static Color Sea => ColorTrace.Retrace(ColorTrace.Ui, SeaAuthored);

        VoyageKit _kit;
        VoyageBoardStyleSO _style;
        VoyageLines _lines;
        VoyageSeats _rules;
        VoyageSquadDeck _deck;

        RectTransform _root, _voyageRoot, _squadRoot, _listContent, _courseColumn, _mapHost, _edgeHost, _presetHost, _squadWords;
        TMP_Text _message, _kicker, _name, _sub, _stats, _lowerText, _raiseText, _squadLabel, _wordsLabel, _summaryTitle, _summaryStats;
        readonly TMP_Text[] _sailText = new TMP_Text[2];
        readonly Button[] _sail = new Button[2];
        Button _lower, _raise, _configure, _save;
        RectTransform _stepper;
        readonly List<RowView> _rows = new();
        readonly List<PresetView> _presets = new();
        readonly List<WordView> _words = new();
        Voyage _voyage;
        VoyageMapGraphic _map;
        Action<int> _pick;
        bool _squadScreen;
        string _openTitle;

        sealed class RowView
        {
            public Button Button;
            public VoyageSoftFill Plate;
            public Image Bar;
            public VoyageRoutePoster Poster;
            public CanvasGroup Water;
            public TMP_Text Title, Meta;
        }

        /// <summary>A squad on the voyage screen: its name, who flies, its three hulls in formation.</summary>
        sealed class PresetView
        {
            public RectTransform Root;
            public Button Button;
            public CanvasGroup Group;
            public VoyageSoftFill Glow;
            public TMP_Text Name, Who;
            public Image Bar;
            public readonly VoyageSeats.HullView[] Hulls = new VoyageSeats.HullView[SavedSquad.SlotCount];
        }

        /// <summary>A squad's name on the squad screen's head.</summary>
        sealed class WordView
        {
            public Button Button;
            public TMP_Text Label;
            public Image Bar;
        }

        // ── Building ─────────────────────────────────────────────────────────

        /// <summary>The board, built once over <paramref name="host"/> (the Voyages page), its text in
        /// <paramref name="font"/>'s face.</summary>
        public static VoyageBoard Create(RectTransform host, TMP_Text font, SO_VesselList vessels, VoyageLines lines)
        {
            var rt = VoyageKit.Node("VoyageBoard", host);
            rt.SetAsLastSibling();
            VoyageKit.Stretch(rt);
            var board = rt.gameObject.AddComponent<VoyageBoard>();
            board._root = rt;
            board._lines = lines;
            board._style = VoyageBoardStyleSO.Load();
            board._kit = new VoyageKit(board._style, lines, font);
            board._rules = new VoyageSeats(board._style, lines, vessels);
            board._rules.Changed += () => board.SquadChanged?.Invoke();
            board.BuildVoyageScreen();
            board.BuildSquadScreen();
            board.ShowScreen(false);
            return board;
        }

        void BuildVoyageScreen()
        {
            var s = _style;
            _voyageRoot = VoyageKit.Node("VoyageScreen", _root);
            VoyageKit.Stretch(_voyageRoot);

            // The voyages: chips in a scroll, so more voyages than fit can be reached.
            var list = VoyageKit.Node("Voyages", _voyageRoot);
            VoyageKit.Anchor(list, 0f, 0f, s.ListRight, 1f);
            list.gameObject.AddComponent<RectMask2D>();
            _listContent = VoyageKit.Node("Chips", list);
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = Vector2.one;
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.sizeDelta = Vector2.zero;
            VoyageKit.Column(_listContent, 16f);
            _listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = list.gameObject.AddComponent<ScrollRect>();
            scroll.content = _listContent;
            scroll.viewport = list;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            _message = _kit.Text(list, 18, _kit.Sub, TextAlignmentOptions.TopLeft);
            VoyageKit.Anchor(_message.rectTransform, 0f, 0f, 1f, 1f);
            _message.textWrappingMode = TextWrappingModes.Normal;

            // The table's column: VoyageJobsView stands the map and the edge view in it, FitTable shapes them. The map
            // takes the pointer through it; only the zoom steps stand over the map.
            _courseColumn = VoyageKit.Node("Table", _voyageRoot);
            VoyageKit.Anchor(_courseColumn, s.CourseLeft, 0f, s.CourseRight, 1f);
            ZoomButton(_lines?.BoardZoomIn, 16f, ZoomStep);
            ZoomButton(_lines?.BoardZoomOut, 66f, 1f / ZoomStep);

            var panel = VoyageKit.Node("Voyage", _voyageRoot);
            VoyageKit.Anchor(panel, s.PanelLeft, 0f, 1f, 1f);
            BuildName(panel);

            var sail = VoyageKit.Node("SetSail", panel);
            VoyageKit.Foot(sail, 0f, FootHeight);
            (_sail[0], _sailText[0]) = _kit.Cta(sail, _lines?.BoardSailKey, _lines?.CardSetSail, () => _voyage?.SetSail?.Invoke());
            var configure = VoyageKit.Node("Configure", panel);
            VoyageKit.Foot(configure, FootHeight + FootGap, FootHeight);
            _configure = _kit.Quiet(configure, _lines?.BoardConfigureKey, _lines?.BoardConfigure, () => ShowScreen(true));
        }

        void ZoomButton(string mark, float fromTop, float factor)
        {
            var rt = VoyageKit.Node("Zoom", _courseColumn);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one;
            rt.sizeDelta = new Vector2(44f, 44f);
            rt.anchoredPosition = new Vector2(-16f, -fromTop);
            var b = _kit.Plated(rt, _kit.Popup, true, out _, out _);
            var t = _kit.Text(rt, 22, _kit.Light, TextAlignmentOptions.Center);
            VoyageKit.Stretch(t.rectTransform);
            t.richText = false;
            t.text = mark ?? string.Empty;
            b.onClick.AddListener(() => { if (_map) _map.ZoomStep(factor); });
        }

        /// <summary>The open voyage's name and numbers, then the squads, down the right panel.</summary>
        void BuildName(RectTransform panel)
        {
            var head = VoyageKit.Node("Name", panel);
            VoyageKit.Stretch(head);
            head.offsetMin = new Vector2(0f, FootHeight * 2f + FootGap * 2f);
            VoyageKit.Column(head, 14f);
            _kicker = _kit.Label(head, 14, _kit.Sub);
            _name = _kit.Text(head, 36, _kit.Light, TextAlignmentOptions.Left);
            _sub = _kit.Text(head, 18, _kit.Light, TextAlignmentOptions.Left);
            _stats = _kit.Text(head, 20, _kit.Light, TextAlignmentOptions.Left);

            // The Ark escort's difficulty, under the numbers.
            _stepper = VoyageKit.Node("Stepper", head);
            VoyageKit.Row(_stepper, 10f);
            (_lower, _lowerText) = SmallButton(_stepper, () => _voyage?.Lower?.Invoke());
            (_raise, _raiseText) = SmallButton(_stepper, () => _voyage?.Raise?.Invoke());

            // The squads stand a group's gap below the voyage (28 or more).
            VoyageKit.Le(VoyageKit.Node("Gap", head)).preferredHeight = 14f;
            _squadLabel = _kit.Label(head, 14, _kit.Sub);
            _presetHost = VoyageKit.Node("Squads", head);
            VoyageKit.Column(_presetHost, 18f);
        }

        (Button b, TMP_Text t) SmallButton(Transform parent, Action onClick)
        {
            var rt = VoyageKit.Node("Step", parent);
            var b = _kit.Plated(rt, _kit.Popup, true, out _, out _);
            var h = VoyageKit.Row(rt, 0f, TextAnchor.MiddleCenter);
            h.padding = new RectOffset(16, 16, 8, 8);
            var t = _kit.Text(rt, 15, _kit.Light, TextAlignmentOptions.Center);
            b.onClick.AddListener(() => onClick());
            return (b, t);
        }

        void BuildSquadScreen()
        {
            _squadRoot = VoyageKit.Node("SquadScreen", _root);
            VoyageKit.Stretch(_squadRoot);
            // The voyage's own sea stands behind, dimmed; the veil also keeps the pointer off it.
            var veil = VoyageKit.Node("Veil", _squadRoot);
            VoyageKit.Stretch(veil);
            VoyageKit.Block(veil, VoyageKit.Fade(_kit.Popup, 0.66f), hit: true);

            // The head: the way back, the voyage on one line, the squads' names.
            var head = VoyageKit.Node("Head", _squadRoot);
            VoyageKit.Top(head, 0f, FootHeight);
            var back = VoyageKit.Node("Back", head);
            back.anchorMin = back.anchorMax = back.pivot = new Vector2(0f, 0.5f);
            back.sizeDelta = new Vector2(220f, 44f);
            _kit.Quiet(back, _lines?.BoardBackKey, _lines?.BoardBack, () => ShowScreen(false));
            var line = VoyageKit.Node("Voyage", head);
            VoyageKit.Stretch(line);
            line.offsetMin = new Vector2(264f, 0f);
            VoyageKit.Row(line, 44f);
            _summaryTitle = _kit.Text(line, 22, _kit.Light, TextAlignmentOptions.Left);
            _summaryStats = _kit.Text(line, 18, _kit.Light, TextAlignmentOptions.Left);
            _squadWords = VoyageKit.Node("Squads", head);
            VoyageKit.Stretch(_squadWords);
            VoyageKit.Row(_squadWords, 40f, TextAnchor.MiddleRight);
            _wordsLabel = _kit.Label(_squadWords, 14, _kit.Sub);

            _deck = new VoyageSquadDeck(_kit, _rules, _squadRoot);

            // The foot: keep the squad, what a vessel brings, and Set sail.
            var foot = VoyageKit.Node("Foot", _squadRoot);
            VoyageKit.Foot(foot, 0f, FootHeight);
            var save = VoyageKit.Node("Save", foot);
            save.anchorMin = Vector2.zero;
            save.anchorMax = new Vector2(0f, 1f);
            save.pivot = new Vector2(0f, 0.5f);
            save.sizeDelta = new Vector2(320f, 0f);
            _save = _kit.Quiet(save, _lines?.BoardSavePresetKey, _lines?.BoardSavePreset, () => _rules.SavePreset());
            var note = _kit.Text(foot, 15, _kit.Light, TextAlignmentOptions.Left);
            VoyageKit.Stretch(note.rectTransform);
            note.rectTransform.offsetMin = new Vector2(368f, 0f);
            note.rectTransform.offsetMax = new Vector2(-568f, 0f);
            VoyageKit.Set(note, _lines?.BoardHangarNote);
            var sail = VoyageKit.Node("SetSail", foot);
            sail.anchorMin = new Vector2(1f, 0f);
            sail.anchorMax = Vector2.one;
            sail.pivot = new Vector2(1f, 0.5f);
            sail.sizeDelta = new Vector2(520f, 0f);
            (_sail[1], _sailText[1]) = _kit.Cta(sail, _lines?.BoardSailKey, _lines?.CardSetSail, () => _voyage?.SetSail?.Invoke());
        }

        RowView RowFor(int index)
        {
            while (_rows.Count <= index)
            {
                var view = new RowView();
                int i = _rows.Count;
                var rt = VoyageKit.Node("Voyage" + i, _listContent);
                VoyageKit.Le(rt).preferredHeight = _style.RowHeight;
                view.Plate = VoyageKit.Band(rt, _kit.RowPicked, 0.04f, hit: true);
                view.Button = rt.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = view.Plate;
                view.Button.onClick.AddListener(() => _pick?.Invoke(i));

                // A sliver of the voyage's own water at the chip's right, fading in from the words.
                var water = VoyageKit.Node("Water", rt);
                VoyageKit.Anchor(water, 0.56f, 0f, 1f, 1f);
                water.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(48, 0);
                view.Water = water.gameObject.AddComponent<CanvasGroup>();
                var poster = VoyageKit.Node("Course", water);
                VoyageKit.Stretch(poster);
                view.Poster = poster.gameObject.AddComponent<VoyageRoutePoster>();
                view.Poster.LineScale = 0.45f;

                var bar = VoyageKit.Node("Bar", rt);
                bar.anchorMin = Vector2.zero;
                bar.anchorMax = new Vector2(0f, 1f);
                bar.pivot = new Vector2(0f, 0.5f);
                bar.sizeDelta = new Vector2(3f, 0f);
                view.Bar = VoyageKit.Block(bar, _kit.Cyan);

                // The chip's two lines stand well apart (section 183).
                var words = VoyageKit.Node("Words", rt);
                VoyageKit.Stretch(words);
                words.offsetMin = new Vector2(22f, 0f);
                words.offsetMax = new Vector2(-12f, 0f);
                VoyageKit.Column(words, 14f, TextAnchor.MiddleLeft);
                view.Title = _kit.Text(words, 18, _kit.Light, TextAlignmentOptions.Left);
                view.Meta = _kit.Text(words, 14, _kit.Sub, TextAlignmentOptions.Left);
                _rows.Add(view);
            }
            return _rows[index];
        }

        PresetView PresetFor(int index)
        {
            while (_presets.Count <= index)
            {
                var view = new PresetView();
                int i = _presets.Count;
                view.Root = VoyageKit.Node("Squad" + i, _presetHost);
                var band = VoyageKit.Band(view.Root, _kit.RowPicked, 0.14f, hit: true);
                view.Button = view.Root.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = band;
                view.Button.onClick.AddListener(() => PickSquad(i));
                view.Group = view.Root.gameObject.AddComponent<CanvasGroup>();

                // The squad's three hulls in formation at the right: the leader in front, the rogues behind to each side.
                var box = VoyageKit.Node("Formation", view.Root);
                VoyageKit.Anchor(box, 0.6f, 0.14f, 0.99f, 0.86f);
                var glow = VoyageKit.Node("Glow", box);
                VoyageKit.Stretch(glow);
                view.Glow = VoyageKit.Glow(glow, VoyageKit.Fade(_kit.Light, 0.2f));
                view.Hulls[1] = Hull(box, 0f, 0.52f, 0.52f, 1f);
                view.Hulls[2] = Hull(box, 0.48f, 0.52f, 1f, 1f);
                view.Hulls[0] = Hull(box, 0.14f, 0f, 0.86f, 0.62f);

                // Its name alone on its line, who flies under it.
                var words = VoyageKit.Node("Words", view.Root);
                VoyageKit.Anchor(words, 0f, 0f, 0.6f, 1f);
                words.offsetMin = new Vector2(30f, 0f);
                VoyageKit.Column(words, 14f, TextAnchor.MiddleLeft);
                (view.Name, view.Bar) = _kit.Underlined(words, 22, TextAnchor.MiddleLeft);
                view.Who = _kit.Text(words, 14, _kit.Light, TextAlignmentOptions.Left);
                _presets.Add(view);
            }
            return _presets[index];
        }

        static VoyageSeats.HullView Hull(RectTransform box, float x0, float y0, float x1, float y1)
        {
            var rt = VoyageKit.Node("Hull", box);
            VoyageKit.Anchor(rt, x0, y0, x1, y1);
            return VoyageSeats.HullBox(rt);
        }

        WordView WordFor(int index)
        {
            while (_words.Count <= index)
            {
                var view = new WordView();
                int i = _words.Count;
                var rt = VoyageKit.Node("Squad" + i, _squadWords);
                var hit = VoyageKit.Block(rt, VoyageKit.Clear, hit: true);
                VoyageKit.Column(rt, 6f, TextAnchor.MiddleCenter);
                view.Label = _kit.Text(rt, 18, _kit.Sub, TextAlignmentOptions.Center);
                var bar = VoyageKit.Node("Bar", rt);
                VoyageKit.Le(bar).preferredHeight = 2f;
                view.Bar = VoyageKit.Block(bar, _kit.Cyan);
                view.Button = rt.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = hit;
                view.Button.onClick.AddListener(() => PickSquad(i));
                _words.Add(view);
            }
            return _words[index];
        }

        // Another squad sails: the squad screen picks that squad's own seat first. The reset comes before the pick,
        // because the pick redraws the window at once (VoyageSeats.Changed).
        void PickSquad(int index)
        {
            var book = VoyageSeats.Book;
            if (book == null || index == book.ActiveIndex) return;
            _deck.Reset();
            _rules.PickSquad(index);
        }

        // ── The two screens ──────────────────────────────────────────────────

        /// <summary>Show the squad screen (<paramref name="squad"/>) or the voyage screen.</summary>
        public void ShowScreen(bool squad)
        {
            bool changed = _squadScreen != squad;
            _squadScreen = squad;
            _voyageRoot.gameObject.SetActive(!squad);
            _squadRoot.gameObject.SetActive(squad);
            Fit();
            if (squad) _deck.Draw();
            if (changed) ScreenChanged?.Invoke();
        }

        /// <summary>
        /// Shape the hosts <see cref="VoyageJobsView"/> stood in the map column. On the voyage screen the column is the
        /// table's (<see cref="CourseColumn"/>): the map over its edge-on view, which takes <see cref="EdgeShare"/> of
        /// the height. On the squad screen the map fills the page behind the squad and the edge view stands down.
        /// </summary>
        public void FitTable(RectTransform mapHost, RectTransform edgeHost)
        {
            _mapHost = mapHost;
            _edgeHost = edgeHost;
            Fit();
        }

        void Fit()
        {
            if (!_mapHost) return;
            if (_mapHost.parent is RectTransform column && column != _root.parent)
            {
                if (_squadScreen) VoyageKit.Stretch(column);
                else VoyageKit.Anchor(column, _style.CourseLeft, 0f, _style.CourseRight, 1f);
            }
            VoyageKit.Anchor(_mapHost, 0f, _squadScreen ? 0f : EdgeShare, 1f, 1f);
            if (!_edgeHost) return;
            _edgeHost.gameObject.SetActive(!_squadScreen);
            VoyageKit.Anchor(_edgeHost, 0f, 0f, 1f, EdgeShare);
        }

        // ── Showing ──────────────────────────────────────────────────────────

        /// <summary>
        /// Show <paramref name="rows"/> with row <paramref name="open"/> open (-1: none), the open voyage's name and
        /// the squads. <paramref name="message"/> stands in the list when there are no rows (no work offered).
        /// </summary>
        public void Show(IReadOnlyList<Row> rows, int open, Action<int> pick, Voyage voyage, VoyageMapGraphic map, string message)
        {
            _pick = pick;
            _voyage = voyage;
            _map = map;
            int count = rows?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                var row = rows[i];
                var view = RowFor(i);
                view.Button.gameObject.SetActive(true);
                bool on = i == open;
                view.Plate.color = VoyageKit.Fade(_kit.RowPicked, on ? 1f : 0.6f);
                view.Bar.enabled = on;
                view.Water.alpha = on ? 1f : 0.5f;
                VoyageKit.Set(view.Title, row.Title);
                view.Title.color = on ? _kit.Light : _kit.Sub;
                VoyageKit.Set(view.Meta, row.Meta);
                view.Poster.Show(map, row.Course, Route, Ark, Sea);
            }
            for (int i = count; i < _rows.Count; i++) _rows[i].Button.gameObject.SetActive(false);
            VoyageKit.Set(_message, count == 0 ? message : null);
            _openTitle = open >= 0 && open < count ? rows[open].Title : null;

            bool placed = !string.IsNullOrEmpty(voyage?.Place);
            VoyageKit.Set(_kicker, voyage?.Kicker);
            VoyageKit.Set(_name, placed ? voyage.Place : voyage?.Title);
            VoyageKit.Set(_sub, placed ? voyage.Title : voyage?.Line);
            string stats = Stats(voyage);
            VoyageKit.Set(_stats, stats);
            Blank(_kicker);
            Blank(_sub);
            Blank(_stats);
            bool step = voyage?.Lower != null && voyage.Raise != null;
            _stepper.gameObject.SetActive(step);
            if (step)
            {
                VoyageKit.Set(_lowerText, voyage.LowerLabel);
                VoyageKit.Set(_raiseText, voyage.RaiseLabel);
            }
            for (int i = 0; i < _sail.Length; i++)
            {
                _sail[i].interactable = voyage?.SetSail != null;
                VoyageKit.Set(_sailText[i], !string.IsNullOrEmpty(voyage?.SailLabel) ? voyage.SailLabel : _lines?.CardSetSail);
            }
            VoyageKit.Set(_summaryTitle, !string.IsNullOrEmpty(_openTitle) ? _openTitle : voyage?.Title);
            VoyageKit.Set(_summaryStats, stats);
            DrawSquads();
            if (_squadScreen) _deck.Draw();
        }

        /// <summary>The squads: formations on the voyage screen, names on the squad screen's head. The one that sails is
        /// lit and underlined.</summary>
        void DrawSquads()
        {
            var book = VoyageSeats.Book;
            int count = book?.Squads?.Count ?? 0;
            string label = count > 0 ? _lines?.BoardSquadLabel : _lines?.CardNoSquad;
            VoyageKit.Set(_squadLabel, label);
            VoyageKit.Set(_wordsLabel, label);
            // Two squads stand as the board drew them; more share the room.
            float height = count <= 2 ? 136f : count == 3 ? 104f : 84f;
            for (int i = 0; i < count; i++)
            {
                var squad = book.Squads[i];
                bool on = i == book.ActiveIndex;
                string name = _rules.SquadName(squad);
                var view = PresetFor(i);
                view.Root.gameObject.SetActive(true);
                VoyageKit.Le(view.Root).preferredHeight = height;
                VoyageKit.Set(view.Name, name);
                VoyageKit.Set(view.Who, squad == null ? null : TemplateText.Fill(_lines?.BoardPresetWho,
                    "leader", _rules.Who(squad, 0, squad.GetSlot(0)), "one", _rules.Who(squad, 1, squad.GetSlot(1)),
                    "two", _rules.Who(squad, 2, squad.GetSlot(2))));
                view.Bar.color = on ? _kit.Cyan : VoyageKit.Clear;
                view.Glow.color = VoyageKit.Fade(_kit.Light, on ? 0.2f : 0.06f);
                view.Group.alpha = on ? 1f : 0.55f;
                for (int seat = 0; seat < view.Hulls.Length; seat++)
                    _rules.SetHull(view.Hulls[seat], squad == null ? null : VoyageSeats.HullOf(_rules.VesselOf(squad.GetSlot(seat).Vessel)));

                var word = WordFor(i);
                word.Button.gameObject.SetActive(true);
                VoyageKit.Set(word.Label, name);
                word.Label.color = on ? _kit.Light : _kit.Sub;
                word.Bar.color = on ? _kit.Cyan : VoyageKit.Clear;
            }
            for (int i = count; i < _presets.Count; i++) _presets[i].Root.gameObject.SetActive(false);
            for (int i = count; i < _words.Count; i++) _words[i].Button.gameObject.SetActive(false);
            // With no squad there is nothing to configure or keep.
            _configure.interactable = count > 0;
            _save.interactable = VoyageSeats.CanSavePreset();
        }

        /// <summary>The open voyage's numbers on one line (the content's <see cref="VoyageLines.BoardStats"/>); nothing
        /// for a voyage with none (the Ark escort).</summary>
        string Stats(Voyage voyage) =>
            voyage == null || string.IsNullOrEmpty(voyage.Distance) ? null
                : TemplateText.Fill(_lines?.BoardStats, "distance", voyage.Distance, "sail", voyage.Sail, "intensity", voyage.Intensity);

        // An empty line takes no room in the name's column.
        static void Blank(TMP_Text t) => t.gameObject.SetActive(!string.IsNullOrEmpty(t.text));
    }
}

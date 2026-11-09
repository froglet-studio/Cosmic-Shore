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
    /// The Voyages tab as Shombith picked it on Frogma (Docs/MENU_REDESIGN_B.md sections 161-164): Rev 81 B5's three columns
    /// and Rev 83 S2's seat editor.
    /// <list type="bullet">
    /// <item><b>The list</b> (left): one row per voyage on offer, its course on the HyperSea's water
    /// (<see cref="VoyageRoutePoster"/>), its place, who brought it and its three numbers. A press opens it: the row lights
    /// and the course column frames its course.</item>
    /// <item><b>The course</b> (middle): the window's own galaxy map over its edge view. <see cref="VoyageJobsView"/> stands
    /// them in <see cref="CourseColumn"/>.</item>
    /// <item><b>The right panel</b>: the open voyage's title, line and numbers, the squads as chips, the active squad's three
    /// seat cards (the pilot large, the hull, the vessel's name, who flies it, the element and artifact) and Set sail.</item>
    /// <item><b>The seat editor</b> ("Hangar bay"): a press on a seat card the player may change opens it over the list and
    /// the course, on the seat's Vessel, Pilot, Element and Artifact. The choice stands large on a lit floor with its name
    /// and line; the options are a list beside it, the picked one marked in Jade. One press picks and saves: the card
    /// behind changes at once. Done closes it.</item>
    /// </list>
    /// The squads are the Ark's (<see cref="SquadBook"/>) under the Ark's rules (<c>SquadsPage</c>: your own seat, and the AI
    /// or open seats of a squad you made; a pilot flies one seat, so picking one who flies another seat swaps them,
    /// <see cref="SavedSquad.KeepCardsOwn"/>). Every word is the voyage content's (<see cref="VoyageLines"/>) or the game's
    /// data; every colour, sprite and proportion is <see cref="VoyageBoardStyleSO"/>'s.
    /// </summary>
    public class VoyageBoard : MonoBehaviour
    {
        /// <summary>One voyage on the list.</summary>
        public sealed class Row
        {
            public string Title, By, Meta;
            public readonly List<Vector2> Course = new();
        }

        /// <summary>The open voyage, in the right panel.</summary>
        public sealed class Voyage
        {
            public string Title, Line, Distance, Sail, Intensity;
            /// <summary>Set sail's label; empty: the content's own.</summary>
            public string SailLabel;
            public Action SetSail;
            /// <summary>A stepper beside the numbers (the Ark escort's difficulty); null hides it.</summary>
            public string LowerLabel, RaiseLabel;
            public Action Lower, Raise;
        }

        /// <summary>The squad changed (another squad picked, a seat changed): the pilots bring the voyages, so the window
        /// redraws.</summary>
        public event Action SquadChanged;

        /// <summary>Where the window's galaxy map and edge view stand.</summary>
        public RectTransform CourseColumn => _courseColumn;

        static readonly Element[] Elements = { Element.Charge, Element.Mass, Element.Space, Element.Time };
        static readonly Color Clear = new(0f, 0f, 0f, 0f);
        static readonly Color Route = new(0.784f, 1f, 0.353f, 1f);
        static readonly Color Ark = new(1f, 0.78f, 0.25f, 1f);
        static readonly Color Sea = new(0.75f, 0.75f, 0.8f, 1f);

        VoyageBoardStyleSO _style;
        VoyageLines _lines;
        SO_VesselList _vessels;
        PilotArtifactCatalogSO _catalog;
        TMP_Text _font;
        Button _ctaFrom;
        VoyageMapGraphic _map;

        RectTransform _root, _listContent, _courseColumn, _panel;
        TMP_Text _message, _title, _line, _distance, _sail, _intensity, _distanceLabel, _sailLabel, _intensityLabel, _squadLabel;
        Button _setSail, _lower, _raise;
        TMP_Text _setSailText, _lowerText, _raiseText;
        RectTransform _chips;
        readonly List<RowView> _rows = new();
        readonly List<ChipView> _chipViews = new();
        readonly SeatView[] _seats = new SeatView[3];
        Voyage _voyage;

        // The seat editor.
        RectTransform _editor;
        CanvasGroup _editorGroup;
        TMP_Text _editorTitle, _heroName, _heroLine;
        Image _hero;
        RectTransform _options;
        readonly TabView[] _tabs = new TabView[4];
        readonly List<OptionView> _optionViews = new();
        int _openSeat = -1, _tab;
        float _fade, _fadeTo;

        // Read on each draw (reused, not allocated per press).
        readonly List<PilotRecord> _crew = new();
        readonly List<ArtifactRecord> _hoard = new();
        readonly List<string> _ids = new();
        readonly List<Option> _offered = new();

        sealed class RowView
        {
            public Button Button;
            public Image Plate, Bar;
            public VoyageRoutePoster Poster;
            public TMP_Text Title, By, Meta;
        }

        sealed class ChipView
        {
            public Button Button;
            public Image Edge, Bar;
            public TMP_Text Label;
        }

        sealed class SeatView
        {
            public Button Button;
            public CanvasGroup Group;
            public Image Edge, Face, Hull;
            public TMP_Text Name, Lines;
        }

        sealed class TabView
        {
            public Button Button;
            public TMP_Text Label;
            public Image Line;
        }

        sealed class OptionView
        {
            public Button Button;
            public LayoutElement Size;
            public Image Top, Icon, Mark;
            public RectTransform IconRect, Words;
            public TMP_Text Name, Note;
        }

        /// <summary>One option in the editor's list: what it shows and what a press writes into the seat.</summary>
        struct Option
        {
            public string Name, Note;
            public Sprite Icon;
            public bool Picked;
            public VesselClassType Vessel;
            public string Id;
            public Element Element;
        }

        // ── Building ─────────────────────────────────────────────────────────

        /// <summary>The board, built once over <paramref name="host"/> (the Voyages page), its text in
        /// <paramref name="font"/>'s face and its Set sail a copy of <paramref name="cta"/>.</summary>
        public static VoyageBoard Create(RectTransform host, TMP_Text font, Button cta, SO_VesselList vessels, VoyageLines lines)
        {
            var go = new GameObject("VoyageBoard", typeof(RectTransform));
            go.transform.SetParent(host, false);
            go.transform.SetAsLastSibling();
            var rt = (RectTransform)go.transform;
            Stretch(rt);
            var board = go.AddComponent<VoyageBoard>();
            board._root = rt;
            board._font = font;
            board._ctaFrom = cta;
            board._vessels = vessels;
            board._lines = lines;
            board._style = VoyageBoardStyleSO.Load();
            board._catalog = PilotArtifactCatalogSO.Load();
            board.Build();
            return board;
        }

        void Build()
        {
            var s = _style;
            // The list: rows in a scroll, so more voyages than fit can be reached.
            var list = Node("List", _root);
            Anchor(list, 0f, 0f, s.ListRight, 1f);
            list.gameObject.AddComponent<RectMask2D>();
            _listContent = Node("Rows", list);
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = Vector2.one;
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.sizeDelta = Vector2.zero;
            var v = _listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8f;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            _listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = list.gameObject.AddComponent<ScrollRect>();
            scroll.content = _listContent;
            scroll.viewport = list;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            _message = Text(list, 18, s.Sub, TextAlignmentOptions.TopLeft);
            Anchor(_message.rectTransform, 0f, 0f, 1f, 1f);
            _message.textWrappingMode = TextWrappingModes.Normal;

            // The course column: VoyageJobsView stands the map and the edge view in it.
            _courseColumn = Node("Course", _root);
            Anchor(_courseColumn, s.CourseLeft, 0f, s.CourseRight, 1f);

            BuildPanel();
            BuildEditor();
        }

        void BuildPanel()
        {
            var s = _style;
            _panel = Node("Panel", _root);
            Anchor(_panel, s.PanelLeft, 0f, 1f, 1f);

            _title = Text(_panel, 28, s.Light, TextAlignmentOptions.TopLeft);
            _title.textWrappingMode = TextWrappingModes.Normal;
            Top(_title.rectTransform, 0f, 72f);
            _line = Text(_panel, 18, s.Light, TextAlignmentOptions.TopLeft);
            _line.textWrappingMode = TextWrappingModes.Normal;
            Top(_line.rectTransform, 80f, 56f);

            var stats = Node("Numbers", _panel);
            Top(stats, 160f, 64f);
            var h = stats.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 16f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = false;
            (_distanceLabel, _distance) = Number(stats);
            (_sailLabel, _sail) = Number(stats);
            (_intensityLabel, _intensity) = Number(stats);

            // The Ark escort's difficulty stepper, under the numbers.
            var stepper = Node("Stepper", _panel);
            Top(stepper, 236f, 40f);
            var sh = stepper.gameObject.AddComponent<HorizontalLayoutGroup>();
            sh.spacing = 8f;
            sh.childControlWidth = sh.childControlHeight = true;
            sh.childForceExpandWidth = sh.childForceExpandHeight = false;
            (_lower, _lowerText) = SmallButton(stepper, () => _voyage?.Lower?.Invoke());
            (_raise, _raiseText) = SmallButton(stepper, () => _voyage?.Raise?.Invoke());

            // From the foot up: Set sail, the seat cards, the squads.
            const float sailH = 56f, gap = 18f, squadH = 40f;
            var sail = Node("SetSail", _panel);
            sail.anchorMin = Vector2.zero;
            sail.anchorMax = new Vector2(1f, 0f);
            sail.pivot = new Vector2(0.5f, 0f);
            sail.sizeDelta = new Vector2(0f, sailH);
            sail.anchoredPosition = Vector2.zero;
            (_setSail, _setSailText) = Cta(sail);

            var cards = Node("Seats", _panel);
            cards.anchorMin = Vector2.zero;
            cards.anchorMax = new Vector2(1f, s.SeatCardShare);
            cards.offsetMin = new Vector2(0f, sailH + gap);
            cards.offsetMax = new Vector2(0f, sailH + gap);
            var ch = cards.gameObject.AddComponent<HorizontalLayoutGroup>();
            ch.spacing = 12f;
            ch.childControlWidth = ch.childControlHeight = true;
            ch.childForceExpandWidth = ch.childForceExpandHeight = true;
            for (int i = 0; i < 3; i++) _seats[i] = SeatCard(cards, i);

            _chips = Node("Squads", _panel);
            _chips.anchorMin = new Vector2(0f, s.SeatCardShare);
            _chips.anchorMax = new Vector2(1f, s.SeatCardShare);
            _chips.pivot = new Vector2(0.5f, 0f);
            _chips.sizeDelta = new Vector2(0f, squadH);
            _chips.anchoredPosition = new Vector2(0f, sailH + gap + 14f);
            var qh = _chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            qh.spacing = 10f;
            qh.childAlignment = TextAnchor.MiddleLeft;
            qh.childControlWidth = qh.childControlHeight = true;
            qh.childForceExpandWidth = qh.childForceExpandHeight = false;
            _squadLabel = Label(_chips, 13, s.Sub);
        }

        SeatView SeatCard(Transform parent, int seat)
        {
            var s = _style;
            var view = new SeatView();
            var rt = Node("Seat" + seat, parent);
            var plate = Sliced(rt, s.Sliver14Fill, new Color(s.Plate.r, s.Plate.g, s.Plate.b, 0.35f));
            plate.raycastTarget = true;
            var fade = Node("Fade", rt);
            Stretch(fade);
            Sliced(fade, s.FadeV, s.CardFoot);
            view.Button = rt.gameObject.AddComponent<Button>();
            view.Button.targetGraphic = plate;
            int index = seat;
            view.Button.onClick.AddListener(() => PressSeat(index));
            view.Group = rt.gameObject.AddComponent<CanvasGroup>();

            var face = Node("Pilot", rt);
            Anchor(face, 0.04f, 0.52f, 0.96f, 0.98f);
            view.Face = Picture(face);
            var hull = Node("Hull", rt);
            Anchor(hull, 0.06f, 0.30f, 0.94f, 0.52f);
            view.Hull = Picture(hull);
            view.Name = Text(rt, 18, s.Light, TextAlignmentOptions.Center);
            Anchor(view.Name.rectTransform, 0f, 0.19f, 1f, 0.30f);
            view.Lines = Text(rt, 15, s.Light, TextAlignmentOptions.Top);
            view.Lines.textWrappingMode = TextWrappingModes.Normal;
            Anchor(view.Lines.rectTransform, 0.03f, 0.02f, 0.97f, 0.19f);

            var edge = Node("Edge", rt);
            Stretch(edge);
            view.Edge = Sliced(edge, s.Sliver14Line, s.Outline);
            return view;
        }

        void BuildEditor()
        {
            var s = _style;
            _editor = Node("SeatEditor", _root);
            Anchor(_editor, 0f, 0f, s.CourseRight, 1f);
            _editorGroup = _editor.gameObject.AddComponent<CanvasGroup>();
            Hide(_editorGroup);

            var ground = Node("Ground", _editor);
            Stretch(ground);
            Sliced(ground, s.Sliver14Fill, s.Popup).raycastTarget = true;
            // The lit floor the choice stands on (Rev 83 S2).
            var pool = Node("Floor", _editor);
            Anchor(pool, 0.06f, 0.16f, 0.62f, 0.32f);
            var floor = pool.gameObject.AddComponent<Image>();
            floor.sprite = s.Pool;
            floor.color = new Color(s.Jade.r, s.Jade.g, s.Jade.b, 0.35f);
            floor.raycastTarget = false;
            var border = Node("Border", _editor);
            Stretch(border);
            Sliced(border, s.Sliver14Line, s.Light);

            _editorTitle = Label(_editor, 13, s.Light);
            Corner(_editorTitle.rectTransform, 40f, 32f, 760f, 20f);
            var tabs = Node("Tabs", _editor);
            Corner(tabs, 40f, 64f, 760f, 40f);
            var th = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            th.spacing = 32f;
            th.childAlignment = TextAnchor.UpperLeft;
            th.childControlWidth = th.childControlHeight = true;
            th.childForceExpandWidth = th.childForceExpandHeight = false;
            for (int i = 0; i < 4; i++) _tabs[i] = Tab(tabs, i);

            var hero = Node("Choice", _editor);
            Anchor(hero, 0.065f, 0.29f, 0.6f, 0.80f);
            _hero = Picture(hero);

            var words = Node("Words", _editor);
            words.anchorMin = Vector2.zero;
            words.anchorMax = new Vector2(0.645f, 0f);
            words.pivot = Vector2.zero;
            words.offsetMin = new Vector2(40f, 56f);
            words.offsetMax = new Vector2(0f, 56f + 150f);
            var wv = words.gameObject.AddComponent<VerticalLayoutGroup>();
            wv.spacing = 10f;
            wv.childAlignment = TextAnchor.LowerLeft;
            wv.childControlWidth = wv.childControlHeight = true;
            wv.childForceExpandWidth = true;
            wv.childForceExpandHeight = false;
            _heroName = Text(words, 36, s.Light, TextAlignmentOptions.BottomLeft);
            _heroLine = Text(words, 18, s.Light, TextAlignmentOptions.TopLeft);
            _heroLine.textWrappingMode = TextWrappingModes.Normal;

            _options = Node("Options", _editor);
            _options.anchorMin = new Vector2(0.677f, 0f);
            _options.anchorMax = new Vector2(0.968f, 1f);
            _options.offsetMin = new Vector2(0f, 56f);
            _options.offsetMax = new Vector2(0f, -32f);
            var ov = _options.gameObject.AddComponent<VerticalLayoutGroup>();
            ov.spacing = 8f;
            ov.childAlignment = TextAnchor.UpperCenter;
            ov.childControlWidth = ov.childControlHeight = true;
            ov.childForceExpandWidth = true;
            ov.childForceExpandHeight = false;

            // Done straddles the editor's foot (style guide section 6: a window's buttons straddle its bottom edge).
            var done = Node("Done", _editor);
            done.anchorMin = done.anchorMax = new Vector2(1f, 0f);
            done.pivot = new Vector2(1f, 0.5f);
            done.sizeDelta = new Vector2(150f, 48f);
            done.anchoredPosition = new Vector2(-40f, 0f);
            var doneFill = Sliced(done, s.Sliver10Fill, s.Popup);
            doneFill.raycastTarget = true;
            var doneEdge = Node("Edge", done);
            Stretch(doneEdge);
            Sliced(doneEdge, s.Sliver10Line, s.Light);
            var doneButton = done.gameObject.AddComponent<Button>();
            doneButton.targetGraphic = doneFill;
            doneButton.onClick.AddListener(CloseEditor);
            var doneText = Label(done, 18, s.Light);
            Stretch(doneText.rectTransform);
            doneText.alignment = TextAlignmentOptions.Center;
            doneText.text = _lines?.BoardDone ?? string.Empty;
        }

        TabView Tab(Transform parent, int index)
        {
            var s = _style;
            var view = new TabView();
            var rt = Node("Tab" + index, parent);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = Clear;
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8f;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            view.Label = Label(rt, 18, s.Sub);
            var line = Node("Line", rt);
            view.Line = line.gameObject.AddComponent<Image>();
            view.Line.raycastTarget = false;
            Le(line).preferredHeight = 2f;
            view.Button = rt.gameObject.AddComponent<Button>();
            view.Button.targetGraphic = hit;
            view.Button.onClick.AddListener(() => { _tab = index; DrawEditor(); });
            return view;
        }

        OptionView OptionRow()
        {
            var s = _style;
            var view = new OptionView();
            var rt = Node("Option", _options);
            var plate = Sliced(rt, s.Sliver10Fill, s.Plate);
            plate.raycastTarget = true;
            view.Button = rt.gameObject.AddComponent<Button>();
            view.Button.targetGraphic = plate;
            view.Size = Le(rt);
            var top = Node("Picked", rt);
            top.anchorMin = new Vector2(0f, 1f);
            top.anchorMax = Vector2.one;
            top.pivot = new Vector2(0.5f, 1f);
            top.sizeDelta = new Vector2(0f, 3f);
            view.Top = top.gameObject.AddComponent<Image>();
            view.Top.color = s.Jade;
            view.Top.raycastTarget = false;
            view.IconRect = Node("Icon", rt);
            view.IconRect.anchorMin = new Vector2(0f, 0f);
            view.IconRect.anchorMax = new Vector2(0f, 1f);
            view.IconRect.pivot = new Vector2(0f, 0.5f);
            view.Icon = Picture(view.IconRect);
            view.Words = Node("Words", rt);
            view.Words.anchorMin = Vector2.zero;
            view.Words.anchorMax = Vector2.one;
            var wv = view.Words.gameObject.AddComponent<VerticalLayoutGroup>();
            wv.spacing = 2f;
            wv.childAlignment = TextAnchor.MiddleLeft;
            wv.childControlWidth = wv.childControlHeight = true;
            wv.childForceExpandWidth = true;
            wv.childForceExpandHeight = false;
            view.Name = Text(view.Words, 18, s.Light, TextAlignmentOptions.Left);
            view.Note = Text(view.Words, 14, s.Sub, TextAlignmentOptions.Left);
            // The picked option's mark: a small Jade diamond at the row's end.
            var mark = Node("Mark", rt);
            mark.anchorMin = mark.anchorMax = new Vector2(1f, 0.5f);
            mark.sizeDelta = new Vector2(9f, 9f);
            mark.anchoredPosition = new Vector2(-24f, 0f);
            mark.localRotation = Quaternion.Euler(0f, 0f, 45f);
            view.Mark = mark.gameObject.AddComponent<Image>();
            view.Mark.color = s.Jade;
            view.Mark.raycastTarget = false;
            return view;
        }

        RowView RowFor(int index)
        {
            while (_rows.Count <= index)
            {
                var s = _style;
                var view = new RowView();
                int i = _rows.Count;
                var rt = Node("Voyage" + i, _listContent);
                Le(rt).preferredHeight = s.RowHeight;
                view.Plate = Sliced(rt, s.Sliver14Fill, Clear);
                view.Plate.raycastTarget = true;
                view.Button = rt.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = view.Plate;
                view.Button.onClick.AddListener(() => _pick?.Invoke(i));
                var bar = Node("Bar", rt);
                bar.anchorMin = Vector2.zero;
                bar.anchorMax = new Vector2(0f, 1f);
                bar.pivot = new Vector2(0f, 0.5f);
                bar.sizeDelta = new Vector2(3f, 0f);
                view.Bar = bar.gameObject.AddComponent<Image>();
                view.Bar.color = s.Cyan;
                view.Bar.raycastTarget = false;
                var thumb = Node("Course", rt);
                thumb.anchorMin = thumb.anchorMax = new Vector2(0f, 0.5f);
                thumb.pivot = new Vector2(0f, 0.5f);
                thumb.sizeDelta = new Vector2(150f, 110f);
                thumb.anchoredPosition = new Vector2(22f, 0f);
                thumb.gameObject.AddComponent<RectMask2D>();
                var poster = Node("Water", thumb);
                Stretch(poster);
                view.Poster = poster.gameObject.AddComponent<VoyageRoutePoster>();
                var words = Node("Words", rt);
                words.anchorMin = Vector2.zero;
                words.anchorMax = Vector2.one;
                words.offsetMin = new Vector2(194f, 0f);
                words.offsetMax = new Vector2(-16f, 0f);
                var wv = words.gameObject.AddComponent<VerticalLayoutGroup>();
                wv.spacing = 8f;
                wv.childAlignment = TextAnchor.MiddleLeft;
                wv.childControlWidth = wv.childControlHeight = true;
                wv.childForceExpandWidth = true;
                wv.childForceExpandHeight = false;
                view.Title = Text(words, 22, s.Light, TextAlignmentOptions.Left);
                view.By = Text(words, 15, s.Sub, TextAlignmentOptions.Left);
                view.Meta = Text(words, 15, s.Sub, TextAlignmentOptions.Left);
                _rows.Add(view);
            }
            return _rows[index];
        }

        Action<int> _pick;

        // ── Showing ──────────────────────────────────────────────────────────

        /// <summary>
        /// Show <paramref name="rows"/> with row <paramref name="open"/> open (-1: none), the open voyage in the right panel
        /// and the squad. <paramref name="message"/> stands in the list when there are no rows (no work offered).
        /// </summary>
        public void Show(IReadOnlyList<Row> rows, int open, Action<int> pick, Voyage voyage, VoyageMapGraphic map, string message)
        {
            var s = _style;
            _pick = pick;
            _map = map;
            _voyage = voyage;
            int count = rows?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                var row = rows[i];
                var view = RowFor(i);
                view.Button.gameObject.SetActive(true);
                bool on = i == open;
                view.Plate.color = on ? s.RowPicked : Clear;
                view.Bar.enabled = on;
                Set(view.Title, row.Title);
                view.Title.color = on ? s.Light : s.Sub;
                Set(view.By, row.By);
                Set(view.Meta, row.Meta);
                view.Poster.Show(map, row.Course, Route, Ark, Sea);
            }
            for (int i = count; i < _rows.Count; i++) _rows[i].Button.gameObject.SetActive(false);
            Set(_message, count == 0 ? message : null);

            Set(_title, voyage?.Title);
            Set(_line, voyage?.Line);
            bool numbers = voyage != null && !string.IsNullOrEmpty(voyage.Distance);
            Set(_distanceLabel, numbers ? _lines?.BoardDistanceLabel : null);
            Set(_sailLabel, numbers ? _lines?.BoardSailLabel : null);
            Set(_intensityLabel, numbers ? _lines?.BoardIntensityLabel : null);
            Set(_distance, voyage?.Distance);
            Set(_sail, voyage?.Sail);
            Set(_intensity, voyage?.Intensity);
            bool step = voyage?.Lower != null && voyage.Raise != null;
            _lower.gameObject.SetActive(step);
            _raise.gameObject.SetActive(step);
            if (step)
            {
                Set(_lowerText, voyage.LowerLabel);
                Set(_raiseText, voyage.RaiseLabel);
            }
            if (_setSail)
            {
                _setSail.interactable = voyage?.SetSail != null;
                Set(_setSailText, !string.IsNullOrEmpty(voyage?.SailLabel) ? voyage.SailLabel : _lines?.CardSetSail);
            }
            Draw();
        }

        void Draw()
        {
            DrawSquads();
            DrawSeats();
            DrawEditor();
        }

        void Update()
        {
            if (Mathf.Approximately(_fade, _fadeTo)) return;
            float step = _style.EditorFade > 0f ? Time.unscaledDeltaTime / _style.EditorFade : 1f;
            _fade = Mathf.MoveTowards(_fade, _fadeTo, step);
            _editorGroup.alpha = _fade;
        }

        // ── The squads and their seats ───────────────────────────────────────

        static SquadBook Book => UGSDataService.Instance?.SquadRepo?.Data?.Book;

        static void SaveBook() => UGSDataService.Instance?.SquadRepo?.MarkDirty();

        void DrawSquads()
        {
            var s = _style;
            var book = Book;
            int count = book?.Squads?.Count ?? 0;
            Set(_squadLabel, count > 0 ? _lines?.BoardSquadLabel : _lines?.CardNoSquad);
            while (_chipViews.Count < count) _chipViews.Add(Chip(_chipViews.Count));
            for (int i = 0; i < _chipViews.Count; i++)
            {
                var chip = _chipViews[i];
                bool show = i < count;
                chip.Button.gameObject.SetActive(show);
                if (!show) continue;
                var squad = book.Squads[i];
                bool on = i == book.ActiveIndex;
                Set(chip.Label, squad == null ? null : squad.IsJoined()
                    ? TemplateText.Fill(_lines?.BoardSquadJoined, "name", squad.Name, "owner", squad.OwnerName)
                    : squad.Name);
                chip.Label.color = on ? s.Light : s.Sub;
                chip.Edge.enabled = !on;
                chip.Bar.enabled = on;
            }
        }

        ChipView Chip(int index)
        {
            var s = _style;
            var view = new ChipView();
            var rt = Node("Squad" + index, _chips);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = Clear;
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(16, 16, 8, 8);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            view.Label = Text(rt, 15, s.Sub, TextAlignmentOptions.Center);
            var edge = Node("Edge", rt);
            Le(edge).ignoreLayout = true;
            Stretch(edge);
            view.Edge = Sliced(edge, s.Sliver10Line, s.Outline);
            var bar = Node("Bar", rt);
            Le(bar).ignoreLayout = true;
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(0f, 2f);
            view.Bar = bar.gameObject.AddComponent<Image>();
            view.Bar.color = s.Cyan;
            view.Bar.raycastTarget = false;
            view.Button = rt.gameObject.AddComponent<Button>();
            view.Button.targetGraphic = hit;
            view.Button.onClick.AddListener(() => PickSquad(index));
            return view;
        }

        void PickSquad(int index)
        {
            var book = Book;
            if (book == null || index == book.ActiveIndex || !book.TrySetActive(index)) return;
            _openSeat = -1;
            SaveBook();
            SquadChanged?.Invoke();
            Draw();
        }

        void DrawSeats()
        {
            var s = _style;
            var active = Book?.GetActive();
            for (int seat = 0; seat < 3; seat++)
            {
                var view = _seats[seat];
                bool has = active != null;
                view.Button.gameObject.SetActive(has);
                if (!has) continue;
                var slot = active.GetSlot(seat);
                var vessel = Vessel(slot.Vessel);
                bool editable = VoyageOpenCard.CanEdit(active, seat);
                var keywords = vessel ? vessel.Keywords : null;
                var pilot = _catalog ? _catalog.ResolvePilot(slot, keywords) : null;
                Show(view.Face, pilot ? pilot.Portrait : null);
                Show(view.Hull, Hull(vessel));
                Set(view.Name, vessel ? vessel.CardName : null);
                string who = TemplateText.Fill(_lines?.BoardSeatWho, "who", Who(active, seat, slot), "pilot", PilotName(slot, pilot, editable));
                string second = editable
                    ? TemplateText.Fill(_lines?.BoardSeatKit, "element", ElementName(slot.Element), "artifact", ArtifactName(slot, vessel, editable))
                    : TemplateText.Fill(_lines?.BoardSeatNotYours, "owner", slot.IsPlayer() ? slot.MemberName : active.OwnerName);
                Set(view.Lines, who + "\n" + second);
                view.Group.alpha = editable ? 1f : 0.55f;
                view.Button.interactable = editable;
                bool open = seat == _openSeat;
                view.Edge.sprite = open && s.Sliver14Line2 ? s.Sliver14Line2 : s.Sliver14Line;
                view.Edge.color = open ? s.Jade : s.Outline;
            }
        }

        void PressSeat(int seat)
        {
            var active = Book?.GetActive();
            if (active == null || !VoyageOpenCard.CanEdit(active, seat)) return;
            _openSeat = _openSeat == seat ? -1 : seat;
            _tab = 0;
            Draw();
        }

        void CloseEditor()
        {
            _openSeat = -1;
            Draw();
        }

        string Who(SavedSquad squad, int seat, SquadSlot slot)
        {
            if (VoyageOpenCard.IsMySeat(squad, seat)) return _lines?.CardSeatYou;
            if (slot.IsOpen()) return _lines?.CardSeatOpen;
            if (slot.IsPlayer()) return slot.MemberName;
            return _lines?.CardSeatAi;
        }

        /// <summary>The person's name on the player's own seats (a person this crew holds), else the callsign.</summary>
        static string PilotName(SquadSlot slot, PilotSO pilot, bool editable)
        {
            if (editable && pilot && PilotLoadoutStore.TryGetPilot(slot.PilotId, out var person) && person.CallsignOrTemplate() == pilot.Id)
                return person.DisplayName();
            return pilot ? pilot.DisplayName : string.Empty;
        }

        string ArtifactName(SquadSlot slot, SO_Vessel vessel, bool editable)
        {
            var kind = _catalog ? _catalog.ResolveArtifact(slot, vessel ? vessel.Keywords : null) : null;
            if (editable && kind && PilotLoadoutStore.TryGetArtifact(slot.ArtifactId, out var held) && held.Kind() == kind.Id) return held.Name;
            return kind ? kind.DisplayName : _lines?.CardNoArtifact;
        }

        // ── The seat editor ──────────────────────────────────────────────────

        void DrawEditor()
        {
            var s = _style;
            var book = Book;
            var active = book?.GetActive();
            if (_openSeat < 0 || active == null || !VoyageOpenCard.CanEdit(active, _openSeat))
            {
                _openSeat = -1;
                _fadeTo = 0f;
                _editorGroup.interactable = _editorGroup.blocksRaycasts = false;
                return;
            }
            _fadeTo = 1f;
            _editorGroup.interactable = _editorGroup.blocksRaycasts = true;

            int seat = _openSeat;
            var slot = active.GetSlot(seat);
            var vessel = Vessel(slot.Vessel);
            string[] roles = { _lines?.CardSeatLeader, _lines?.CardSeatRogueOne, _lines?.CardSeatRogueTwo };
            Set(_editorTitle, TemplateText.Fill(_lines?.BoardEditorTitle, "role", roles[seat], "who", Who(active, seat, slot), "squad", active.Name));
            string[] names = { _lines?.CardSettingVessel, _lines?.CardSettingPilot, _lines?.CardSettingElement, _lines?.CardSettingArtifact };
            for (int i = 0; i < 4; i++)
            {
                Set(_tabs[i].Label, names[i]);
                bool on = i == _tab;
                _tabs[i].Label.color = on ? s.Light : s.Sub;
                _tabs[i].Line.color = on ? s.Light : Clear;
            }

            Offer(active, seat, slot, vessel);
            float height = _tab switch { 0 => s.OptionHeights.x, 1 => s.OptionHeights.y, _ => s.OptionHeights.z };
            float iconW = _tab switch { 0 => 120f, 1 => 72f, 3 => 56f, _ => 0f };
            while (_optionViews.Count < _offered.Count) _optionViews.Add(OptionRow());
            Option picked = default;
            for (int i = 0; i < _optionViews.Count; i++)
            {
                var view = _optionViews[i];
                bool show = i < _offered.Count;
                view.Button.gameObject.SetActive(show);
                if (!show) continue;
                var option = _offered[i];
                if (option.Picked) picked = option;
                view.Size.preferredHeight = height;
                view.Size.minHeight = height * 0.6f;
                bool icon = iconW > 0f && option.Icon;
                view.IconRect.gameObject.SetActive(icon);
                view.IconRect.sizeDelta = new Vector2(iconW, -12f);
                view.IconRect.anchoredPosition = new Vector2(18f, 0f);
                Show(view.Icon, option.Icon);
                view.Words.offsetMin = new Vector2(icon ? 18f + iconW + 16f : 24f, 0f);
                view.Words.offsetMax = new Vector2(-48f, 0f);
                Set(view.Name, option.Name);
                Set(view.Note, option.Note);
                view.Note.gameObject.SetActive(!string.IsNullOrEmpty(option.Note));
                view.Top.enabled = option.Picked;
                view.Mark.enabled = option.Picked;
                var taken = option;
                view.Button.onClick.RemoveAllListeners();
                view.Button.onClick.AddListener(() => Pick(taken));
            }

            // The choice, large: the hull's render, the pilot full length, the artifact's icon; an element is its name.
            Sprite hero = _tab switch
            {
                0 => vessel ? (vessel.SquadImage ? vessel.SquadImage : Hull(vessel)) : null,
                1 => picked.Icon,
                3 => picked.Icon,
                _ => null,
            };
            Show(_hero, hero);
            Set(_heroName, picked.Name);
            Set(_heroLine, _tab switch
            {
                0 => vessel ? vessel.Description : null,
                1 => TemplateText.Fill(_lines?.BoardPilotLine, "vessel", vessel ? vessel.CardName : null, "squad", active.Name),
                2 => _lines?.BoardElementLine,
                _ => string.IsNullOrEmpty(picked.Id) ? _lines?.BoardNoArtifactLine : ArtifactLine(picked.Id),
            });
        }

        string ArtifactLine(string id) =>
            _catalog && _catalog.TryGetArtifact(id, out var kind) && kind ? kind.Description : null;

        /// <summary>The open tab's options for <paramref name="seat"/>, under the same rules the Ark's picker keeps.</summary>
        void Offer(SavedSquad squad, int seat, SquadSlot slot, SO_Vessel vessel)
        {
            _offered.Clear();
            switch (_tab)
            {
                case 0:
                    if (_vessels && _vessels.VesselList != null)
                        foreach (var v in _vessels.VesselList)
                            if (v && !v.IsLocked)
                                _offered.Add(new Option { Name = v.CardName, Icon = Hull(v), Vessel = v.Class, Picked = v.Class == slot.Vessel });
                    break;
                case 1:
                {
                    // The crew's people whose callsign can fly the hull, in crew order; one who flies another seat swaps.
                    string current = PilotLoadoutStore.TryGetPilot(slot.PilotId, out var seated) ? seated.Id : slot.PilotId;
                    if (!_catalog || !vessel) break;
                    PilotLoadoutStore.GetCrew(_crew);
                    foreach (var person in _crew)
                    {
                        if (!_catalog.TryGetPilot(person.Id, out var callsign) || !callsign || callsign.Requirement == null
                            || !callsign.Requirement.IsMetBy(vessel.Class, vessel.Keywords)) continue;
                        bool mine = person.Id == current;
                        string note = mine ? _lines?.BoardPilotFlying : null;
                        for (int k = 0; k < 3 && note == null; k++)
                        {
                            if (k == seat) continue;
                            var other = squad.GetSlot(k);
                            string id = PilotLoadoutStore.TryGetPilot(other.PilotId, out var p) ? p.Id : other.PilotId;
                            if (id != person.Id) continue;
                            var ov = Vessel(other.Vessel);
                            note = TemplateText.Fill(_lines?.BoardPilotSwaps, "vessel", ov ? ov.CardName : null);
                        }
                        _offered.Add(new Option { Name = person.DisplayName(), Note = note, Icon = callsign.Portrait, Id = person.Id, Picked = mine });
                    }
                    break;
                }
                case 2:
                    foreach (var e in Elements)
                        _offered.Add(new Option { Name = ElementName(e), Element = e, Picked = e == slot.Element });
                    break;
                default:
                {
                    // None, then the hoard's artifacts the hull can carry (each one of a kind).
                    string current = PilotLoadoutStore.TryGetArtifact(slot.ArtifactId, out var carried) ? carried.Id : null;
                    _offered.Add(new Option { Name = _lines?.CardNoArtifact, Id = null, Picked = string.IsNullOrEmpty(current) });
                    if (!_catalog || !vessel) break;
                    PilotLoadoutStore.GetHoard(_hoard);
                    foreach (var artifact in _hoard)
                        if (_catalog.TryGetArtifact(artifact.Id, out var kind) && kind && kind.Requirement != null
                            && kind.Requirement.IsMetBy(vessel.Class, vessel.Keywords))
                            _offered.Add(new Option { Name = artifact.Name, Icon = kind.Icon, Id = artifact.Id, Picked = artifact.Id == current });
                    break;
                }
            }
        }

        /// <summary>One press picks and saves: the seat changes, the pilot swap and the crew fill run, and the card behind
        /// changes at once (the Ark's rules, as <c>VoyageOpenCard.ChangeSetting</c> keeps them).</summary>
        void Pick(Option option)
        {
            if (option.Picked) return;
            var book = Book;
            var active = book?.GetActive();
            int seat = _openSeat;
            if (active == null || seat < 0 || !VoyageOpenCard.CanEdit(active, seat)) return;
            var squad = SquadMembership.Copy(active);
            var slot = squad.GetSlot(seat);
            string previousPilot = slot.PilotId;
            switch (_tab)
            {
                case 0:
                    slot = slot.WithVessel(option.Vessel);
                    // The player's own card starts on the person they keep in that vessel's Hangar seat (a crew id).
                    if (VoyageOpenCard.IsMySeat(squad, seat)) slot.PilotId = PilotLoadoutStore.GetPilot(option.Vessel);
                    break;
                case 1:
                    slot.PilotId = option.Id;
                    break;
                case 2:
                    slot.Element = option.Element;
                    break;
                default:
                    slot.ArtifactId = option.Id;
                    break;
            }
            squad.SetSlot(seat, slot);
            squad.KeepCardsOwn(seat, previousPilot);
            PilotLoadoutStore.GetCrew(_crew);
            _ids.Clear();
            foreach (var p in _crew) if (!string.IsNullOrEmpty(p.Id)) _ids.Add(p.Id);
            squad.FillPilots(_ids);
            int index = book.ActiveIndex;
            if (index < 0 || index >= book.Squads.Count) return;
            book.Squads[index] = squad;
            SaveBook();
            SquadChanged?.Invoke();
            Draw();
        }

        // ── Data ─────────────────────────────────────────────────────────────

        SO_Vessel Vessel(VesselClassType cls) =>
            _vessels && _vessels.TryGetVesselByClass(cls, out var v, reportMissing: false) ? v : null;

        static Sprite Hull(SO_Vessel v) =>
            !v ? null : v.SquadImage ? v.SquadImage : v.CardSilohoutteActive ? v.CardSilohoutteActive : v.IconActive;

        string ElementName(Element e) => e switch
        {
            Element.Charge => _lines?.ElementCharge,
            Element.Mass => _lines?.ElementMass,
            Element.Space => _lines?.ElementSpace,
            Element.Time => _lines?.ElementTime,
            _ => string.Empty,
        };

        // ── Small builders ───────────────────────────────────────────────────

        static void Hide(CanvasGroup group)
        {
            group.alpha = 0f;
            group.interactable = group.blocksRaycasts = false;
        }

        static void Set(TMP_Text t, string value)
        {
            if (t) t.text = value ?? string.Empty;
        }

        static void Show(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.enabled = sprite;
        }

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

        static void Anchor(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>A full-width band <paramref name="height"/> tall, <paramref name="y"/> below its parent's top.</summary>
        static void Top(RectTransform rt, float y, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, -y);
        }

        /// <summary>A box at (<paramref name="x"/>, <paramref name="y"/>) from its parent's top left.</summary>
        static void Corner(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        static LayoutElement Le(Component c) =>
            c.TryGetComponent<LayoutElement>(out var le) ? le : c.gameObject.AddComponent<LayoutElement>();

        static Image Sliced(RectTransform rt, Sprite sprite, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Image Picture(RectTransform rt)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.enabled = false;
            return img;
        }

        TMP_Text Text(Transform parent, float size, Color color, TextAlignmentOptions align)
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
            return t;
        }

        /// <summary>A label: the style's label face, in capitals, spaced (the style guide's eyebrow).</summary>
        TMP_Text Label(Transform parent, float size, Color color)
        {
            var t = Text(parent, size, color, TextAlignmentOptions.Left);
            if (_style.LabelFont)
            {
                t.font = _style.LabelFont;
                t.fontSharedMaterial = _style.LabelFont.material;
            }
            t.fontStyle = FontStyles.UpperCase;
            t.characterSpacing = 14f;
            return t;
        }

        (TMP_Text label, TMP_Text value) Number(Transform parent)
        {
            var col = Node("Number", parent);
            var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6f;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            var label = Label(col, 13, _style.Sub);
            var value = Text(col, 28, _style.Light, TextAlignmentOptions.Left);
            return (label, value);
        }

        (Button b, TMP_Text t) SmallButton(Transform parent, Action onClick)
        {
            var s = _style;
            var rt = Node("Step", parent);
            var fill = Sliced(rt, s.Sliver10Fill, s.Plate);
            fill.raycastTarget = true;
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(16, 16, 8, 8);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            var t = Text(rt, 15, s.Light, TextAlignmentOptions.Center);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = fill;
            b.onClick.AddListener(() => onClick());
            return (b, t);
        }

        /// <summary>Set sail: a copy of the window's own call to action (the Lime CTA), filling <paramref name="rt"/>.</summary>
        (Button b, TMP_Text t) Cta(RectTransform rt)
        {
            Button b;
            TMP_Text t;
            if (_ctaFrom)
            {
                b = Instantiate(_ctaFrom, rt);
                b.name = "SetSail";
                b.onClick.RemoveAllListeners();
                b.gameObject.SetActive(true);
                var brt = (RectTransform)b.transform;
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.pivot = new Vector2(0.5f, 0.5f);
                brt.offsetMin = brt.offsetMax = Vector2.zero;
                brt.localScale = Vector3.one;
                t = b.GetComponentInChildren<TMP_Text>(true);
            }
            else
            {
                var fill = Sliced(rt, _style.Sliver10Fill, _style.Plate);
                fill.raycastTarget = true;
                b = rt.gameObject.AddComponent<Button>();
                b.targetGraphic = fill;
                t = Text(rt, 18, _style.Light, TextAlignmentOptions.Center);
                Stretch(t.rectTransform);
            }
            b.onClick.AddListener(() => _voyage?.SetSail?.Invoke());
            return (b, t);
        }
    }
}

using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The voyage's jobs on the Voyage window (Garrett, 2026-10-28: "the jobs should be the different voyages"), laid out
    /// as Garrett sketched it on 2026-11-01: the galaxy map (top view) with its edge view under it, and under those the
    /// voyage cards in a grid like the arcade games' tiles, one per job the squad's pilots bring from where the Ark is now
    /// (and the way home when it is away, and the Ark escort). The map is a galaxy explorer ("the map divides them as a
    /// fun interactive galaxy explorer deciding where you want your voyages to take you"): drag, pinch or scroll it, and
    /// tap a place to open the voyage that sails there. OPEN on a card, or a press on it, expands it over the others to its
    /// brief and focuses the map on its course; the map stays. Set sail, on the open card, loads the voyage scene with that
    /// job (<see cref="VoyageLaunch"/>; freestyle is not involved). When the voyage is over the scene returns to the menu and
    /// this window offers the next jobs, from where the squad anchored, grown from the news the last outcome wrote.
    ///
    /// <para>The first card is the prefab's own (<see cref="template"/>), in the grid; the rest are copies of it, made here.
    /// The map and its edge view stand just before the grid. While a voyage is out the one card is the job under way,
    /// open, its Set sail dark.</para>
    ///
    /// <para>Rebuilt to Shombith's pick on 2026-11-02 (Docs/MENU_REDESIGN_B.md section 119): with a
    /// <see cref="mapColumn"/> the HyperSea map and its edge view fill that column on the left, the voyage cards stand
    /// two across on the right, and a job's card (or its place pressed on the map) slides the open voyage over the cards
    /// (<see cref="VoyageOpenCard"/>: the voyage, its Ark crew, its story) instead of expanding over them. The Ark
    /// escort's card still opens in place, with its difficulty stepper.</para>
    /// </summary>
    public class VoyageJobsView : MonoBehaviour
    {
        [Tooltip("The card every job is drawn on; copies of it stand beside it in its grid, one per job.")]
        [SerializeField] VoyageJobCard template;

        [Tooltip("The vessels the brief names the squad's hulls from (their card names).")]
        [SerializeField] SO_VesselList vessels;
        [Tooltip("The galaxy map's height above the cards.")]
        [SerializeField] float mapHeight = 360f;
        [Tooltip("The edge view's height, under the map.")]
        [SerializeField] float edgeHeight = 96f;
        [Tooltip("Cards across the grid.")]
        [SerializeField] int columns = 3;
        [Tooltip("Where the HyperSea map and its edge view stand (the map over the edge view, filling it). Empty: just " +
                 "before the cards, as the window first stood them.")]
        [SerializeField] RectTransform mapColumn;
        [Tooltip("The column the voyage cards stand in: the open voyage slides over it. Empty: a job's card expands " +
                 "over the others instead.")]
        [SerializeField] RectTransform cardColumn;
        [Tooltip("The space between the map and the edge view under it, in the map column.")]
        [SerializeField] float edgeGap = 12f;
        [Tooltip("The Voyages tab as Shombith picked it (Docs/MENU_REDESIGN_B.md sections 180-184): two screens built by " +
                 "VoyageBoard over this page - the voyage on the flat map with its explorer, then Configure squad (Rev 98 " +
                 "1A and 2D); the Saga, Quests and Episodes pages are the Sea table (Rev 90, sections 171-172). Off: the " +
                 "map column and the voyage cards as before.")]
        [SerializeField] bool board = true;
        [Tooltip("The Saga tab is locked (Shombith, 2026-11-12: \"lock saga\"): its table is dimmed and says Coming soon, " +
                 "no part is listed and nothing launches from it. Off: the saga's parts, the squad and Set sail.")]
        [SerializeField] bool sagaLocked = true;

        const string MapName = "GalaxyMap";
        const string EdgeName = "EdgeView";

        readonly List<VoyageJobCard> _cards = new();
        readonly List<string> _destinations = new();
        VoyageMapGraphic _map;
        RectTransform _mapHost;
        VoyageEdgeGraphic _edge;
        RectTransform _edgeHost;
        GridLayoutGroup _grid;
        LayoutElement _gridSize;
        float _gridWidth;
        int _open = -1;
        string _placeCaption;
        ModalWindowManager _window;
        VoyageOpenCard _card;
        int _jobCount;
        VoyageBoard _board;
        readonly List<VoyageBoard.Row> _boardRows = new();

        void OnEnable()
        {
            VoyageHarbour.Changed += Refresh;
            // The window is shown and hidden by its canvas group, so it is re-read on every open: the squad (and so the
            // pilots bringing jobs) may have changed since.
            _window = GetComponentInParent<ModalWindowManager>(true);
            if (_window) _window.OnModalOpened += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            VoyageHarbour.Changed -= Refresh;
            if (_window) _window.OnModalOpened -= Refresh;
            _window = null;
        }

        void OnDestroy()
        {
            if (_board)
            {
                _board.SquadChanged -= Refresh;
                _board.ScreenChanged -= Refresh;
            }
            if (!_card) return;
            _card.Closed -= CloseCard;
            _card.Step -= StepCard;
            _card.SquadChanged -= Refresh;
        }

        void LateUpdate()
        {
            // The grid's cells share its width: re-cut them when the window's width changes.
            if (!_grid) return;
            float width = ((RectTransform)_grid.transform).rect.width;
            if (Mathf.Approximately(width, _gridWidth)) return;
            _gridWidth = width;
            int cols = Mathf.Max(1, columns);
            float cell = Mathf.Max(1f, (width - _grid.spacing.x * (cols - 1)) / cols);
            _grid.cellSize = new Vector2(cell, VoyageJobCard.ClosedHeight);
        }

        /// <summary>Redraw the map and the cards from the jobs on offer now.</summary>
        public void Refresh()
        {
            if (!template) return;
            Grid();
            _destinations.Clear();
            // In the menu no harbour stands (it stands in the voyage scene, VoyageScene): the board is read from the voyage
            // settings and the saved galaxy. A harbour, where one stands, still says what is out.
            var harbour = VoyageHarbour.Current;
            var style = harbour ? harbour.Definition : VoyageToyDefinitionSO.Load();
            var voyage = harbour ? harbour.Content : style ? style.Content : VoyageContentSO.Load();
            if (!voyage || voyage.Content == null)
            {
                // No voyage content: a plain card, no map.
                var shelf = voyage ? voyage : VoyageContentSO.Load();
                var card = Use(0)[0];
                card.ShowStepper(null, null, null, null);
                card.Show(string.Empty, shelf ? shelf.Lines.VoyageName : null, null, null, false, null, null, null, null);
                Stand(-1);
                ShowMap(false);
                HideCard();
                return;
            }
            var content = voyage.Content;
            var lines = voyage.Lines;

            var escort = style ? style.ArkEscort : null;
            if (board && RefreshBoard(content, lines, style, harbour, escort)) return;
            if (harbour && harbour.IsEscorting && escort)
            {
                var card = Use(1)[0];
                var course = harbour.EscortCourse;
                ShowEscort(card, content, lines, escort, harbour.EscortDifficulty, course, sail: false, open: true, onToggle: null);
                Stand(0);
                HideCard();
                // The map frames the escort's course while it is out, as it does a job's (ARK_ESCORT.md §5).
                if (course != null) Map(content, lines, style, course[0], course, CourseLine(content, lines, course));
                else ShowMap(false);
                return;
            }

            if (harbour && harbour.IsUnderway)
            {
                HideCard();
                var job = harbour.CurrentJob;
                var cards = Use(1);
                cards[0].ShowStepper(null, null, null, null);
                if (job == null)
                {
                    cards[0].Show(string.Empty, lines.VoyageName, null, null, false, null, null, null, null);
                    Stand(-1);
                    Map(content, lines, style, null, null, null);
                    return;
                }
                var to = content.Location(job.To);
                var underway = VoyageRules.Route(content, job.From, job.To);
                string course = CourseLine(content, lines, underway);
                cards[0].Show(
                    VoyageRules.Fill(lines.CardUnderway, content.Location(job.From), to, to, content.Person(job.PilotId)),
                    job.Title, course, Brief(content, lines, job, underway), true, null, null, null, null);
                Stand(0);
                Map(content, lines, style, job.From, underway, course);
                return;
            }

            VoyageState state;
            var gameData = VoyageLaunch.ResolveGameData();
            var jobs = harbour ? harbour.Board(out state)
                : VoyageHarbour.Board(style, VoyageRun.ActiveSquad(gameData), VoyageRun.LiveDomain(gameData), out state);
            string here = state?.At;
            int extra = escort ? 1 : 0;
            if (jobs.Count == 0)
            {
                // Only the escort's card (after the no-work card) can be open here.
                if (_open != 1 || !escort) _open = -1;
                var none = Use(1 + extra);
                none[0].ShowStepper(null, null, null, null);
                none[0].Show(string.Empty, lines.NoWork, null, null, false, null, null, null, null);
                if (escort) ShowEscort(none[1], content, lines, escort, Difficulty, VoyageRules.EscortRoute(state, content), sail: true,
                    open: _open == 1, onToggle: () => OpenCard(1));
                Stand(_open);
                HideCard();
                Map(content, lines, style, here, null, _placeCaption);
                return;
            }
            if (_open >= jobs.Count + extra) _open = -1;

            var shown = Use(jobs.Count + extra);
            if (escort) ShowEscort(shown[jobs.Count], content, lines, escort, Difficulty, VoyageRules.EscortRoute(state, content),
                sail: true, open: _open == jobs.Count, onToggle: () => OpenCard(jobs.Count));
            bool focused = false;
            bool slide = cardColumn;
            _jobCount = jobs.Count;
            for (int i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                _destinations.Add(job.To);
                var from = content.Location(job.From);
                var to = content.Location(job.To);
                string eyebrow = VoyageRules.Fill(job.IsReturn ? lines.CardHomeRoute : lines.CardRoute, from, to, to,
                    content.Person(job.PilotId));
                var taken = job;
                int index = i;
                bool open = i == _open;
                var route = VoyageRules.Route(content, job.From, job.To);
                string course = CourseLine(content, lines, route);
                shown[i].ShowStepper(null, null, null, null);
                // With the slide-in voyage the cards stay small: the open one slides over them instead.
                bool expand = open && !slide;
                shown[i].Show(eyebrow, job.Title, course, expand ? Brief(content, lines, job, route) : null, expand,
                    () => OpenCard(index), lines.CardOpen, lines.CardClose, () => SetSail(taken));
                if (open)
                {
                    Map(content, lines, style, here, route, course);
                    focused = true;
                    if (slide) ShowCard(OpenModel(content, lines, job, route, i, jobs.Count, () => SetSail(taken)));
                }
            }
            bool jobOpen = _open >= 0 && _open < jobs.Count;
            Stand(slide && jobOpen ? -1 : _open);
            if (!slide || !jobOpen) HideCard();
            if (!focused) Map(content, lines, style, here, null, _placeCaption);
        }

        // ── The board (the Voyages tab as two screens, Rev 98 1A and 2D) ──────────────────────────────

        /// <summary>
        /// Draw the Voyages tab as the board: one row per voyage on offer (and the Ark escort), the open one framed on the
        /// course column's map and shown in the right panel with the squad. A voyage is always open while any is offered.
        /// False when the board cannot stand (no page to build it on): the old layout draws instead.
        /// </summary>
        bool RefreshBoard(VoyageContent content, VoyageLines lines, VoyageToyDefinitionSO style, VoyageHarbour harbour,
            ArkEscortSO escort)
        {
            if (!EnsureBoard(lines)) return false;
            HideCard();
            if (cardColumn) cardColumn.gameObject.SetActive(false);
            // The cards' grid stands down (never the page itself, which the board stands on).
            var grid = template ? template.transform.parent : null;
            if (grid && grid != transform) grid.gameObject.SetActive(false);
            _boardRows.Clear();
            var map = EnsureMap();
            // The voyage screen's map is the explorer, flat: its tier ladder, readout, drag, pinch and wheel, and a press
            // on a place sets the course there (Rev 98 1A). Behind the squad screen it is the course alone.
            if (map) map.Plain = _board.OnSquadScreen;
            FitBoardColumn();

            // The escort or a job under way: its one row, open, Set sail dark.
            if (harbour && harbour.IsEscorting && escort)
            {
                var course = harbour.EscortCourse;
                var shown = EscortVoyage(content, lines, escort, harbour.EscortDifficulty, course, sail: false);
                _boardRows.Add(EscortRow(content, lines, escort, harbour.EscortDifficulty, course));
                if (course != null) Map(content, lines, style, course[0], course, CourseLine(content, lines, course));
                else Map(content, lines, style, null, null, null);
                if (map) map.FrameOn(_boardRows[0].Course);
                _board.Show(_boardRows, 0, _ => { }, shown, map, null);
                return true;
            }
            if (harbour && harbour.IsUnderway)
            {
                var job = harbour.CurrentJob;
                if (job == null)
                {
                    Map(content, lines, style, null, null, null);
                    _board.Show(_boardRows, -1, _ => { }, null, map, lines.VoyageName);
                    return true;
                }
                var underway = VoyageRules.Route(content, job.From, job.To);
                var model = OpenModel(content, lines, job, underway, 0, 1, null);
                _boardRows.Add(BoardRow(lines, model, 0, content.Person(job.PilotId)));
                var voyage = BoardVoyage(lines, job, model, null, 0, 1, content.Person(job.PilotId));
                voyage.SailLabel = lines.BoardUnderway;
                Map(content, lines, style, job.From, underway, CourseLine(content, lines, underway));
                if (map) map.FrameOn(model.Course);
                _board.Show(_boardRows, 0, _ => { }, voyage, map, null);
                return true;
            }

            var gameData = VoyageLaunch.ResolveGameData();
            var jobs = harbour ? harbour.Board(out VoyageState state)
                : VoyageHarbour.Board(style, VoyageRun.ActiveSquad(gameData), VoyageRun.LiveDomain(gameData), out state);
            string here = state?.At;
            int count = jobs.Count + (escort ? 1 : 0);
            _destinations.Clear();
            if (count == 0)
            {
                Map(content, lines, style, here, null, _placeCaption);
                _board.Show(_boardRows, -1, _ => { }, null, map, lines.NoWork);
                return true;
            }
            if (_open < 0 || _open >= count) _open = 0;
            VoyageBoard.Voyage open = null;
            for (int i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                _destinations.Add(job.To);
                var route = VoyageRules.Route(content, job.From, job.To);
                var taken = job;
                var model = OpenModel(content, lines, job, route, i, jobs.Count, () => SetSail(taken));
                _boardRows.Add(BoardRow(lines, model, i, content.Person(job.PilotId)));
                if (i != _open) continue;
                open = BoardVoyage(lines, job, model, model.SetSail, i, jobs.Count, content.Person(job.PilotId));
                Map(content, lines, style, here, route, CourseLine(content, lines, route));
                if (map) map.FrameOn(model.Course);
            }
            if (escort)
            {
                var course = VoyageRules.EscortRoute(state, content);
                _boardRows.Add(EscortRow(content, lines, escort, Difficulty, course));
                if (_open == jobs.Count)
                {
                    open = EscortVoyage(content, lines, escort, Difficulty, course, sail: true);
                    if (course is { Count: >= 2 }) Map(content, lines, style, course[0], course, CourseLine(content, lines, course));
                    else Map(content, lines, style, here, null, null);
                    if (map) map.FrameOn(_boardRows[^1].Course);
                }
            }
            _board.Show(_boardRows, _open, PickBoardRow, open, map, null);
            return true;
        }

        void PickBoardRow(int index)
        {
            if (index == _open) return;
            _open = index;
            _placeCaption = null;
            Refresh();
        }

        bool EnsureBoard(VoyageLines lines)
        {
            if (_board) return true;
            if (lines == null || transform is not RectTransform page) return false;
            _board = VoyageBoard.Create(page, template ? template.LabelTemplate : null, vessels, lines);
            _board.SquadChanged += Refresh;
            _board.ScreenChanged += Refresh;
            // The window's tabs as the board draws them: plain words, the shown one underlined, one step down the type
            // scale (Rev 88: "the top texts are too big").
            var style = VoyageBoardStyleSO.Load();
            var tabs = GetComponentInParent<VoyageWindowTabs>(true);
            if (tabs)
            {
                tabs.Plain(style.LabelFont, 15f, 34f);
                // The Saga, Quests and Episodes pages take the same table (Rev 90).
                VoyageSlabPage.Stand(tabs, new VoyageKit(style, lines, template ? template.LabelTemplate : null), vessels, page,
                    sagaLocked);
            }
            // The map and its edge view stand in the board's course column.
            if (mapColumn)
            {
                var course = _board.CourseColumn;
                mapColumn.anchorMin = course.anchorMin;
                mapColumn.anchorMax = course.anchorMax;
                mapColumn.pivot = new Vector2(0.5f, 0.5f);
                mapColumn.offsetMin = mapColumn.offsetMax = Vector2.zero;
                mapColumn.SetAsLastSibling();
                _board.transform.SetAsLastSibling();
            }
            return true;
        }

        /// <summary>The table in the course column: the map over its edge-on view (Rev 98 1A); behind the squad screen
        /// the map alone, over the whole page.</summary>
        void FitBoardColumn()
        {
            if (!mapColumn || !_board) return;
            EnsureEdge();
            _board.FitTable(_mapHost, _edgeHost);
        }

        static VoyageBoard.Row BoardRow(VoyageLines lines, VoyageOpenCard.Model model, int index, CrewPerson person)
        {
            var row = new VoyageBoard.Row
            {
                Title = TemplateText.Fill(lines.BoardRowTitle, "n", (index + 1).ToString("00", CultureInfo.InvariantCulture),
                    "place", model.To),
                By = person != null ? TemplateText.Fill(lines.BoardRowBy, "pilot", person.Name) : null,
                Meta = BoardMeta(lines, model),
            };
            row.Course.AddRange(model.Course);
            return row;
        }

        static string BoardMeta(VoyageLines lines, VoyageOpenCard.Model model) =>
            string.IsNullOrEmpty(model.LightYears) ? null
                : TemplateText.Fill(lines.BoardRowMeta, "ly", model.LightYears, "minutes", model.Minutes, "intensity", model.Intensity);

        static VoyageBoard.Voyage BoardVoyage(VoyageLines lines, VoyageJob job, VoyageOpenCard.Model model, System.Action setSail,
            int index, int count, CrewPerson person) => new()
        {
            Kicker = TemplateText.Fill(person != null ? lines.BoardKicker : lines.BoardKickerAlone,
                "n", (index + 1).ToString("00", CultureInfo.InvariantCulture),
                "count", count.ToString("00", CultureInfo.InvariantCulture), "pilot", person?.Name),
            Place = model.To,
            Title = job.Title,
            Line = job.Summary,
            Distance = model.CourseValue,
            Sail = model.SailValue,
            Intensity = string.IsNullOrEmpty(model.Intensity) ? null
                : TemplateText.Fill(lines.BoardIntensityValue, "intensity", model.Intensity),
            SetSail = setSail,
        };

        VoyageBoard.Row EscortRow(VoyageContent content, VoyageLines lines, ArkEscortSO escort, int difficulty,
            IReadOnlyList<string> course)
        {
            string legs = escort.Legs.Count.ToString(CultureInfo.InvariantCulture);
            var row = new VoyageBoard.Row
            {
                Title = TemplateText.Fill(escort.CardTitle, "difficulty", difficulty.ToString(CultureInfo.InvariantCulture), "legs", legs),
                By = TemplateText.Fill(escort.CardLine, "legs", legs, "difficulty", difficulty.ToString(CultureInfo.InvariantCulture)),
                Meta = course is { Count: >= 2 } ? CourseLine(content, lines, course) : null,
            };
            if (course is { Count: >= 2 })
            {
                var sea = VoyageChart.For(content)?.Along(course);
                if (sea != null) foreach (var p in sea.Points) row.Course.Add(VoyageMapGraphic.MapPoint(p));
            }
            return row;
        }

        /// <summary>The Ark escort in the right panel: its title and line at the picked difficulty, and, before it sails, the
        /// difficulty stepper and Set sail.</summary>
        VoyageBoard.Voyage EscortVoyage(VoyageContent content, VoyageLines lines, ArkEscortSO escort, int difficulty,
            IReadOnlyList<string> course, bool sail)
        {
            var row = EscortRow(content, lines, escort, difficulty, course);
            var voyage = new VoyageBoard.Voyage { Title = row.Title, Line = row.By };
            if (!sail)
            {
                voyage.SailLabel = lines.BoardUnderway;
                return voyage;
            }
            voyage.SetSail = () => SetSailEscort(difficulty);
            voyage.LowerLabel = escort.LowerLabel;
            voyage.RaiseLabel = escort.RaiseLabel;
            voyage.Lower = () => { Difficulty = difficulty - 1; Refresh(); };
            voyage.Raise = () => { Difficulty = difficulty + 1; Refresh(); };
            return voyage;
        }

        /// <summary>Open card <paramref name="index"/> (closing any other), or close it if it is the open one.</summary>
        void OpenCard(int index)
        {
            _open = _open == index ? -1 : index;
            _placeCaption = null;
            Refresh();
        }

        /// <summary>
        /// A place tapped on the map: the voyage that sails there opens (the first, if several do); a place no voyage
        /// sails to names itself under the map (the content's <see cref="VoyageLines.MapPlace"/>).
        /// </summary>
        void OnPlaceTapped(string id)
        {
            int index = _destinations.IndexOf(id);
            if (index >= 0)
            {
                if (_open != index) OpenCard(index);
                return;
            }
            var harbour = VoyageHarbour.Current;
            var settings = harbour ? null : VoyageToyDefinitionSO.Load();
            var voyage = harbour ? harbour.Content : settings ? settings.Content : VoyageContentSO.Load();
            var place = voyage && voyage.Content != null ? voyage.Content.Location(id) : null;
            if (place == null) return;
            _open = -1;
            _placeCaption = TemplateText.Fill(voyage.Lines.MapPlace, "name", place.Name, "blurb", place.Blurb);
            Refresh();
        }

        // ── The grid ─────────────────────────────────────────────────────────

        /// <summary>The template's grid, ready to stand cards in.</summary>
        void Grid()
        {
            if (_grid || !template) return;
            var parent = template.transform.parent;
            if (!parent || !parent.TryGetComponent(out _grid)) return;
            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.constraintCount = Mathf.Max(1, columns);
            if (!parent.TryGetComponent(out _gridSize)) _gridSize = parent.gameObject.AddComponent<LayoutElement>();
        }

        /// <summary>
        /// Stand the cards in their cells in order, and card <paramref name="open"/> (if any) over them; the grid is at
        /// least an open card tall while one stands.
        /// </summary>
        void Stand(int open)
        {
            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i]) _cards[i].transform.SetSiblingIndex(i);
            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i]) _cards[i].Overlay(i == open && _cards[i].gameObject.activeSelf);
            if (_gridSize) _gridSize.minHeight = open >= 0 ? VoyageJobCard.OpenHeight : -1f;
        }

        // ── The one galaxy map and its edge view ─────────────────────────────

        /// <summary>
        /// Draw the window's one galaxy map: every place with the squad's spot, or, with a card open, that job's course
        /// framed (Garrett, 2026-11-01: the map "remains" while a card is drilled into), and the edge view under it.
        /// </summary>
        void Map(VoyageContent content, VoyageLines lines, VoyageToyDefinitionSO style, string here,
            IReadOnlyList<string> route, string caption)
        {
            var map = EnsureMap();
            if (!map) return;
            ShowMap(true);
            map.SetLines(lines);
            // A place this crew has not found yet is not on its chart (DEPTH.md §8).
            var galaxy = VoyageHarbour.Galaxy(style);
            map.Visible = galaxy != null ? id => VoyageRules.IsVisible(galaxy, content, id) : null;
            map.Show(content, style, here, route, caption, focus: route != null);
            var edge = EnsureEdge();
            bool charted = map.Chart != null;
            if (_edgeHost) _edgeHost.gameObject.SetActive(charted && edge);
            if (charted && edge) edge.Follow(map, EdgeCaption(content, lines, here, route), template.LabelTemplate);
        }

        void ShowMap(bool show)
        {
            if (_mapHost) _mapHost.gameObject.SetActive(show);
            if (_edgeHost && !show) _edgeHost.gameObject.SetActive(false);
        }

        /// <summary>The edge view's caption: the place it centres on (the course's end, else here) and its height.</summary>
        static string EdgeCaption(VoyageContent content, VoyageLines lines, string here, IReadOnlyList<string> route)
        {
            if (lines == null || string.IsNullOrEmpty(lines.EdgeCaption)) return null;
            var place = content.Location(route != null && route.Count > 0 ? route[route.Count - 1] : here);
            if (place == null) return null;
            float z = place.GalaxyZ;
            string ly = Mathf.Abs(Mathf.Round(z)).ToString("N0", CultureInfo.InvariantCulture);
            string height = Mathf.Abs(z) < 10f ? lines.EdgeMidplane
                : TemplateText.Fill(z > 0f ? lines.EdgeAbove : lines.EdgeBelow, "ly", ly);
            return TemplateText.Fill(lines.EdgeCaption, "place", place.Name, "height", height);
        }

        /// <summary>A host for the map or its edge view, just before the grid, as wide as the column, masked so a
        /// zoomed map does not spill over the cards.</summary>
        RectTransform Host(string name, float height)
        {
            if (mapColumn) return ColumnHost(name);
            var grid = template ? template.transform.parent : null;
            var parent = grid ? grid.parent : null;
            if (!parent) return null;
            var existing = parent.Find(name);
            if (existing) return (RectTransform)existing;
            var host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent, false);
            host.transform.SetSiblingIndex(grid.GetSiblingIndex());
            var rt = (RectTransform)host.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, height);
            var le = host.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            host.AddComponent<RectMask2D>();
            return rt;
        }

        /// <summary>A host in the map column: the map fills it above the edge view, the edge view stands at its foot.</summary>
        RectTransform ColumnHost(string name)
        {
            var existing = mapColumn.Find(name);
            if (existing) return (RectTransform)existing;
            var host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(mapColumn, false);
            var rt = (RectTransform)host.transform;
            bool edge = name == EdgeName;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = edge ? new Vector2(1f, 0f) : Vector2.one;
            rt.pivot = edge ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
            rt.offsetMin = edge ? Vector2.zero : new Vector2(0f, edgeHeight + edgeGap);
            rt.offsetMax = edge ? new Vector2(0f, edgeHeight) : Vector2.zero;
            host.AddComponent<RectMask2D>();
            return rt;
        }

        static T Fill<T>(RectTransform host) where T : Graphic
        {
            var found = host.GetComponentInChildren<T>(true);
            if (found) return found;
            var go = new GameObject(typeof(T).Name, typeof(RectTransform));
            go.transform.SetParent(host, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go.AddComponent<T>();
        }

        VoyageMapGraphic EnsureMap()
        {
            if (_map) return _map;
            _mapHost = Host(MapName, mapHeight);
            if (!_mapHost) return null;
            _map = Fill<VoyageMapGraphic>(_mapHost);
            _map.SetLabelTemplate(template.LabelTemplate);
            _map.PlaceTapped += OnPlaceTapped;
            return _map;
        }

        VoyageEdgeGraphic EnsureEdge()
        {
            if (_edge) return _edge;
            // After the map: it stands just before the grid too, so it is placed second.
            _edgeHost = Host(EdgeName, edgeHeight);
            if (!_edgeHost) return null;
            _edge = Fill<VoyageEdgeGraphic>(_edgeHost);
            return _edge;
        }

        /// <summary>
        /// The open card's brief, the content's <see cref="VoyageLines.CardDetails"/>: the mission, the place, the squad
        /// that will sail it, the course's intensity, the pay and who brought the job.
        /// </summary>
        string Brief(VoyageContent content, VoyageLines lines, VoyageJob job, IReadOnlyList<string> route)
        {
            string template = job.IsReturn ? lines?.CardHomeDetails : lines?.CardDetails;
            if (string.IsNullOrEmpty(template)) return null;
            var to = content.Location(job.To);
            var person = content.Person(job.PilotId);
            var kind = content.Job(job.TemplateId);
            var sea = VoyageChart.For(content)?.Along(route);
            string text = VoyageRules.Fill(template, content.Location(job.From), to, to, person);
            // The job's own intensity (the sea's plus the place's pressure) and its own pay, as the board priced it.
            int intensity = job.Intensity > 0 ? job.Intensity : sea?.Intensity ?? 1;
            int pay = job.Pay > 0 ? job.Pay : kind?.Reward ?? 0;
            var galaxy = VoyageHarbour.Galaxy(null);
            string standing = person != null && galaxy != null
                ? VoyageRules.TierName(content, VoyageRules.Tier(galaxy, content, person.Faction)) : string.Empty;
            return TemplateText.Fill(text,
                "summary", job.Summary,
                "place", to?.Blurb,
                "story", person?.Story,
                "squad", Squad(lines),
                "role", VoyageRules.RoleName(content, job.Role),
                "standing", standing,
                "warnings", Warnings(lines, job),
                "intensity", intensity.ToString(CultureInfo.InvariantCulture),
                "reward", !job.IsReturn ? pay.ToString(CultureInfo.InvariantCulture) : string.Empty);
        }

        /// <summary>A job's warnings (Docs/Voyages/DEPTH.md §7) in the brief's authored block, or nothing when there are none.</summary>
        static string Warnings(VoyageLines lines, VoyageJob job)
        {
            if (lines == null || job?.Warnings == null || job.Warnings.Count == 0 || string.IsNullOrEmpty(lines.CardWarnings)) return string.Empty;
            return TemplateText.Fill(lines.CardWarnings, "warnings", string.Join(lines.CardWarningSeparator ?? string.Empty, job.Warnings));
        }

        /// <summary>The crew's own record (Docs/Voyages/DEPTH.md §9), through the authored log line; empty before a voyage.</summary>
        static string LogLine(VoyageLines lines)
        {
            var galaxy = VoyageHarbour.Galaxy(null);
            if (galaxy?.Log == null || galaxy.Log.Voyages == 0 || string.IsNullOrEmpty(lines.StoryLogLine)) return null;
            var log = galaxy.Log;
            return TemplateText.Fill(lines.StoryLogLine,
                "voyages", log.Voyages.ToString(CultureInfo.InvariantCulture),
                "extracted", log.Extracted.ToString(CultureInfo.InvariantCulture),
                "relics", log.Relics.ToString(CultureInfo.InvariantCulture),
                "charted", log.Charted.ToString(CultureInfo.InvariantCulture),
                "arks", log.ArksLost.ToString(CultureInfo.InvariantCulture),
                "kaiju", log.KaijuFelled.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>The active squad as its seats: each one's vessel and pilot, through the authored seat line.</summary>
        string Squad(VoyageLines lines)
        {
            var squad = VoyageRun.ActiveSquad((CosmicShore.Utility.GameDataSO)null);
            if (squad == null || string.IsNullOrEmpty(lines.CardSquadSeat)) return string.Empty;
            var catalog = PilotArtifactCatalogSO.Load();
            var seats = new List<string>();
            for (int i = 0; i < SavedSquad.SlotCount; i++)
            {
                var slot = squad.GetSlot(i);
                string vessel = vessels && vessels.TryGetVesselByClass(slot.Vessel, out var v, reportMissing: false)
                    ? v.CardName : string.Empty;
                var pilot = catalog ? catalog.ResolvePilot(slot, null) : null;
                // The seat's own person when the crew holds them (Docs/Pilots/INVENTORY.md §3), else the callsign's name.
                string name = PilotLoadoutStore.TryGetPilot(slot.PilotId, out var person) && pilot && UniqueId.TemplateOf(person.Id) == pilot.Id
                    ? person.DisplayName() : pilot ? pilot.DisplayName : string.Empty;
                seats.Add(TemplateText.Fill(lines.CardSquadSeat, "vessel", vessel, "pilot", name));
            }
            return string.Join(lines.CardSquadSeparator ?? string.Empty, seats);
        }

        /// <summary>
        /// The card's course line on the real HyperSea (Docs/Voyages/VOYAGE_BOARD.md): how many light years the Ark
        /// sails (light takes as many years), the thickest water on the way and how rough the course is, filled into the
        /// content's authored <see cref="VoyageLines.CardCourse"/>. Null when the content is not charted.
        /// </summary>
        static string CourseLine(VoyageContent content, VoyageLines lines, IReadOnlyList<string> route)
        {
            if (lines == null || string.IsNullOrEmpty(lines.CardCourse)) return null;
            var voyage = VoyageChart.For(content)?.Along(route);
            if (voyage == null) return null;
            string ly = voyage.LightYears.ToString("N0", CultureInfo.InvariantCulture);
            return TemplateText.Fill(lines.CardCourse,
                "ly", ly,
                "years", ly,
                "minutes", voyage.Minutes.ToString("N0", CultureInfo.InvariantCulture),
                "sea", SeaName(lines, voyage.ThickestSea),
                "intensity", voyage.Intensity.ToString(CultureInfo.InvariantCulture));
        }

        internal static string SeaName(VoyageLines lines, SeaKind sea) => sea switch
        {
            SeaKind.Spray => lines.SeaSpray,
            SeaKind.Froth => lines.SeaFroth,
            SeaKind.Water => lines.SeaWater,
            SeaKind.Thick => lines.SeaThick,
            _ => lines.SeaReef,
        };

        const string DifficultyKey = "ArkEscort.Difficulty";
        const int DefaultDifficulty = 3;

        /// <summary>The escort difficulty this viewer last picked (a per-device convenience).</summary>
        static int Difficulty
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, DefaultDifficulty),
                ArkEscortSO.MinDifficulty, ArkEscortSO.MaxDifficulty);
            set => PlayerPrefs.SetInt(DifficultyKey,
                Mathf.Clamp(value, ArkEscortSO.MinDifficulty, ArkEscortSO.MaxDifficulty));
        }

        /// <summary>The Ark escort's card: its title at the picked difficulty; open, a stepper to change it, and Set sail. With
        /// a <paramref name="course"/> (location ids, from first; ARK_ESCORT.md §5) the card names where the escort sails
        /// from and to, as the jobs' cards do, and its course line is measured on the real HyperSea.</summary>
        void ShowEscort(VoyageJobCard card, VoyageContent content, VoyageLines lines, ArkEscortSO escort, int difficulty,
            IReadOnlyList<string> course, bool sail, bool open, System.Action onToggle)
        {
            string d = difficulty.ToString();
            // The voyage this escort sails (or will): its worlds in order and its modifiers, as the card's course line.
            var voyage = sail ? ArkEscortRun.PreviewVoyage(escort, difficulty) : VoyageHarbour.Current?.EscortVoyage;
            string legs = (voyage is { Legs: { Count: > 0 } } ? voyage.Legs.Count : escort.Legs.Count).ToString();
            string worlds = voyage is { WorldNames: { Count: > 0 } } ? voyage.Describe() : null;
            string line = TemplateText.Fill(escort.CardLine, "legs", legs, "difficulty", d);
            string sea = null;
            if (course is { Count: >= 2 })
            {
                // Where from and where to, in the jobs' own authored route line, and the real sea between them.
                line = VoyageRules.Fill(lines.CardHomeRoute, content.Location(course[0]), content.Location(course[^1]),
                    content.Location(course[^1]), null) + "\n" + line;
                sea = CourseLine(content, lines, course);
            }
            string courseText = string.IsNullOrEmpty(sea) ? worlds : string.IsNullOrEmpty(worlds) ? sea : sea + "\n" + worlds;
            // Like a job's card it opens over the others, where the player picks the difficulty and sets sail.
            card.Show(line, TemplateText.Fill(escort.CardTitle, "difficulty", d, "legs", legs), courseText, null, open, onToggle,
                lines?.CardOpen, lines?.CardClose, sail ? () => SetSailEscort(difficulty) : null);
            if (sail && open)
                card.ShowStepper(escort.LowerLabel, escort.RaiseLabel,
                    () => { Difficulty = difficulty - 1; Refresh(); },
                    () => { Difficulty = difficulty + 1; Refresh(); });
            else card.ShowStepper(null, null, null, null);
        }

        // Set sail loads the voyage scene with the choice (VoyageLaunch); freestyle is not involved.
        static void SetSailEscort(int difficulty) =>
            VoyageLaunch.Begin(VoyageLaunchRequest.ForEscort(difficulty));

        static void SetSail(VoyageJob job) =>
            VoyageLaunch.Begin(VoyageLaunchRequest.ForJob(job));

        // ── The open voyage (slides over the cards) ──────────────────────────

        void ShowCard(VoyageOpenCard.Model model)
        {
            if (!_card)
            {
                var lines = VoyageHarbour.Current && VoyageHarbour.Current.Content ? VoyageHarbour.Current.Content.Lines
                    : VoyageContentSO.Load()?.Lines;
                if (lines == null || !cardColumn) return;
                _card = VoyageOpenCard.Create(cardColumn, template.LabelTemplate, template.SetSailButton, _map, vessels, lines);
                _card.Closed += CloseCard;
                _card.Step += StepCard;
                _card.SquadChanged += Refresh;
            }
            _card.SetMap(_map);
            _card.Show(model, keepLevel: true);
        }

        void HideCard()
        {
            if (_card) _card.Hide();
        }

        void CloseCard()
        {
            _open = -1;
            Refresh();
        }

        /// <summary>The open voyage's pager: the voyage <paramref name="step"/> away, going round.</summary>
        void StepCard(int step)
        {
            if (_jobCount <= 0 || _open < 0 || _open >= _jobCount) return;
            _open = VoyageCardRules.Wrap(_open, step, _jobCount);
            _placeCaption = null;
            Refresh();
        }

        /// <summary>
        /// The open voyage's words and numbers, all from the content's lines (<see cref="VoyageLines"/>), the job, the
        /// crew member who brought it, its places and its course on the real sea.
        /// </summary>
        VoyageOpenCard.Model OpenModel(VoyageContent content, VoyageLines lines, VoyageJob job, IReadOnlyList<string> route,
            int index, int count, System.Action setSail)
        {
            var m = new VoyageOpenCard.Model();
            var from = content.Location(job.From);
            var to = content.Location(job.To);
            var person = content.Person(job.PilotId);
            var voyage = VoyageChart.For(content)?.Along(route);
            string n = (index + 1).ToString(CultureInfo.InvariantCulture);
            string ly = voyage != null ? voyage.LightYears.ToString("N0", CultureInfo.InvariantCulture) : string.Empty;
            string minutes = voyage != null ? voyage.Minutes.ToString("N0", CultureInfo.InvariantCulture) : string.Empty;
            string intensity = job.Intensity > 0 ? job.Intensity.ToString(CultureInfo.InvariantCulture)
                : voyage != null ? voyage.Intensity.ToString(CultureInfo.InvariantCulture) : string.Empty;
            m.Pager = TemplateText.Fill(lines.CardPager, "n", n, "count", count.ToString(CultureInfo.InvariantCulture));
            m.Kicker = TemplateText.Fill(lines.CardKicker, "n", n, "place", to?.Name);
            m.Title = job.Title;
            m.CourseChip = voyage != null ? TemplateText.Fill(lines.CardCourseChip, "ly", ly) : string.Empty;
            m.FoundBy = person != null ? TemplateText.Fill(lines.CardFoundBy, "pilot", person.Name) : string.Empty;
            m.CourseValue = voyage != null ? TemplateText.Fill(lines.CardCourseValue, "ly", ly) : string.Empty;
            m.SailValue = voyage != null ? TemplateText.Fill(lines.CardSailValue, "minutes", minutes) : string.Empty;
            m.IntensityValue = voyage != null ? TemplateText.Fill(lines.CardIntensityValue, "intensity", intensity) : string.Empty;
            m.Cells = voyage != null
                ? TemplateText.Fill(lines.CardCells, "cells", voyage.Cells.ToString(CultureInfo.InvariantCulture), "minutes", minutes)
                : string.Empty;
            m.From = from?.Name;
            m.To = to?.Name;
            m.LightYears = voyage != null ? ly : null;
            m.Minutes = voyage != null ? minutes : null;
            m.Intensity = intensity;
            if (voyage != null)
                foreach (var p in voyage.Points) m.Course.Add(VoyageMapGraphic.MapPoint(p));
            m.Beats.AddRange(VoyageCardRules.Beats(
                new VoyageCardRules.Beat(lines.StoryNews, job.Source?.Line, from?.Name),
                new VoyageCardRules.Beat(lines.StoryPlace, to?.Blurb, to?.Name),
                new VoyageCardRules.Beat(lines.StoryCrew, person?.Story, TemplateText.Fill(lines.StoryCrewBy, "pilot", person?.Name)),
                new VoyageCardRules.Beat(lines.StoryWater,
                    voyage != null ? TemplateText.Fill(lines.StoryWaterLine, "years", ly, "to", to?.Name, "minutes", minutes) : null,
                    voyage != null ? TemplateText.Fill(lines.StoryWaterBy, "sea", SeaName(lines, voyage.ThickestSea)) : null),
                new VoyageCardRules.Beat(lines.StoryWorld,
                    job.Warnings is { Count: > 0 } ? string.Join(" ", job.Warnings) : null,
                    job.Warnings is { Count: > 0 } ? TemplateText.Fill(lines.StoryWorldBy, "warnings", string.Join(" ", job.Warnings)) : null),
                new VoyageCardRules.Beat(lines.StoryLog, LogLine(lines), null)));
            m.SetSail = setSail;
            return m;
        }

        /// <summary><paramref name="count"/> cards showing (the template first, then copies after it, in its grid), the
        /// rest hidden; at least the template stands, so the window never loses its card.</summary>
        List<VoyageJobCard> Use(int count)
        {
            if (_cards.Count == 0) _cards.Add(template);
            int need = Mathf.Max(1, count);
            while (_cards.Count < need)
            {
                var copy = Instantiate(template, template.transform.parent);
                copy.name = template.name + "_" + _cards.Count;
                // A copy of a card that showed the escort's stepper carries its buttons, unowned: the copy makes its own.
                foreach (var b in copy.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    if (b.name is "StepDown" or "StepUp") Destroy(b.gameObject);
                copy.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + _cards.Count);
                _cards.Add(copy);
            }
            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i]) _cards[i].gameObject.SetActive(i < need);
            return _cards;
        }
    }
}

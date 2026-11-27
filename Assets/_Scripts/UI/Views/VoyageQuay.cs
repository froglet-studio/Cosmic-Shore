using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The quay, the HyperSea page in the window, laid to Frogma Rev 138 Board's own numbers (Rev 134 Board B with Full
    /// screen; picked by Shombith 2026-11-24, Docs/MENU_REDESIGN_B.md section 231). <see cref="VoyageSeaPage"/> stands it in
    /// the window and fades it as the full screen opens. Every piece is placed absolutely in the board's units (the window
    /// page is x 40-1880, y 160-1046 of the 1920 x 1080 board), never by a layout group, so it lands where the board draws it.
    /// <list type="bullet">
    /// <item><b>Docked at</b>: where the Ark is and how many jobs are on the quay.</item>
    /// <item><b>The jobs</b>: one 230 x 420 card per voyage type's giver (Salvage, Escort the Ark, Bounty, Hold the Core,
    /// the Voyage Codex's order) and the Milestone 1 Crystal Run as it is, 240 apart: the giver's picture, the kind, the
    /// voyage's name and the archetypes it asks for, stacked; the picked one bright with one cyan line at its foot.</item>
    /// <item><b>On the chart</b> (620 x 420, right): the lane from the port to the voyage's end, a ring per cell between,
    /// the length and the difficulty under it, Full screen in its corner. While the live HyperSea map stands behind it
    /// (<see cref="LiveMap"/>) the map draws the course.</item>
    /// <item><b>The ask</b> (Rev 146 A, "Seat asks", picked by Shombith 2026-11-26, section 236): the giver and their
    /// faction, the giver's words, then "Bring me" and one row per asked seat: the archetype, the giver's reason, and at
    /// the right the squad's vessel that fills it, met fully or by half (<see cref="VoyageSeatFill"/>).</item>
    /// <item><b>Your crew says</b>: each seat's pilot, their line and their mood (<see cref="VoyageMood"/>); See every
    /// archetype (Y) under it.</item>
    /// <item><b>The foot</b>: the squad fit, the pay in crystals and Take the job (A), which opens The voyage.</item>
    /// </list>
    /// </summary>
    public sealed class VoyageQuay
    {
        // Rev 138 Board, in the page's units (board x - 40, y - 160).
        public const float CardY = 30f, CardWidth = 230f, CardHeight = 420f, CardStep = 240f;
        public const int MaxCards = 5;
        public static readonly Rect Chart = new(1220f, 30f, 620f, 420f);
        // Rev 146 A ("Seat asks", picked by Shombith 2026-11-26, section 236): the giver's words in two lines, then a row
        // per asked seat (58 tall): the archetype, the giver's reason, and the squad vessel that fills it at the right.
        const float AskY = 470f, AskWidth = 1180f, QuoteY = 494f, QuoteHeight = 84f, BringY = 580f, RowY = 602f,
            RowHeight = 58f, ReasonX = 200f, CrewX = 1220f, CrewY = 531f, CrewStep = 56f, FootY = 829f, FootHeight = 56f,
            TakeWidth = 290f, SeeX = 1220f, SeeY = 720f, SeeWidth = 350f, SeeHeight = 44f, Inset = 18f, FootShade = 190f,
            DimPicture = 0.55f, FitWidth = 180f, CardRow = 22f;

        readonly VoyageBoard _board;
        readonly VoyageKit _kit;
        readonly VoyageSeats _rules;
        readonly VoyageQuaySO _words;
        readonly VoyageArchetypesSO _archetypes;
        readonly PilotArtifactCatalogSO _catalog;

        public RectTransform Root { get; }
        readonly CanvasGroup _group;
        readonly List<CardView> _cards = new();
        readonly List<int> _order = new();
        TMP_Text _docked, _askLabel, _askQuote, _bringMe, _yourSquad, _crewLabel, _fitText, _pay, _chartLabel, _chartFoot, _from, _to;
        RectTransform _ask, _crew, _fit, _payRow, _chart;
        Image _chartFill;
        bool _liveMap;
        readonly List<AskRow> _askRows = new();
        readonly List<IReadOnlyList<VesselArchetype>> _squadArchetypes = new();
        readonly List<CrewRow> _crewRows = new();
        readonly Image[] _fitSegments = new Image[3];
        readonly List<VoyageRing> _cellRings = new();
        VoyageRing _fromMark, _toMark;
        VoyageChartLine _lane;
        Button _take;

        sealed class CardView
        {
            public RectTransform Root;
            public Image Picture, Foot;
            public VoyageOutline Edge;
            public TMP_Text Kind, Title, AsksNone;
            public readonly List<(RectTransform root, VoyageGlyph glyph, TMP_Text name)> Asks = new();
            public Button Button;
        }

        sealed class AskRow
        {
            public RectTransform Root;
            public VoyageGlyph Glyph;
            public TMP_Text Name, Reason, Squad;
            public VoyageMetMark Met;
        }

        sealed class CrewRow
        {
            public RectTransform Root;
            public Image Face;
            public TMP_Text Quote;
            public VoyageMoodMark Mood;
        }

        public VoyageQuay(VoyageBoard board, VoyageKit kit, VoyageSeats rules, RectTransform frame)
        {
            _board = board;
            _kit = kit;
            _rules = rules;
            _words = VoyageQuaySO.Load();
            _archetypes = VoyageArchetypesSO.Load();
            _catalog = PilotArtifactCatalogSO.Load();
            Root = VoyageKit.Node("Quay", frame);
            Root.anchorMin = Root.anchorMax = Root.pivot = Vector2.zero;
            _group = Root.gameObject.AddComponent<CanvasGroup>();
            _docked = Text(Root, "Docked", 0f, 0f, AskWidth, 22f, 15f, _kit.Sub);
            BuildChart();
            BuildAsk();
            BuildCrew();
            BuildFoot();
        }

        /// <summary>The chart's rect in the page's units: the window's table stands there (the full screen grows from it).</summary>
        public static Rect ChartRect => Chart;

        // Over the live map the chart keeps only a whisper of its ground, so its label and foot stay readable.
        const float ChartFillOverMap = 0.12f;

        /// <summary>
        /// True while the live HyperSea map stands behind the chart (Shombith, 2026-11-26: "the hypersea map UI does not show
        /// up in the minimized board"): the chart's panel turns clear and its drawn lane stands down for the map's own course.
        /// </summary>
        public bool LiveMap
        {
            get => _liveMap;
            set => _liveMap = value;
        }

        // ── Building ─────────────────────────────────────────────────────────

        static RectTransform At(string name, RectTransform parent, float x, float y, float w, float h)
        {
            var rt = VoyageKit.Node(name, parent);
            VoyageKit.At(rt, x, y, w, h);
            return rt;
        }

        TMP_Text Text(RectTransform parent, string name, float x, float y, float w, float h, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var t = _kit.Text(At(name, parent, x, y, w, h), size, color, align);
            VoyageKit.Stretch(t.rectTransform);
            return t;
        }

        TMP_Text Label(RectTransform parent, string name, float x, float y, float w, float h, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var t = _kit.Label(At(name, parent, x, y, w, h), size, color, align);
            VoyageKit.Stretch(t.rectTransform);
            return t;
        }

        static VoyageGlyph Glyph(RectTransform parent, float x, float y, float size)
        {
            var g = At("Glyph", parent, x, y, size, size).gameObject.AddComponent<VoyageGlyph>();
            g.raycastTarget = false;
            return g;
        }

        void BuildChart()
        {
            // A dark panel inside one thin line; "On the chart" at its top left; the lane drawn in the chart's own units.
            // With the live HyperSea map standing behind it (LiveMap) the panel is clear and the map draws the course.
            _chart = At("Chart", Root, Chart.x, Chart.y, Chart.width, Chart.height);
            (_chartFill, _) = _kit.Box(_chart, VoyageKit.Fade(_kit.Popup, 0.86f), VoyageKit.Fade(_kit.Outline, 0.8f));
            _chartFill.raycastTarget = false;
            _chartLabel = Label(_chart, "OnTheChart", 20f, 16f, 300f, 20f, 14f, _kit.Sub);
            VoyageKit.Set(_chartLabel, _words ? _words.OnTheChart : null);
            var lane = At("Lane", _chart, 0f, 0f, Chart.width, Chart.height);
            _lane = lane.gameObject.AddComponent<VoyageChartLine>();
            _lane.color = VoyageKit.Fade(_kit.Light, 0.55f);
            for (int i = 0; i < 4; i++)
            {
                var ring = At("Cell" + i, _chart, 0f, 0f, 90f, 90f).gameObject.AddComponent<VoyageRing>();
                ring.Width = 1f;
                ring.raycastTarget = false;
                _cellRings.Add(ring);
            }
            _fromMark = Diamond("From", true);
            _toMark = Diamond("To", false);
            _from = Text(_chart, "FromName", 0f, 0f, 260f, 22f, 15f, _kit.Light);
            _to = Text(_chart, "ToName", 0f, 0f, 260f, 22f, 15f, _kit.Light);
            _chartFoot = Text(_chart, "Length", 20f, Chart.height - 38f, Chart.width - 40f, 22f, 15f, _kit.Light);
        }

        VoyageRing Diamond(string name, bool filled)
        {
            var mark = At(name, _chart, 0f, 0f, 20f, 20f).gameObject.AddComponent<VoyageRing>();
            mark.Diamond = true;
            mark.Width = 2f;
            mark.raycastTarget = false;
            mark.Set(_kit.Light, filled ? _kit.Light : _kit.RowPicked);
            return mark;
        }

        void BuildAsk()
        {
            // The giver and their faction (caps 14, Sub); the giver's words (26, three lines); "Bring me" and the classes.
            _ask = At("Ask", Root, 0f, 0f, AskWidth, 700f);
            _askLabel = Label(_ask, "Giver", 0f, AskY, AskWidth, 20f, 14f, _kit.Sub);
            _askQuote = Text(_ask, "Words", 0f, QuoteY, AskWidth, QuoteHeight, 26f, _kit.Light, TextAlignmentOptions.TopLeft);
            _askQuote.textWrappingMode = TextWrappingModes.Normal;
            _askQuote.overflowMode = TextOverflowModes.Ellipsis;
            _askQuote.lineSpacing = 10f;
            // "Bring me" over the asks at the left and "Your squad" over the vessels at the right (caps 14, Sub).
            _bringMe = Label(_ask, "BringMe", 0f, BringY, 300f, 20f, 14f, _kit.Sub);
            _bringMe.overflowMode = TextOverflowModes.Overflow;
            VoyageKit.Set(_bringMe, _words ? _words.BringMe : null);
            _yourSquad = Label(_ask, "YourSquad", AskWidth - 300f, BringY, 300f, 20f, 14f, _kit.Sub, TextAlignmentOptions.Right);
            VoyageKit.Set(_yourSquad, _words ? _words.YourSquad : null);
            // A row per asked seat: the role's glyph (20) and the archetype (19, Light); the giver's reason (16, Sub); the
            // squad's vessel (16) right-aligned with its met mark (16) before it; one muted line under the row.
            for (int i = 0; i < 3; i++)
            {
                var row = new AskRow { Root = At("Ask" + i, _ask, 0f, RowY + i * RowHeight, AskWidth, RowHeight) };
                row.Glyph = Glyph(row.Root, 0f, 19f, 20f);
                row.Name = Text(row.Root, "Archetype", 30f, 0f, ReasonX - 40f, RowHeight - 1f, 19f, _kit.Light);
                row.Reason = Text(row.Root, "Reason", ReasonX, 0f, 680f, RowHeight - 1f, 16f, _kit.Sub);
                row.Reason.overflowMode = TextOverflowModes.Ellipsis;
                row.Squad = Text(row.Root, "Squad", AskWidth - 260f, 0f, 260f, RowHeight - 1f, 16f, _kit.Light,
                    TextAlignmentOptions.Right);
                row.Met = At("Met", row.Root, 0f, 21f, 16f, 16f).gameObject.AddComponent<VoyageMetMark>();
                VoyageKit.Block(At("Rule", row.Root, 0f, RowHeight - 1f, AskWidth, 1f), VoyageKit.Fade(_kit.Outline, 0.5f));
                _askRows.Add(row);
            }
            var see = At("SeeEveryArchetype", Root, SeeX, SeeY, SeeWidth, SeeHeight);
            _kit.Quiet(see, _words ? _words.SeeClassesKey : null, _words ? _words.SeeClasses : null, _board.BrowseVessels,
                out var seeWords);
            if (seeWords) seeWords.fontSize = 15f;
        }

        void BuildCrew()
        {
            // "Your crew says" (caps 14, Sub); a row per seat 56 apart: the face (36 x 44), the line (17), the mood mark.
            _crew = At("CrewSays", Root, CrewX, 0f, 620f, 700f);
            _crewLabel = Label(_crew, "Label", 0f, AskY, 620f, 20f, 14f, _kit.Sub);
            VoyageKit.Set(_crewLabel, _words ? _words.CrewSays : null);
            for (int i = 0; i < SavedSquad.SlotCount; i++)
            {
                var row = new CrewRow { Root = At("Seat" + i, _crew, 0f, CrewY + i * CrewStep, 620f, 44f) };
                var face = At("Face", row.Root, 0f, 0f, 36f, 44f);
                VoyageKit.Block(face, VoyageKit.Fade(_kit.Popup, 0.6f));
                row.Face = VoyageKit.CoverBox(face);
                row.Quote = Text(row.Root, "Line", 48f, 0f, 520f, 44f, 17f, _kit.Light);
                row.Mood = At("Mood", row.Root, 600f, 13f, 18f, 18f).gameObject.AddComponent<VoyageMoodMark>();
                row.Mood.raycastTarget = false;
                _crewRows.Add(row);
            }
        }

        void BuildFoot()
        {
            // Left: the fit meter (three 8 tall segments, 4 apart, 180 in all) and "Squad fit N of 3" (15).
            _fit = At("Fit", Root, 0f, FootY, 700f, FootHeight);
            float seg = (FitWidth - 8f) / 3f;
            for (int i = 0; i < 3; i++)
                _fitSegments[i] = VoyageKit.Block(At("Segment" + i, _fit, i * (seg + 4f), 24f, seg, 8f), _kit.Light);
            _fitText = Text(_fit, "Words", FitWidth + 14f, 0f, 500f, FootHeight, 15f, _kit.Light);

            // Right: the pay (a crystal and the number, 24) and Take the job (A), the screen's one call to action.
            float takeX = Chart.xMax - TakeWidth;
            _payRow = At("Pay", Root, takeX - 24f - 200f, FootY, 200f, FootHeight);
            var gem = Glyph(_payRow, 200f - 22f - 64f, 17f, 22f);
            gem.Form = VoyageGlyph.Shape.Gem;
            gem.color = _kit.Light;
            _pay = Text(_payRow, "Crystals", 200f - 56f, 0f, 56f, FootHeight, 24f, _kit.Light);
            (_take, _) = _kit.Cta(At("TakeTheJob", Root, takeX, FootY, TakeWidth, FootHeight),
                _words ? _words.TakeJobKey : null, _words ? _words.TakeJob : null, _board.TakeJob);
        }

        CardView CardFor(int index)
        {
            while (_cards.Count <= index)
            {
                var view = new CardView { Root = At("Job" + _cards.Count, Root, 0f, CardY, CardWidth, CardHeight) };
                var ground = VoyageKit.Block(view.Root, _kit.Popup, hit: true);
                view.Button = view.Root.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = ground;
                view.Button.transition = Selectable.Transition.None;
                var pic = VoyageKit.Node("Picture", view.Root);
                VoyageKit.Stretch(pic);
                view.Picture = VoyageKit.CoverBox(pic);
                // The foot darkens to the page's ground from halfway down its last 190.
                var shade = At("Shade", view.Root, 0f, CardHeight - FootShade, CardWidth, FootShade);
                var g = shade.gameObject.AddComponent<VoyageGradient>();
                g.raycastTarget = false;
                var ink = _kit.RowPicked;
                var deep = VoyageKit.Fade(ink, 0.95f / Mathf.Max(0.01f, ink.a));
                g.Set(VoyageKit.Fade(ink, 0f), deep, deep, 0.5f);
                // From the foot up (Rev 146 A): the asked archetypes stacked, a role glyph (14) and the name (15) each 22
                // apart, or "Asks for no one"; the name (19, two lines); the kind (caps 14). DrawCard stands them.
                for (int i = 0; i < 3; i++)
                {
                    var row = At("Ask" + i, view.Root, Inset, 0f, CardWidth - 2f * Inset, CardRow);
                    var glyph = Glyph(row, 0f, 4f, 14f);
                    var name = Text(row, "Name", 24f, 0f, CardWidth - 2f * Inset - 24f, CardRow, 15f, _kit.Light);
                    view.Asks.Add((row, glyph, name));
                }
                view.AsksNone = Text(view.Root, "AsksNone", Inset, 0f, CardWidth - 2f * Inset, CardRow, 15f, _kit.Sub);
                VoyageKit.Set(view.AsksNone, _words ? _words.AsksNone : null);
                view.Title = Text(view.Root, "Title", Inset, 0f, CardWidth - 2f * Inset, 52f, 19f, _kit.Light,
                    TextAlignmentOptions.BottomLeft);
                view.Title.textWrappingMode = TextWrappingModes.Normal;
                view.Kind = Label(view.Root, "Kind", Inset, 0f, CardWidth - 2f * Inset, 20f, 14f, _kit.Light);
                var edge = VoyageKit.Node("Edge", view.Root);
                VoyageKit.Stretch(edge);
                view.Edge = edge.gameObject.AddComponent<VoyageOutline>();
                view.Edge.raycastTarget = false;
                view.Foot = VoyageKit.Block(At("Line", view.Root, 0f, CardHeight - 2f, CardWidth, 2f), _kit.Cyan);
                _cards.Add(view);
            }
            return _cards[index];
        }

        /// <summary>Stand the quay at <paramref name="page"/> (the window page in the frame's units) and fade it out as the
        /// full screen opens (<paramref name="full"/> 0-1).</summary>
        public void Lay(Rect page, float full)
        {
            Root.anchoredPosition = page.position;
            Root.sizeDelta = page.size;
            _group.alpha = 1f - full;
            _group.blocksRaycasts = full < 0.5f;
            Root.gameObject.SetActive(full < 1f);
        }

        // ── Drawing ──────────────────────────────────────────────────────────

        static VoyageContent Content => VoyageContentSO.Load() is { } so ? so.Content : null;

        /// <summary>The giver of <paramref name="offer"/>'s voyage type (the Voyage Codex), or null (the Crystal Run).</summary>
        public static VoyageTypeInfo TypeOf(VoyageBoard.Offer offer) =>
            offer == null ? null : Content?.VoyageType(VoyageArchetypesSO.JobOf(offer.Key));

        /// <summary>The faction of the pilot a seat flies with: the player's own pilot record, else the authored crew.</summary>
        public static FactionType FactionOf(string pilotId)
        {
            if (string.IsNullOrEmpty(pilotId)) return FactionType.None;
            if (PilotLoadoutStore.TryGetPilot(pilotId, out var record) && record != null && record.FactionType != FactionType.None)
                return record.FactionType;
            return Content?.Person(pilotId)?.Faction ?? FactionType.None;
        }

        /// <summary>The mood of the pilot in <paramref name="slot"/> (<paramref name="pilot"/> its callsign) on
        /// <paramref name="offer"/>'s voyage.</summary>
        public static PilotMood MoodOf(VoyageBoard.Offer offer, SquadSlot slot, PilotSO pilot)
        {
            var info = TypeOf(offer);
            if (info == null) return PilotMood.None;
            string id = !string.IsNullOrEmpty(slot.PilotId) ? slot.PilotId : pilot ? pilot.Id : null;
            return VoyageMood.Of(info, FactionOf(id));
        }

        /// <summary>
        /// The jobs the quay stands, in its order (Rev 138 Board): each voyage type's giver in the Codex's order, then the
        /// other work that sails now (the Crystal Run), at most <see cref="MaxCards"/>. A type's job shows even while its
        /// Set sail waits; other locked work does not.
        /// </summary>
        public static void Order(IReadOnlyList<VoyageBoard.Offer> offers, List<int> into)
        {
            into.Clear();
            if (offers == null) return;
            var types = Content?.VoyageTypes;
            if (types != null)
                foreach (var info in types)
                    for (int i = 0; i < offers.Count && into.Count < MaxCards; i++)
                        if (offers[i] != null && VoyageArchetypesSO.JobOf(offers[i].Key) == info?.Type && !into.Contains(i))
                        {
                            into.Add(i);
                            break;
                        }
            for (int i = 0; i < offers.Count && into.Count < MaxCards; i++)
                if (offers[i] != null && !offers[i].Locked && !into.Contains(i)) into.Add(i);
        }

        /// <summary>The kind a card shows: its voyage type's, else the job's own (the Crystal Run keeps its words).</summary>
        public string KindOf(VoyageBoard.Offer offer)
        {
            string kind = _words ? _words.KindOf(VoyageArchetypesSO.JobOf(offer?.Key)) : null;
            return string.IsNullOrEmpty(kind) ? offer?.Kind : kind;
        }

        /// <summary>The giver's name: the Codex's person, else their callsign, else the pilot who brings the job.</summary>
        public string GiverName(VoyageTypeInfo info, VoyageBoard.Offer offer) => GiverName(info, offer, _catalog);

        /// <summary>The giver's name with <paramref name="catalog"/> for the callsigns (Your Ark's header asks it too).</summary>
        public static string GiverName(VoyageTypeInfo info, VoyageBoard.Offer offer, PilotArtifactCatalogSO catalog)
        {
            if (info != null && !string.IsNullOrEmpty(info.Giver))
            {
                var person = Content?.Person(info.Giver);
                if (person != null && !string.IsNullOrEmpty(person.Name)) return person.Name;
                if (catalog && catalog.TryGetPilot(info.Giver, out var callsign) && callsign) return callsign.DisplayName;
            }
            return offer?.Pilot;
        }

        /// <summary>The giver's faction in words: the Codex's person's, else the job's house.</summary>
        public static string GiverFaction(VoyageTypeInfo info, VoyageBoard.Offer offer)
        {
            var faction = info != null ? Content?.Person(info.Giver)?.Faction ?? FactionType.None : FactionType.None;
            if (faction == FactionType.None && offer != null) faction = offer.House;
            return faction == FactionType.None ? offer?.Faction : FactionCatalog.TypeName(faction);
        }

        /// <summary>The giver's portrait (the Codex's callsign), else the job's pilot's.</summary>
        public Sprite GiverFace(VoyageTypeInfo info, VoyageBoard.Offer offer) => GiverFace(info, offer, _catalog);

        /// <summary>The giver's portrait with <paramref name="catalog"/> for the callsigns.</summary>
        public static Sprite GiverFace(VoyageTypeInfo info, VoyageBoard.Offer offer, PilotArtifactCatalogSO catalog)
        {
            if (info != null && catalog && catalog.TryGetPilot(info.Giver, out var giver) && giver && giver.Portrait)
                return giver.Portrait;
            return offer?.Portrait;
        }

        /// <summary>Draw <paramref name="offers"/> with <paramref name="open"/> picked, at <paramref name="difficulty"/>.</summary>
        public void Draw(IReadOnlyList<VoyageBoard.Offer> offers, int open, int difficulty)
        {
            Order(offers, _order);
            for (int n = 0; n < _order.Count; n++) DrawCard(CardFor(n), n, offers, _order[n], open);
            for (int i = _order.Count; i < _cards.Count; i++) _cards[i].Root.gameObject.SetActive(false);

            VoyageKit.Set(_docked, _words && !string.IsNullOrEmpty(_board.Here)
                ? TemplateText.Fill(_words.Docked, "place", _board.Here, "count", _order.Count.ToString(CultureInfo.InvariantCulture))
                : null);

            var offer = open >= 0 && open < offers.Count ? offers[open] : null;
            _ask.gameObject.SetActive(offer != null);
            _crew.gameObject.SetActive(false);
            _fit.gameObject.SetActive(false);
            _payRow.gameObject.SetActive(false);
            _take.interactable = offer != null;
            DrawChart(offer, difficulty);
            if (offer == null || !_words) return;

            var info = TypeOf(offer);
            string job = VoyageArchetypesSO.JobOf(offer.Key);
            string giver = GiverName(info, offer);
            VoyageKit.Set(_askLabel, TemplateText.Fill(_words.AskLabel, "kind", KindOf(offer), "giver", giver,
                "faction", GiverFaction(info, offer)));
            string say = info != null && !string.IsNullOrEmpty(info.Say) ? info.Say : offer.Story;
            VoyageKit.Set(_askQuote, string.IsNullOrEmpty(say) ? null : TemplateText.Fill(_words.AskQuote, "say", say));
            var wants = _archetypes ? _archetypes.AskOf(job, offer.VoyageId) : System.Array.Empty<VesselArchetype>();
            var squad = VoyageSeatRecord.Get(offer.Key) ?? VoyageSeats.Active;
            DrawAsks(job, offer.VoyageId, wants, squad);
            DrawCrew(offer, squad);
            DrawFit(job, offer.VoyageId, squad, wants.Count > 0);
            int pay = VoyageBoard.PayAt(offer, difficulty);
            _payRow.gameObject.SetActive(offer.Pays);
            VoyageKit.Set(_pay, offer.Pays ? pay.ToString(CultureInfo.InvariantCulture) : null);
        }

        /// <summary>The chart: the port's diamond (filled) bottom left, the voyage's end top right, a dashed lane between and
        /// a ring for each cell crossed on the way (at most four), the names beside the diamonds, the length and sea under.</summary>
        void DrawChart(VoyageBoard.Offer offer, int difficulty)
        {
            // The live map draws the real course itself; the drawn lane stands in only when there is no map to show.
            _chartFill.color = _liveMap ? VoyageKit.Fade(_kit.Popup, ChartFillOverMap) : VoyageKit.Fade(_kit.Popup, 0.86f);
            bool shown = offer != null && !_liveMap;
            var a = new Vector2(60f, 328f);
            var b = new Vector2(450f, 150f);
            _lane.Set(shown ? new[] { a, b } : null, 2f, 8f, 8f, 0f);
            _fromMark.gameObject.SetActive(shown);
            _toMark.gameObject.SetActive(shown);
            _from.gameObject.SetActive(shown);
            _to.gameObject.SetActive(shown);
            int rings = shown ? Mathf.Clamp(offer.Cells - 1, 0, _cellRings.Count) : 0;
            for (int i = 0; i < _cellRings.Count; i++)
            {
                bool on = i < rings;
                _cellRings[i].gameObject.SetActive(on);
                if (!on) continue;
                var at = Vector2.Lerp(a, b, (i + 1f) / (rings + 1f));
                VoyageKit.At(_cellRings[i].rectTransform, at.x - 45f, at.y - 45f, 90f, 90f);
                _cellRings[i].Set(VoyageKit.Fade(_kit.Sub, 0.9f), VoyageKit.Fade(_kit.Popup, 0.35f));
            }
            VoyageKit.At(_fromMark.rectTransform, a.x - 10f, a.y - 10f, 20f, 20f);
            VoyageKit.At(_toMark.rectTransform, b.x - 10f, b.y - 10f, 20f, 20f);
            VoyageKit.At(_from.rectTransform, a.x + 18f, a.y - 11f, 260f, 22f);
            VoyageKit.At(_to.rectTransform, b.x + 18f, b.y - 11f, Chart.width - b.x - 18f, 22f);
            VoyageKit.Set(_from, shown ? (string.IsNullOrEmpty(offer.From) ? _board.Here : offer.From) : null);
            VoyageKit.Set(_to, shown ? offer.Place : null);
            VoyageKit.Set(_chartFoot, offer != null && _words
                ? TemplateText.Fill(_words.ChartFoot, "cells", Mathf.Max(1, offer.Cells).ToString(CultureInfo.InvariantCulture),
                    "difficulty", Mathf.Clamp(difficulty, 1, 9).ToString(CultureInfo.InvariantCulture), "danger", _words.Danger(difficulty))
                : null);
        }

        void DrawCard(CardView view, int slot, IReadOnlyList<VoyageBoard.Offer> offers, int index, int open)
        {
            var offer = offers[index];
            bool on = index == open;
            view.Root.gameObject.SetActive(true);
            VoyageKit.At(view.Root, slot * CardStep, CardY, CardWidth, CardHeight);
            var info = TypeOf(offer);
            var face = GiverFace(info, offer);
            bool portrait = face;
            var picture = portrait ? face : offer.Picture;
            VoyageKit.ShowCover(view.Picture, picture, new Vector2(CardWidth, CardHeight),
                portrait ? new Vector2(0.5f, 0.2f) : new Vector2(0.5f, 0.5f));
            view.Picture.color = new Color(1f, 1f, 1f, on ? 1f : DimPicture);
            VoyageKit.Set(view.Kind, KindOf(offer));
            view.Kind.color = on ? _kit.Light : _kit.Sub;
            VoyageKit.Set(view.Title, offer.Title);
            view.Title.color = on ? _kit.Light : VoyageKit.Fade(_kit.Light, 0.85f);
            var wants = _archetypes ? _archetypes.AskOf(VoyageArchetypesSO.JobOf(offer.Key), offer.VoyageId) : System.Array.Empty<VesselArchetype>();
            // The asks stand on the card's foot; the name and the kind stand on the asks.
            int rows = Mathf.Max(1, wants.Count);
            float top = CardHeight - Inset - rows * CardRow;
            for (int i = 0; i < view.Asks.Count; i++)
            {
                var (root, glyph, name) = view.Asks[i];
                bool shown = i < wants.Count;
                root.gameObject.SetActive(shown);
                if (!shown) continue;
                VoyageKit.At(root, Inset, top + i * CardRow, CardWidth - 2f * Inset, CardRow);
                glyph.Form = VoyageGlyph.Of(wants[i]);
                glyph.color = on ? _kit.Light : _kit.Sub;
                VoyageKit.Set(name, _archetypes.NameOf(wants[i]));
                name.color = on ? _kit.Light : VoyageKit.Fade(_kit.Light, 0.85f);
            }
            view.AsksNone.gameObject.SetActive(wants.Count == 0);
            VoyageKit.At(view.AsksNone.rectTransform.parent as RectTransform, Inset, top, CardWidth - 2f * Inset, CardRow);
            VoyageKit.At(view.Title.rectTransform.parent as RectTransform, Inset, top - 10f - 52f, CardWidth - 2f * Inset, 52f);
            VoyageKit.At(view.Kind.rectTransform.parent as RectTransform, Inset, top - 10f - 52f - 6f - 20f,
                CardWidth - 2f * Inset, 20f);
            // The picked job: one cyan line at its foot; the others inside one thin line.
            view.Foot.enabled = on;
            view.Edge.enabled = !on;
            view.Edge.Solid(VoyageKit.Fade(_kit.Outline, 0.6f));
            view.Button.onClick.RemoveAllListeners();
            view.Button.onClick.AddListener(() => _board.Pick(index));
        }

        /// <summary>
        /// The squad's vessels in seat order with each one's archetypes, and which asked seat each fills
        /// (<see cref="VoyageSeatFill"/>): what the quay's rows and Your Ark's chips both show.
        /// </summary>
        public static VoyageSeatFill.Filled[] FillOf(VoyageArchetypesSO archetypes, string job, string voyageId, SavedSquad squad,
            List<IReadOnlyList<VesselArchetype>> into)
        {
            into.Clear();
            if (!archetypes || squad == null) return System.Array.Empty<VoyageSeatFill.Filled>();
            for (int s = 0; s < SavedSquad.SlotCount; s++) into.Add(archetypes.ArchetypesOf(squad.GetSlot(s).Vessel));
            return VoyageSeatFill.Assign(archetypes.AskOf(job, voyageId), into);
        }

        /// <summary>The giver's reason for asked seat <paramref name="seat"/>: a named voyage's swapped seat says the crew
        /// it counters; every other seat keeps its type's reason for that archetype.</summary>
        public static string ReasonOf(VoyageArchetypesSO archetypes, string job, string voyageId, int seat)
        {
            if (!archetypes) return null;
            var seats = archetypes.AskOf(job, voyageId);
            if (seat < 0 || seat >= seats.Count) return null;
            var a = seats[seat];
            var own = archetypes.VoyageAskOf(voyageId);
            // The crew's name can open the sentence lower case ("the Ironmouth breakers fly Sappers"); a reason starts capital.
            if (own != null && own.Countered == a && archetypes.CounterReason(voyageId) is { Length: > 0 } counter)
                return char.ToUpperInvariant(counter[0]) + counter.Substring(1);
            var type = archetypes.TypeAskOf(job);
            if (type == null) return null;
            for (int i = 0; i < type.Seats.Count && i < type.Reasons.Count; i++)
                if (type.Seats[i] == a) return type.Reasons[i];
            return null;
        }

        void DrawAsks(string job, string voyageId, IReadOnlyList<VesselArchetype> wants, SavedSquad squad)
        {
            _bringMe.gameObject.SetActive(wants.Count > 0);
            _yourSquad.gameObject.SetActive(wants.Count > 0 && squad != null);
            var filled = FillOf(_archetypes, job, voyageId, squad, _squadArchetypes);
            for (int i = 0; i < _askRows.Count; i++)
            {
                var row = _askRows[i];
                bool shown = i < wants.Count;
                row.Root.gameObject.SetActive(shown);
                if (!shown) continue;
                row.Glyph.Form = VoyageGlyph.Of(wants[i]);
                row.Glyph.color = _kit.Light;
                VoyageKit.Set(row.Name, _archetypes.NameOf(wants[i]));
                VoyageKit.Set(row.Reason, ReasonOf(_archetypes, job, voyageId, i));
                var f = i < filled.Length ? filled[i] : default;
                bool has = squad != null;
                row.Squad.gameObject.SetActive(has);
                row.Met.gameObject.SetActive(has);
                if (!has) continue;
                var vessel = f.Vessel >= 0 ? _rules.VesselOf(squad.GetSlot(f.Vessel).Vessel) : null;
                string name = vessel ? vessel.CardName : null;
                VoyageKit.Set(row.Squad, f.Meets <= 0f || string.IsNullOrEmpty(name) ? _words.SquadNone
                    : TemplateText.Fill(f.Meets >= 1f ? _words.SquadFull : _words.SquadHalf, "vessel", name));
                row.Squad.color = f.Meets > 0f ? _kit.Light : _kit.Sub;
                row.Met.Set(f.Meets, f.Meets > 0f ? _kit.Light : VoyageKit.Fade(_kit.Sub, 0.8f));
                float w = Mathf.Ceil(row.Squad.GetPreferredValues(row.Squad.text).x);
                VoyageKit.At(row.Met.rectTransform, AskWidth - w - 10f - 16f, 21f, 16f, 16f);
            }
        }

        void DrawCrew(VoyageBoard.Offer offer, SavedSquad squad)
        {
            if (squad == null) return;
            bool any = false;
            var refuse = ColorTrace.Retrace(ColorTrace.Ui, VoyageHudColors.ThreatAuthored);
            for (int seat = 0; seat < _crewRows.Count; seat++)
            {
                var row = _crewRows[seat];
                var slot = squad.GetSlot(seat);
                var vessel = _rules.VesselOf(slot.Vessel);
                var pilot = _rules.PilotOf(slot, vessel);
                var mood = MoodOf(offer, slot, pilot);
                row.Root.gameObject.SetActive(mood != PilotMood.None);
                if (mood == PilotMood.None) continue;
                any = true;
                VoyageKit.ShowCover(row.Face, pilot ? pilot.Portrait : null, new Vector2(36f, 44f), VoyageKit.FaceFocus);
                VoyageKit.Set(row.Quote, TemplateText.Fill(_words.CrewQuote, "say", _words.Say(mood)));
                row.Quote.color = mood == PilotMood.Refuses ? refuse : _kit.Light;
                row.Mood.Set(mood, _kit.Light, VoyageKit.Fade(_kit.Outline, 0.8f), refuse);
            }
            _crew.gameObject.SetActive(any);
        }

        void DrawFit(string job, string voyageId, SavedSquad squad, bool shown)
        {
            _fit.gameObject.SetActive(shown && squad != null && _archetypes);
            if (!_fit.gameObject.activeSelf) return;
            var vessels = new List<VesselClassType>(SavedSquad.SlotCount);
            for (int s = 0; s < SavedSquad.SlotCount; s++) vessels.Add(squad.GetSlot(s).Vessel);
            float fit = _archetypes.Fit(job, voyageId, vessels);
            for (int i = 0; i < _fitSegments.Length; i++)
                _fitSegments[i].color = VoyageKit.FitSegment(_kit, fit, i);
            VoyageKit.Set(_fitText, TemplateText.Fill(_words.Fit, "fit", VoyageArchetypesSO.FitText(fit)));
        }
    }
}

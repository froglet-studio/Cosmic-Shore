using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Voyages tab as Shombith picked it on Frogma (Docs/MENU_REDESIGN_B.md sections 201-203): three pages under one
    /// header of steps, replacing the voyage screen and squad deck of section 184.
    /// <list type="number">
    /// <item><b>The HyperSea</b> (<see cref="VoyageSeaPage"/>, Rev 111 Full on Rev 108's table): the live map lying back as a
    /// 50 degree table on the moving sea lines, its side view joined to its front edge and sliding with it, the zoom row
    /// and + / - above it, Full screen (the table animates flat to a borderless full-screen map whose zoom goes down to
    /// Neighbourhood and Cell), the crew's portraits down the right with a sliding Crew / Hide toggle, and the picked
    /// voyage with Open voyage.</item>
    /// <item><b>The voyage</b> (<see cref="VoyageBriefPage"/>, Rev 100 P2A Briefing): the offering pilot's portrait and
    /// story, the route drawing itself stop by stop, the difficulty 1-9 with the pilot's recommendation marked, the
    /// rewards arriving, Back and Ready your Ark.</item>
    /// <item><b>Your Ark</b> (<see cref="VoyageArkPage"/>, Rev 100 P3A as Rev 108 drew it): your lobby first as glass seat
    /// cards on sea lines, Fly a squad instead, and the seat popup (<see cref="VoyageSeatPicker"/>: tabs, quick kits, the
    /// Sees filter, one press), Back and Set sail.</item>
    /// </list>
    /// <see cref="VoyageJobsView"/> feeds it the voyages on offer (<see cref="Offer"/>) and lends it the window's map.
    /// Every word is the voyage content's (<see cref="VoyageLines"/>) or the game's data; every colour and sprite is
    /// <see cref="VoyageBoardStyleSO"/>'s, read through the worn colour set.
    /// </summary>
    public class VoyageBoard : MonoBehaviour
    {
        public enum Page
        {
            Sea = 0,
            Voyage = 1,
            Ark = 2,
        }

        /// <summary>A stop on a voyage's route: its name, its line and its point on the map.</summary>
        public sealed class Stop
        {
            public string Name, Line;
            public Vector2 At;
        }

        /// <summary>A voyage on offer: a job a pilot of the crew recommends (or the Ark escort, or a voyage under way).</summary>
        public sealed class Offer
        {
            /// <summary>The job's kind in words (Bounty hunt, Piracy...).</summary>
            public string Kind;
            public string Title, Place;
            /// <summary>Who recommends it, their house and their portrait; empty for the escort.</summary>
            public string Pilot, Faction, Story;
            public Sprite Portrait;
            /// <summary>Its numbers: light years and minutes (empty when the course is not charted).</summary>
            public string Ly, Minutes;
            /// <summary>The squad's role on it, already in words.</summary>
            public string Role;
            /// <summary>The crystals it pays at <see cref="Recommended"/> and their element.</summary>
            public int Pay;
            public bool Pays;
            public Element Element;
            /// <summary>The difficulty its pilot recommends (1-9).</summary>
            public int Recommended = 5;
            /// <summary>The course on the map (map points) and its stops.</summary>
            public readonly List<Vector2> Course = new();
            public readonly List<Stop> Stops = new();
            /// <summary>Sails it at a difficulty; null when it cannot sail (it is under way).</summary>
            public Action<int> SetSail;
            /// <summary>Set sail's words when not the content's own (Under way).</summary>
            public string SailLabel;
            /// <summary>Pilots (callsign ids) and vessels the seat popup suggests for it.</summary>
            public readonly List<string> SuggestedPilots = new();
            public readonly List<VesselClassType> SuggestedVessels = new();
        }

        /// <summary>The squad changed (another squad picked, a seat changed): the pilots bring the voyages, so the window
        /// redraws.</summary>
        public event Action SquadChanged;

        /// <summary>The page on show changed, or the table went full screen and back.</summary>
        public event Action ScreenChanged;

        /// <summary>The page on show.</summary>
        public Page Shown => _page;

        /// <summary>True on the Ark page (the map behind is the course alone).</summary>
        public bool OnSquadScreen => _page == Page.Ark;

        /// <summary>True while the locked Saga and Episodes strip belongs at the page's foot: the HyperSea page, not full
        /// screen.</summary>
        public bool ShowsStrip => _page == Page.Sea && !_sea.IsFull;

        /// <summary>The difficulty picked for the open voyage (its pilot's recommendation until another is picked).</summary>
        public int Difficulty => _difficulty;

        /// <summary>The voyage open now, or null.</summary>
        public Offer Open => _open >= 0 && _open < _offers.Count ? _offers[_open] : null;

        public VoyageSeaPage Sea => _sea;
        public VoyageBriefPage Brief => _brief;
        public VoyageArkPage Ark => _ark;

        public const float HeaderHeight = 64f;

        VoyageKit _kit;
        VoyageLines _lines;
        VoyageSeats _rules;
        RectTransform _root, _pages;
        VoyageSeaPage _sea;
        VoyageBriefPage _brief;
        VoyageArkPage _ark;
        Page _page;
        readonly List<Offer> _offers = new();
        int _open = -1;
        int _difficulty = 5;
        string _difficultyFor;
        Action<int> _pick;

        // The header: the three steps (each page has its own Back at its foot, as the board draws it).
        readonly TMP_Text[] _steps = new TMP_Text[3];
        readonly Image[] _stepBars = new Image[3];

        // ── Building ─────────────────────────────────────────────────────────

        /// <summary>The board, built once over <paramref name="host"/> (the Voyages page), its text in
        /// <paramref name="font"/>'s face. <paramref name="party"/> is who is in the player's lobby (may be null).</summary>
        public static VoyageBoard Create(RectTransform host, TMP_Text font, SO_VesselList vessels, VoyageLines lines,
            HostConnectionDataSO party = null)
        {
            var rt = VoyageKit.Node("VoyageBoard", host);
            rt.SetAsLastSibling();
            VoyageKit.Stretch(rt);
            var board = rt.gameObject.AddComponent<VoyageBoard>();
            board._root = rt;
            board._lines = lines;
            var style = VoyageBoardStyleSO.Load();
            board._kit = new VoyageKit(style, lines, font);
            board._rules = new VoyageSeats(style, lines, vessels);
            board._rules.Changed += () => board.SquadChanged?.Invoke();
            board.BuildGround(host);
            board.BuildHeader();
            board._pages = VoyageKit.Node("Pages", rt);
            VoyageKit.Stretch(board._pages);
            board._pages.offsetMax = new Vector2(0f, -HeaderHeight);
            board._sea = new VoyageSeaPage(board, board._kit, board._pages);
            board._brief = new VoyageBriefPage(board, board._kit, board._pages);
            board._ark = new VoyageArkPage(board, board._kit, board._rules, board._pages, party);
            board.ShowPage(Page.Sea);
            return board;
        }

        /// <summary>
        /// The pages' own ground (Frogma Rev 100-111: a near-opaque deep gradient, rgba(0,4,31,.92) to .97, with a few
        /// faint long waves drifting through it), under everything on the Voyages page, so the menu's busy sea does not
        /// show through the board.
        /// </summary>
        void BuildGround(RectTransform host)
        {
            var ground = VoyageKit.Node("VoyageGround", host);
            ground.SetAsFirstSibling();
            VoyageKit.Stretch(ground);
            var fill = ground.gameObject.AddComponent<VoyageGradient>();
            fill.raycastTarget = false;
            fill.Set(VoyageKit.Fade(_kit.Popup, 0.92f), VoyageKit.Fade(_kit.Popup, 0.95f), VoyageKit.Fade(_kit.Popup, 0.97f), 0.5f);
            var waves = VoyageKit.Node("Waves", ground);
            VoyageKit.Stretch(waves);
            var lines = waves.gameObject.AddComponent<VoyageSeaLines>();
            lines.color = VoyageKit.Fade(_kit.Outline, 0.45f);
            lines.Lines = 4;
            lines.Waves = 1.2f;
            lines.Swell = 0.3f;
            lines.Width = 1.5f;
            lines.Speed = 0.006f;
        }

        void BuildHeader()
        {
            var head = VoyageKit.Node("Steps", _root);
            VoyageKit.Top(head, 0f, HeaderHeight);
            var row = VoyageKit.Node("Row", head);
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(0.5f, 0.5f);
            row.sizeDelta = new Vector2(760f, 44f);
            VoyageKit.Row(row, 28f, TextAnchor.MiddleCenter);
            string[] words = { _lines?.StepSea, _lines?.StepVoyage, _lines?.StepArk };
            for (int i = 0; i < 3; i++)
            {
                if (i > 0)
                {
                    // A hairline between the steps.
                    var line = VoyageKit.Node("Between", row);
                    var le = VoyageKit.Le(line);
                    le.preferredWidth = 56f;
                    le.preferredHeight = 1f;
                    VoyageKit.Block(line, VoyageKit.Fade(_kit.Outline, 0.9f));
                }
                int page = i;
                var step = VoyageKit.Node("Step" + i, row);
                VoyageKit.Column(step, 8f, TextAnchor.MiddleCenter);
                var hit = VoyageKit.Block(step, VoyageKit.Clear, hit: true);
                var t = _kit.Label(step, 15, _kit.Sub, TextAlignmentOptions.Center);
                VoyageKit.Set(t, words[i]);
                var bar = VoyageKit.Node("Bar", step);
                VoyageKit.Le(bar).preferredHeight = 2f;
                _stepBars[i] = VoyageKit.Block(bar, _kit.Cyan);
                _steps[i] = t;
                // A step already reached can be pressed to go back to it; a later one needs its call to action.
                var b = step.gameObject.AddComponent<Button>();
                b.targetGraphic = hit;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { if (page < (int)_page) ShowPage((Page)page); });
            }

        }

        // ── Pages ────────────────────────────────────────────────────────────

        /// <summary>Show <paramref name="page"/>. The voyage and Ark pages need an open voyage.</summary>
        public void ShowPage(Page page)
        {
            if (page != Page.Sea && Open == null) page = Page.Sea;
            bool changed = _page != page;
            _page = page;
            if (page != Page.Sea) _sea.ExitFull(instant: true);
            _sea.Root.gameObject.SetActive(page == Page.Sea);
            _brief.Root.gameObject.SetActive(page == Page.Voyage);
            _ark.Root.gameObject.SetActive(page == Page.Ark);
            for (int i = 0; i < 3; i++)
            {
                bool on = i == (int)page;
                _steps[i].color = on ? _kit.Light : i < (int)page ? VoyageKit.Fade(_kit.Light, 0.75f) : _kit.Sub;
                _stepBars[i].enabled = on;
            }
            if (changed) Rise(PageRoot(page));
            if (page == Page.Voyage) _brief.Show(Open, _difficulty, replay: changed);
            if (page == Page.Ark) _ark.Show(Open, _difficulty);
            if (changed) ScreenChanged?.Invoke();
        }

        /// <summary>One step back: a picker open on the Ark page closes first.</summary>
        public void StepBack()
        {
            if (_ark.PickerOpen)
            {
                _ark.ClosePicker();
                return;
            }
            if (_page > Page.Sea) ShowPage(_page - 1);
        }

        /// <summary>Take the window's map column (the map over its edge view, built by <see cref="VoyageJobsView"/>) into
        /// the HyperSea page's table.</summary>
        public void AdoptMap(RectTransform column, RectTransform mapHost, RectTransform edgeHost) =>
            _sea.Adopt(column, mapHost, edgeHost);

        // ── Showing ──────────────────────────────────────────────────────────

        /// <summary>
        /// Show <paramref name="offers"/> with offer <paramref name="open"/> open (-1: none) on <paramref name="map"/>.
        /// <paramref name="message"/> stands where the voyages would when none is offered.
        /// </summary>
        public void Show(IReadOnlyList<Offer> offers, int open, Action<int> pick, VoyageMapGraphic map, string message)
        {
            _offers.Clear();
            if (offers != null) _offers.AddRange(offers);
            _open = open >= 0 && open < _offers.Count ? open : -1;
            _pick = pick;
            var shown = Open;
            // The difficulty is the pilot's until the crew picks another; a new voyage starts on its own.
            string key = shown == null ? null : shown.Title + "|" + shown.Pilot;
            if (key != _difficultyFor)
            {
                _difficultyFor = key;
                _difficulty = shown?.Recommended ?? 5;
            }
            _sea.Show(_offers, _open, map, message);
            if (_page != Page.Sea && shown == null) ShowPage(Page.Sea);
            else if (_page == Page.Voyage) _brief.Show(shown, _difficulty, replay: false);
            else if (_page == Page.Ark) _ark.Show(shown, _difficulty);
        }

        /// <summary>Pick offer <paramref name="index"/> (a portrait, a crew card, a place on the map).</summary>
        public void Pick(int index) => _pick?.Invoke(index);

        /// <summary>The crew picked another difficulty for the open voyage (1-9).</summary>
        public void SetDifficulty(int difficulty)
        {
            _difficulty = VoyageDifficulty.Clamp(difficulty);
            if (_page == Page.Voyage) _brief.Show(Open, _difficulty, replay: false);
            _sea.Redraw();
        }

        /// <summary>Set sail with the open voyage at the picked difficulty.</summary>
        public void SetSail()
        {
            var open = Open;
            if (open?.SetSail == null) return;
            _ark.RememberSeat();
            open.SetSail(_difficulty);
        }

        /// <summary>The map the HyperSea page shows (the voyage page draws its route on the same water).</summary>
        public VoyageMapGraphic Map => _sea.Map;

        /// <summary>The crystals the open voyage pays at <paramref name="difficulty"/>.</summary>
        public static int PayAt(Offer offer, int difficulty) =>
            offer == null || !offer.Pays ? 0 : VoyageDifficulty.PayAt(offer.Pay, offer.Recommended, difficulty);

        internal string KindLine(Offer offer) => offer?.Kind ?? string.Empty;

        internal string ElementName(Element e) => _rules.ElementName(e);

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_page == Page.Sea) _sea.Tick(dt);
            else if (_page == Page.Voyage) _brief.Tick(dt);
            else _ark.Tick(dt);
            StepRise(dt);
        }

        // ── Page motion (Frogma's "rise": opacity 0 to 1 and 16 up, .7s cubic-bezier(.2,.7,.2,1)) ─────────────

        const float RiseSeconds = 0.7f, RiseLift = 16f;
        RectTransform _rising;
        CanvasGroup _risingGroup;
        float _riseT = 1f;

        RectTransform PageRoot(Page page) => page == Page.Sea ? _sea.Root : page == Page.Voyage ? _brief.Root : _ark.Root;

        void Rise(RectTransform root)
        {
            if (_rising && _rising != root) Settle();
            _rising = root;
            if (!root.TryGetComponent(out _risingGroup)) _risingGroup = root.gameObject.AddComponent<CanvasGroup>();
            _riseT = 0f;
            StepRise(0f);
        }

        void StepRise(float dt)
        {
            if (!_rising || _riseT >= 1f) return;
            _riseT = Mathf.Min(1f, _riseT + dt / RiseSeconds);
            float e = VoyageKit.Rise(_riseT);
            _risingGroup.alpha = e;
            // Only a page standing in the window's slot lifts; the map in full screen is placed by its own sweep.
            if (_rising.parent == _pages && !(_rising == _sea.Root && _sea.IsFull))
            {
                var lift = new Vector2(0f, -RiseLift * (1f - e));
                _rising.offsetMin = lift;
                _rising.offsetMax = lift;
            }
            if (_riseT >= 1f) Settle();
        }

        void Settle()
        {
            if (_risingGroup) _risingGroup.alpha = 1f;
            if (_rising && _rising.parent == _pages && !(_rising == _sea.Root && _sea.IsFull))
                _rising.offsetMin = _rising.offsetMax = Vector2.zero;
            _riseT = 1f;
        }

        void OnDisable()
        {
            // The window closed: a full-screen map comes home at once, and a picker closes.
            if (_sea != null) _sea.ExitFull(instant: true);
            if (_ark != null && _ark.PickerOpen) _ark.ClosePicker();
        }

        void OnDestroy()
        {
            _sea?.Dispose();
            _ark?.Dispose();
        }

        internal void RaiseScreenChanged() => ScreenChanged?.Invoke();
    }
}

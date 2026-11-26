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
    /// Vessels, one archetype on the floor (Frogma Rev 146 A, "Seat asks", picked by Shombith 2026-11-26; Docs/MENU_REDESIGN_B.md
    /// section 236; the hangar look of Rev 136 E / Rev 138 Vessels, sections 229 and 231). It opens over Your Ark when a seat's
    /// hull is pressed, on the archetype that seat fills for the voyage:
    /// <list type="bullet">
    /// <item>the voyage on top with the archetypes it asks for at the right; the seat's line under it ("Seat 2 - flown by the
    /// AI - fills Harraser, half (Squirrel's second)");</item>
    /// <item>the vessels that LEAD with the archetype standing on the hangar floor: each one's hangar preview, its name, one
    /// thin line (cyan on the one picked), its archetypes and how it meets the seat; on an AI seat what the AI cannot fly is
    /// dimmed and cannot be taken;</item>
    /// <item>at the right, small tiles of the vessels that have the archetype second or third, the one in the seat now
    /// marked;</item>
    /// <item>the archetype's line, what it counters and what counters it; the nine archetypes in their three roles along the
    /// foot (LB / RB step them), the asked ones brighter, one with no leader yet "(none yet)";</item>
    /// <item>the bar: the squad fit meter and how the pick changes it, Back (B) and Take &lt;vessel&gt; (A), the one Lime.</item>
    /// </list>
    /// A press on a vessel picks it on the page; Take saves it to the seat for THIS voyage (<see cref="VoyageSeats.PickVoyage"/>),
    /// as the seat picker does, and closes. An archetype is a label only (<see cref="VoyageArchetypesSO"/>); nothing here
    /// changes a vessel.
    /// </summary>
    public sealed class VoyageVesselsPage
    {
        // Frogma Rev 138 Vessels, in the page's units: x 40-1880, y 176-1046 of the 1920 x 1080 board, as Your Ark.
        static readonly Vector2 Screen = new(1840f, 870f);
        // Rev 146 A Vessels, board y - 176: the floor holds the left 1200, the tiles the right 540.
        const float Margin = 0f, SeatLineY = 62f, StandY = 84f, StandHeight = 420f, FloorLeft = 80f, FloorRight = 1200f;
        const float MaxStandWidth = 520f, StandGap = 80f, PictureY = 0f, PictureHeight = 290f, NameY = 306f, RuleY = 354f,
            LineY = 372f;
        const float AlsoX = 1300f, AlsoY = 95f, TileY = 124f, TileWidth = 260f, TileHeight = 180f, TileGap = 20f,
            TilePicture = 128f;
        const int MaxTiles = 6, MaxInRow = 4;
        // More than four leaders stand in two rows of short stands (Sapper has seven).
        const float SmallPicture = 140f, SmallStand = 264f, MinStandWidth = 220f;
        const float FloorY = 324f, FloorHeight = 546f, HeadY = 633f, TabsY = 701f, TabsHeight = 56f;
        const float BarY = 814f, BarHeight = 56f, FitWidth = 180f, BackWidth = 180f, TakeWidth = 340f;
        const float DimAlpha = 0.4f;

        readonly VoyageBoard _board;
        readonly VoyageKit _kit;
        readonly VoyageSeats _rules;
        readonly VoyageArchetypesSO _archetypes;

        readonly RectTransform _root, _frame;
        VoyageJobHeader _header;
        int _difficulty;
        Vector2 _fitFor = new(-1f, -1f);

        TMP_Text _seatLine, _head, _headLine, _headCounters, _headCountered, _none, _fitText, _alsoTitle, _take;
        VoyageGlyph _headGlyph;
        RectTransform _fitRow;
        readonly List<StandView> _stands = new();
        readonly List<TileView> _tiles = new();
        readonly List<TabView> _tabs = new();
        readonly List<(VesselRole role, VoyageGlyph glyph, Image rule)> _groups = new();
        readonly Image[] _fitSegments = new Image[3];

        VoyageBoard.Offer _offer;
        string _key;
        int _seat;
        VesselArchetype _class;
        VesselClassType _picked;
        readonly List<VoyageSeats.Option> _offered = new();
        readonly List<VoyageSeats.Option> _shown = new();
        readonly List<VoyageSeats.Option> _also = new();
        readonly List<IReadOnlyList<VesselArchetype>> _squadArchetypes = new();

        sealed class StandView
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Button Button;
            public Image Rule;
            public RectTransform Picture;
            public VoyageSeats.HullView Hull;
            public TMP_Text Name, Line;
        }

        sealed class TileView
        {
            public RectTransform Root, Picture;
            public Button Button;
            public CanvasGroup Group;
            public VoyageSeats.HullView Hull;
            public VoyageOutline Edge;
            public TMP_Text Name, Note;
            public VoyageMetMark Met;
        }

        sealed class TabView
        {
            public VesselArchetype Class;
            public TMP_Text Name;
            public Image Bar;
        }

        public bool IsOpen => _root.gameObject.activeSelf;

        public VoyageVesselsPage(VoyageBoard board, VoyageKit kit, VoyageSeats rules, RectTransform parent)
        {
            _board = board;
            _kit = kit;
            _rules = rules;
            _archetypes = VoyageArchetypesSO.Load();

            _root = VoyageKit.Node("Vessels", parent);
            VoyageKit.Stretch(_root);
            // The page's slot under the menu's bars and the Voyages header (Rev 138 Vessels keeps them), on the page's
            // flat ground (#00041F), taking every press: the page it opened from stays behind it.
            var plate = _kit.RowPicked;
            VoyageKit.Block(_root, new Color(plate.r, plate.g, plate.b, 1f), hit: true);
            _frame = VoyageKit.Node("Frame", _root);
            VoyageKit.Fit(_frame, Screen);

            BuildFloor();
            BuildTop();
            BuildAlso();
            BuildHead();
            BuildTabs();
            BuildBar();
            _none = _kit.Text(At("None", FloorLeft, StandY + NameY, FloorRight - FloorLeft, 30f), 18, _kit.Sub, TextAlignmentOptions.Center);
            VoyageKit.Stretch(_none.rectTransform);

            _root.gameObject.SetActive(false);
        }

        RectTransform At(string name, float x, float y, float w, float h, RectTransform parent = null)
        {
            var rt = VoyageKit.Node(name, parent ? parent : _frame);
            VoyageKit.At(rt, x, y, w, h);
            return rt;
        }

        // The floor's soft blue light, as Your Ark's: the worn set's Light, toward Outline.
        Color FloorLight => Color.Lerp(_kit.Light, _kit.Outline, 0.35f);

        void BuildFloor()
        {
            // The hangar floor: a faint band under the vessels, its top edge one muted line (as Your Ark's).
            var floor = At("Floor", -40f, FloorY, 1920f, FloorHeight);
            VoyageKit.Block(floor, VoyageKit.Fade(FloorLight, 0.015f));
            VoyageKit.Block(At("Edge", 0f, 0f, 1920f, 1f, floor), VoyageKit.Fade(_kit.Outline, 0.4f));
        }

        void BuildTop()
        {
            // The job and the archetypes it asks for, as over Your Ark; the seat being chosen for (15, Sub) under it.
            _header = new VoyageJobHeader(_kit, _frame, 0f, 0f, Screen.x);
            _seatLine = _kit.Text(At("SeatLine", Margin, SeatLineY, 900f, 22f), 15, _kit.Sub, TextAlignmentOptions.Left);
            VoyageKit.Stretch(_seatLine.rectTransform);
        }

        // A glyph in a layout row: a box as wide as the glyph (the row may stretch its height), the glyph square at its middle.
        VoyageGlyph Glyph(Transform parent, float size)
        {
            var box = VoyageKit.Node("GlyphBox", parent);
            var le = VoyageKit.Le(box);
            le.preferredWidth = le.minWidth = size;
            le.preferredHeight = le.minHeight = size;
            var rt = VoyageKit.Node("Glyph", box);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;
            var g = rt.gameObject.AddComponent<VoyageGlyph>();
            g.raycastTarget = false;
            return g;
        }

        void BuildAlso()
        {
            // The tiles' title (caps 14, Sub) over two columns of small tiles at the right.
            _alsoTitle = _kit.Label(At("AlsoTitle", AlsoX, AlsoY, 2f * TileWidth + TileGap, 20f), 14, _kit.Sub);
            VoyageKit.Stretch(_alsoTitle.rectTransform);
        }

        TileView TileFor(int index)
        {
            while (_tiles.Count <= index)
            {
                // A small tile: the hangar preview (128 tall), the name (17) with the met mark at the right, a note (14).
                int n = _tiles.Count;
                var view = new TileView
                {
                    Root = At("Also" + n, AlsoX + n % 2 * (TileWidth + TileGap), TileY + n / 2 * TileHeight, TileWidth, TileHeight),
                };
                view.Group = view.Root.gameObject.AddComponent<CanvasGroup>();
                var hit = VoyageKit.Block(view.Root, VoyageKit.Clear, hit: true);
                view.Button = view.Root.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = hit;
                view.Button.transition = Selectable.Transition.None;
                view.Picture = At("Picture", 0f, 0f, TileWidth, TilePicture, view.Root);
                view.Hull = VoyageSeats.HullBox(view.Picture);
                view.Name = _kit.Text(At("Name", 10f, TilePicture + 6f, TileWidth - 50f, 24f, view.Root), 17, _kit.Light,
                    TextAlignmentOptions.Left);
                VoyageKit.Stretch(view.Name.rectTransform);
                view.Met = At("Met", TileWidth - 28f, TilePicture + 10f, 16f, 16f, view.Root).gameObject.AddComponent<VoyageMetMark>();
                view.Note = _kit.Text(At("Note", 10f, TilePicture + 28f, TileWidth - 20f, 24f, view.Root), 14, _kit.Cyan,
                    TextAlignmentOptions.Left);
                VoyageKit.Stretch(view.Note.rectTransform);
                var edge = VoyageKit.Node("Edge", view.Root);
                VoyageKit.Stretch(edge);
                view.Edge = edge.gameObject.AddComponent<VoyageOutline>();
                view.Edge.raycastTarget = false;
                _tiles.Add(view);
            }
            return _tiles[index];
        }

        void BuildHead()
        {
            // Over the tabs, left: the archetype's role glyph (18) and name (16, Light), its line, what it counters and
            // what counters it (15, Sub), 24 apart.
            var head = At("Head", FloorLeft, HeadY, Screen.x - FloorLeft, 24f);
            VoyageKit.Row(head, 24f, TextAnchor.MiddleLeft).childForceExpandHeight = true;
            var name = VoyageKit.Node("Archetype", head);
            VoyageKit.Row(name, 10f, TextAnchor.MiddleLeft).childForceExpandHeight = true;
            _headGlyph = Glyph(name, 18f);
            _head = _kit.Text(name, 16, _kit.Light, TextAlignmentOptions.Left);
            _head.overflowMode = TextOverflowModes.Overflow;
            _headLine = _kit.Text(head, 15, _kit.Sub, TextAlignmentOptions.Left);
            _headLine.overflowMode = TextOverflowModes.Overflow;
            _headCounters = _kit.Text(head, 15, _kit.Sub, TextAlignmentOptions.Left);
            _headCounters.overflowMode = TextOverflowModes.Overflow;
            _headCountered = _kit.Text(head, 15, _kit.Sub, TextAlignmentOptions.Left);
            _headCountered.overflowMode = TextOverflowModes.Overflow;
        }

        void BuildTabs()
        {
            // The nine archetypes in their three roles along the foot, centred, 30 between roles: the role's glyph (20),
            // then each archetype (15), one muted line under the role; the open one Light with a cyan bar (2); the asked
            // ones Sub; the rest muted. LB / RB at the row's ends step through all nine.
            var row = At("Archetypes", Margin, TabsY, Screen.x - 2f * Margin, TabsHeight);
            var h = VoyageKit.Row(row, 30f, TextAnchor.MiddleCenter);
            h.childForceExpandHeight = true;
            var prev = _kit.Label(row, 13, _kit.Sub, TextAlignmentOptions.Center);
            prev.overflowMode = TextOverflowModes.Overflow;
            VoyageKit.Set(prev, _archetypes ? _archetypes.PrevRoleKey : null);
            if (_archetypes)
                foreach (var role in _archetypes.Roles)
                {
                    if (role == null) continue;
                    var group = VoyageKit.Node("Role" + (int)role.Role, row);
                    var gh = VoyageKit.Row(group, 4f, TextAnchor.MiddleCenter);
                    gh.childForceExpandHeight = true;
                    gh.padding = new RectOffset(10, 10, 0, 0);
                    var glyph = Glyph(group, 20f);
                    glyph.Form = VoyageGlyph.Of(role.Role);
                    var under = VoyageKit.Node("Under", group);
                    VoyageKit.Le(under).ignoreLayout = true;
                    VoyageKit.Foot(under, 0f, 1f);
                    var rule = VoyageKit.Block(under, VoyageKit.Fade(_kit.Outline, 0.6f));
                    _groups.Add((role.Role, glyph, rule));
                    foreach (var a in _archetypes.Archetypes)
                    {
                        if (VesselArchetypes.RoleOf(a) != role.Role) continue;
                        var tab = VoyageKit.Node("Archetype" + (int)a, group);
                        var hit = VoyageKit.Block(tab, VoyageKit.Clear, hit: true);
                        var b = tab.gameObject.AddComponent<Button>();
                        b.targetGraphic = hit;
                        b.transition = Selectable.Transition.None;
                        b.onClick.AddListener(() => ShowClass(a));
                        var inner = VoyageKit.Row(tab, 0f, TextAnchor.MiddleCenter);
                        inner.padding = new RectOffset(12, 12, 0, 0);
                        var view = new TabView { Class = a };
                        view.Name = _kit.Text(tab, 15, _kit.Sub, TextAlignmentOptions.Left);
                        view.Name.overflowMode = TextOverflowModes.Overflow;
                        var bar = VoyageKit.Node("Bar", tab);
                        VoyageKit.Le(bar).ignoreLayout = true;
                        VoyageKit.Foot(bar, 0f, 2f);
                        view.Bar = VoyageKit.Block(bar, _kit.Cyan);
                        _tabs.Add(view);
                    }
                }
            var next = _kit.Label(row, 13, _kit.Sub, TextAlignmentOptions.Center);
            next.overflowMode = TextOverflowModes.Overflow;
            VoyageKit.Set(next, _archetypes ? _archetypes.NextRoleKey : null);
        }

        void BuildBar()
        {
            // The fit meter (three 10 high segments, 4 apart) and how the pick changes it (15) at the left; Back (B) and
            // Take the vessel (A) at the right.
            _fitRow = At("Fit", Margin, BarY, 900f, BarHeight);
            float seg = (FitWidth - 8f) / 3f;
            for (int i = 0; i < _fitSegments.Length; i++)
                _fitSegments[i] = VoyageKit.Block(At("Segment" + i, i * (seg + 4f), 24f, seg, 8f, _fitRow), _kit.Light);
            _fitText = _kit.Text(At("Words", FitWidth + 24f, 0f, 700f, BarHeight, _fitRow), 16, _kit.Light, TextAlignmentOptions.Left);
            VoyageKit.Stretch(_fitText.rectTransform);
            _fitText.overflowMode = TextOverflowModes.Overflow;

            float takeX = Screen.x - Margin - TakeWidth;
            var back = At("Back", takeX - 24f - BackWidth, BarY, BackWidth, BarHeight);
            _kit.Quiet(back, _archetypes ? _archetypes.BackKey : null, _archetypes ? _archetypes.Back : null, Close);
            var take = At("Take", takeX, BarY, TakeWidth, BarHeight);
            (_, _take) = _kit.Cta(take, _archetypes ? _archetypes.TakeKey : null, _archetypes ? _archetypes.Take : null, Take);
        }

        StandView StandFor(int index)
        {
            while (_stands.Count <= index)
            {
                // A vessel standing on the floor: its hangar preview, its name (22), one thin line, one line about it (15).
                var view = new StandView { Root = At("Vessel" + _stands.Count, 0f, StandY, MaxStandWidth, StandHeight) };
                view.Group = view.Root.gameObject.AddComponent<CanvasGroup>();
                var hit = VoyageKit.Block(view.Root, VoyageKit.Clear, hit: true);
                view.Button = view.Root.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = hit;
                view.Button.transition = Selectable.Transition.None;
                // The hangar preview standing on the floor, as Your Ark stands a seat's hull.
                view.Picture = VoyageKit.Node("Picture", view.Root);
                view.Hull = VoyageSeats.HullBox(view.Picture);
                view.Name = _kit.Text(VoyageKit.Node("Name", view.Root), 22, _kit.Light, TextAlignmentOptions.Left);
                view.Rule = VoyageKit.Block(VoyageKit.Node("Rule", view.Root), _kit.Outline);
                view.Line = _kit.Text(VoyageKit.Node("Line", view.Root), 15, _kit.Sub, TextAlignmentOptions.TopLeft);
                view.Line.textWrappingMode = TextWrappingModes.Normal;
                view.Line.lineSpacing = 6f;
                _stands.Add(view);
            }
            return _stands[index];
        }

        // A stand at (x, y), w wide; a short stand (two rows of leaders) keeps the same order at a smaller picture.
        void Lay(StandView view, float x, float y, float w, bool picked, bool small)
        {
            float picture = small ? SmallPicture : PictureHeight, drop = PictureHeight - picture;
            VoyageKit.At(view.Root, x, y, w, small ? SmallStand : StandHeight);
            VoyageKit.At(view.Picture, 0f, PictureY, w, picture);
            VoyageKit.At(view.Name.rectTransform.parent as RectTransform, 0f, NameY - drop, w, 30f);
            VoyageKit.Stretch(view.Name.rectTransform);
            view.Name.fontSize = small ? 19f : 22f;
            VoyageKit.At(view.Rule.rectTransform, 0f, RuleY - drop - (small ? 8f : 0f), w, picked ? 2f : 1f);
            VoyageKit.At(view.Line.rectTransform.parent as RectTransform, 0f, LineY - drop - (small ? 10f : 0f), w, 44f);
            VoyageKit.Stretch(view.Line.rectTransform);
        }

        // ── Opening ──────────────────────────────────────────────────────────

        /// <summary>Open on <paramref name="seat"/> of <paramref name="offer"/>'s seats (the record under
        /// <paramref name="key"/>), on the archetype that seat fills for the voyage: the seat's own in the squad's best
        /// assignment, else the first asked seat nobody meets, else the seat vessel's primary.</summary>
        public void Open(VoyageBoard.Offer offer, int seat, string key, int difficulty)
        {
            _offer = offer;
            _difficulty = difficulty;
            _key = string.IsNullOrEmpty(key) ? offer?.Key : key;
            _seat = seat;
            var squad = VoyageSeatRecord.Get(_key);
            _picked = squad != null ? squad.GetSlot(seat).Vessel : default;
            _class = SeatAsk(squad);
            if (_class == VesselArchetype.None && _tabs.Count > 0) _class = _tabs[0].Class;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            _fitFor = new Vector2(-1f, -1f);
            Tick(0f);
            Refresh();
        }

        public void Close()
        {
            _root.gameObject.SetActive(false);
        }

        /// <summary>Nothing to remove: the page lives in the board's page slot.</summary>
        public void Dispose()
        {
        }

        /// <summary>Scale the board's screen to the screen the page opens over; the shoulders step the role.</summary>
        public void Tick(float dt)
        {
            if (!IsOpen) return;
            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.rightShoulder.wasPressedThisFrame) StepClass(+1);
                else if (pad.leftShoulder.wasPressedThisFrame) StepClass(-1);
            }
            var room = _root.rect.size;
            if (room == _fitFor) return;
            _fitFor = room;
            VoyageKit.Fit(_frame, Screen);
        }

        string Job => VoyageArchetypesSO.JobOf(_key);

        VoyageSeatFill.Filled[] Filled(SavedSquad squad) =>
            VoyageQuay.FillOf(_archetypes, Job, _offer?.VoyageId, squad, _squadArchetypes);

        VesselArchetype SeatAsk(SavedSquad squad)
        {
            if (!_archetypes || squad == null) return VesselArchetype.None;
            var filled = Filled(squad);
            int at = VoyageSeatFill.SeatOf(filled, _seat);
            if (at >= 0) return filled[at].Archetype;
            foreach (var f in filled)
                if (f.Meets <= 0f) return f.Archetype;
            return _archetypes.PrimaryOf(squad.GetSlot(_seat).Vessel);
        }

        void StepClass(int step)
        {
            if (_tabs.Count == 0) return;
            int at = 0;
            for (int i = 0; i < _tabs.Count; i++)
                if (_tabs[i].Class == _class) at = i;
            ShowClass(_tabs[(at + step + _tabs.Count) % _tabs.Count].Class);
        }

        void ShowClass(VesselArchetype a)
        {
            _class = a;
            // The seat's own vessel stays picked when it has this archetype; else the first leader this seat can take.
            var squad = VoyageSeatRecord.Get(_key);
            var current = squad != null ? squad.GetSlot(_seat).Vessel : default;
            Gather(squad);
            _picked = default;
            foreach (var o in _shown)
                if (o.Vessel == current) _picked = current;
            foreach (var o in _also)
                if (o.Vessel == current) _picked = current;
            if (_picked == default)
                foreach (var o in _shown)
                    if (CanTake(squad, o.Vessel))
                    {
                        _picked = o.Vessel;
                        break;
                    }
            Refresh();
        }

        // ── Drawing ──────────────────────────────────────────────────────────

        /// <summary>The vessels this seat is offered that lead with the open archetype (the floor, in its role's order) and
        /// those that have it second or third (the tiles, at most six).</summary>
        void Gather(SavedSquad squad)
        {
            _shown.Clear();
            _also.Clear();
            _rules.Offer(VoyageSeats.Vessel, squad, _seat, _offered);
            if (!_archetypes) return;
            var role = _archetypes.EntryOf(VesselArchetypes.RoleOf(_class));
            if (role != null)
                foreach (var v in role.Vessels)
                    if (_archetypes.PrimaryOf(v) == _class && Offered(v) is { } o) _shown.Add(o);
            foreach (var v in _archetypes.Vessels)
            {
                if (_also.Count >= MaxTiles) continue;
                int rank = IndexOf(_archetypes.ArchetypesOf(v), _class);
                if (rank > 0 && Offered(v) is { } o) _also.Add(o);
            }
        }

        VoyageSeats.Option? Offered(VesselClassType v)
        {
            foreach (var o in _offered)
                if (o.Vessel == v) return o;
            return null;
        }

        /// <summary>Whether the seat can take <paramref name="vessel"/>: yours takes any; an AI seat only one the AI can
        /// fly (<see cref="ArkEscortRoles.HasRole"/>).</summary>
        bool CanTake(SavedSquad squad, VesselClassType vessel) =>
            squad != null && (VoyageOpenCard.IsMySeat(squad, _seat) || ArkEscortRoles.HasRole(vessel));

        public void Refresh()
        {
            if (!IsOpen) return;
            var squad = VoyageSeatRecord.Get(_key);
            if (squad == null || !VoyageSeats.CanEdit(squad, _seat) || !_archetypes)
            {
                Close();
                return;
            }
            bool you = VoyageOpenCard.IsMySeat(squad, _seat);

            _header.Draw(_offer, _difficulty, _key);
            DrawSeatLine(squad, you);

            Gather(squad);
            var entry = _archetypes.EntryOf(_class);
            string name = _archetypes.NameOf(_class);
            int n = _shown.Count;
            bool small = n > MaxInRow;
            int cols = small ? (n + 1) / 2 : Mathf.Max(1, n);
            float room = FloorRight - FloorLeft, gap = small ? 40f : StandGap;
            float w = Mathf.Clamp((room - (cols - 1) * gap) / cols, MinStandWidth, MaxStandWidth);
            for (int i = 0; i < n; i++)
            {
                var o = _shown[i];
                var view = StandFor(i);
                bool picked = o.Vessel == _picked;
                bool can = CanTake(squad, o.Vessel);
                view.Root.gameObject.SetActive(true);
                int row = small ? i / cols : 0, inRow = small ? Mathf.Min(cols, n - row * cols) : n;
                float x0 = FloorLeft + (room - (inRow * w + (inRow - 1) * gap)) * 0.5f;
                Lay(view, x0 + (i - row * cols) * (w + gap), StandY + row * (SmallStand + 8f), w, picked, small);
                var vessel = _rules.VesselOf(o.Vessel);
                _rules.SetHull(view.Hull, VoyageSeats.HullOf(vessel));
                VoyageKit.Set(view.Name, o.Name);
                view.Rule.color = picked ? _kit.Cyan : VoyageKit.Fade(_kit.Outline, 0.6f);
                VoyageKit.Set(view.Line, can ? Join2(ArchetypeList(o.Vessel), MeetsLine(o.Vessel)) : _archetypes.NeedsPerson);
                view.Group.alpha = can ? 1f : DimAlpha;
                view.Button.interactable = can;
                Press(view.Button, o.Vessel);
            }
            for (int i = n; i < _stands.Count; i++) _stands[i].Root.gameObject.SetActive(false);
            VoyageKit.Set(_none, n == 0 ? TemplateText.Fill(_archetypes.NoneOwned, "role", name) : null);

            // The tiles: the vessels with the archetype second or third, the one in the seat now marked.
            var current = squad.GetSlot(_seat).Vessel;
            _alsoTitle.gameObject.SetActive(_also.Count > 0);
            VoyageKit.Set(_alsoTitle, TemplateText.Fill(_archetypes.AlsoTitle, "archetype", name));
            for (int i = 0; i < _also.Count; i++)
            {
                var o = _also[i];
                var tile = TileFor(i);
                bool can = CanTake(squad, o.Vessel);
                bool here = o.Vessel == current, picked = o.Vessel == _picked;
                tile.Root.gameObject.SetActive(true);
                _rules.SetHull(tile.Hull, VoyageSeats.HullOf(_rules.VesselOf(o.Vessel)));
                VoyageKit.Set(tile.Name, o.Name);
                tile.Met.Set(0.5f, _kit.Light);
                VoyageKit.Set(tile.Note, here ? _archetypes.InSeat : null);
                tile.Edge.enabled = picked || here;
                tile.Edge.Solid(picked ? _kit.Cyan : VoyageKit.Fade(_kit.Outline, 0.8f));
                tile.Group.alpha = can ? 1f : DimAlpha;
                tile.Button.interactable = can;
                Press(tile.Button, o.Vessel);
            }
            for (int i = _also.Count; i < _tiles.Count; i++) _tiles[i].Root.gameObject.SetActive(false);

            // The archetype's line, what it counters and what counters it.
            _headGlyph.Form = VoyageGlyph.Of(_class);
            _headGlyph.color = _kit.Light;
            VoyageKit.Set(_head, name);
            VoyageKit.Set(_headLine, entry?.Line);
            VoyageKit.Set(_headCounters, TemplateText.Fill(_archetypes.CountersLine, "list", NameList(_archetypes.Counters(_class))));
            VoyageKit.Set(_headCountered, TemplateText.Fill(_archetypes.CounteredByLine, "list", NameList(_archetypes.CounteredBy(_class))));

            var asks = _archetypes.AskOf(Job, _offer?.VoyageId);
            foreach (var tab in _tabs)
            {
                bool on = tab.Class == _class;
                bool asked = false;
                foreach (var a in asks) asked |= a == tab.Class;
                var col = on ? _kit.Light : asked ? _kit.Sub : VoyageKit.Fade(_kit.Sub, 0.5f);
                tab.Name.color = col;
                VoyageKit.Set(tab.Name, Leads(tab.Class)
                    ? _archetypes.NameOf(tab.Class)
                    : TemplateText.Fill(_archetypes.NoneYet, "archetype", _archetypes.NameOf(tab.Class)));
                tab.Bar.enabled = on;
            }
            foreach (var (role, glyph, rule) in _groups)
                glyph.color = VesselArchetypes.RoleOf(_class) == role ? _kit.Light : _kit.Sub;
            var pickedVessel = _rules.VesselOf(_picked);
            VoyageKit.Set(_take, TemplateText.Fill(_archetypes.Take, "vessel",
                pickedVessel ? pickedVessel.CardName.ToUpperInvariant() : string.Empty));
            DrawFit(squad, Job);
        }

        void Press(Button button, VesselClassType vessel)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                _picked = vessel;
                Refresh();
            });
        }

        // The seat's line: who sits in it and the asked archetype its vessel fills ("fills Harraser, half (Squirrel's second)").
        void DrawSeatLine(SavedSquad squad, bool you)
        {
            string who = you ? _archetypes.WhoYou : _archetypes.WhoAi;
            var filled = Filled(squad);
            int at = VoyageSeatFill.SeatOf(filled, _seat);
            string line;
            if (at < 0) line = TemplateText.Fill(_archetypes.SeatLine, "seat", _rules.Role(_seat), "who", who);
            else
            {
                var f = filled[at];
                var vessel = _rules.VesselOf(squad.GetSlot(_seat).Vessel);
                var words = VoyageQuaySO.Load();
                line = f.Meets >= 1f
                    ? TemplateText.Fill(_archetypes.SeatFills, "seat", _rules.Role(_seat), "who", who, "archetype", _archetypes.NameOf(f.Archetype))
                    : TemplateText.Fill(_archetypes.SeatFillsHalf, "seat", _rules.Role(_seat), "who", who,
                        "archetype", _archetypes.NameOf(f.Archetype), "vessel", vessel ? vessel.CardName : null,
                        "rank", words ? words.Rank(f.Rank(_squadArchetypes)) : null);
            }
            VoyageKit.Set(_seatLine, line);
        }

        bool Leads(VesselArchetype a)
        {
            foreach (var v in _archetypes.Vessels)
                if (_archetypes.PrimaryOf(v) == a) return true;
            return false;
        }

        static int IndexOf(IReadOnlyList<VesselArchetype> list, VesselArchetype a)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == a) return i;
            return -1;
        }

        // "Harraser · Sapper": the vessel's archetypes, primary first.
        string ArchetypeList(VesselClassType vessel)
        {
            var words = VoyageQuaySO.Load();
            string join = words ? words.ArchetypeJoin.Trim() : "·";
            var all = _archetypes.ArchetypesOf(vessel);
            var b = new System.Text.StringBuilder();
            for (int i = 0; i < all.Count; i++)
            {
                if (i > 0) b.Append(' ').Append(join).Append(' ');
                b.Append(_archetypes.NameOf(all[i]));
            }
            return b.ToString();
        }

        // How the vessel meets the open archetype when the voyage asks for it: fully on its primary, by half on its second
        // or third; nothing when the voyage does not ask for this archetype.
        string MeetsLine(VesselClassType vessel)
        {
            bool asked = false;
            foreach (var a in _archetypes.AskOf(Job, _offer?.VoyageId)) asked |= a == _class;
            if (!asked) return null;
            float meets = VesselArchetypes.Meets(_archetypes.ArchetypesOf(vessel), _class);
            return meets >= 1f ? _archetypes.MeetsFull : meets > 0f ? _archetypes.MeetsHalf : null;
        }

        string NameList(List<VesselArchetype> list)
        {
            if (list.Count == 0) return _archetypes.Nothing;
            var b = new System.Text.StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) b.Append(_archetypes.ListJoin);
                b.Append(_archetypes.NameOf(list[i]));
            }
            return b.ToString();
        }

        static string Join2(string a, string b) =>
            string.IsNullOrEmpty(a) ? b : string.IsNullOrEmpty(b) ? a : a + "\n" + b;

        readonly List<VesselClassType> _fitScratch = new();

        // The squad fit now and with the pick in this seat; nothing for a voyage that asks for nothing (the Crystal Run).
        void DrawFit(SavedSquad squad, string job)
        {
            bool shown = _archetypes.AskOf(job, _offer?.VoyageId).Count > 0;
            _fitRow.gameObject.SetActive(shown);
            if (!shown) return;
            _fitScratch.Clear();
            for (int s = 0; s < SavedSquad.SlotCount; s++) _fitScratch.Add(squad.GetSlot(s).Vessel);
            float from = _archetypes.Fit(job, _offer?.VoyageId, _fitScratch);
            if (_picked != default) _fitScratch[_seat] = _picked;
            float to = _archetypes.Fit(job, _offer?.VoyageId, _fitScratch);
            for (int i = 0; i < _fitSegments.Length; i++)
                _fitSegments[i].color = VoyageKit.FitSegment(_kit, to, i);
            string fit = Mathf.Approximately(to, from)
                ? TemplateText.Fill(_archetypes.FitSame, "fit", VoyageArchetypesSO.FitText(to))
                : TemplateText.Fill(_archetypes.FitChange, "from", VoyageArchetypesSO.FitText(from),
                    "to", VoyageArchetypesSO.FitText(to));
            // "Pay 33" after the fit, as the quay's foot and Your Ark's bar.
            var words = VoyageQuaySO.Load();
            string pay = _offer != null && _offer.Pays && words
                ? TemplateText.Fill(words.ArkPay, "pay", VoyageBoard.PayAt(_offer, _difficulty).ToString(System.Globalization.CultureInfo.InvariantCulture))
                : null;
            VoyageKit.Set(_fitText, string.IsNullOrEmpty(pay) ? fit : fit + "    " + pay);
        }

        // Take the vessel: the pick goes in the seat for this voyage, and Your Ark shows it.
        void Take()
        {
            var squad = VoyageSeatRecord.Get(_key);
            if (squad != null && _picked != default && squad.GetSlot(_seat).Vessel != _picked && CanTake(squad, _picked))
                foreach (var o in _offered)
                    if (o.Vessel == _picked)
                    {
                        _rules.PickVoyage(_key, _seat, VoyageSeats.Vessel, o);
                        break;
                    }
            Close();
        }
    }
}

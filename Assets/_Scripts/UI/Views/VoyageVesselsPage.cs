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
    /// Vessels, one role on the floor (Frogma Rev 136 E, picked by Shombith 2026-11-24, drawn with the game's hangar
    /// previews in Rev 138 Vessels; Docs/MENU_REDESIGN_B.md sections 229 and 231). It opens over Your Ark when a seat's
    /// hull is pressed, in Your Ark's hangar look:
    /// <list type="bullet">
    /// <item>the voyage on top, with the archetypes it asks for at the right; the seat's line under it ("Seat 3 - flown by the
    /// AI - choose a vessel");</item>
    /// <item>the role's vessels standing on the hangar floor: each one's hangar preview, its name, one thin line (cyan
    /// on the one picked) and one line about it; on an AI seat what the AI cannot fly is dimmed and cannot be taken;</item>
    /// <item>the wanted line over the role row, the three roles along the foot (LB / RB step them), the wanted ones
    /// brighter;</item>
    /// <item>the bar: the squad fit meter and how the pick changes it, Back (B) and Take the vessel (A), the one Lime.</item>
    /// </list>
    /// A press on a vessel picks it on the page; Take the vessel saves it to the seat for THIS voyage
    /// (<see cref="VoyageSeats.PickVoyage"/>), as the seat picker does, and closes. An archetype is a label only
    /// (<see cref="VoyageArchetypesSO"/>); nothing here changes a vessel.
    /// </summary>
    public sealed class VoyageVesselsPage
    {
        // Frogma Rev 138 Vessels, in the page's units: x 40-1880, y 176-1046 of the 1920 x 1080 board, as Your Ark.
        static readonly Vector2 Screen = new(1840f, 870f);
        const float Margin = 0f, SeatLineY = 62f, StandY = 74f, StandHeight = 520f;
        const float MaxStandWidth = 500f, StandGap = 90f, PictureY = 0f, PictureHeight = 236f, NameY = 315f, RuleY = 363f,
            LineY = 382f;
        const float FloorY = 324f, FloorHeight = 546f, HeadY = 617f, TabsY = 684f, TabsHeight = 56f;
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

        TMP_Text _seatLine, _head, _headLine, _none, _fitText;
        RectTransform _fitRow;
        readonly List<StandView> _stands = new();
        readonly List<TabView> _tabs = new();
        readonly Image[] _fitSegments = new Image[3];

        VoyageBoard.Offer _offer;
        string _key;
        int _seat;
        VesselRole _class;
        VesselClassType _picked;
        readonly List<VoyageSeats.Option> _offered = new();
        readonly List<VoyageSeats.Option> _shown = new();

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

        sealed class TabView
        {
            public VesselRole Class;
            public VoyageGlyph Glyph;
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
            BuildHead();
            BuildTabs();
            BuildBar();
            _none = _kit.Text(At("None", Margin, StandY + NameY, Screen.x - 2f * Margin, 30f), 18, _kit.Sub, TextAlignmentOptions.Center);
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

        VoyageGlyph Glyph(Transform parent, float size)
        {
            var rt = VoyageKit.Node("Glyph", parent);
            var le = VoyageKit.Le(rt);
            le.preferredWidth = le.minWidth = le.preferredHeight = le.minHeight = size;
            var g = rt.gameObject.AddComponent<VoyageGlyph>();
            g.raycastTarget = false;
            return g;
        }

        void BuildHead()
        {
            // Over the role row: "<Role>s are wanted on this voyage" (15, Light) and the role's line (15, Sub), centred.
            var head = At("Head", Margin, HeadY, Screen.x - 2f * Margin, 24f);
            VoyageKit.Row(head, 14f, TextAnchor.MiddleCenter).childForceExpandHeight = true;
            _head = _kit.Text(head, 15, _kit.Light, TextAlignmentOptions.Left);
            _head.overflowMode = TextOverflowModes.Overflow;
            _headLine = _kit.Text(head, 15, _kit.Sub, TextAlignmentOptions.Left);
            _headLine.overflowMode = TextOverflowModes.Overflow;
        }

        void BuildTabs()
        {
            // The three roles along the foot, 8 apart, centred: a glyph (22) and the name (15); the open one Light and
            // underlined in cyan (2); the wanted ones Sub; the rest muted. LB / RB at the row's ends.
            var row = At("Roles", Margin, TabsY, Screen.x - 2f * Margin, TabsHeight);
            var h = VoyageKit.Row(row, 8f, TextAnchor.MiddleCenter);
            h.childForceExpandHeight = true;
            var prev = _kit.Label(row, 13, _kit.Sub, TextAlignmentOptions.Center);
            prev.overflowMode = TextOverflowModes.Overflow;
            VoyageKit.Set(prev, _archetypes ? _archetypes.PrevRoleKey : null);
            if (_archetypes)
                foreach (var entry in _archetypes.Roles)
                {
                    if (entry == null) continue;
                    var c = entry.Role;
                    var tab = VoyageKit.Node("Role" + (int)c, row);
                    var hit = VoyageKit.Block(tab, VoyageKit.Clear, hit: true);
                    var b = tab.gameObject.AddComponent<Button>();
                    b.targetGraphic = hit;
                    b.transition = Selectable.Transition.None;
                    b.onClick.AddListener(() => ShowClass(c));
                    var inner = VoyageKit.Row(tab, 10f, TextAnchor.MiddleCenter);
                    inner.padding = new RectOffset(18, 18, 0, 0);
                    var view = new TabView { Class = c, Glyph = Glyph(tab, 22f) };
                    view.Glyph.Form = VoyageGlyph.Of(c);
                    view.Name = _kit.Text(tab, 15, _kit.Sub, TextAlignmentOptions.Left);
                    view.Name.overflowMode = TextOverflowModes.Overflow;
                    VoyageKit.Set(view.Name, entry.Name);
                    var bar = VoyageKit.Node("Bar", tab);
                    VoyageKit.Le(bar).ignoreLayout = true;
                    VoyageKit.Foot(bar, 0f, 2f);
                    view.Bar = VoyageKit.Block(bar, _kit.Cyan);
                    _tabs.Add(view);
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
            _kit.Cta(take, _archetypes ? _archetypes.TakeKey : null, _archetypes ? _archetypes.Take : null, Take);
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
                _stands.Add(view);
            }
            return _stands[index];
        }

        void Lay(StandView view, float x, float w, bool picked)
        {
            VoyageKit.At(view.Root, x, StandY, w, StandHeight);
            VoyageKit.At(view.Picture, 0f, PictureY, w, PictureHeight);
            VoyageKit.At(view.Name.rectTransform.parent as RectTransform, 0f, NameY, w, 30f);
            VoyageKit.Stretch(view.Name.rectTransform);
            VoyageKit.At(view.Rule.rectTransform, 0f, RuleY, w, picked ? 2f : 1f);
            VoyageKit.At(view.Line.rectTransform.parent as RectTransform, 0f, LineY, w, 60f);
            VoyageKit.Stretch(view.Line.rectTransform);
        }

        // ── Opening ──────────────────────────────────────────────────────────

        /// <summary>Open on <paramref name="seat"/> of <paramref name="offer"/>'s seats (the record under
        /// <paramref name="key"/>), on the role of the vessel in it.</summary>
        public void Open(VoyageBoard.Offer offer, int seat, string key, int difficulty)
        {
            _offer = offer;
            _difficulty = difficulty;
            _key = string.IsNullOrEmpty(key) ? offer?.Key : key;
            _seat = seat;
            var squad = VoyageSeatRecord.Get(_key);
            _picked = squad != null ? squad.GetSlot(seat).Vessel : default;
            _class = _archetypes ? _archetypes.RoleOf(_picked) : VesselRole.None;
            if (_class == VesselRole.None && _tabs.Count > 0) _class = _tabs[0].Class;
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

        void StepClass(int step)
        {
            if (_tabs.Count == 0) return;
            int at = 0;
            for (int i = 0; i < _tabs.Count; i++)
                if (_tabs[i].Class == _class) at = i;
            ShowClass(_tabs[(at + step + _tabs.Count) % _tabs.Count].Class);
        }

        void ShowClass(VesselRole c)
        {
            _class = c;
            // The seat's own vessel stays picked when it is of this role; else the first this seat can take.
            var squad = VoyageSeatRecord.Get(_key);
            var current = squad != null ? squad.GetSlot(_seat).Vessel : default;
            if (_archetypes && _archetypes.RoleOf(current) == c) _picked = current;
            else
            {
                _picked = default;
                Gather(squad);
                foreach (var o in _shown)
                    if (CanTake(squad, o.Vessel))
                    {
                        _picked = o.Vessel;
                        break;
                    }
            }
            Refresh();
        }

        // ── Drawing ──────────────────────────────────────────────────────────

        /// <summary>The open role's vessels this seat is offered, in the role's own order.</summary>
        void Gather(SavedSquad squad)
        {
            _shown.Clear();
            _rules.Offer(VoyageSeats.Vessel, squad, _seat, _offered);
            var entry = _archetypes ? _archetypes.EntryOf(_class) : null;
            if (entry == null) return;
            foreach (var v in entry.Vessels)
                foreach (var o in _offered)
                    if (o.Vessel == v)
                    {
                        _shown.Add(o);
                        break;
                    }
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
            string job = VoyageArchetypesSO.JobOf(_key);

            _header.Draw(_offer, _difficulty, _key);
            VoyageKit.Set(_seatLine, TemplateText.Fill(_archetypes.SeatLine, "seat", _rules.Role(_seat),
                "who", you ? _archetypes.WhoYou : _archetypes.WhoAi));

            Gather(squad);
            var entry = _archetypes.EntryOf(_class);
            int n = _shown.Count;
            float w = n == 0 ? MaxStandWidth : Mathf.Min(MaxStandWidth, (Screen.x - 2f * Margin - (n - 1) * StandGap) / n);
            float x0 = (Screen.x - (n * w + (n - 1) * StandGap)) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                var o = _shown[i];
                var view = StandFor(i);
                bool picked = o.Vessel == _picked;
                bool can = CanTake(squad, o.Vessel);
                view.Root.gameObject.SetActive(true);
                Lay(view, x0 + i * (w + StandGap), w, picked);
                var vessel = _rules.VesselOf(o.Vessel);
                _rules.SetHull(view.Hull, VoyageSeats.HullOf(vessel));
                VoyageKit.Set(view.Name, o.Name);
                view.Rule.color = picked ? _kit.Cyan : VoyageKit.Fade(_kit.Outline, 0.6f);
                string about = _archetypes.EntryOf(_archetypes.PrimaryOf(o.Vessel))?.Line;
                VoyageKit.Set(view.Line, !can
                    ? _archetypes.NeedsPerson
                    : ArkEscortRoles.HasRole(o.Vessel) && !string.IsNullOrEmpty(_archetypes.AiCanFly)
                        ? Join(about, _archetypes.AiCanFly)
                        : about);
                view.Group.alpha = can ? 1f : DimAlpha;
                view.Button.interactable = can;
                var vesselClass = o.Vessel;
                view.Button.onClick.RemoveAllListeners();
                view.Button.onClick.AddListener(() =>
                {
                    _picked = vesselClass;
                    Refresh();
                });
            }
            for (int i = n; i < _stands.Count; i++) _stands[i].Root.gameObject.SetActive(false);
            VoyageKit.Set(_none, n == 0 ? TemplateText.Fill(_archetypes.NoneOwned, "role", entry?.Name) : null);

            bool wanted = _archetypes.Wanted(job, _offer?.VoyageId, _class);
            VoyageKit.Set(_head, wanted ? TemplateText.Fill(_archetypes.WantedLine, "role", entry?.Name) : null);
            _head.gameObject.SetActive(wanted);
            VoyageKit.Set(_headLine, entry?.Line);

            foreach (var tab in _tabs)
            {
                bool on = tab.Class == _class;
                var col = on ? _kit.Light : _archetypes.Wanted(job, _offer?.VoyageId, tab.Class) ? _kit.Sub : VoyageKit.Fade(_kit.Sub, 0.5f);
                tab.Name.color = col;
                tab.Glyph.color = col;
                tab.Bar.enabled = on;
            }
            DrawFit(squad, job);
        }

        static string Join(string a, string b) =>
            string.IsNullOrEmpty(a) ? b : string.IsNullOrEmpty(b) ? a : a + " " + b;

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

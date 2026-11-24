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
    /// Vessels, one class on the floor (Frogma Rev 136 E, picked by Shombith 2026-11-24, drawn with the game's hangar
    /// previews in Rev 138 Vessels; Docs/MENU_REDESIGN_B.md sections 229 and 231). It opens over Your Ark when a seat's
    /// hull is pressed, in Your Ark's hangar look:
    /// <list type="bullet">
    /// <item>the voyage on top, with the classes it wants at the right; the seat's line under it ("Seat 3 - flown by the
    /// AI - choose a vessel");</item>
    /// <item>the class's vessels standing on the hangar floor: each one's hangar preview, its name, one thin line (cyan
    /// on the one picked) and one line about it; on an AI seat what the AI cannot fly is dimmed and cannot be taken;</item>
    /// <item>the wanted line over the class row, the seven classes along the foot (LB / RB step them), the wanted ones
    /// brighter;</item>
    /// <item>the bar: the squad fit meter and how the pick changes it, Back (B) and Take the vessel (A), the one Lime.</item>
    /// </list>
    /// A press on a vessel picks it on the page; Take the vessel saves it to the seat for THIS voyage
    /// (<see cref="VoyageSeats.PickVoyage"/>), as the seat picker does, and closes. A class is a label only
    /// (<see cref="VoyageClassesSO"/>); nothing here changes a vessel.
    /// </summary>
    public sealed class VoyageVesselsPage
    {
        // Frogma Rev 138 Vessels, in its own units (the 1920 x 1080 board).
        static readonly Vector2 Screen = new(1920f, 1080f);
        const float Margin = 40f, VoyageY = 176f, SeatLineY = 240f, StandY = 250f, StandHeight = 580f;
        const float MaxStandWidth = 500f, StandGap = 90f, PictureY = 10f, NameY = 320f, RuleY = 364f, LineY = 382f;
        const float FloorY = 500f, FloorHeight = 340f, HeadY = 800f, TabsY = 860f, TabsHeight = 56f;
        const float BarY = 990f, BarHeight = 56f, FitWidth = 180f, BackWidth = 180f, TakeWidth = 340f;
        const float DimAlpha = 0.4f;

        readonly VoyageBoard _board;
        readonly VoyageKit _kit;
        readonly VoyageSeats _rules;
        readonly VoyageClassesSO _classes;

        readonly RectTransform _root, _frame;
        RectTransform _overlay;
        Vector2 _fitFor = new(-1f, -1f);

        TMP_Text _voyageTitle, _wantedLabel, _seatLine, _head, _headLine, _none, _fitText;
        RectTransform _wantedRow, _fitRow;
        readonly List<(RectTransform root, VoyageGlyph glyph, TMP_Text name)> _wantedTags = new();
        readonly List<StandView> _stands = new();
        readonly List<TabView> _tabs = new();
        readonly Image[] _fitSegments = new Image[3];

        VoyageBoard.Offer _offer;
        string _key;
        int _seat;
        VoyageClass _class;
        VesselClassType _picked;
        readonly List<VoyageSeats.Option> _offered = new();
        readonly List<VoyageSeats.Option> _shown = new();

        sealed class StandView
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Button Button;
            public Image Picture, Rule;
            public TMP_Text Name, Line;
        }

        sealed class TabView
        {
            public VoyageClass Class;
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
            _classes = VoyageClassesSO.Load();

            _root = VoyageKit.Node("Vessels", parent);
            VoyageKit.Stretch(_root);
            // The whole screen, the page's flat ground (#00041F), taking every press: Your Ark stays behind it.
            var plate = _kit.RowPicked;
            VoyageKit.Block(_root, new Color(plate.r, plate.g, plate.b, 1f), hit: true);
            _frame = VoyageKit.Node("Frame", _root);
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(0.5f, 0.5f);
            _frame.sizeDelta = Screen;

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
            var floor = At("Floor", 0f, FloorY, Screen.x, FloorHeight);
            var g = floor.gameObject.AddComponent<VoyageGradient>();
            g.raycastTarget = false;
            var light = FloorLight;
            g.Set(VoyageKit.Fade(light, 0f), VoyageKit.Fade(light, 0.03f), VoyageKit.Fade(light, 0.06f), 0.5f);
            VoyageKit.Block(At("Edge", 0f, 0f, Screen.x, 1f, floor), VoyageKit.Fade(_kit.Outline, 0.4f));
        }

        void BuildTop()
        {
            // The voyage (18) at the left; "Wanted" (caps 15, Sub) and the classes it wants (15) at the right.
            _voyageTitle = _kit.Text(At("Voyage", Margin, VoyageY, 900f, 26f), 18, _kit.Light, TextAlignmentOptions.Left);
            VoyageKit.Stretch(_voyageTitle.rectTransform);
            _wantedRow = At("Wanted", Screen.x - Margin - 900f, VoyageY, 900f, 26f);
            var row = VoyageKit.Row(_wantedRow, 20f, TextAnchor.MiddleRight);
            row.childForceExpandHeight = true;
            _wantedLabel = _kit.Label(_wantedRow, 15, _kit.Sub);
            _wantedLabel.overflowMode = TextOverflowModes.Overflow;
            for (int i = 0; i < 3; i++)
            {
                var tag = VoyageKit.Node("Tag" + i, _wantedRow);
                VoyageKit.Row(tag, 8f).childForceExpandHeight = true;
                var glyph = Glyph(tag, 20f);
                var name = _kit.Text(tag, 15, _kit.Light, TextAlignmentOptions.Left);
                name.overflowMode = TextOverflowModes.Overflow;
                _wantedTags.Add((tag, glyph, name));
            }
            // The seat being chosen for (15, Sub).
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
            // Over the class row: "<Class>s are wanted on this voyage" (15, Light) and the class's line (15, Sub), centred.
            var head = At("Head", Margin, HeadY, Screen.x - 2f * Margin, 24f);
            VoyageKit.Row(head, 14f, TextAnchor.MiddleCenter).childForceExpandHeight = true;
            _head = _kit.Text(head, 15, _kit.Light, TextAlignmentOptions.Left);
            _head.overflowMode = TextOverflowModes.Overflow;
            _headLine = _kit.Text(head, 15, _kit.Sub, TextAlignmentOptions.Left);
            _headLine.overflowMode = TextOverflowModes.Overflow;
        }

        void BuildTabs()
        {
            // The seven classes along the foot, 8 apart, centred: a glyph (22) and the name (15); the open one Light and
            // underlined in cyan (2); the wanted ones Sub; the rest muted. LB / RB at the row's ends.
            var row = At("Classes", Margin, TabsY, Screen.x - 2f * Margin, TabsHeight);
            var h = VoyageKit.Row(row, 8f, TextAnchor.MiddleCenter);
            h.childForceExpandHeight = true;
            var prev = _kit.Label(row, 13, _kit.Sub, TextAlignmentOptions.Center);
            prev.overflowMode = TextOverflowModes.Overflow;
            VoyageKit.Set(prev, _classes ? _classes.PrevClassKey : null);
            if (_classes)
                foreach (var entry in _classes.Classes)
                {
                    if (entry == null) continue;
                    var c = entry.Class;
                    var tab = VoyageKit.Node("Class" + (int)c, row);
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
            VoyageKit.Set(next, _classes ? _classes.NextClassKey : null);
        }

        void BuildBar()
        {
            // The fit meter (three 10 high segments, 4 apart) and how the pick changes it (15) at the left; Back (B) and
            // Take the vessel (A) at the right.
            _fitRow = At("Fit", Margin, BarY, 900f, BarHeight);
            VoyageKit.Row(_fitRow, 24f, TextAnchor.MiddleLeft);
            var meter = VoyageKit.Node("Meter", _fitRow);
            var mle = VoyageKit.Le(meter);
            mle.preferredWidth = mle.minWidth = FitWidth;
            mle.preferredHeight = mle.minHeight = 10f;
            VoyageKit.Row(meter, 4f, TextAnchor.MiddleLeft, expandWidth: true).childForceExpandHeight = true;
            for (int i = 0; i < 3; i++)
            {
                var seg = VoyageKit.Node("Segment" + i, meter);
                VoyageKit.Le(seg).flexibleWidth = 1f;
                _fitSegments[i] = VoyageKit.Block(seg, _kit.Light);
            }
            _fitText = _kit.Text(_fitRow, 15, _kit.Light, TextAlignmentOptions.Left);
            _fitText.overflowMode = TextOverflowModes.Overflow;

            float takeX = Screen.x - Margin - TakeWidth;
            var back = At("Back", takeX - 24f - BackWidth, BarY, BackWidth, BarHeight);
            _kit.Quiet(back, _classes ? _classes.BackKey : null, _classes ? _classes.Back : null, Close);
            var take = At("Take", takeX, BarY, TakeWidth, BarHeight);
            _kit.Cta(take, _classes ? _classes.TakeKey : null, _classes ? _classes.Take : null, Take);
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
                var pic = VoyageKit.Node("Picture", view.Root);
                view.Picture = VoyageKit.Picture(pic);
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
            VoyageKit.At(view.Picture.rectTransform, 0f, PictureY, w, w * 9f / 16f);
            VoyageKit.At(view.Name.rectTransform.parent as RectTransform, 0f, NameY, w, 30f);
            VoyageKit.Stretch(view.Name.rectTransform);
            VoyageKit.At(view.Rule.rectTransform, 0f, RuleY, w, picked ? 2f : 1f);
            VoyageKit.At(view.Line.rectTransform.parent as RectTransform, 0f, LineY, w, 60f);
            VoyageKit.Stretch(view.Line.rectTransform);
        }

        // ── Opening ──────────────────────────────────────────────────────────

        /// <summary>Open on <paramref name="seat"/> of <paramref name="offer"/>'s seats (the record under
        /// <paramref name="key"/>), on the class of the vessel in it.</summary>
        public void Open(VoyageBoard.Offer offer, int seat, string key)
        {
            _offer = offer;
            _key = string.IsNullOrEmpty(key) ? offer?.Key : key;
            _seat = seat;
            var squad = VoyageSeatRecord.Get(_key);
            _picked = squad != null ? squad.GetSlot(seat).Vessel : default;
            _class = _classes ? _classes.ClassOf(_picked) : VoyageClass.None;
            if (_class == VoyageClass.None && _tabs.Count > 0) _class = _tabs[0].Class;
            Lift();
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            _fitFor = new Vector2(-1f, -1f);
            Tick(0f);
            Refresh();
        }

        public void Close()
        {
            _root.gameObject.SetActive(false);
            if (_overlay) _overlay.gameObject.SetActive(false);
        }

        /// <summary>The page covers the whole screen, so on first open it moves into its own canvas over the root
        /// canvas, as the seat picker does.</summary>
        void Lift()
        {
            if (!_overlay)
            {
                var canvas = _board ? _board.GetComponentInParent<Canvas>() : null;
                var rootCanvas = canvas ? canvas.rootCanvas : null;
                if (!rootCanvas) return;
                _overlay = VoyageKit.Node("VoyagesVessels", rootCanvas.transform);
                VoyageKit.Stretch(_overlay);
                var c = _overlay.gameObject.AddComponent<Canvas>();
                c.overrideSorting = true;
                c.sortingOrder = rootCanvas.sortingOrder + 600;
                _overlay.gameObject.AddComponent<GraphicRaycaster>();
                _root.SetParent(_overlay, false);
                VoyageKit.Stretch(_root);
            }
            _overlay.gameObject.SetActive(true);
        }

        /// <summary>Removes the page's canvas with the board.</summary>
        public void Dispose()
        {
            if (_overlay) Object.Destroy(_overlay.gameObject);
        }

        /// <summary>Scale the board's screen to the screen the page opens over; the shoulders step the class.</summary>
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
            float k = Mathf.Max(0.05f, Mathf.Min(room.x / Screen.x, room.y / Screen.y));
            _frame.localScale = new Vector3(k, k, 1f);
        }

        void StepClass(int step)
        {
            if (_tabs.Count == 0) return;
            int at = 0;
            for (int i = 0; i < _tabs.Count; i++)
                if (_tabs[i].Class == _class) at = i;
            ShowClass(_tabs[(at + step + _tabs.Count) % _tabs.Count].Class);
        }

        void ShowClass(VoyageClass c)
        {
            _class = c;
            // The seat's own vessel stays picked when it is of this class; else the first this seat can take.
            var squad = VoyageSeatRecord.Get(_key);
            var current = squad != null ? squad.GetSlot(_seat).Vessel : default;
            if (_classes && _classes.ClassOf(current) == c) _picked = current;
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

        /// <summary>The open class's vessels this seat is offered, in the class's own order.</summary>
        void Gather(SavedSquad squad)
        {
            _shown.Clear();
            _rules.Offer(VoyageSeats.Vessel, squad, _seat, _offered);
            var entry = _classes ? _classes.EntryOf(_class) : null;
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
            if (squad == null || !VoyageSeats.CanEdit(squad, _seat) || !_classes)
            {
                Close();
                return;
            }
            bool you = VoyageOpenCard.IsMySeat(squad, _seat);
            string job = VoyageClassesSO.JobOf(_key);

            VoyageKit.Set(_voyageTitle, _offer?.Title);
            DrawWanted(job);
            VoyageKit.Set(_seatLine, TemplateText.Fill(_classes.SeatLine, "seat", _rules.Role(_seat),
                "who", you ? _classes.WhoYou : _classes.WhoAi));

            Gather(squad);
            var entry = _classes.EntryOf(_class);
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
                VoyageKit.Show(view.Picture, vessel && vessel.PreviewImage ? vessel.PreviewImage : o.Picture);
                VoyageKit.Set(view.Name, o.Name);
                view.Rule.color = picked ? _kit.Cyan : VoyageKit.Fade(_kit.Outline, 0.6f);
                VoyageKit.Set(view.Line, !can
                    ? _classes.NeedsPerson
                    : ArkEscortRoles.HasRole(o.Vessel) && !string.IsNullOrEmpty(_classes.AiCanFly)
                        ? Join(entry?.Line, _classes.AiCanFly)
                        : entry?.Line);
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
            VoyageKit.Set(_none, n == 0 ? TemplateText.Fill(_classes.NoneOwned, "class", entry?.Name) : null);

            bool wanted = _classes.Wanted(job, _class);
            VoyageKit.Set(_head, wanted ? TemplateText.Fill(_classes.WantedLine, "class", entry?.Name) : null);
            _head.gameObject.SetActive(wanted);
            VoyageKit.Set(_headLine, entry?.Line);

            foreach (var tab in _tabs)
            {
                bool on = tab.Class == _class;
                var col = on ? _kit.Light : _classes.Wanted(job, tab.Class) ? _kit.Sub : VoyageKit.Fade(_kit.Sub, 0.5f);
                tab.Name.color = col;
                tab.Glyph.color = col;
                tab.Bar.enabled = on;
            }
            DrawFit(squad, job);
        }

        static string Join(string a, string b) =>
            string.IsNullOrEmpty(a) ? b : string.IsNullOrEmpty(b) ? a : a + " " + b;

        void DrawWanted(string job)
        {
            var wants = _classes.WantedBy(job);
            _wantedLabel.gameObject.SetActive(wants.Count > 0);
            VoyageKit.Set(_wantedLabel, _classes.WantedLabel);
            for (int i = 0; i < _wantedTags.Count; i++)
            {
                var (root, glyph, name) = _wantedTags[i];
                bool on = i < wants.Count;
                root.gameObject.SetActive(on);
                if (!on) continue;
                glyph.Form = VoyageGlyph.Of(wants[i]);
                glyph.color = _kit.Light;
                VoyageKit.Set(name, _classes.NameOf(wants[i]));
            }
        }

        readonly List<VesselClassType> _fitScratch = new();

        // The squad fit now and with the pick in this seat; nothing for a voyage that wants no class (the Crystal Run).
        void DrawFit(SavedSquad squad, string job)
        {
            bool shown = _classes.WantedBy(job).Count > 0;
            _fitRow.gameObject.SetActive(shown);
            if (!shown) return;
            _fitScratch.Clear();
            for (int s = 0; s < SavedSquad.SlotCount; s++) _fitScratch.Add(squad.GetSlot(s).Vessel);
            int from = _classes.Fit(job, _fitScratch);
            if (_picked != default) _fitScratch[_seat] = _picked;
            int to = _classes.Fit(job, _fitScratch);
            for (int i = 0; i < _fitSegments.Length; i++)
                _fitSegments[i].color = i < to ? _kit.Light : VoyageKit.Fade(_kit.Outline, 0.6f);
            VoyageKit.Set(_fitText, to == from
                ? TemplateText.Fill(_classes.FitSame, "fit", to.ToString(System.Globalization.CultureInfo.InvariantCulture))
                : TemplateText.Fill(_classes.FitChange, "from", from.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "to", to.ToString(System.Globalization.CultureInfo.InvariantCulture)));
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

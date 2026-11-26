using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Pilots, who flies a seat's vessel (Frogma Rev 138 Pilots; Docs/MENU_REDESIGN_B.md section 231). It opens in the
    /// Voyages page slot when a seat's pilot is pressed on Your Ark (or Change wingmate), under the menu's bars:
    /// <list type="bullet">
    /// <item>the seat and the voyage over "Who flies the &lt;vessel&gt;";</item>
    /// <item>the crew who can fly the hull, one row each: face, name, faction and why, and their mood for this job; the
    /// picked row on the row ground with one cyan line down its left edge; the list scrolls past six;</item>
    /// <item>the picked pilot at the right: their picture, name, faction, what they say of this job and their mood;</item>
    /// <item>Take the seat (A), the one Lime, and Back (B).</item>
    /// </list>
    /// A press on a row picks it on the page; Take the seat saves it to the seat for THIS voyage
    /// (<see cref="VoyageSeats.PickVoyage"/>), as the seat picker does, and closes.
    /// </summary>
    public sealed class VoyagePilotsPage
    {
        // Frogma Rev 138 Pilots, in the page's units: x 40-1880, y 176-1046 of the 1920 x 1080 board, as Your Ark.
        static readonly Vector2 Screen = new(1840f, 870f);
        const float ListX = 80f, KickerY = 0f, HeadingY = 24f, ListY = 70f, RowWidth = 640f, RowHeight = 84f, RowStep = 96f;
        const int RowsShown = 6;
        const float SceneX = 1060f, SceneY = 4f, SceneWidth = 640f, SceneHeight = 760f;
        const float BarY = 660f, BarHeight = 56f, TakeWidth = 255f, BackWidth = 180f;

        readonly VoyageKit _kit;
        readonly VoyageSeats _rules;
        readonly VoyageQuaySO _words = VoyageQuaySO.Load();
        readonly VoyageArchetypesSO _archetypes = VoyageArchetypesSO.Load();
        readonly RectTransform _root, _frame, _content;
        readonly ScrollRect _scroll;
        readonly List<RowView> _rows = new();
        readonly List<VoyageSeats.Option> _offered = new();
        Vector2 _fitFor = new(-1f, -1f);

        TMP_Text _kicker, _heading, _sceneName, _sceneFaction, _sceneQuote, _sceneMood;
        Image _scenePicture;
        VoyageMoodMark _sceneMark;

        VoyageBoard.Offer _offer;
        string _key;
        int _seat;
        string _picked;

        sealed class RowView
        {
            public RectTransform Root;
            public Image Ground, Edge, Face;
            public TMP_Text Name, Meta, Mood;
            public VoyageMoodMark Mark;
            public Button Button;
        }

        public bool IsOpen => _root.gameObject.activeSelf;

        public VoyagePilotsPage(VoyageKit kit, VoyageSeats rules, RectTransform pages)
        {
            _kit = kit;
            _rules = rules;
            _root = VoyageKit.Node("Pilots", pages);
            VoyageKit.Stretch(_root);
            // The page's slot on the page's flat ground, taking every press: Your Ark stays behind it (as Vessels).
            var plate = _kit.RowPicked;
            VoyageKit.Block(_root, new Color(plate.r, plate.g, plate.b, 1f), hit: true);
            _frame = VoyageKit.Node("Frame", _root);
            VoyageKit.Fit(_frame, Screen);

            // The seat and the voyage (caps 14, Sub) over "Who flies the Dolphin" (26).
            _kicker = _kit.Label(At("Kicker", _frame, ListX, KickerY, RowWidth, 20f), 14, _kit.Sub);
            VoyageKit.Stretch(_kicker.rectTransform);
            _kicker.overflowMode = TextOverflowModes.Overflow;
            _heading = Text(_frame, "Heading", ListX, HeadingY, RowWidth, 34f, 26f, _kit.Light);

            // The list: six rows in view, scrolling past them.
            var view = At("List", _frame, ListX, ListY, RowWidth, RowsShown * RowStep - (RowStep - RowHeight));
            view.gameObject.AddComponent<RectMask2D>();
            VoyageKit.Block(view, VoyageKit.Clear, hit: true);
            _content = At("Rows", view, 0f, 0f, RowWidth, RowStep);
            _scroll = view.gameObject.AddComponent<ScrollRect>();
            _scroll.content = _content;
            _scroll.viewport = view;
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 24f;

            BuildScene();

            var take = At("Take", _frame, ListX, BarY, TakeWidth, BarHeight);
            _kit.Cta(take, _words ? _words.TakeSeatKey : null, _words ? _words.TakeSeat : null, Take);
            var back = At("Back", _frame, ListX + TakeWidth + 24f, BarY, BackWidth, BarHeight);
            _kit.Quiet(back, _archetypes ? _archetypes.BackKey : null, _archetypes ? _archetypes.Back : null, Close);

            _root.gameObject.SetActive(false);
        }

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
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        static VoyageMoodMark Mark(RectTransform parent, float x, float y)
        {
            var m = At("Mark", parent, x, y, 16f, 16f).gameObject.AddComponent<VoyageMoodMark>();
            m.raycastTarget = false;
            return m;
        }

        void BuildScene()
        {
            // The picked pilot (Rev 138 Pilots, right): their picture over the panel, then their name (26), faction (16,
            // Sub), what they say of this job (20) and their mood (16), on a fade at the panel's foot.
            var panel = At("Scene", _frame, SceneX, SceneY, SceneWidth, SceneHeight);
            _kit.Box(panel, VoyageKit.Fade(_kit.Popup, 0.6f), VoyageKit.Fade(_kit.Outline, 0.6f));
            _scenePicture = VoyageKit.CoverBox(At("Picture", panel, 1f, 1f, SceneWidth - 2f, SceneHeight - 2f));
            var fade = At("Fade", panel, 1f, SceneHeight - 280f, SceneWidth - 2f, 279f).gameObject.AddComponent<VoyageGradient>();
            fade.raycastTarget = false;
            fade.Set(VoyageKit.Fade(_kit.Popup, 0f), VoyageKit.Fade(_kit.Popup, 0.75f), VoyageKit.Fade(_kit.Popup, 0.95f), 0.4f);
            _sceneName = Text(panel, "Name", 30f, 552f, SceneWidth - 60f, 34f, 26f, _kit.Light);
            _sceneFaction = Text(panel, "Faction", 30f, 590f, SceneWidth - 60f, 22f, 16f, _kit.Sub);
            _sceneQuote = Text(panel, "Quote", 30f, 620f, SceneWidth - 60f, 52f, 20f, _kit.Light, TextAlignmentOptions.TopLeft);
            _sceneQuote.textWrappingMode = TextWrappingModes.Normal;
            _sceneMark = Mark(panel, 30f, 680f);
            _sceneMood = Text(panel, "Mood", 54f, 677f, SceneWidth - 84f, 22f, 16f, _kit.Light);
        }

        RowView RowFor(int index)
        {
            while (_rows.Count <= index)
            {
                var row = new RowView { Root = At("Pilot" + _rows.Count, _content, 0f, _rows.Count * RowStep, RowWidth, RowHeight) };
                row.Ground = VoyageKit.Block(row.Root, _kit.RowPicked, hit: true);
                row.Edge = VoyageKit.Block(At("Edge", row.Root, 0f, 0f, 2f, RowHeight), _kit.Cyan);
                row.Button = row.Root.gameObject.AddComponent<Button>();
                row.Button.targetGraphic = row.Ground;
                row.Button.transition = Selectable.Transition.None;
                row.Face = VoyageKit.CoverBox(At("Face", row.Root, 16f, 11f, 52f, 62f));
                row.Name = Text(row.Root, "Name", 84f, 16f, 250f, 26f, 19f, _kit.Light);
                row.Meta = Text(row.Root, "Meta", 84f, 44f, 250f, 34f, 14f, _kit.Sub, TextAlignmentOptions.TopLeft);
                row.Meta.textWrappingMode = TextWrappingModes.Normal;
                row.Mark = Mark(row.Root, 348f, 34f);
                row.Mood = Text(row.Root, "Mood", 374f, 31f, 240f, 22f, 16f, _kit.Light);
                _rows.Add(row);
            }
            return _rows[index];
        }

        // ── Opening ──────────────────────────────────────────────────────────

        /// <summary>Open on <paramref name="seat"/> of <paramref name="offer"/>'s seats (the record under
        /// <paramref name="key"/>), on the pilot in it.</summary>
        public void Open(VoyageBoard.Offer offer, int seat, string key)
        {
            _offer = offer;
            _key = string.IsNullOrEmpty(key) ? offer?.Key : key;
            _seat = seat;
            _picked = null;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            _fitFor = new Vector2(-1f, -1f);
            _scroll.verticalNormalizedPosition = 1f;
            Tick(0f);
            Refresh();
        }

        public void Close() => _root.gameObject.SetActive(false);

        public void Tick(float dt)
        {
            if (!IsOpen) return;
            var room = _root.rect.size;
            if (room == _fitFor) return;
            _fitFor = room;
            VoyageKit.Fit(_frame, Screen);
        }

        public void Refresh()
        {
            if (!IsOpen) return;
            var squad = VoyageSeatRecord.Get(_key);
            if (squad == null || !VoyageSeats.CanEdit(squad, _seat) || !_words)
            {
                Close();
                return;
            }
            var slot = squad.GetSlot(_seat);
            var vessel = _rules.VesselOf(slot.Vessel);
            _rules.Offer(VoyageSeats.Pilot, squad, _seat, _offered);
            // The pilot in the seat heads the list.
            int seated = _offered.FindIndex(o => o.Picked);
            if (seated > 0)
            {
                var first = _offered[seated];
                _offered.RemoveAt(seated);
                _offered.Insert(0, first);
            }
            if (string.IsNullOrEmpty(_picked))
                foreach (var o in _offered)
                    if (o.Picked) _picked = o.Id;
            if (string.IsNullOrEmpty(_picked) && _offered.Count > 0) _picked = _offered[0].Id;

            VoyageKit.Set(_kicker, TemplateText.Fill(_words.PilotsKicker, "seat", _rules.Role(_seat), "voyage", _offer?.Title));
            VoyageKit.Set(_heading, TemplateText.Fill(_words.PilotsHeading, "vessel", vessel ? vessel.CardName : null));

            var info = VoyageQuay.TypeOf(_offer);
            var refuse = ColorTrace.Retrace(ColorTrace.Ui, VoyageHudColors.ThreatAuthored);
            VoyageSeats.Option picked = default;
            for (int i = 0; i < _offered.Count; i++)
            {
                var o = _offered[i];
                var row = RowFor(i);
                row.Root.gameObject.SetActive(true);
                bool on = o.Id == _picked;
                if (on) picked = o;
                row.Ground.color = on ? _kit.RowPicked : VoyageKit.Fade(_kit.RowPicked, 0.45f);
                row.Edge.enabled = on;
                VoyageKit.ShowCover(row.Face, o.Picture, new Vector2(52f, 62f), VoyageKit.FaceFocus);
                VoyageKit.Set(row.Name, o.Name);
                var faction = VoyageQuay.FactionOf(o.Id);
                var mood = info != null ? VoyageMood.Of(info, faction) : PilotMood.None;
                VoyageKit.Set(row.Meta, mood == PilotMood.None
                    ? FactionCatalog.TypeName(faction)
                    : TemplateText.Fill(_words.PilotsRowMeta, "faction", FactionCatalog.TypeName(faction), "why", _words.Why(mood)));
                bool moody = mood != PilotMood.None;
                row.Mark.gameObject.SetActive(moody);
                VoyageKit.Set(row.Mood, moody ? _words.MoodName(mood) : null);
                if (moody)
                {
                    row.Mark.Set(mood, _kit.Light, VoyageKit.Fade(_kit.Outline, 0.8f), refuse);
                    row.Mood.color = mood == PilotMood.Refuses ? refuse : _kit.Light;
                }
                // A pilot who refuses the job shows muted (Set sail waits on them, as on Your Ark).
                row.Name.color = mood == PilotMood.Refuses ? _kit.Sub : _kit.Light;
                string id = o.Id;
                row.Button.onClick.RemoveAllListeners();
                row.Button.onClick.AddListener(() =>
                {
                    _picked = id;
                    Refresh();
                });
            }
            for (int i = _offered.Count; i < _rows.Count; i++) _rows[i].Root.gameObject.SetActive(false);
            _content.sizeDelta = new Vector2(RowWidth, Mathf.Max(1, _offered.Count) * RowStep);
            DrawScene(picked, info, refuse);
        }

        void DrawScene(VoyageSeats.Option o, VoyageTypeInfo info, Color refuse)
        {
            VoyageKit.ShowCover(_scenePicture, o.Picture, new Vector2(SceneWidth - 2f, SceneHeight - 2f), VoyageKit.FaceFocus);
            VoyageKit.Set(_sceneName, o.Name);
            var faction = VoyageQuay.FactionOf(o.Id);
            VoyageKit.Set(_sceneFaction, string.IsNullOrEmpty(o.Id) ? null : FactionCatalog.TypeName(faction));
            var mood = info != null && !string.IsNullOrEmpty(o.Id) ? VoyageMood.Of(info, faction) : PilotMood.None;
            bool moody = mood != PilotMood.None;
            var said = mood == PilotMood.Refuses ? refuse : _kit.Light;
            VoyageKit.Set(_sceneQuote, moody ? TemplateText.Fill(_words.CrewQuote, "say", _words.Say(mood)) : null);
            _sceneQuote.color = said;
            _sceneMark.gameObject.SetActive(moody);
            if (moody) _sceneMark.Set(mood, _kit.Light, VoyageKit.Fade(_kit.Outline, 0.8f), refuse);
            VoyageKit.Set(_sceneMood, moody ? _words.MoodName(mood) : null);
            _sceneMood.color = said;
        }

        // Take the seat: the picked pilot flies the seat for this voyage, and Your Ark shows it.
        void Take()
        {
            var squad = VoyageSeatRecord.Get(_key);
            if (squad != null && !string.IsNullOrEmpty(_picked))
                foreach (var o in _offered)
                    if (o.Id == _picked && !o.Picked)
                    {
                        _rules.PickVoyage(_key, _seat, VoyageSeats.Pilot, o);
                        break;
                    }
            Close();
        }
    }
}

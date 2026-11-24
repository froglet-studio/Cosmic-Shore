using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The WHAT'S NEW window (Frogma Rev 121 C, the changelog; picked by Shombith on 2026-11-21: "I like C build C";
    /// Docs/MENU_REDESIGN_B.md section 216, spec Docs/UI/specs/rev121-c-whats-new.md). One window over the menu: every
    /// update listed on the left, newest first with its "New" tag; the picked one on the right, its picture, version and
    /// date, name and line, up to three features side by side, and its smaller changes. Close at the foot, and on the
    /// newest update its own button (Set sail opens the Voyage window).
    ///
    /// <para><b>Opened</b> from Settings (<see cref="GameSettingsPanelController"/>'s What's new button) and once by
    /// itself after an update (<see cref="ScreenSwitcher"/>, <see cref="WhatsNewRules.ShouldOpenByItself"/>). Opening it
    /// either way records the newest update as seen.</para>
    ///
    /// <para><b>Built in code</b> with the Voyage window's kit (<see cref="VoyageKit"/>: its faces, plates and buttons;
    /// every colour through the worn colour set), by <see cref="ScreenSwitcher"/> at start (EnsureWhatsNewModal), as the
    /// Quests window is. Every word is a <see cref="WhatsNewSO"/> field.</para>
    /// </summary>
    public class WhatsNewModal : ModalWindowManager
    {
        // The board's measures (Rev 121 C, at 1920 x 1080).
        static readonly Vector2 Design = new(1920f, 1080f);
        const float WinX = 160f, WinY = 150f, WinW = 1600f, WinH = 860f;
        const float ListW = 420f, ListTop = 150f, RowH = 80f, RowGap = 6f;
        const float Pad = 48f, HeroH = 260f;

        VoyageKit _kit;
        WhatsNewSO _data;
        bool _built;
        int _picked;

        RectTransform _frame, _list;
        TMP_Text _eyebrow, _title;
        readonly List<Row> _rows = new();

        RawImage _seaArt;
        Image _art;
        TMP_Text _version, _name, _line, _featuresHeading, _alsoHeading;
        readonly List<(GameObject root, TMP_Text title, TMP_Text line)> _features = new();
        RectTransform _alsoColumn;
        readonly List<TMP_Text> _also = new();
        Button _action;
        TMP_Text _actionWords;
        Button _close;

        sealed class Row
        {
            public RectTransform Root;
            public Button Button;
            public Image Picked, Edge;
            public TMP_Text Version, Tag, Name;
        }

        /// <summary>The kit and the words the window is built with. Called by <see cref="ScreenSwitcher"/> before the
        /// window's first frame.</summary>
        public void Setup(VoyageKit kit, WhatsNewSO data)
        {
            _kit = kit;
            _data = data;
        }

        protected override void Start()
        {
            base.Start();
            Build();
            OnModalOpened += Opened;
            ColorTrace.Changed += Redraw;
        }

        void OnDestroy()
        {
            OnModalOpened -= Opened;
            ColorTrace.Changed -= Redraw;
        }

        void OnRectTransformDimensionsChange()
        {
            if (_frame) VoyageKit.Fit(_frame, Design);
        }

        // Every visit opens on the newest update, and records it as seen.
        void Opened()
        {
            Build();
            if (_frame) VoyageKit.Fit(_frame, Design);
            MarkSeen(_data);
            Pick(0);
            if (_rows.Count > 0 && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(_rows[0].Button.gameObject);
        }

        /// <summary>Records the newest update as shown, so the window does not open by itself for it again.</summary>
        public static void MarkSeen(WhatsNewSO data)
        {
            var id = data && data.Newest != null ? data.Newest.Id : null;
            if (string.IsNullOrWhiteSpace(id)) return;
            PlayerPrefs.SetString(WhatsNewRules.SeenPrefKey, id);
            PlayerPrefs.Save();
        }

        // ── Building ───────────────────────────────────────────────────────

        void Build()
        {
            if (_built || _kit == null || !_data) return;
            _built = true;
            var root = (RectTransform)transform;

            // The dim over the menu; it takes the presses meant for the screens behind.
            var dim = VoyageKit.Node("Dim", root);
            VoyageKit.Stretch(dim);
            VoyageKit.Block(dim, VoyageKit.Fade(_kit.Popup, 0.74f), hit: true);

            _frame = VoyageKit.Node("Frame", root);
            VoyageKit.Fit(_frame, Design);

            var win = VoyageKit.Node("Window", _frame);
            VoyageKit.At(win, WinX, WinY, WinW, WinH);
            var plate = VoyageKit.Node("Plate", win);
            VoyageKit.Stretch(plate);
            VoyageKit.Sliced(plate, _kit.Style.Sliver10Fill, VoyageKit.Fade(_kit.Popup, 0.96f)).raycastTarget = true;
            var edge = VoyageKit.Node("Edge", win);
            VoyageKit.Stretch(edge);
            VoyageKit.Sliced(edge, _kit.Style.Sliver10Line, _kit.Outline);

            BuildHeader(win);
            BuildList(win);
            BuildDetail(win);
            BuildFoot(win);
            Redraw();
        }

        void BuildHeader(RectTransform win)
        {
            var head = VoyageKit.Node("Header", win);
            VoyageKit.At(head, Pad, 44f, ListW - Pad, 80f);
            VoyageKit.Column(head, 6f);
            _eyebrow = _kit.Label(head, 14, _kit.Sub);
            _title = _kit.Text(head, 36, _kit.Light, TextAlignmentOptions.Left);
        }

        void BuildList(RectTransform win)
        {
            _list = VoyageKit.Node("Updates", win);
            VoyageKit.At(_list, 32f, ListTop, ListW - 32f, WinH - ListTop - 40f);
            var rule = VoyageKit.Node("Rule", win);
            VoyageKit.At(rule, ListW, 40f, 1f, WinH - 80f);
            VoyageKit.Block(rule, _kit.Outline);

            int count = _data.Updates.Count;
            for (int i = 0; i < count; i++) _rows.Add(MakeRow(i));

            // The pad walks the list up and down; right goes to the window's buttons.
            for (int i = 0; i < _rows.Count; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnUp = i > 0 ? _rows[i - 1].Button : null;
                nav.selectOnDown = i < _rows.Count - 1 ? _rows[i + 1].Button : null;
                _rows[i].Button.navigation = nav;
            }
        }

        Row MakeRow(int index)
        {
            var r = new Row();
            r.Root = VoyageKit.Node("Update" + (index + 1), _list);
            VoyageKit.At(r.Root, 0f, index * (RowH + RowGap), ListW - 32f, RowH);

            // The picked row: a faint band fading to the right, and the selection line on its left edge.
            var band = VoyageKit.Node("Picked", r.Root);
            VoyageKit.Stretch(band);
            r.Picked = VoyageKit.Block(band, VoyageKit.Fade(_kit.RowPicked, 1f), hit: true);
            var line = VoyageKit.Node("Line", r.Root);
            line.anchorMin = Vector2.zero;
            line.anchorMax = new Vector2(0f, 1f);
            line.pivot = new Vector2(0f, 0.5f);
            line.sizeDelta = new Vector2(2f, 0f);
            line.anchoredPosition = Vector2.zero;
            r.Edge = VoyageKit.Block(line, _kit.Cyan);

            var words = VoyageKit.Node("Words", r.Root);
            VoyageKit.Stretch(words);
            words.offsetMin = new Vector2(20f, 14f);
            words.offsetMax = new Vector2(-20f, -14f);
            VoyageKit.Column(words, 4f, TextAnchor.MiddleLeft);
            var top = VoyageKit.Node("Top", words);
            VoyageKit.Row(top, 12f);
            r.Version = _kit.Label(top, 14, _kit.Sub);
            r.Tag = _kit.Label(top, 14, _kit.Light);
            r.Name = _kit.Text(words, 20, _kit.Light, TextAlignmentOptions.Left);

            r.Button = r.Root.gameObject.AddComponent<Button>();
            r.Button.targetGraphic = r.Picked;
            r.Button.transition = Selectable.Transition.None;
            int at = index;
            r.Button.onClick.AddListener(() => Pick(at));
            // Moving the pad onto a row shows it, as a press does.
            var hover = r.Root.gameObject.AddComponent<WhatsNewRowFocus>();
            hover.Picked = () => Pick(at);
            return r;
        }

        void BuildDetail(RectTransform win)
        {
            float x = ListW + Pad, w = WinW - x - Pad;

            // The picture, its foot fading into the window.
            var hero = VoyageKit.Node("Picture", win);
            VoyageKit.At(hero, x, 44f, w, HeroH);
            var mask = hero.gameObject.AddComponent<RectMask2D>();
            mask.softness = new Vector2Int(0, 120);
            var sea = VoyageKit.Node("Sea", hero);
            VoyageKit.Stretch(sea);
            _seaArt = sea.gameObject.AddComponent<RawImage>();
            _seaArt.texture = VoyageGalaxyArt.Texture();
            _seaArt.raycastTarget = false;
            var fit = sea.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = VoyageGalaxyArt.Aspect;
            var art = VoyageKit.Node("Art", hero);
            VoyageKit.Stretch(art);
            _art = VoyageKit.Picture(art);

            var col = VoyageKit.Node("Detail", win);
            VoyageKit.At(col, x, 250f, w, WinH - 250f - 120f);
            VoyageKit.Column(col, 0f);
            _version = _kit.Label(col, 14, _kit.Sub);
            Gap(col, 8f);
            _name = _kit.Text(col, 44, _kit.Light, TextAlignmentOptions.Left);
            Gap(col, 12f);
            _line = _kit.Text(col, 18, _kit.Light, TextAlignmentOptions.Left);
            Gap(col, 34f);
            _featuresHeading = _kit.Label(col, 14, _kit.Sub);
            Gap(col, 14f);

            var features = VoyageKit.Node("Features", col);
            VoyageKit.Le(features).preferredHeight = 110f;
            var grid = VoyageKit.Row(features, 36f, TextAnchor.UpperLeft, expandWidth: true);
            grid.childControlHeight = true;
            for (int i = 0; i < 3; i++)
            {
                var f = VoyageKit.Node("Feature" + (i + 1), features);
                VoyageKit.Le(f).flexibleWidth = 1f;
                VoyageKit.Column(f, 8f);
                VoyageKit.Hairline(f, true, _kit.Outline, 0f);
                Gap(f, 6f);
                var t = _kit.Text(f, 20, _kit.Light, TextAlignmentOptions.Left);
                var l = _kit.Text(f, 15, _kit.Sub, TextAlignmentOptions.TopLeft);
                l.textWrappingMode = TextWrappingModes.Normal;
                l.overflowMode = TextOverflowModes.Overflow;
                _features.Add((f.gameObject, t, l));
            }

            Gap(col, 34f);
            _alsoHeading = _kit.Label(col, 14, _kit.Sub);
            Gap(col, 12f);
            _alsoColumn = VoyageKit.Node("Also", col);
            VoyageKit.Column(_alsoColumn, 8f);
        }

        static void Gap(RectTransform parent, float height) => VoyageKit.Le(VoyageKit.Node("Gap", parent)).preferredHeight = height;

        void BuildFoot(RectTransform win)
        {
            var foot = VoyageKit.Node("Buttons", win);
            foot.anchorMin = foot.anchorMax = foot.pivot = new Vector2(1f, 0f);
            foot.sizeDelta = new Vector2(480f, 56f);
            foot.anchoredPosition = new Vector2(-Pad, 40f);

            var close = VoyageKit.Node("Close", foot);
            close.anchorMin = close.anchorMax = close.pivot = new Vector2(0f, 0.5f);
            close.sizeDelta = new Vector2(200f, 56f);
            close.anchoredPosition = Vector2.zero;
            _close = _kit.Quiet(close, _data.CloseKey, _data.CloseLabel, ModalWindowOut);

            var act = VoyageKit.Node("Action", foot);
            act.anchorMin = act.anchorMax = act.pivot = new Vector2(1f, 0.5f);
            act.sizeDelta = new Vector2(260f, 56f);
            act.anchoredPosition = Vector2.zero;
            (_action, _actionWords) = _kit.Cta(act, _data.ButtonKey, null, Act);
        }

        // ── Drawing ────────────────────────────────────────────────────────

        void Pick(int index)
        {
            if (!_data || _data.Updates.Count == 0) return;
            _picked = Mathf.Clamp(index, 0, _data.Updates.Count - 1);
            Redraw();
        }

        public void Redraw()
        {
            if (!_built || _kit == null || !_data) return;
            VoyageKit.Set(_eyebrow, _data.Eyebrow);
            VoyageKit.Set(_title, _data.Title);

            for (int i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                var u = _data.Updates[i];
                bool on = i == _picked;
                r.Picked.color = on ? _kit.RowPicked : VoyageKit.Clear;
                r.Edge.enabled = on;
                r.Version.color = on ? _kit.Light : _kit.Sub;
                VoyageKit.Set(r.Version, u?.Version);
                VoyageKit.Set(r.Tag, i == 0 ? _data.NewTag : null);
                r.Tag.gameObject.SetActive(i == 0 && !string.IsNullOrEmpty(_data.NewTag));
                VoyageKit.Set(r.Name, u?.Name);
                r.Name.color = on ? _kit.Light : _kit.Sub;
            }

            var e = _data.Updates.Count > 0 ? _data.Updates[_picked] : null;
            _seaArt.color = VoyageKit.Fade(_kit.Light, e != null && e.Art ? 0f : 0.6f);
            VoyageKit.Show(_art, e?.Art);
            VoyageKit.Set(_version, e == null ? null : TemplateText.Fill(_data.VersionLine, "version", e.Version, "date", e.Date));
            VoyageKit.Set(_name, e?.Name);
            VoyageKit.Set(_line, e?.Line);

            int fc = e?.Features?.Count ?? 0;
            VoyageKit.Set(_featuresHeading, fc > 0 ? _data.FeaturesHeading : null);
            for (int i = 0; i < _features.Count; i++)
            {
                var f = i < fc ? e.Features[i] : null;
                _features[i].root.SetActive(f != null);
                VoyageKit.Set(_features[i].title, f?.Title);
                VoyageKit.Set(_features[i].line, f?.Line);
            }

            int ac = e?.Also?.Count ?? 0;
            VoyageKit.Set(_alsoHeading, ac > 0 ? _data.AlsoHeading : null);
            while (_also.Count < ac)
            {
                var t = _kit.Text(_alsoColumn, 15, _kit.Sub, TextAlignmentOptions.Left);
                _also.Add(t);
            }
            for (int i = 0; i < _also.Count; i++)
            {
                _also[i].gameObject.SetActive(i < ac);
                VoyageKit.Set(_also[i], i < ac ? e.Also[i] : null);
            }

            // The entry's own button, on the newest update only.
            bool act = _picked == 0 && e != null && e.Button != WhatsNewSO.Action.None && !string.IsNullOrEmpty(e.ButtonLabel);
            _action.gameObject.SetActive(act);
            VoyageKit.Set(_actionWords, act ? e.ButtonLabel : null);

            var closeNav = new Navigation { mode = Navigation.Mode.Explicit };
            closeNav.selectOnRight = act ? _action : null;
            closeNav.selectOnLeft = _rows.Count > 0 ? _rows[_picked].Button : null;
            _close.navigation = closeNav;
            var actNav = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = _close };
            _action.navigation = actNav;
            for (int i = 0; i < _rows.Count; i++)
            {
                var nav = _rows[i].Button.navigation;
                nav.selectOnRight = act ? _action : _close;
                _rows[i].Button.navigation = nav;
            }
        }

        void Act()
        {
            var e = _data ? _data.Newest : null;
            if (e == null) return;
            switch (e.Button)
            {
                case WhatsNewSO.Action.OpenVoyages:
                    var switcher = Switcher;
                    ModalWindowOut();
                    if (switcher) switcher.OpenModal(ScreenSwitcher.ModalWindows.MISSION);
                    break;
            }
        }
    }
}

using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Crew recommends, as a popup over the HyperSea page (Frogma Rev 113 Crew, picked by Shombith 2026-11-16;
    /// Docs/MENU_REDESIGN_B.md section 209.1). The factions your crew has work for run down the left with their counts
    /// ("All" first), the voyages that sail now stand on top with Open, and every other crew voyage sits under its
    /// faction, showing its pilot, its job and its difficulty, locked for Milestone 1: pressing one says it opens after
    /// Milestone 1 (<see cref="VoyageLines.CrewLockedPress"/>). Grouping is <see cref="VoyageCrewRules"/>'s.
    /// <para>Drawn in the board's own units: a 1520 x 900 panel at (200, 124) of the 1920 x 1080 screen, in its own canvas
    /// over the window, as the seat popup is.</para>
    /// </summary>
    public sealed class VoyageCrewPopup
    {
        static readonly Vector2 Screen = new(1920f, 1080f);
        const float Width = 1520f, Height = 900f, PanelX = 200f, PanelY = 124f;
        const float RailX = 40f, RailY = 130f, RailWidth = 280f, RailRow = 54f;
        const float RightX = 360f, RightWidth = 1120f, OpenHeight = 176f, OpenGap = 18f, RowHeight = 100f, RowGap = 14f;
        const float OpenSeconds = 0.45f, DimSeconds = 0.35f;

        readonly VoyageBoard _board;
        readonly VoyageKit _kit;
        readonly VoyageLines _lines;
        readonly RectTransform _root, _frame, _panel;
        RectTransform _overlay, _rail, _openArea, _crewHead, _scroll, _list;
        TMP_Text _railNote, _message;
        Image _crewLock;
        CanvasGroup _rootGroup, _frameGroup;
        Vector2 _fitFor = new(-1f, -1f);
        float _openT = 1f, _dimT = 1f, _fitK = 1f;

        readonly List<VoyageBoard.Offer> _offers = new();
        readonly List<int> _open = new();
        readonly List<VoyageCrewRules.Group> _groups = new();
        readonly List<RailView> _railViews = new();
        readonly List<OpenView> _openViews = new();
        readonly List<GroupView> _groupViews = new();
        FactionType? _house;

        sealed class RailView
        {
            public RectTransform Root;
            public Image Ground, Edge;
            public TMP_Text Name, Count;
        }

        sealed class OpenView
        {
            public RectTransform Root;
            public Image Picture;
            public TMP_Text Kind, Title, Difficulty, Meta;
            public VoyagePips Pips;
            public Button Open;
        }

        sealed class GroupView
        {
            public RectTransform Root, Grid;
            public TMP_Text Name;
            public readonly List<RowView> Rows = new();
        }

        sealed class RowView
        {
            public RectTransform Root;
            public Button Button;
            public Image Face, Lock;
            public TMP_Text By, Title, Difficulty;
            public VoyagePips Pips;
        }

        public bool IsOpen => _root.gameObject.activeSelf;

        public VoyageCrewPopup(VoyageBoard board, VoyageKit kit, RectTransform parent)
        {
            _board = board;
            _kit = kit;
            _lines = kit.Lines;
            _root = VoyageKit.Node("CrewRecommends", parent);
            VoyageKit.Stretch(_root);
            // The page behind dims (rgba(0,1,10,.72)) and takes no press.
            VoyageKit.Block(_root, VoyageKit.Fade(_kit.Popup, 0.72f), hit: true);
            _frame = VoyageKit.Node("Frame", _root);
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(0.5f, 0.5f);
            _frame.sizeDelta = Screen;
            _panel = At("Panel", PanelX, PanelY, Width, Height, _frame);
            // The board's glass modal; a window over the page, so nothing behind reads through, and it does not lift.
            var glass = _kit.Glass(_panel, hit: true);
            glass.Fill.Set(Opaque(VoyageKit.Fade(_kit.Outline, 0.36f)), _kit.Popup, _kit.Popup, 0.55f);
            if (_panel.TryGetComponent<VoyageHoverLift>(out var lift)) lift.enabled = false;
            var c = glass.Content;

            var title = _kit.Text(c, 28, _kit.Light, TextAlignmentOptions.Left);
            VoyageKit.At(title.rectTransform, 40f, 30f, 1200f, 36f);
            VoyageKit.Set(title, _lines?.CrewTitle);
            var line = _kit.Text(c, 15, _kit.Sub, TextAlignmentOptions.Left);
            VoyageKit.At(line.rectTransform, 40f, 76f, 1200f, 22f);
            VoyageKit.Set(line, _lines?.CrewLine);
            _kit.Square(At("Close", Width - 40f - 44f, 30f, 44f, 44f, c), _lines?.PickClose, Close);

            _rail = At("Factions", RailX, RailY, RailWidth, Height - RailY - 40f, c);
            _railNote = _kit.Text(_rail, 14, VoyageKit.Fade(_kit.Sub, 0.8f), TextAlignmentOptions.TopLeft);
            _railNote.textWrappingMode = TextWrappingModes.Normal;
            VoyageKit.Set(_railNote, _lines?.CrewLeftOut);

            var openLabel = _kit.Label(c, 14, _kit.Light);
            VoyageKit.At(openLabel.rectTransform, RightX, RailY, RightWidth, 20f);
            VoyageKit.Set(openLabel, _lines?.CrewOpenNow);
            _openArea = At("OpenNow", RightX, RailY + 32f, RightWidth, OpenHeight, c);

            _crewHead = At("CrewVoyages", RightX, 0f, RightWidth, 24f, c);
            var head = _kit.Label(_crewHead, 14, _kit.Sub);
            VoyageKit.Stretch(head.rectTransform);
            VoyageKit.Set(head, _lines?.CrewVoyages);
            var lockedRow = VoyageKit.Node("Locked", _crewHead);
            lockedRow.anchorMin = lockedRow.anchorMax = lockedRow.pivot = new Vector2(1f, 0.5f);
            lockedRow.sizeDelta = new Vector2(420f, 24f);
            VoyageKit.Row(lockedRow, 8f, TextAnchor.MiddleRight);
            var lockBox = VoyageKit.Node("Lock", lockedRow);
            var lle = VoyageKit.Le(lockBox);
            lle.preferredWidth = lle.minWidth = lle.preferredHeight = 16f;
            _crewLock = lockBox.gameObject.AddComponent<Image>();
            _crewLock.preserveAspect = true;
            _crewLock.raycastTarget = false;
            var lockedWords = _kit.Text(lockedRow, 15, Amber, TextAlignmentOptions.Right);
            lockedWords.overflowMode = TextOverflowModes.Overflow;
            VoyageKit.Set(lockedWords, _lines?.CrewLocked);

            BuildList(c);
            _message = _kit.Text(c, 16, _kit.Light, TextAlignmentOptions.Left);
            VoyageKit.At(_message.rectTransform, RightX, Height - 40f - 24f, RightWidth, 24f);

            _root.gameObject.SetActive(false);
        }

        // The board's lock amber (#FFAE00 on the board), the Ark's own amber read through the worn colour set.
        Color Amber => ColorTrace.Retrace(ColorTrace.Ui, VoyageKit.ArkAuthored);

        Color Opaque(Color c)
        {
            var under = _kit.Popup;
            return new Color(Mathf.Lerp(under.r, c.r, c.a), Mathf.Lerp(under.g, c.g, c.a), Mathf.Lerp(under.b, c.b, c.a), 1f);
        }

        static RectTransform At(string name, float x, float y, float w, float h, RectTransform parent)
        {
            var rt = VoyageKit.Node(name, parent);
            VoyageKit.At(rt, x, y, w, h);
            return rt;
        }

        void BuildList(RectTransform c)
        {
            _scroll = At("Voyages", RightX, 0f, RightWidth, 100f, c);
            _scroll.gameObject.AddComponent<RectMask2D>();
            var hit = VoyageKit.Block(_scroll, VoyageKit.Clear, hit: true);
            var scroll = _scroll.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            _list = VoyageKit.Node("List", _scroll);
            _list.anchorMin = new Vector2(0f, 1f);
            _list.anchorMax = Vector2.one;
            _list.pivot = new Vector2(0.5f, 1f);
            _list.sizeDelta = Vector2.zero;
            var column = VoyageKit.Column(_list, 22f, TextAnchor.UpperLeft);
            column.childForceExpandWidth = true;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandHeight = false;
            var fit = _list.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _list;
            scroll.viewport = _scroll;
            hit.raycastTarget = true;
        }

        // ── Opening ──────────────────────────────────────────────────────────

        /// <summary>Open over the page with <paramref name="offers"/>; <paramref name="lockSprite"/> is the tabs' lock.</summary>
        public void Open(IReadOnlyList<VoyageBoard.Offer> offers, Sprite lockSprite)
        {
            _house = null;
            Lift();
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            _fitFor = new Vector2(-1f, -1f);
            if (!_rootGroup && !_root.TryGetComponent(out _rootGroup)) _rootGroup = _root.gameObject.AddComponent<CanvasGroup>();
            if (!_frameGroup && !_frame.TryGetComponent(out _frameGroup)) _frameGroup = _frame.gameObject.AddComponent<CanvasGroup>();
            _openT = _dimT = 0f;
            _crewLock.sprite = lockSprite;
            _crewLock.enabled = lockSprite;
            _crewLock.color = Amber;
            VoyageKit.Set(_message, null);
            Show(offers);
            Tick(0f);
        }

        /// <summary>Draw <paramref name="offers"/> again (the window refreshed while the popup is open).</summary>
        public void Show(IReadOnlyList<VoyageBoard.Offer> offers)
        {
            _offers.Clear();
            if (offers != null) _offers.AddRange(offers);
            VoyageCrewRules.OpenNow(_offers, _open);
            VoyageCrewRules.Groups(_offers, _groups);
            if (_house != null && !_groups.Exists(g => g.House == _house)) _house = null;
            DrawRail();
            DrawOpen();
            DrawGroups();
        }

        public void Close()
        {
            _root.gameObject.SetActive(false);
            if (_overlay) _overlay.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            if (_overlay) Object.Destroy(_overlay.gameObject);
        }

        void Lift()
        {
            if (!_overlay)
            {
                var canvas = _board ? _board.GetComponentInParent<Canvas>() : null;
                var rootCanvas = canvas ? canvas.rootCanvas : null;
                if (!rootCanvas) return;
                _overlay = VoyageKit.Node("VoyagesCrewRecommends", rootCanvas.transform);
                VoyageKit.Stretch(_overlay);
                var c = _overlay.gameObject.AddComponent<Canvas>();
                c.overrideSorting = true;
                c.sortingOrder = rootCanvas.sortingOrder + 550;
                _overlay.gameObject.AddComponent<GraphicRaycaster>();
                _root.SetParent(_overlay, false);
                VoyageKit.Stretch(_root);
            }
            _overlay.gameObject.SetActive(true);
        }

        /// <summary>Scale the board's screen to the one the popup opens over, and play Frogma's modal ease.</summary>
        public void Tick(float dt)
        {
            if (!IsOpen) return;
            var room = _root.rect.size;
            bool opening = _openT < 1f;
            if (room == _fitFor && !opening) return;
            if (room != _fitFor)
            {
                _fitFor = room;
                _fitK = Mathf.Max(0.05f, Mathf.Min(room.x / Screen.x, room.y / Screen.y));
            }
            _openT = Mathf.Min(1f, _openT + dt / OpenSeconds);
            _dimT = Mathf.Min(1f, _dimT + dt / DimSeconds);
            float e = VoyageKit.Rise(_openT);
            float k = _fitK * (0.97f + 0.03f * e);
            _frame.localScale = new Vector3(k, k, 1f);
            _frame.anchoredPosition = new Vector2(0f, -24f * (1f - e) * _fitK);
            if (_frameGroup) _frameGroup.alpha = e;
            if (_rootGroup) _rootGroup.alpha = Mathf.SmoothStep(0f, 1f, _dimT);
        }

        // ── The faction rail ─────────────────────────────────────────────────

        void DrawRail()
        {
            int rows = 1 + _groups.Count;
            for (int i = 0; i < rows; i++)
            {
                var view = RailFor(i);
                view.Root.gameObject.SetActive(true);
                FactionType? house = i == 0 ? null : _groups[i - 1].House;
                int count = i == 0 ? _offers.Count : _groups[i - 1].Offers.Count;
                bool on = house == _house;
                VoyageKit.Set(view.Name, i == 0 ? _lines?.CrewAll : VoyageCrewRules.FactionName(_lines, house.Value));
                VoyageKit.Set(view.Count, count.ToString(CultureInfo.InvariantCulture));
                // The picked row: a 2 unit cyan line at its left over a faint cyan ground; the others in Sub.
                view.Ground.color = on ? VoyageKit.Fade(_kit.Cyan, 0.06f) : VoyageKit.Clear;
                view.Edge.enabled = on;
                view.Name.color = view.Count.color = on ? _kit.Light : _kit.Sub;
            }
            for (int i = rows; i < _railViews.Count; i++) _railViews[i].Root.gameObject.SetActive(false);
            VoyageKit.At(_railNote.rectTransform, 18f, rows * RailRow + 18f, RailWidth - 36f, 60f);
        }

        RailView RailFor(int index)
        {
            while (_railViews.Count <= index)
            {
                int i = _railViews.Count;
                var view = new RailView { Root = At("Faction" + i, 0f, i * RailRow, RailWidth, RailRow, _rail) };
                view.Ground = VoyageKit.Block(view.Root, VoyageKit.Clear, hit: true);
                var b = view.Root.gameObject.AddComponent<Button>();
                b.targetGraphic = view.Ground;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => PickHouse(i));
                var edge = VoyageKit.Node("Edge", view.Root);
                edge.anchorMin = Vector2.zero;
                edge.anchorMax = new Vector2(0f, 1f);
                edge.pivot = new Vector2(0f, 0.5f);
                edge.sizeDelta = new Vector2(2f, 0f);
                view.Edge = VoyageKit.Block(edge, _kit.Cyan);
                view.Name = _kit.Label(view.Root, 15, _kit.Sub);
                VoyageKit.Stretch(view.Name.rectTransform);
                view.Name.rectTransform.offsetMin = new Vector2(18f, 0f);
                view.Count = _kit.Text(view.Root, 15, _kit.Sub, TextAlignmentOptions.Right);
                VoyageKit.Stretch(view.Count.rectTransform);
                view.Count.rectTransform.offsetMax = new Vector2(-18f, 0f);
                _railViews.Add(view);
            }
            return _railViews[index];
        }

        void PickHouse(int row)
        {
            _house = row <= 0 || row > _groups.Count ? null : _groups[row - 1].House;
            VoyageKit.Set(_message, null);
            DrawRail();
            DrawGroups();
        }

        // ── Open now ─────────────────────────────────────────────────────────

        void DrawOpen()
        {
            float w = (RightWidth - OpenGap) * 0.5f;
            int rows = Mathf.Max(1, (_open.Count + 1) / 2);
            for (int i = 0; i < _open.Count; i++)
            {
                var view = OpenFor(i);
                view.Root.gameObject.SetActive(true);
                VoyageKit.At(view.Root, i % 2 * (w + OpenGap), i / 2 * (OpenHeight + OpenGap), w, OpenHeight);
                int index = _open[i];
                var offer = _offers[index];
                VoyageKit.ShowCover(view.Picture, offer.Portrait ? offer.Portrait : offer.Picture, new Vector2(150f, 80f),
                    offer.Portrait ? VoyageKit.FaceFocus : new Vector2(0.5f, 0.5f));
                VoyageKit.Set(view.Kind, offer.Kind);
                VoyageKit.Set(view.Title, offer.Title);
                view.Pips.Lit = offer.Recommended;
                VoyageKit.Set(view.Difficulty, offer.Recommended.ToString(CultureInfo.InvariantCulture));
                VoyageKit.Set(view.Meta, offer.Stops.Count > 0
                    ? TemplateText.Fill(_lines?.CrewStops, "stops", offer.Stops.Count.ToString(CultureInfo.InvariantCulture))
                    : null);
                view.Open.onClick.RemoveAllListeners();
                view.Open.onClick.AddListener(() => OpenVoyage(index));
            }
            for (int i = _open.Count; i < _openViews.Count; i++) _openViews[i].Root.gameObject.SetActive(false);
            float openH = rows * OpenHeight + (rows - 1) * OpenGap;
            _openArea.sizeDelta = new Vector2(RightWidth, openH);
            float headY = RailY + 32f + openH + 26f;
            VoyageKit.At(_crewHead, RightX, headY, RightWidth, 24f);
            float listY = headY + 24f + 18f;
            VoyageKit.At(_scroll, RightX, listY, RightWidth, Mathf.Max(100f, Height - 40f - 36f - listY));
        }

        OpenView OpenFor(int index)
        {
            while (_openViews.Count <= index)
            {
                // A glass card with the picked voyage's cyan line: its picture, kind and name, its difficulty and length,
                // Open at the bottom right.
                var view = new OpenView { Root = VoyageKit.Node("Open" + _openViews.Count, _openArea) };
                var card = _kit.Glass(view.Root, hit: false);
                card.Edge.Solid(_kit.Cyan);
                card.Edge.Width = 1f;
                var c = card.Content;
                var pic = At("Picture", 24f, 22f, 150f, 80f, c);
                view.Picture = VoyageKit.CoverBox(pic);
                var words = At("Words", 196f, 22f, 330f, 80f, c);
                VoyageKit.Column(words, 8f, TextAnchor.UpperLeft);
                view.Kind = _kit.Label(words, 14, _kit.Light);
                view.Title = _kit.Text(words, 23, _kit.Light, TextAlignmentOptions.TopLeft);
                view.Title.textWrappingMode = TextWrappingModes.Normal;
                VoyageKit.Le(view.Title).preferredHeight = 58f;
                var meta = At("Meta", 24f, OpenHeight - 24f - 18f, 280f, 18f, c);
                var row = VoyageKit.Row(meta, 12f);
                row.childForceExpandWidth = false;
                view.Pips = _kit.Pips(meta);
                view.Difficulty = _kit.Text(meta, 14, _kit.Sub, TextAlignmentOptions.Left);
                VoyageKit.Le(view.Difficulty).minWidth = VoyageKit.Le(view.Difficulty).preferredWidth = 16f;
                view.Meta = _kit.Text(meta, 14, _kit.Sub, TextAlignmentOptions.Left);
                VoyageKit.Le(view.Meta).flexibleWidth = 1f;
                var open = VoyageKit.Node("Open", c);
                open.anchorMin = open.anchorMax = open.pivot = new Vector2(1f, 0f);
                open.sizeDelta = new Vector2(220f, 48f);
                open.anchoredPosition = new Vector2(-20f, 16f);
                (view.Open, _) = _kit.Cta(open, _lines?.BoardSailKey, _lines?.CrewOpen, null);
                _openViews.Add(view);
            }
            return _openViews[index];
        }

        void OpenVoyage(int index)
        {
            Close();
            _board.Pick(index);
            _board.OpenPicked();
        }

        // ── Crew voyages, by faction ─────────────────────────────────────────

        void DrawGroups()
        {
            int shown = 0;
            foreach (var g in _groups)
            {
                if (_house != null && g.House != _house) continue;
                var view = GroupFor(shown++);
                view.Root.gameObject.SetActive(true);
                VoyageKit.Set(view.Name, VoyageCrewRules.FactionName(_lines, g.House));
                int rows = (g.Offers.Count + 1) / 2;
                VoyageKit.Le(view.Grid).preferredHeight = rows * RowHeight + Mathf.Max(0, rows - 1) * RowGap;
                for (int i = 0; i < g.Offers.Count; i++)
                {
                    var row = RowFor(view, i);
                    row.Root.gameObject.SetActive(true);
                    var offer = _offers[g.Offers[i]];
                    VoyageKit.ShowCover(row.Face, offer.Portrait, new Vector2(58f, 70f), VoyageKit.FaceFocus);
                    VoyageKit.Set(row.By, string.IsNullOrEmpty(offer.Pilot) ? offer.Kind
                        : TemplateText.Fill(_lines?.SeaCrewRecommends, "pilot", offer.Pilot));
                    VoyageKit.Set(row.Title, offer.Title);
                    row.Pips.Lit = offer.Recommended;
                    VoyageKit.Set(row.Difficulty, offer.Recommended.ToString(CultureInfo.InvariantCulture));
                    row.Lock.sprite = _crewLock.sprite;
                    row.Lock.enabled = _crewLock.sprite;
                    row.Lock.color = Amber;
                    string title = offer.Title;
                    row.Button.onClick.RemoveAllListeners();
                    row.Button.onClick.AddListener(() => VoyageKit.Set(_message, TemplateText.Fill(_lines?.CrewLockedPress, "title", title)));
                }
                for (int i = g.Offers.Count; i < view.Rows.Count; i++) view.Rows[i].Root.gameObject.SetActive(false);
            }
            for (int i = shown; i < _groupViews.Count; i++) _groupViews[i].Root.gameObject.SetActive(false);
            _crewHead.gameObject.SetActive(_groups.Count > 0);
            LayoutRebuilder.MarkLayoutForRebuild(_list);
        }

        GroupView GroupFor(int index)
        {
            while (_groupViews.Count <= index)
            {
                var view = new GroupView { Root = VoyageKit.Node("Group" + _groupViews.Count, _list) };
                VoyageKit.Column(view.Root, 10f, TextAnchor.UpperLeft).childForceExpandWidth = true;
                view.Name = _kit.Label(view.Root, 13, _kit.Sub);
                view.Grid = VoyageKit.Node("Voyages", view.Root);
                var g = view.Grid.gameObject.AddComponent<GridLayoutGroup>();
                g.cellSize = new Vector2((RightWidth - RowGap) * 0.5f, RowHeight);
                g.spacing = new Vector2(RowGap, RowGap);
                g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                g.constraintCount = 2;
                _groupViews.Add(view);
            }
            return _groupViews[index];
        }

        RowView RowFor(GroupView group, int index)
        {
            while (group.Rows.Count <= index)
            {
                // A locked voyage: the pilot's face, who recommends it, the job, its difficulty, the lock; dimmed to .72.
                var view = new RowView { Root = VoyageKit.Node("Voyage" + group.Rows.Count, group.Grid) };
                var (fill, _) = _kit.Box(view.Root, VoyageKit.Fade(_kit.Popup, 0.7f), VoyageKit.Fade(_kit.Outline, 0.6f), hit: true);
                view.Root.gameObject.AddComponent<CanvasGroup>().alpha = 0.72f;
                view.Button = view.Root.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = fill;
                view.Button.transition = Selectable.Transition.None;
                var face = At("Face", 18f, 15f, 58f, 70f, view.Root);
                view.Face = VoyageKit.CoverBox(face);
                var words = VoyageKit.Node("Words", view.Root);
                VoyageKit.Stretch(words);
                words.offsetMin = new Vector2(18f + 58f + 16f, 10f);
                words.offsetMax = new Vector2(-18f - 20f - 16f, -10f);
                VoyageKit.Column(words, 6f, TextAnchor.MiddleLeft);
                view.By = _kit.Label(words, 13, _kit.Light);
                view.Title = _kit.Text(words, 18, _kit.Light, TextAlignmentOptions.Left);
                var meta = VoyageKit.Node("Meta", words);
                var row = VoyageKit.Row(meta, 10f);
                row.childForceExpandWidth = false;
                VoyageKit.Le(meta).preferredHeight = 16f;
                view.Pips = _kit.Pips(meta);
                view.Difficulty = _kit.Text(meta, 14, _kit.Sub, TextAlignmentOptions.Left);
                var lockRt = VoyageKit.Node("Lock", view.Root);
                lockRt.anchorMin = lockRt.anchorMax = lockRt.pivot = new Vector2(1f, 0.5f);
                lockRt.sizeDelta = new Vector2(20f, 20f);
                lockRt.anchoredPosition = new Vector2(-18f, 0f);
                view.Lock = lockRt.gameObject.AddComponent<Image>();
                view.Lock.preserveAspect = true;
                view.Lock.raycastTarget = false;
                group.Rows.Add(view);
            }
            return group.Rows[index];
        }
    }
}

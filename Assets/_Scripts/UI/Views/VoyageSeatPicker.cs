using System;
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
    /// The seat popup on the Ark page (Frogma Rev 100 P3A's picker, drawn as glass by Rev 108; Docs/MENU_REDESIGN_B.md
    /// sections 201-202): one seat's Vessel, Pilot, Element and Artifact as four tabs, each showing what the seat has now;
    /// the quick kits that set all four in one press (For this voyage, Last flown); the Sees filter over the pilots (what
    /// a pilot's callsign shows in the world, grouped as crystals, prisms, life, vessels and paths); the grid of what the
    /// seat may take, the voyage's own picks tagged; and the picked one's preview. One press takes it
    /// (<see cref="VoyageSeats.Pick"/>, the Ark's rules), and the seat card behind updates.
    /// </summary>
    public sealed class VoyageSeatPicker
    {
        // Frogma Rev 108's picker, in its own units: a 1520 x 830 panel at (200, 154) of the 1920 x 1080 board.
        static readonly Vector2 Screen = new(1920f, 1080f);
        const float Width = 1520f, Height = 830f, PanelX = 200f, PanelY = 154f;
        const float GridX = 40f, GridY = 374f, GridWidth = 1008f, GridHeight = 372f, CellWidth = 158f, CellHeight = 116f, CellGap = 12f;
        const float KitWidth = 242f, KitHeight = 84f, KitGap = 14f, PreviewX = 1080f, PreviewY = 180f, PreviewWidth = 400f,
            PreviewHeight = 548f;

        /// <summary>A whole seat in one value (a quick kit), written to and read from one line.</summary>
        public struct Kit
        {
            public VesselClassType Vessel;
            public string PilotId, ArtifactId;
            public Element Element;

            public static string Write(Kit kit) => string.Join("|", ((int)kit.Vessel).ToString(CultureInfo.InvariantCulture),
                kit.PilotId ?? string.Empty, ((int)kit.Element).ToString(CultureInfo.InvariantCulture), kit.ArtifactId ?? string.Empty);

            public static Kit? Read(string line)
            {
                if (string.IsNullOrEmpty(line)) return null;
                var parts = line.Split('|');
                if (parts.Length != 4 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int vessel)
                    || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int element)) return null;
                return new Kit
                {
                    Vessel = (VesselClassType)vessel, PilotId = parts[1], Element = (Element)element,
                    ArtifactId = string.IsNullOrEmpty(parts[3]) ? null : parts[3],
                };
            }
        }

        /// <summary>The Sees filter's groups (0 is All), by what a pilot's callsign shows.</summary>
        public static int SeesGroup(PilotVision vision) => vision switch
        {
            PilotVision.Anvil or PilotVision.Volt or PilotVision.Zodiac or PilotVision.Sundial or PilotVision.Opal => 1,
            PilotVision.Ensign or PilotVision.Rampart or PilotVision.Citadel or PilotVision.Hornet => 2,
            PilotVision.Manger or PilotVision.Ivy or PilotVision.Talon or PilotVision.Doe => 3,
            PilotVision.Wingman or PilotVision.Bandit or PilotVision.Galleon => 4,
            PilotVision.Needle or PilotVision.Flume => 5,
            _ => 0,
        };

        readonly VoyageBoard _board;
        readonly VoyageKit _kit;
        readonly VoyageLines _lines;
        readonly VoyageSeats _rules;
        readonly PilotArtifactCatalogSO _catalog;

        readonly RectTransform _root, _frame;
        Vector2 _fitFor = new(-1f, -1f);
        RectTransform _overlay;
        TMP_Text _title;
        readonly TabView[] _tabs = new TabView[4];
        readonly KitView[] _kits = new KitView[2];
        RectTransform _seesRow, _grid;
        readonly List<Image> _seesFills = new();
        readonly List<TMP_Text> _seesTexts = new();
        TMP_Text _count;
        readonly List<CellView> _cells = new();
        Image _previewPicture;
        RectTransform _previewFlower;
        TMP_Text _previewName, _previewNote, _previewTag;
        RectTransform _previewBox;

        VoyageBoard.Offer _offer;
        int _seat, _kind, _sees;
        Kit? _last;
        readonly List<VoyageSeats.Option> _options = new();
        readonly List<VoyageSeats.Option> _scratch = new();

        sealed class TabView
        {
            public TMP_Text Name, Value;
            public Image Bar;
        }

        sealed class KitView
        {
            public RectTransform Root;
            public Button Button;
            public Image Thumb;
            public TMP_Text Name, Who;
        }

        sealed class CellView
        {
            public RectTransform Root, TagRoot;
            public Button Button;
            public Image Picture;
            public VoyageOutline Edge;
            public RectTransform Flower;
            public TMP_Text Name, Tag;
        }

        public bool IsOpen => _root.gameObject.activeSelf;

        public VoyageSeatPicker(VoyageBoard board, VoyageKit kit, VoyageSeats rules, RectTransform parent)
        {
            _board = board;
            _kit = kit;
            _lines = kit.Lines;
            _rules = rules;
            _catalog = PilotArtifactCatalogSO.Load();

            _root = VoyageKit.Node("SeatPicker", parent);
            VoyageKit.Stretch(_root);
            // The page behind stays seen, dimmed, and takes no press.
            VoyageKit.Block(_root, VoyageKit.Fade(_kit.Popup, 0.85f), hit: true);
            // The board's 1920 x 1080 screen, scaled to the screen it opens over; the panel stands where the board drew it.
            _frame = VoyageKit.Node("Frame", _root);
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(0.5f, 0.5f);
            _frame.sizeDelta = Screen;
            var panel = At("Panel", PanelX, PanelY, Width, Height, _frame);
            var glass = _kit.Glass(panel, hit: true);
            // A window over the page: the glass's shading, with nothing behind reading through.
            glass.Fill.Set(Opaque(VoyageKit.Fade(_kit.Outline, 0.36f)), _kit.Popup, _kit.Popup, 0.55f);
            var c = glass.Content;

            _title = _kit.Text(c, 32, _kit.Light, TextAlignmentOptions.Left);
            VoyageKit.At(_title.rectTransform, 40f, 20f, 900f, 44f);
            var close = At("Close", Width - 40f - 44f, 30f, 44f, 44f, c);
            _kit.Square(close, _lines?.PickClose, Close);

            BuildTabs(c);
            BuildKits(c);
            BuildSees(c);
            BuildGrid(c);
            BuildPreview(c);

            var note = _kit.Text(c, 16, _kit.Sub, TextAlignmentOptions.Left);
            VoyageKit.At(note.rectTransform, GridX, GridY + GridHeight + 12f, 900f, 24f);
            VoyageKit.Set(note, _lines?.PickNote);
            var done = At("Done", PreviewX + PreviewWidth - 296f, 744f, 296f, 56f, c);
            _kit.Cta(done, _lines?.BoardSailKey, _lines?.BoardDone, Close);

            _root.gameObject.SetActive(false);
        }

        // The glass's top colour over the dark ground, made opaque.
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

        void BuildTabs(RectTransform c)
        {
            // The seat's four settings: each its name (caps 15) over what the seat has now (15, Sub), 40 apart; the open one
            // lit and underlined in cyan.
            var row = At("Tabs", 40f, 76f, 900f, 56f, c);
            var h = VoyageKit.Row(row, 40f, TextAnchor.UpperLeft);
            h.childForceExpandHeight = true;
            string[] names = { _lines?.CardSettingVessel, _lines?.CardSettingPilot, _lines?.CardSettingElement, _lines?.CardSettingArtifact };
            for (int kind = 0; kind < 4; kind++)
            {
                int k = kind;
                var tab = VoyageKit.Node("Tab" + kind, row);
                var hit = VoyageKit.Block(tab, VoyageKit.Clear, hit: true);
                var b = tab.gameObject.AddComponent<Button>();
                b.targetGraphic = hit;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => ShowKind(k));
                VoyageKit.Column(tab, 4f, TextAnchor.UpperLeft);
                var view = new TabView();
                view.Name = _kit.Label(tab, 15, _kit.Sub);
                view.Name.overflowMode = TextOverflowModes.Overflow;
                VoyageKit.Set(view.Name, names[kind]);
                view.Value = _kit.Text(tab, 15, _kit.Sub, TextAlignmentOptions.Left);
                view.Value.overflowMode = TextOverflowModes.Overflow;
                var bar = VoyageKit.Node("Bar", tab);
                VoyageKit.Le(bar).preferredHeight = 2f;
                view.Bar = VoyageKit.Block(bar, _kit.Cyan);
                _tabs[kind] = view;
            }
        }

        void BuildKits(RectTransform c)
        {
            var label = _kit.Label(c, 14, _kit.Sub);
            VoyageKit.At(label.rectTransform, 40f, 182f, 900f, 20f);
            VoyageKit.Set(label, _lines?.PickKits);
            string[] names = { _lines?.PickKitVoyage, _lines?.PickKitLast };
            for (int i = 0; i < 2; i++)
            {
                int which = i;
                // A kit: 242 x 84, a thumb, its name (17) and what it sets (14, Sub).
                var view = new KitView { Root = At("Kit" + i, 40f + i * (KitWidth + KitGap), 210f, KitWidth, KitHeight, c) };
                var (fill, _) = _kit.Box(view.Root, VoyageKit.Fade(_kit.Popup, 0.7f), VoyageKit.Fade(_kit.Outline, 0.8f), hit: true);
                view.Button = view.Root.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = fill;
                view.Button.onClick.AddListener(() => TakeKit(which));
                var thumb = At("Thumb", 14f, 14f, 46f, 56f, view.Root);
                _kit.Box(thumb, VoyageKit.Clear, VoyageKit.Fade(_kit.Light, 0.6f));
                view.Thumb = VoyageKit.CoverBox(thumb);
                var words = At("Words", 74f, 18f, KitWidth - 74f - 12f, 52f, view.Root);
                VoyageKit.Column(words, 4f, TextAnchor.UpperLeft);
                view.Name = _kit.Text(words, 17, _kit.Light, TextAlignmentOptions.Left);
                VoyageKit.Set(view.Name, names[i]);
                view.Who = _kit.Text(words, 14, _kit.Sub, TextAlignmentOptions.Left);
                _kits[i] = view;
            }
        }

        void BuildSees(RectTransform c)
        {
            _seesRow = At("Sees", GridX, 316f, GridWidth, 36f, c);
            var row = VoyageKit.Row(_seesRow, 10f);
            row.childForceExpandHeight = true;
            var label = _kit.Label(_seesRow, 14, _kit.Sub);
            label.overflowMode = TextOverflowModes.Overflow;
            VoyageKit.Set(label, _lines?.PickSees);
            VoyageKit.Le(label.rectTransform).minWidth = 50f;
            string[] names = { _lines?.PickSeesAll, _lines?.PickSeesCrystals, _lines?.PickSeesPrisms, _lines?.PickSeesLife,
                _lines?.PickSeesVessels, _lines?.PickSeesPaths };
            for (int i = 0; i < names.Length; i++)
            {
                int group = i;
                // A chip: its word (15) in an outlined box, 14 in; the picked one filled with Light, its word dark.
                var chip = VoyageKit.Node("Chip" + i, _seesRow);
                var pad = VoyageKit.Row(chip, 0f, TextAnchor.MiddleCenter);
                pad.padding = new RectOffset(14, 14, 0, 0);
                var (fill, _) = _kit.Box(chip, VoyageKit.Fade(_kit.Popup, 0.7f), _kit.Outline, hit: true);
                var b = chip.gameObject.AddComponent<Button>();
                b.targetGraphic = fill;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { _sees = group; Refresh(); });
                var text = _kit.Text(chip, 15, _kit.Light, TextAlignmentOptions.Center);
                text.overflowMode = TextOverflowModes.Overflow;
                VoyageKit.Set(text, names[i]);
                _seesFills.Add(fill);
                _seesTexts.Add(text);
            }
            _count = _kit.Text(_seesRow, 14, _kit.Sub, TextAlignmentOptions.Right);
            VoyageKit.Le(_count).flexibleWidth = 1f;
        }

        void BuildGrid(RectTransform c)
        {
            // Six across, 158 x 116, 12 apart; more scroll.
            var view = At("Choices", GridX, GridY, GridWidth, GridHeight, c);
            view.gameObject.AddComponent<RectMask2D>();
            var hit = VoyageKit.Block(view, VoyageKit.Clear, hit: true);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            _grid = VoyageKit.Node("Grid", view);
            _grid.anchorMin = new Vector2(0f, 1f);
            _grid.anchorMax = Vector2.one;
            _grid.pivot = new Vector2(0.5f, 1f);
            _grid.sizeDelta = Vector2.zero;
            var g = _grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(CellWidth, CellHeight);
            g.spacing = new Vector2(CellGap, CellGap);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 6;
            var fit = _grid.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _grid;
            scroll.viewport = view;
            hit.raycastTarget = true;
        }

        void BuildPreview(RectTransform c)
        {
            // The picked one, 400 x 548: its picture filling the top, its house, its name (32) and what it sees (16).
            _previewBox = At("Preview", PreviewX, PreviewY, PreviewWidth, PreviewHeight, c);
            VoyageKit.Block(_previewBox, _kit.Popup);
            _previewPicture = VoyageKit.CoverBox(_previewBox);
            var shade = VoyageKit.Node("Shade", _previewBox);
            VoyageKit.Stretch(shade);
            shade.anchorMax = new Vector2(1f, 0.6f);
            var g = shade.gameObject.AddComponent<VoyageGradient>();
            g.raycastTarget = false;
            g.Set(VoyageKit.Fade(_kit.Popup, 0f), VoyageKit.Fade(_kit.Popup, 0.92f), _kit.Popup, 0.5f);
            var flower = At("Element", PreviewWidth * 0.5f - 64f, 120f, 128f, 128f, _previewBox);
            _previewFlower = VoyageKit.Flower(flower, Element.Charge, _kit.Light);
            VoyageKit.Stretch(_previewFlower);
            var edge = VoyageKit.Node("Edge", _previewBox);
            VoyageKit.Stretch(edge);
            edge.gameObject.AddComponent<VoyageOutline>().Solid(VoyageKit.Fade(_kit.Outline, 0.8f));
            var words = VoyageKit.Node("Words", _previewBox);
            words.anchorMin = Vector2.zero;
            words.anchorMax = new Vector2(1f, 0f);
            words.pivot = new Vector2(0.5f, 0f);
            words.sizeDelta = new Vector2(-56f, 200f);
            words.anchoredPosition = new Vector2(0f, 28f);
            VoyageKit.Column(words, 10f, TextAnchor.LowerLeft);
            _previewTag = _kit.Label(words, 14, _kit.Light);
            _previewName = _kit.Text(words, 32, _kit.Light, TextAlignmentOptions.Left);
            _previewNote = _kit.Text(words, 16, _kit.Light, TextAlignmentOptions.TopLeft);
            _previewNote.textWrappingMode = TextWrappingModes.Normal;
            VoyageKit.Le(_previewNote).preferredHeight = 72f;
        }

        /// <summary>Scale the board's screen to the screen the popup opens over (on open and when it is resized).</summary>
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
            // Frogma's modal: the dim fades in (.35s ease) and the panel comes up 24 and from .97 (.45s
            // cubic-bezier(.2,.7,.2,1)).
            _openT = Mathf.Min(1f, _openT + dt / OpenSeconds);
            _dimT = Mathf.Min(1f, _dimT + dt / DimSeconds);
            float e = VoyageKit.Rise(_openT);
            float k = _fitK * (0.97f + 0.03f * e);
            _frame.localScale = new Vector3(k, k, 1f);
            _frame.anchoredPosition = new Vector2(0f, -24f * (1f - e) * _fitK);
            if (_frameGroup) _frameGroup.alpha = e;
            if (_rootGroup) _rootGroup.alpha = Mathf.SmoothStep(0f, 1f, _dimT);
        }

        const float OpenSeconds = 0.45f, DimSeconds = 0.35f;
        float _openT = 1f, _dimT = 1f, _fitK = 1f;
        CanvasGroup _rootGroup, _frameGroup;

        // ── Opening ──────────────────────────────────────────────────────────

        /// <summary>Open on <paramref name="seat"/>'s <paramref name="kind"/> tab (<see cref="VoyageSeats.Vessel"/>...)
        /// for <paramref name="offer"/>; <paramref name="last"/> is the Last flown kit, if any.</summary>
        public void Open(VoyageBoard.Offer offer, int seat, int kind, Kit? last)
        {
            _offer = offer;
            _seat = seat;
            _kind = kind;
            _last = last;
            _sees = 0;
            Lift();
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            _fitFor = new Vector2(-1f, -1f);
            if (!_rootGroup && !_root.TryGetComponent(out _rootGroup)) _rootGroup = _root.gameObject.AddComponent<CanvasGroup>();
            if (!_frameGroup && !_frame.TryGetComponent(out _frameGroup)) _frameGroup = _frame.gameObject.AddComponent<CanvasGroup>();
            _openT = _dimT = 0f;
            Tick(0f);
            Refresh();
        }

        public void Close()
        {
            _root.gameObject.SetActive(false);
            if (_overlay) _overlay.gameObject.SetActive(false);
        }

        /// <summary>The popup is a window over the whole screen (the page's own rect is shorter than the popup), so
        /// on first open it moves into its own canvas over the root canvas, as the full-screen map does.</summary>
        void Lift()
        {
            if (!_overlay)
            {
                var canvas = _board ? _board.GetComponentInParent<Canvas>() : null;
                var rootCanvas = canvas ? canvas.rootCanvas : null;
                if (!rootCanvas) return;
                _overlay = VoyageKit.Node("VoyagesSeatPicker", rootCanvas.transform);
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

        /// <summary>Removes the popup's canvas with the board.</summary>
        public void Dispose()
        {
            if (_overlay) UnityEngine.Object.Destroy(_overlay.gameObject);
        }

        void ShowKind(int kind)
        {
            _kind = kind;
            _sees = 0;
            Refresh();
        }

        public void Refresh()
        {
            if (!IsOpen) return;
            var squad = VoyageSeats.Active;
            if (squad == null || !VoyageSeats.CanEdit(squad, _seat))
            {
                Close();
                return;
            }
            var slot = squad.GetSlot(_seat);
            var vessel = _rules.VesselOf(slot.Vessel);
            var pilot = _rules.PilotOf(slot, vessel);
            bool you = VoyageOpenCard.IsMySeat(squad, _seat);
            VoyageKit.Set(_title, you ? _lines?.PickTitle : TemplateText.Fill(_lines?.PickTitleSeat, "seat", _rules.Role(_seat)));

            string[] values =
            {
                vessel ? vessel.CardName : null, VoyageSeats.PilotName(slot, pilot, true), _rules.ElementName(slot.Element),
                _rules.ArtifactName(slot, vessel, true),
            };
            for (int k = 0; k < 4; k++)
            {
                bool on = k == _kind;
                VoyageKit.Set(_tabs[k].Value, values[k]);
                _tabs[k].Bar.enabled = on;
                _tabs[k].Name.color = on ? _kit.Light : _kit.Sub;
                _tabs[k].Value.color = on ? _kit.Light : _kit.Sub;
            }

            // The quick kits: the voyage's own picks, and the seat last sailed with.
            var voyage = ForThisVoyage();
            DrawKit(_kits[0], voyage);
            DrawKit(_kits[1], _last);

            _rules.Offer(_kind, squad, _seat, _scratch);
            _options.Clear();
            int total = _scratch.Count;
            foreach (var o in _scratch)
                if (_kind != VoyageSeats.Pilot || _sees == 0 || SeesGroup(VisionOf(o.Id)) == _sees) _options.Add(o);
            _seesRow.gameObject.SetActive(_kind == VoyageSeats.Pilot);
            for (int i = 0; i < _seesTexts.Count; i++)
            {
                bool on = i == _sees;
                _seesTexts[i].color = on ? _kit.Popup : _kit.Light;
                _seesFills[i].color = on ? _kit.Light : VoyageKit.Fade(_kit.Popup, 0.7f);
            }
            VoyageKit.Set(_count, TemplateText.Fill(_lines?.PickCount, "shown", _options.Count.ToString(CultureInfo.InvariantCulture),
                "total", total.ToString(CultureInfo.InvariantCulture)));

            VoyageSeats.Option picked = default;
            bool any = false;
            for (int i = 0; i < _options.Count; i++)
            {
                var o = _options[i];
                var cell = CellFor(i);
                cell.Root.gameObject.SetActive(true);
                VoyageKit.Show(cell.Picture, o.Picture);
                cell.Flower.gameObject.SetActive(_kind == VoyageSeats.ElementKind);
                if (_kind == VoyageSeats.ElementKind) Petals(cell.Flower, o.Element);
                VoyageKit.Set(cell.Name, o.Name);
                bool tagged = ForVoyage(o);
                cell.TagRoot.gameObject.SetActive(tagged);
                VoyageKit.Set(cell.Tag, tagged ? _lines?.PickForVoyage : null);
                cell.Edge.Solid(o.Picked ? _kit.Cyan : VoyageKit.Fade(_kit.Outline, 0.8f));
                cell.Edge.Width = o.Picked ? 2f : 1f;
                cell.Name.color = _kit.Light;
                var option = o;
                cell.Button.onClick.RemoveAllListeners();
                cell.Button.onClick.AddListener(() => _rules.Pick(_seat, _kind, option));
                if (o.Picked)
                {
                    picked = o;
                    any = true;
                }
            }
            for (int i = _options.Count; i < _cells.Count; i++) _cells[i].Root.gameObject.SetActive(false);
            Preview(any ? picked : default, any);
        }

        void Preview(VoyageSeats.Option o, bool any)
        {
            VoyageKit.ShowCover(_previewPicture, any ? o.Picture : null, new Vector2(PreviewWidth, PreviewHeight), new Vector2(0.5f, 0.2f));
            _previewFlower.parent.gameObject.SetActive(any && _kind == VoyageSeats.ElementKind);
            if (any && _kind == VoyageSeats.ElementKind) Petals(_previewFlower, o.Element);
            VoyageKit.Set(_previewName, any ? o.Name : null);
            // Over the name: a pilot's house (Rev 108), else the voyage's tag when it is one of its picks.
            string house = any && _kind == VoyageSeats.Pilot && PilotLoadoutStore.TryGetPilot(o.Id, out var record)
                ? record.FactionName : null;
            VoyageKit.Set(_previewTag, !string.IsNullOrEmpty(house) ? house : any && ForVoyage(o) ? _lines?.PickForVoyage : null);
            string note = any ? o.Note : null;
            if (any && _kind == VoyageSeats.Pilot && string.IsNullOrEmpty(note) && _catalog
                && _catalog.TryGetPilot(o.Id, out var callsign) && callsign) note = callsign.Description;
            VoyageKit.Set(_previewNote, note);
        }

        CellView CellFor(int index)
        {
            while (_cells.Count <= index)
            {
                // A choice: an outlined box (cyan when picked), its picture, its name (15) at the foot, the voyage's tag
                // (caps 14, cyan, underlined) at the top.
                var view = new CellView { Root = VoyageKit.Node("Choice" + _cells.Count, _grid) };
                var (fill, edge) = _kit.Box(view.Root, VoyageKit.Fade(_kit.Popup, 0.6f), _kit.Outline, hit: true);
                view.Edge = edge;
                view.Button = view.Root.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = fill;
                view.Button.transition = Selectable.Transition.None;
                var pic = VoyageKit.Node("Picture", view.Root);
                VoyageKit.Stretch(pic);
                pic.offsetMin = new Vector2(6f, 30f);
                pic.offsetMax = new Vector2(-6f, -6f);
                view.Picture = VoyageKit.Picture(pic);
                var flower = VoyageKit.Node("Element", pic);
                flower.anchorMin = flower.anchorMax = flower.pivot = new Vector2(0.5f, 0.5f);
                flower.sizeDelta = new Vector2(56f, 56f);
                var petals = VoyageKit.Flower(flower, Element.Charge, _kit.Light);
                VoyageKit.Stretch(petals);
                view.Flower = flower;
                view.Name = _kit.Text(view.Root, 15, _kit.Light, TextAlignmentOptions.Left);
                VoyageKit.At(view.Name.rectTransform, 10f, CellHeight - 8f - 20f, CellWidth - 20f, 20f);
                view.TagRoot = At("Tag", 8f, 8f, CellWidth - 16f, 24f, view.Root);
                view.Tag = _kit.Label(view.TagRoot, 14, _kit.Cyan, TextAlignmentOptions.Left);
                VoyageKit.Stretch(view.Tag.rectTransform);
                view.Tag.characterSpacing = 8f;
                var line = VoyageKit.Node("Line", view.TagRoot);
                VoyageKit.Foot(line, 0f, 2f);
                VoyageKit.Block(line, _kit.Cyan);
                _cells.Add(view);
            }
            return _cells[index];
        }

        PilotVision VisionOf(string personId) =>
            _catalog && !string.IsNullOrEmpty(personId) && _catalog.TryGetPilot(personId, out var p) && p ? p.Vision : PilotVision.None;

        bool ForVoyage(VoyageSeats.Option o)
        {
            if (_offer == null) return false;
            switch (_kind)
            {
                case VoyageSeats.Vessel: return _offer.SuggestedVessels.Contains(o.Vessel);
                case VoyageSeats.Pilot:
                    return _catalog && !string.IsNullOrEmpty(o.Id) && _catalog.TryGetPilot(o.Id, out var p) && p
                        && _offer.SuggestedPilots.Contains(p.Id);
                case VoyageSeats.ElementKind: return _offer.Pays && o.Element == _offer.Element;
                default: return false;
            }
        }

        // ── Quick kits ───────────────────────────────────────────────────────

        // The voyage's own kit: its first suggested vessel the seat may take, a suggested pilot who flies it, its element.
        Kit? ForThisVoyage()
        {
            var squad = VoyageSeats.Active;
            if (_offer == null || squad == null) return null;
            var vessels = new List<VoyageSeats.Option>();
            _rules.Offer(VoyageSeats.Vessel, squad, _seat, vessels);
            VesselClassType? cls = null;
            foreach (var v in _offer.SuggestedVessels)
                if (vessels.Exists(o => o.Vessel == v)) { cls = v; break; }
            if (cls == null) return null;
            string pilot = null;
            if (_catalog)
            {
                var crew = new List<PilotRecord>();
                PilotLoadoutStore.GetCrew(crew);
                var vessel = _rules.VesselOf(cls.Value);
                foreach (var person in crew)
                    if (_catalog.TryGetPilot(person.Id, out var callsign) && callsign && _offer.SuggestedPilots.Contains(callsign.Id)
                        && vessel && callsign.Requirement != null && callsign.Requirement.IsMetBy(vessel.Class, vessel.Keywords))
                    {
                        pilot = person.Id;
                        break;
                    }
            }
            var slot = squad.GetSlot(_seat);
            return new Kit
            {
                Vessel = cls.Value, PilotId = pilot, Element = _offer.Pays ? _offer.Element : slot.Element, ArtifactId = slot.ArtifactId,
            };
        }

        void DrawKit(KitView view, Kit? kit)
        {
            view.Root.gameObject.SetActive(kit != null);
            if (kit == null) return;
            var k = kit.Value;
            var vessel = _rules.VesselOf(k.Vessel);
            string pilot = PilotLoadoutStore.TryGetPilot(k.PilotId, out var person) ? person.DisplayName()
                : _catalog && _catalog.TryGetPilot(k.PilotId, out var callsign) && callsign ? callsign.DisplayName : null;
            VoyageKit.Set(view.Who, TemplateText.Fill(_lines?.PickKitWho, "vessel", vessel ? vessel.CardName : null,
                "pilot", pilot, "element", _rules.ElementName(k.Element)));
            var face = _catalog && _catalog.TryGetPilot(k.PilotId, out var seen) && seen ? seen.Portrait : null;
            VoyageKit.ShowCover(view.Thumb, face, new Vector2(46f, 56f), VoyageKit.FaceFocus);
        }

        void TakeKit(int which)
        {
            var kit = which == 0 ? ForThisVoyage() : _last;
            if (kit == null) return;
            var k = kit.Value;
            var squad = VoyageSeats.Active;
            if (squad == null) return;
            // Vessel first: the pilots and artifacts a seat may take follow its hull.
            if (squad.GetSlot(_seat).Vessel != k.Vessel) _rules.Pick(_seat, VoyageSeats.Vessel, new VoyageSeats.Option { Vessel = k.Vessel });
            TakeIfOffered(VoyageSeats.Pilot, o => o.Id == k.PilotId && !string.IsNullOrEmpty(k.PilotId));
            TakeIfOffered(VoyageSeats.ElementKind, o => o.Element == k.Element);
            TakeIfOffered(VoyageSeats.Artifact, o => o.Id == k.ArtifactId);
            Refresh();
        }

        void TakeIfOffered(int kind, Predicate<VoyageSeats.Option> match)
        {
            var squad = VoyageSeats.Active;
            if (squad == null) return;
            _rules.Offer(kind, squad, _seat, _scratch);
            foreach (var o in _scratch)
                if (match(o))
                {
                    if (!o.Picked) _rules.Pick(_seat, kind, o);
                    return;
                }
        }

        /// <summary>Swap a <see cref="UI.VoyageKit.Flower"/>'s petals to <paramref name="element"/>'s.</summary>
        public static void Petals(RectTransform flower, Element element)
        {
            if (!flower) return;
            var sprite = Resources.Load<Sprite>("ElementPetals/" + element.ToString().ToLowerInvariant() + "_petal");
            int i = 0;
            foreach (var img in flower.GetComponentsInChildren<Image>(true))
            {
                ElementalBarsView.ConfigurePetal(img, sprite, i++);
                img.enabled = sprite;
            }
        }
    }
}

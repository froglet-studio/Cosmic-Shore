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
    /// The seat popup on the Ark page, as Frogma Rev 113 Picker redrew it on Shombith's note about its borders
    /// (Docs/MENU_REDESIGN_B.md section 209 and 209.1): a flat panel inside one thin line; one seat's Vessel, Pilot, Element and
    /// Artifact as four tabs, each showing what the seat has now; a grid of what the seat may take, three across, each tile
    /// inside one thin line (the picked one cyan) with "For this voyage" in plain text on the voyage's own picks; the Sees
    /// filter over the pilots on the Pilot tab; and the picked one's preview.
    /// <para>One press saves the pick to the seat for THIS voyage (<see cref="VoyageSeats.PickVoyage"/> into
    /// <see cref="VoyageSeatRecord"/>), never to a saved squad, and the line under the grid says what was saved. Set sail
    /// reads the same seats.</para>
    /// </summary>
    public sealed class VoyageSeatPicker
    {
        // Frogma Rev 113 Picker, in its own units: a 1520 x 830 panel at (200, 154) of the 1920 x 1080 board; the grid and
        // the preview from 180 down to 100 above the panel's foot, 32 apart, the preview 400 wide.
        static readonly Vector2 Screen = new(1920f, 1080f);
        const float Width = 1520f, Height = 830f, PanelX = 200f, PanelY = 154f;
        const float BodyY = 180f, BodyHeight = Height - BodyY - 100f, PreviewWidth = 400f, Gap = 32f;
        const float GridX = 40f, GridWidth = Width - 80f - PreviewWidth - Gap, CellGap = 14f, CellHeight = 170f;
        const float SeesHeight = 36f, SavedHeight = 56f, SavedGap = 22f;
        const float PreviewX = Width - 40f - PreviewWidth, PreviewY = BodyY, PreviewHeight = BodyHeight;

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
        RectTransform _seesRow, _gridView, _grid, _saved;
        readonly List<Image> _seesFills = new();
        readonly List<TMP_Text> _seesTexts = new();
        TMP_Text _count, _savedText;
        readonly List<CellView> _cells = new();
        Image _previewPicture;
        RectTransform _previewFlower;
        TMP_Text _previewName, _previewNote, _previewTag;
        RectTransform _previewBox;

        VoyageBoard.Offer _offer;
        string _key;
        int _seat, _kind, _sees;
        string _savedLine;
        readonly List<VoyageSeats.Option> _options = new();
        readonly List<VoyageSeats.Option> _scratch = new();

        sealed class TabView
        {
            public TMP_Text Name, Value;
            public Image Bar;
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
            // The page behind stays seen, dimmed (rgba(0,1,10,.72)), and takes no press.
            VoyageKit.Block(_root, VoyageKit.Fade(_kit.Popup, 0.72f), hit: true);
            // The board's 1920 x 1080 screen, scaled to the screen it opens over; the panel stands where the board drew it.
            _frame = VoyageKit.Node("Frame", _root);
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(0.5f, 0.5f);
            _frame.sizeDelta = Screen;
            var panel = At("Panel", PanelX, PanelY, Width, Height, _frame);
            // A flat panel (#00041F) inside one thin line (#434C89): no glass rim (Shombith: "weird borders").
            var plate = _kit.RowPicked;
            _kit.Box(panel, new Color(plate.r, plate.g, plate.b, 1f), _kit.Outline, hit: true);
            var c = panel;

            _title = _kit.Text(c, 28, _kit.Light, TextAlignmentOptions.Left);
            VoyageKit.At(_title.rectTransform, 40f, 30f, 900f, 36f);
            var close = At("Close", Width - 40f - 44f, 30f, 44f, 44f, c);
            _kit.Square(close, _lines?.PickClose, Close);

            BuildTabs(c);
            BuildSees(c);
            BuildGrid(c);
            BuildSaved(c);
            BuildPreview(c);

            var note = _kit.Text(c, 15, _kit.Sub, TextAlignmentOptions.Left);
            VoyageKit.At(note.rectTransform, 40f, Height - 30f - 56f, Width - 80f - 300f - 20f, 56f);
            VoyageKit.Set(note, _lines?.PickNote);
            var done = At("Done", Width - 40f - 300f, Height - 30f - 56f, 300f, 56f, c);
            _kit.Cta(done, _lines?.BoardSailKey, _lines?.BoardDone, Close);

            _root.gameObject.SetActive(false);
        }

        static RectTransform At(string name, float x, float y, float w, float h, RectTransform parent)
        {
            var rt = VoyageKit.Node(name, parent);
            VoyageKit.At(rt, x, y, w, h);
            return rt;
        }

        void BuildTabs(RectTransform c)
        {
            // The seat's four settings, 18 under the title: each its name (caps 15) over what the seat has now (14, Sub),
            // 40 apart; the open one lit and underlined in cyan.
            var row = At("Tabs", 40f, 84f, 1000f, 64f, c);
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
                var col = VoyageKit.Column(tab, 8f, TextAnchor.UpperLeft);
                col.padding = new RectOffset(0, 0, 0, 10);
                var view = new TabView();
                view.Name = _kit.Label(tab, 15, _kit.Sub);
                view.Name.overflowMode = TextOverflowModes.Overflow;
                VoyageKit.Set(view.Name, names[kind]);
                view.Value = _kit.Text(tab, 14, _kit.Sub, TextAlignmentOptions.Left);
                view.Value.overflowMode = TextOverflowModes.Overflow;
                var bar = VoyageKit.Node("Bar", tab);
                VoyageKit.Le(bar).ignoreLayout = true;
                VoyageKit.Foot(bar, 0f, 2f);
                view.Bar = VoyageKit.Block(bar, _kit.Cyan);
                _tabs[kind] = view;
            }
        }

        void BuildSees(RectTransform c)
        {
            // The pilots' Sees filter (the Pilot tab only), over the grid.
            _seesRow = At("Sees", GridX, BodyY, GridWidth, SeesHeight, c);
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
                // A chip: its word (15) inside one thin line, 14 in; the picked one filled with Light, its word dark.
                var chip = VoyageKit.Node("Chip" + i, _seesRow);
                var pad = VoyageKit.Row(chip, 0f, TextAnchor.MiddleCenter);
                pad.padding = new RectOffset(14, 14, 0, 0);
                var (fill, _) = _kit.Box(chip, VoyageKit.Fade(_kit.Popup, 0.7f), VoyageKit.Fade(_kit.Outline, 0.6f), hit: true);
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
            // Three across, 170 tall, 14 apart; more scroll.
            _gridView = At("Choices", GridX, BodyY, GridWidth, 100f, c);
            _gridView.gameObject.AddComponent<RectMask2D>();
            var hit = VoyageKit.Block(_gridView, VoyageKit.Clear, hit: true);
            var scroll = _gridView.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            _grid = VoyageKit.Node("Grid", _gridView);
            _grid.anchorMin = new Vector2(0f, 1f);
            _grid.anchorMax = Vector2.one;
            _grid.pivot = new Vector2(0.5f, 1f);
            _grid.sizeDelta = Vector2.zero;
            var g = _grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2((GridWidth - 2f * CellGap) / 3f, CellHeight);
            g.spacing = new Vector2(CellGap, CellGap);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 3;
            var fit = _grid.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _grid;
            scroll.viewport = _gridView;
            hit.raycastTarget = true;
        }

        void BuildSaved(RectTransform c)
        {
            // Under the grid: a tick and what was saved (17), no bar.
            _saved = At("Saved", GridX, BodyY + BodyHeight - SavedHeight, GridWidth, SavedHeight, c);
            VoyageKit.Row(_saved, 14f, TextAnchor.MiddleLeft).padding = new RectOffset(4, 4, 0, 0);
            // The tick, drawn: two 2 unit strokes (the board's 18 x 14 path M1 7l5 5L17 1).
            var tick = VoyageKit.Node("Tick", _saved);
            var tle = VoyageKit.Le(tick);
            tle.preferredWidth = tle.minWidth = 18f;
            tle.preferredHeight = tle.minHeight = 14f;
            Stroke(tick, new Vector2(1f, 7f), new Vector2(6f, 12f));
            Stroke(tick, new Vector2(6f, 12f), new Vector2(17f, 1f));
            _savedText = _kit.Text(_saved, 17, _kit.Light, TextAlignmentOptions.Left);
            VoyageKit.Le(_savedText).flexibleWidth = 1f;
            _saved.gameObject.SetActive(false);
        }

        // A 2 unit line from a to b in an 18 x 14 box drawn top-down (SVG units).
        void Stroke(RectTransform box, Vector2 a, Vector2 b)
        {
            var rt = VoyageKit.Node("Stroke", box);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var d = b - a;
            rt.sizeDelta = new Vector2(d.magnitude + 2f, 2f);
            var mid = (a + b) * 0.5f;
            rt.anchoredPosition = new Vector2(mid.x, -mid.y);
            rt.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            VoyageKit.Block(rt, _kit.Light);
        }

        void BuildPreview(RectTransform c)
        {
            // The picked one: its picture filling the top over #00010A inside one thin line, fading to the ground; "For
            // this voyage" (or a pilot's house), its name (28) and what it does (15).
            _previewBox = At("Preview", PreviewX, PreviewY, PreviewWidth, PreviewHeight, c);
            VoyageKit.Block(_previewBox, _kit.Popup);
            var picture = At("Picture", 0.06f * PreviewWidth, 70f, 0.88f * PreviewWidth, 220f, _previewBox);
            _previewPicture = VoyageKit.CoverBox(picture);
            var shade = VoyageKit.Node("Shade", _previewBox);
            VoyageKit.Stretch(shade);
            shade.anchorMax = new Vector2(1f, 0.6f);
            var g = shade.gameObject.AddComponent<VoyageGradient>();
            g.raycastTarget = false;
            g.Set(VoyageKit.Fade(_kit.Popup, 0f), VoyageKit.Fade(_kit.Popup, 0.95f), _kit.Popup, 0.4f);
            var flower = At("Element", PreviewWidth * 0.5f - 64f, 120f, 128f, 128f, _previewBox);
            _previewFlower = VoyageKit.Flower(flower, Element.Charge, _kit.Light);
            VoyageKit.Stretch(_previewFlower);
            var edge = VoyageKit.Node("Edge", _previewBox);
            VoyageKit.Stretch(edge);
            edge.gameObject.AddComponent<VoyageOutline>().Solid(VoyageKit.Fade(_kit.Outline, 0.6f));
            var words = VoyageKit.Node("Words", _previewBox);
            words.anchorMin = Vector2.zero;
            words.anchorMax = new Vector2(1f, 0f);
            words.pivot = new Vector2(0.5f, 0f);
            words.sizeDelta = new Vector2(-56f, 200f);
            words.anchoredPosition = new Vector2(0f, 28f);
            VoyageKit.Column(words, 12f, TextAnchor.LowerLeft);
            _previewTag = _kit.Label(words, 14, _kit.Light);
            _previewName = _kit.Text(words, 28, _kit.Light, TextAlignmentOptions.Left);
            _previewNote = _kit.Text(words, 15, ForVoyageColor, TextAlignmentOptions.TopLeft);
            _previewNote.textWrappingMode = TextWrappingModes.Normal;
            VoyageKit.Le(_previewNote).preferredHeight = 68f;
        }

        // The board's quiet light (#B8C0F0): between Sub and Light, from the worn set.
        Color ForVoyageColor => Color.Lerp(_kit.Sub, _kit.Light, 0.6f);

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
        /// for <paramref name="offer"/>, whose own seats (<see cref="VoyageSeatRecord"/>) it changes: the record under
        /// <paramref name="key"/> (a friend in the host's lobby changes <see cref="VoyageSeatRecord.PartyKey"/>), else the
        /// offer's.</summary>
        public void Open(VoyageBoard.Offer offer, int seat, int kind, string key = null)
        {
            _offer = offer;
            _key = string.IsNullOrEmpty(key) ? offer?.Key : key;
            _seat = seat;
            _kind = kind;
            _sees = 0;
            _savedLine = null;
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
            _savedLine = null;
            Refresh();
        }

        /// <summary>The seats the popup changes: the open voyage's own.</summary>
        SavedSquad Seats() => VoyageSeatRecord.Get(_key);

        public void Refresh()
        {
            if (!IsOpen) return;
            var squad = Seats();
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
            }

            _rules.Offer(_kind, squad, _seat, _scratch);
            _options.Clear();
            int total = _scratch.Count;
            foreach (var o in _scratch)
                if (_kind != VoyageSeats.Pilot || _sees == 0 || SeesGroup(VisionOf(o.Id)) == _sees) _options.Add(o);
            bool sees = _kind == VoyageSeats.Pilot;
            _seesRow.gameObject.SetActive(sees);
            for (int i = 0; i < _seesTexts.Count; i++)
            {
                bool on = i == _sees;
                _seesTexts[i].color = on ? _kit.Popup : _kit.Light;
                _seesFills[i].color = on ? _kit.Light : VoyageKit.Fade(_kit.Popup, 0.7f);
            }
            VoyageKit.Set(_count, TemplateText.Fill(_lines?.PickCount, "shown", _options.Count.ToString(CultureInfo.InvariantCulture),
                "total", total.ToString(CultureInfo.InvariantCulture)));

            // The grid stands under the Sees row when it shows, and above the saved line.
            float top = BodyY + (sees ? SeesHeight + 18f : 0f);
            VoyageKit.At(_gridView, GridX, top, GridWidth, BodyY + BodyHeight - SavedHeight - SavedGap - top);
            _saved.gameObject.SetActive(!string.IsNullOrEmpty(_savedLine));
            VoyageKit.Set(_savedText, _savedLine);

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
                // One thin line round every tile; the picked one's is cyan.
                cell.Edge.Solid(o.Picked ? _kit.Cyan : VoyageKit.Fade(_kit.Outline, 0.6f));
                cell.Edge.Width = 1f;
                var option = o;
                cell.Button.onClick.RemoveAllListeners();
                cell.Button.onClick.AddListener(() => Take(option));
                if (o.Picked)
                {
                    picked = o;
                    any = true;
                }
            }
            for (int i = _options.Count; i < _cells.Count; i++) _cells[i].Root.gameObject.SetActive(false);
            Preview(any ? picked : default, any);
        }

        // One press saves the pick to this voyage's seat and says so under the grid.
        void Take(VoyageSeats.Option option)
        {
            var squad = Seats();
            bool you = squad != null && VoyageOpenCard.IsMySeat(squad, _seat);
            if (!_rules.PickVoyage(_key, _seat, _kind, option)) return;
            string setting = _kind switch
            {
                VoyageSeats.Vessel => _lines?.PickWordVessel,
                VoyageSeats.Pilot => _lines?.PickWordPilot,
                VoyageSeats.ElementKind => _lines?.PickWordElement,
                _ => _lines?.PickWordArtifact,
            };
            _savedLine = you
                ? TemplateText.Fill(_lines?.PickSaved, "choice", option.Name, "setting", setting, "voyage", _offer?.Title)
                : TemplateText.Fill(_lines?.PickSavedSeat, "choice", option.Name, "setting", setting, "seat", _rules.Role(_seat),
                    "voyage", _offer?.Title);
            Refresh();
        }

        void Preview(VoyageSeats.Option o, bool any)
        {
            VoyageKit.ShowCover(_previewPicture, any ? o.Picture : null, new Vector2(0.88f * PreviewWidth, 220f),
                _kind == VoyageSeats.Pilot ? VoyageKit.FaceFocus : new Vector2(0.5f, 0.5f));
            _previewFlower.parent.gameObject.SetActive(any && _kind == VoyageSeats.ElementKind);
            if (any && _kind == VoyageSeats.ElementKind) Petals(_previewFlower, o.Element);
            VoyageKit.Set(_previewName, any ? o.Name : null);
            // Over the name: a pilot's house, else "For this voyage" when it is one of the voyage's picks.
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
                // A tile: rgba(0,4,31,.7) inside one thin line, its picture over its name (18), "For this voyage" in plain
                // caps 13 at its top left.
                var view = new CellView { Root = VoyageKit.Node("Choice" + _cells.Count, _grid) };
                var (fill, edge) = _kit.Box(view.Root, VoyageKit.Fade(_kit.RowPicked, 0.7f / 0.86f), VoyageKit.Fade(_kit.Outline, 0.6f), hit: true);
                view.Edge = edge;
                view.Button = view.Root.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = fill;
                view.Button.transition = Selectable.Transition.None;
                var pic = VoyageKit.Node("Picture", view.Root);
                VoyageKit.Stretch(pic);
                pic.anchorMin = new Vector2(0.15f, 0f);
                pic.anchorMax = new Vector2(0.85f, 1f);
                pic.offsetMin = new Vector2(0f, 52f);
                pic.offsetMax = new Vector2(0f, -36f);
                view.Picture = VoyageKit.Picture(pic);
                var flower = VoyageKit.Node("Element", pic);
                flower.anchorMin = flower.anchorMax = flower.pivot = new Vector2(0.5f, 0.5f);
                flower.sizeDelta = new Vector2(64f, 64f);
                var petals = VoyageKit.Flower(flower, Element.Charge, _kit.Light);
                VoyageKit.Stretch(petals);
                view.Flower = flower;
                view.Name = _kit.Text(view.Root, 18, _kit.Light, TextAlignmentOptions.Center);
                view.Name.rectTransform.anchorMin = Vector2.zero;
                view.Name.rectTransform.anchorMax = new Vector2(1f, 0f);
                view.Name.rectTransform.pivot = new Vector2(0.5f, 0f);
                view.Name.rectTransform.sizeDelta = new Vector2(-20f, 24f);
                view.Name.rectTransform.anchoredPosition = new Vector2(0f, 18f);
                view.TagRoot = At("Tag", 10f, 10f, 260f, 18f, view.Root);
                view.Tag = _kit.Label(view.TagRoot, 13, ForVoyageColor, TextAlignmentOptions.Left);
                VoyageKit.Stretch(view.Tag.rectTransform);
                view.Tag.characterSpacing = 8f;
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

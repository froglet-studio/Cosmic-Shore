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
    /// The seat picker on the Ark page, as the Hangar line-up draws it (Frogma Rev 115 A, picked by Shombith 2026-11-16;
    /// Docs/MENU_REDESIGN_B.md section 211.1): an 820 x 996 panel that slides in from the right over a light scrim, with
    /// one thin line down its left edge, so the seats stay in view and your seat changes behind it as you pick. "Change
    /// your seat" and Close; the tabs Vessel, Pilot, Element and Artifact, the open one underlined in cyan; the tab's
    /// choices three across (200 x 150 tiles inside one thin line, the picked one cyan, "For this voyage" on the voyage's
    /// own picks; the Sees filter over the pilots); the picked one's name and a line about it; "Your seat now"; and at the
    /// foot what was saved and Done (A).
    /// <para>One press saves the pick to the seat for THIS voyage (<see cref="VoyageSeats.PickVoyage"/> into
    /// <see cref="VoyageSeatRecord"/>), never to a saved squad. Set sail reads the same seats.</para>
    /// </summary>
    public sealed class VoyageSeatPicker
    {
        // Frogma Rev 115 A, in its own units: an 820 x 996 panel at (1100, 84) of the 1920 x 1080 board, under the scrim
        // that starts below the menu bar (y 84).
        static readonly Vector2 Screen = new(1920f, 1080f);
        const float Width = 820f, Height = 996f, PanelX = 1100f, PanelY = 84f, Pad = 40f;
        const float TabsY = 96f, GridY = 160f, GridBottom = 500f, CellWidth = 200f, CellHeight = 150f, CellGap = 16f;
        const float SeesHeight = 36f, NameY = 520f, NowY = 640f, FootHeight = 56f;
        const float ScrimAlpha = 0.45f, OpenSeconds = 0.4f, DimSeconds = 0.3f;

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
        TMP_Text _count, _savedText, _pickedName, _pickedNote, _nowWords;
        readonly List<CellView> _cells = new();
        RectTransform _panel, _nowFlower;
        Image _nowFace;

        VoyageBoard.Offer _offer;
        string _key;
        int _seat, _kind, _sees;
        string _savedLine;
        readonly List<VoyageSeats.Option> _options = new();
        readonly List<VoyageSeats.Option> _scratch = new();

        sealed class TabView
        {
            public TMP_Text Name;
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
            // Nothing behind takes a press while the picker is open; the scrim is light (45%) and starts under the menu bar,
            // so the seats stay seen.
            VoyageKit.Block(_root, VoyageKit.Clear, hit: true);
            // The board's 1920 x 1080 screen, scaled to the screen it opens over; the panel stands where the board drew it.
            _frame = VoyageKit.Node("Frame", _root);
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = new Vector2(0.5f, 0.5f);
            _frame.sizeDelta = Screen;
            var scrim = At("Scrim", 0f, PanelY, Screen.x, Screen.y - PanelY, _frame);
            _scrim = VoyageKit.Block(scrim, VoyageKit.Fade(_kit.Popup, ScrimAlpha));
            _panel = At("Panel", PanelX, PanelY, Width, Height, _frame);
            // A flat panel (#00041F) with one thin line (#434C89) down its left edge.
            var plate = _kit.RowPicked;
            VoyageKit.Block(_panel, new Color(plate.r, plate.g, plate.b, 1f), hit: true);
            VoyageKit.Block(At("Edge", 0f, 0f, 1f, Height, _panel), _kit.Outline);
            var c = _panel;

            _title = _kit.Text(c, 26, _kit.Light, TextAlignmentOptions.Left);
            VoyageKit.At(_title.rectTransform, Pad, 34f, 640f, 34f);
            var close = At("Close", Width - Pad - 44f, 30f, 44f, 44f, c);
            _kit.Square(close, _lines?.PickClose, Close);

            BuildTabs(c);
            BuildSees(c);
            BuildGrid(c);
            BuildPicked(c);
            BuildNow(c);
            BuildFoot(c);

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
            // Vessel, Pilot, Element, Artifact (caps 15), 36 apart; the open one Light and underlined in cyan (2).
            var row = At("Tabs", Pad, TabsY, Width - 2f * Pad, 34f, c);
            var h = VoyageKit.Row(row, 36f, TextAnchor.UpperLeft);
            h.childForceExpandHeight = true;
            h.childForceExpandWidth = false;
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
                var col = VoyageKit.Column(tab, 0f, TextAnchor.UpperLeft);
                col.padding = new RectOffset(0, 0, 0, 10);
                var view = new TabView();
                view.Name = _kit.Label(tab, 15, _kit.Sub);
                view.Name.overflowMode = TextOverflowModes.Overflow;
                VoyageKit.Set(view.Name, names[kind]);
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
            _seesRow = At("Sees", Pad, GridY, Width - 2f * Pad, SeesHeight, c);
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
            // Three across, 200 x 150, 16 apart; more scroll.
            _gridView = At("Choices", Pad, GridY, GridWidth, GridBottom - GridY, c);
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
            g.cellSize = new Vector2(CellWidth, CellHeight);
            g.spacing = new Vector2(CellGap, CellGap);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 3;
            var fit = _grid.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _grid;
            scroll.viewport = _gridView;
            hit.raycastTarget = true;
        }

        /// <summary>The grid's width: three tiles and the two gaps between them.</summary>
        const float GridWidth = 3f * CellWidth + 2f * CellGap;

        void BuildPicked(RectTransform c)
        {
            // The picked one's name (22) over a line about it (15, the quiet light).
            var words = At("Picked", Pad, NameY, Width - 2f * Pad, 110f, c);
            VoyageKit.Column(words, 10f, TextAnchor.UpperLeft);
            _pickedName = _kit.Text(words, 22, _kit.Light, TextAlignmentOptions.Left);
            _pickedNote = _kit.Text(words, 15, ForVoyageColor, TextAlignmentOptions.TopLeft);
            _pickedNote.textWrappingMode = TextWrappingModes.Normal;
            _pickedNote.lineSpacing = 12f;
            VoyageKit.Le(_pickedNote).preferredHeight = 70f;
        }

        void BuildNow(RectTransform c)
        {
            // "Your seat now" (caps 14, Sub) over the seat as it stands: the pilot's face (44 x 52), "vessel - pilot -
            // element - artifact" (16) and the element's flower (26).
            var head = _kit.Label(At("NowHead", Pad, NowY, Width - 2f * Pad, 20f, c), 14, _kit.Sub);
            VoyageKit.Stretch(head.rectTransform);
            VoyageKit.Set(head, _lines?.PickSeatNow);
            var row = At("Now", Pad, NowY + 34f, Width - 2f * Pad, 52f, c);
            var h = VoyageKit.Row(row, 16f, TextAnchor.MiddleLeft);
            h.childForceExpandWidth = false;
            var face = VoyageKit.Node("Face", row);
            var fle = VoyageKit.Le(face);
            fle.preferredWidth = fle.minWidth = 44f;
            fle.preferredHeight = fle.minHeight = 52f;
            VoyageKit.Block(face, VoyageKit.Fade(_kit.Popup, 0.6f));
            _nowFace = VoyageKit.CoverBox(face);
            _nowWords = _kit.Text(row, 16, _kit.Light, TextAlignmentOptions.Left);
            _nowWords.overflowMode = TextOverflowModes.Ellipsis;
            VoyageKit.Le(_nowWords).flexibleWidth = 1f;
            var flowerBox = VoyageKit.Node("Flower", row);
            var ffl = VoyageKit.Le(flowerBox);
            ffl.preferredWidth = ffl.minWidth = ffl.preferredHeight = ffl.minHeight = 26f;
            _nowFlower = VoyageKit.Flower(flowerBox, Element.Charge, _kit.Light);
            VoyageKit.Stretch(_nowFlower);
        }

        void BuildFoot(RectTransform c)
        {
            // What was saved, with a drawn tick, at the left; Done (A) at the right, 40 from the foot.
            BuildSaved(c);
            var done = At("Done", Width - Pad - 220f, Height - Pad - FootHeight, 220f, FootHeight, c);
            _kit.Cta(done, _lines?.BoardSailKey, _lines?.BoardDone, Close);
        }

        void BuildSaved(RectTransform c)
        {
            // Under the grid: a tick and what was saved (17), no bar.
            _saved = At("Saved", Pad, Height - Pad - FootHeight, Width - 2f * Pad - 240f, FootHeight, c);
            VoyageKit.Row(_saved, 14f, TextAnchor.MiddleLeft).padding = new RectOffset(4, 4, 0, 0);
            // The tick, drawn: two 2 unit strokes (the board's 18 x 14 path M1 7l5 5L17 1).
            var tick = VoyageKit.Node("Tick", _saved);
            var tle = VoyageKit.Le(tick);
            tle.preferredWidth = tle.minWidth = 18f;
            tle.preferredHeight = tle.minHeight = 14f;
            Stroke(tick, new Vector2(1f, 7f), new Vector2(6f, 12f));
            Stroke(tick, new Vector2(6f, 12f), new Vector2(17f, 1f));
            _savedText = _kit.Text(_saved, 16, _kit.Light, TextAlignmentOptions.Left);
            _savedText.textWrappingMode = TextWrappingModes.Normal;
            VoyageKit.Le(_savedText).flexibleWidth = 1f;
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

        // The board's quiet light (#B8C0F0): between Sub and Light, from the worn set.
        Color ForVoyageColor => Color.Lerp(_kit.Sub, _kit.Light, 0.6f);

        /// <summary>Scale the board's screen to the screen the picker opens over, and slide the panel in from the right
        /// while the scrim fades up (Frogma's .4s cubic-bezier(.2,.7,.2,1)).</summary>
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
                _frame.localScale = new Vector3(_fitK, _fitK, 1f);
            }
            _openT = Mathf.Min(1f, _openT + dt / OpenSeconds);
            _dimT = Mathf.Min(1f, _dimT + dt / DimSeconds);
            VoyageKit.At(_panel, PanelX + SlideOffset(VoyageKit.Rise(_openT)), PanelY, Width, Height);
            _scrim.color = VoyageKit.Fade(_kit.Popup, ScrimAlpha * Mathf.SmoothStep(0f, 1f, _dimT));
        }

        /// <summary>How far right of its place the panel stands at eased time <paramref name="e"/> (0 off the board's
        /// right edge, 1 in place).</summary>
        public static float SlideOffset(float e) => (1f - Mathf.Clamp01(e)) * Width;

        float _openT = 1f, _dimT = 1f, _fitK = 1f;
        Image _scrim;

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
            bool wasOpen = IsOpen;
            Lift();
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            _fitFor = new Vector2(-1f, -1f);
            // Opening over an open picker (another seat) keeps it in place; a closed one slides in.
            if (!wasOpen) _openT = _dimT = 0f;
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

            for (int k = 0; k < 4; k++)
            {
                bool on = k == _kind;
                _tabs[k].Bar.enabled = on;
                _tabs[k].Name.color = on ? _kit.Light : _kit.Sub;
            }
            DrawNow(slot, vessel, pilot);

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
            float top = GridY + (sees ? SeesHeight + 14f : 0f);
            VoyageKit.At(_gridView, Pad, top, GridWidth, GridBottom - top);
            // What was just saved, else that a press saves to this voyage's seat.
            VoyageKit.Set(_savedText, string.IsNullOrEmpty(_savedLine) ? _lines?.PickSavedHere : _savedLine);

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
            VoyageKit.Set(_pickedName, any ? o.Name : null);
            string note = any ? o.Note : null;
            if (any && _kind == VoyageSeats.Pilot && string.IsNullOrEmpty(note) && _catalog
                && _catalog.TryGetPilot(o.Id, out var callsign) && callsign) note = callsign.Description;
            VoyageKit.Set(_pickedNote, note);
        }

        // "Your seat now": the seat as it stands after every press, the same seat the page behind shows.
        void DrawNow(SquadSlot slot, SO_Vessel vessel, PilotSO pilot)
        {
            VoyageKit.ShowCover(_nowFace, pilot ? pilot.Portrait : null, new Vector2(44f, 52f), VoyageKit.FaceFocus);
            VoyageKit.Set(_nowWords, TemplateText.Fill(_lines?.PickSeatLine,
                "vessel", vessel ? vessel.CardName : null,
                "pilot", VoyageSeats.PilotName(slot, pilot, true),
                "element", _rules.ElementName(slot.Element),
                "artifact", _rules.ArtifactName(slot, vessel, true)));
            bool flower = slot.Element != Element.None && slot.Element != Element.Omni;
            _nowFlower.parent.gameObject.SetActive(flower);
            if (flower) Petals(_nowFlower, slot.Element);
        }

        CellView CellFor(int index)
        {
            while (_cells.Count <= index)
            {
                // A 200 x 150 tile, rgba(0,4,31,.7) inside one thin line: its picture over its name (16), "For this voyage"
                // in plain caps at its top left.
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
                pic.anchorMin = new Vector2(0.1f, 0f);
                pic.anchorMax = new Vector2(0.9f, 1f);
                pic.offsetMin = new Vector2(0f, 40f);
                pic.offsetMax = new Vector2(0f, -26f);
                view.Picture = VoyageKit.Picture(pic);
                var flower = VoyageKit.Node("Element", pic);
                flower.anchorMin = flower.anchorMax = flower.pivot = new Vector2(0.5f, 0.5f);
                flower.sizeDelta = new Vector2(56f, 56f);
                var petals = VoyageKit.Flower(flower, Element.Charge, _kit.Light);
                VoyageKit.Stretch(petals);
                view.Flower = flower;
                view.Name = _kit.Text(view.Root, 16, _kit.Light, TextAlignmentOptions.Center);
                view.Name.rectTransform.anchorMin = Vector2.zero;
                view.Name.rectTransform.anchorMax = new Vector2(1f, 0f);
                view.Name.rectTransform.pivot = new Vector2(0.5f, 0f);
                view.Name.rectTransform.sizeDelta = new Vector2(-20f, 24f);
                view.Name.rectTransform.anchoredPosition = new Vector2(0f, 12f);
                view.TagRoot = At("Tag", 8f, 8f, CellWidth - 16f, 16f, view.Root);
                view.Tag = _kit.Label(view.TagRoot, 11, ForVoyageColor, TextAlignmentOptions.Left);
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

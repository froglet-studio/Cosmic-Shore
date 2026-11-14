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
        const float Width = 1240f, Height = 780f, PreviewWidth = 300f, Cell = 140f;

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

        readonly RectTransform _root;
        RectTransform _overlay;
        TMP_Text _title;
        readonly TabView[] _tabs = new TabView[4];
        readonly KitView[] _kits = new KitView[2];
        RectTransform _seesRow, _grid;
        readonly List<Button> _seesButtons = new();
        readonly List<TMP_Text> _seesTexts = new();
        TMP_Text _count;
        readonly List<CellView> _cells = new();
        Image _previewPicture;
        RectTransform _previewFlower;
        TMP_Text _previewName, _previewNote, _previewTag;

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
            public TMP_Text Name, Who;
        }

        sealed class CellView
        {
            public RectTransform Root;
            public Button Button;
            public Image Picture, Edge;
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
            var panel = VoyageKit.Node("Panel", _root);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(Width, Height);
            var glass = _kit.Glass(panel, hit: true);
            var solid = _kit.Popup;
            solid.a = 1f;
            glass.Fill.color = solid; // a window over the page: nothing behind reads through
            glass.Pick(_kit, true);
            var c = glass.Content;

            _title = _kit.Text(c, 24, _kit.Light, TextAlignmentOptions.Left);
            Place(_title.rectTransform, 32f, 24f, 600f, 36f);
            var close = VoyageKit.Node("Close", c);
            close.anchorMin = close.anchorMax = close.pivot = Vector2.one;
            close.sizeDelta = new Vector2(44f, 44f);
            close.anchoredPosition = new Vector2(-24f, -20f);
            _kit.Square(close, _lines?.PickClose, Close);

            BuildTabs(c);
            BuildKits(c);
            BuildSees(c);
            BuildGrid(c);
            BuildPreview(c);

            var note = _kit.Text(c, 15, _kit.Sub, TextAlignmentOptions.Left);
            note.rectTransform.anchorMin = note.rectTransform.anchorMax = note.rectTransform.pivot = Vector2.zero;
            note.rectTransform.sizeDelta = new Vector2(700f, 44f);
            note.rectTransform.anchoredPosition = new Vector2(32f, 22f);
            VoyageKit.Set(note, _lines?.PickNote);
            var done = VoyageKit.Node("Done", c);
            done.anchorMin = done.anchorMax = done.pivot = new Vector2(1f, 0f);
            done.sizeDelta = new Vector2(220f, 48f);
            done.anchoredPosition = new Vector2(-32f, 20f);
            _kit.Quiet(done, _lines?.BoardDoneKey, _lines?.BoardDone, Close);

            _root.gameObject.SetActive(false);
        }

        // Top-left placement inside the panel.
        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        void BuildTabs(RectTransform c)
        {
            var row = VoyageKit.Node("Tabs", c);
            Place(row, 32f, 76f, Width - 64f, 64f);
            VoyageKit.Row(row, 12f, TextAnchor.MiddleLeft, expandWidth: true);
            string[] names = { _lines?.CardSettingVessel, _lines?.CardSettingPilot, _lines?.CardSettingElement, _lines?.CardSettingArtifact };
            for (int kind = 0; kind < 4; kind++)
            {
                int k = kind;
                var tab = VoyageKit.Node("Tab" + kind, row);
                VoyageKit.Le(tab).preferredHeight = 64f;
                var hit = VoyageKit.Block(tab, VoyageKit.Fade(_kit.Popup, 0.5f), hit: true);
                var b = tab.gameObject.AddComponent<Button>();
                b.targetGraphic = hit;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => ShowKind(k));
                var view = new TabView();
                var words = VoyageKit.Node("Words", tab);
                VoyageKit.Stretch(words);
                words.offsetMin = new Vector2(16f, 8f);
                words.offsetMax = new Vector2(-16f, -8f);
                VoyageKit.Column(words, 2f, TextAnchor.MiddleLeft);
                view.Name = _kit.Label(words, 13, _kit.Sub);
                VoyageKit.Set(view.Name, names[kind]);
                view.Value = _kit.Text(words, 17, _kit.Light, TextAlignmentOptions.Left);
                var bar = VoyageKit.Node("Bar", tab);
                VoyageKit.Foot(bar, 0f, 2f);
                view.Bar = VoyageKit.Block(bar, _kit.Cyan);
                _tabs[kind] = view;
            }
        }

        void BuildKits(RectTransform c)
        {
            var label = _kit.Label(c, 13, _kit.Sub);
            Place(label.rectTransform, 32f, 156f, 600f, 20f);
            VoyageKit.Set(label, _lines?.PickKits);
            var row = VoyageKit.Node("Kits", c);
            Place(row, 32f, 182f, Width - 64f, 60f);
            VoyageKit.Row(row, 12f, TextAnchor.MiddleLeft, expandWidth: true);
            string[] names = { _lines?.PickKitVoyage, _lines?.PickKitLast };
            for (int i = 0; i < 2; i++)
            {
                int which = i;
                var view = new KitView { Root = VoyageKit.Node("Kit" + i, row) };
                VoyageKit.Le(view.Root).preferredHeight = 60f;
                var glass = _kit.Glass(view.Root);
                view.Button = view.Root.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = glass.Fill;
                view.Button.onClick.AddListener(() => TakeKit(which));
                var words = VoyageKit.Node("Words", glass.Content);
                VoyageKit.Stretch(words);
                words.offsetMin = new Vector2(16f, 6f);
                words.offsetMax = new Vector2(-16f, -6f);
                VoyageKit.Column(words, 2f, TextAnchor.MiddleLeft);
                view.Name = _kit.Label(words, 13, _kit.Light);
                VoyageKit.Set(view.Name, names[i]);
                view.Who = _kit.Text(words, 15, _kit.Sub, TextAlignmentOptions.Left);
                _kits[i] = view;
            }
        }

        void BuildSees(RectTransform c)
        {
            _seesRow = VoyageKit.Node("Sees", c);
            Place(_seesRow, 32f, 258f, Width - 64f - PreviewWidth - 24f, 36f);
            VoyageKit.Row(_seesRow, 10f);
            var label = _kit.Label(_seesRow, 13, _kit.Sub);
            label.overflowMode = TextOverflowModes.Overflow;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            VoyageKit.Set(label, _lines?.PickSees);
            VoyageKit.Le(label.rectTransform).minWidth = 64f;
            string[] names = { _lines?.PickSeesAll, _lines?.PickSeesCrystals, _lines?.PickSeesPrisms, _lines?.PickSeesLife,
                _lines?.PickSeesVessels, _lines?.PickSeesPaths };
            for (int i = 0; i < names.Length; i++)
            {
                int group = i;
                var chip = VoyageKit.Node("Chip" + i, _seesRow);
                var le = VoyageKit.Le(chip);
                le.preferredHeight = 34f;
                le.preferredWidth = 104f;
                var b = _kit.Quiet(chip, null, names[i], () => { _sees = group; Refresh(); }, out var text);
                text.fontSize = 13;
                _seesButtons.Add(b);
                _seesTexts.Add(text);
            }
            _count = _kit.Text(_seesRow, 14, _kit.Sub, TextAlignmentOptions.Right);
            VoyageKit.Le(_count).flexibleWidth = 1f;
        }

        void BuildGrid(RectTransform c)
        {
            var view = VoyageKit.Node("Choices", c);
            Place(view, 32f, 306f, Width - 64f - PreviewWidth - 24f, Height - 306f - 84f);
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
            g.cellSize = new Vector2(Cell, Cell + 24f);
            g.spacing = new Vector2(12f, 12f);
            var fit = _grid.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _grid;
            scroll.viewport = view;
            hit.raycastTarget = true;
        }

        void BuildPreview(RectTransform c)
        {
            var rt = VoyageKit.Node("Preview", c);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one;
            rt.sizeDelta = new Vector2(PreviewWidth, Height - 258f - 84f);
            rt.anchoredPosition = new Vector2(-32f, -258f);
            var glass = _kit.Glass(rt, hit: false);
            var pic = VoyageKit.Node("Picture", glass.Content);
            VoyageKit.Anchor(pic, 0f, 0.45f, 1f, 1f);
            pic.offsetMin = new Vector2(20f, 0f);
            pic.offsetMax = new Vector2(-20f, -20f);
            _previewPicture = VoyageKit.Picture(pic);
            var flower = VoyageKit.Node("Element", pic);
            flower.anchorMin = flower.anchorMax = flower.pivot = new Vector2(0.5f, 0.5f);
            flower.sizeDelta = new Vector2(96f, 96f);
            _previewFlower = VoyageKit.Flower(flower, Element.Charge, _kit.Light);
            VoyageKit.Stretch(_previewFlower);
            var words = VoyageKit.Node("Words", glass.Content);
            VoyageKit.Anchor(words, 0f, 0f, 1f, 0.45f);
            words.offsetMin = new Vector2(20f, 16f);
            words.offsetMax = new Vector2(-20f, -8f);
            VoyageKit.Column(words, 6f, TextAnchor.UpperLeft);
            _previewTag = _kit.Label(words, 12, _kit.Jade);
            _previewName = _kit.Text(words, 22, _kit.Light, TextAlignmentOptions.Left);
            _previewNote = _kit.Text(words, 15, _kit.Sub, TextAlignmentOptions.TopLeft);
            _previewNote.textWrappingMode = TextWrappingModes.Normal;
            VoyageKit.Le(_previewNote).flexibleHeight = 1f;
        }

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
            for (int i = 0; i < _seesTexts.Count; i++) _seesTexts[i].color = i == _sees ? _kit.Light : _kit.Sub;
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
                VoyageKit.Set(cell.Tag, ForVoyage(o) ? _lines?.PickForVoyage : null);
                if (cell.Edge) cell.Edge.color = o.Picked ? _kit.Cyan : _kit.Outline;
                cell.Name.color = o.Picked ? _kit.Light : _kit.Sub;
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
            VoyageKit.Show(_previewPicture, any ? o.Picture : null);
            _previewFlower.parent.gameObject.SetActive(any && _kind == VoyageSeats.ElementKind);
            if (any && _kind == VoyageSeats.ElementKind) Petals(_previewFlower, o.Element);
            VoyageKit.Set(_previewName, any ? o.Name : null);
            VoyageKit.Set(_previewTag, any && ForVoyage(o) ? _lines?.PickForVoyage : null);
            string note = any ? o.Note : null;
            if (any && _kind == VoyageSeats.Pilot && string.IsNullOrEmpty(note) && _catalog
                && _catalog.TryGetPilot(o.Id, out var callsign) && callsign) note = callsign.Description;
            VoyageKit.Set(_previewNote, note);
        }

        CellView CellFor(int index)
        {
            while (_cells.Count <= index)
            {
                var view = new CellView { Root = VoyageKit.Node("Choice" + _cells.Count, _grid) };
                view.Button = _kit.Plated(view.Root, VoyageKit.Fade(_kit.Popup, 0.6f), true, out _, out view.Edge);
                var pic = VoyageKit.Node("Picture", view.Root);
                VoyageKit.Stretch(pic);
                pic.offsetMin = new Vector2(10f, 40f);
                pic.offsetMax = new Vector2(-10f, -22f);
                view.Picture = VoyageKit.Picture(pic);
                var flower = VoyageKit.Node("Element", pic);
                flower.anchorMin = flower.anchorMax = flower.pivot = new Vector2(0.5f, 0.5f);
                flower.sizeDelta = new Vector2(56f, 56f);
                var petals = VoyageKit.Flower(flower, Element.Charge, _kit.Light);
                VoyageKit.Stretch(petals);
                view.Flower = flower;
                view.Name = _kit.Text(view.Root, 14, _kit.Light, TextAlignmentOptions.Center);
                var n = view.Name.rectTransform;
                VoyageKit.Foot(n, 8f, 28f);
                view.Tag = _kit.Label(view.Root, 10, _kit.Jade, TextAlignmentOptions.Center);
                VoyageKit.Top(view.Tag.rectTransform, 6f, 16f);
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

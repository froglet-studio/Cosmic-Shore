using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Data;
using TMPro;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Voyages page's first step, the HyperSea, built to Frogma Rev 111 Full's numbers (the final cut; Docs/MENU_REDESIGN_B.md
    /// sections 203 and 205).
    /// <list type="bullet">
    /// <item><b>The table</b>: the window's live map lying back at 50 degrees (<see cref="VoyageTableTilt"/>), 1216 wide
    /// at its front edge and 444 deep as seen, its rim drawn, the side view (<see cref="VoyageEdgeGraphic"/>) as its front
    /// face, 96 tall. The map prints no names and no caption of its own (<see cref="VoyageMapGraphic.Unnamed"/>): each
    /// voyage stands on it as a pin (a stalk as tall as its difficulty, a ring, the place's name from Sector inwards), the
    /// picked one in cyan, home an amber diamond. Drag pans, the wheel zooms, a press on a pin picks its voyage.</item>
    /// <item><b>The zoom row</b> above it: Region, Sector, Arm and Galaxy with their widths, + and -; the tier on show is
    /// underlined.</item>
    /// <item><b>Full screen</b>: the table lies flat and grows to a borderless full-screen map in 0.9 s; the zoom row rides
    /// into its top-left corner and gains Neighbourhood and Cell, the crew stays on the right, the picked voyage moves to
    /// the foot, and Back from full screen takes it home.</item>
    /// <item><b>Open now</b> (Frogma Rev 113 Sea, Milestone 1): the voyages that sail now as cards down the right, the
    /// picked one marked by one thin cyan line; under them "Crew recommends · N", which opens the crew popup
    /// (<see cref="VoyageCrewPopup"/>), and a line saying how many crew voyages are locked. It replaced Rev 111's column of
    /// crew faces and their sliding drawer.</item>
    /// <item><b>The picked voyage</b>: its kind and pilot, its name, its difficulty pips and pay, and Open voyage.</item>
    /// </list>
    /// Every number is the board's: the page is laid out in a frame of the board's own units (1840 x 748 in the window,
    /// 1920 x 1080 in full screen) scaled evenly to the room it has (<see cref="Layout"/>).
    /// </summary>
    public sealed class VoyageSeaPage
    {
        const float FullSeconds = 0.9f;
        // Frogma Rev 111 Full, in its own units: the page in the window (x 40-1880, y 172-920 of the 1920 x 1080 board) and
        // the full screen.
        static readonly Vector2 WindowSize = new(1840f, 748f), ScreenSize = new(1920f, 1080f);
        const float RowHeight = 44f, BandHeight = 40f, StepSize = 44f, TierWidth = 178f, DeepTierWidth = 236f;
        // Rev 113 Sea's "Open now" column: x 1400, y 176 of the board (1360, 4 here), 480 wide; cards 140 tall, 14 apart.
        const float SideX = 1360f, SideY = 4f, SideWidth = 480f, SideLabel = 20f, SideGap = 14f, OpenCardHeight = 140f,
            ButtonHeight = 56f, ButtonLift = 10f, LockedLine = 20f;
        const float TableX = 112f, TableY = 64f, TableWidth = 1216f, TableHeight = 540f, EdgeHeight = 96f;
        const float FullButtonX = 988f, FullButtonWidth = 220f, BackFullWidth = 320f, Margin = 40f;
        const float CardX = 860f, CardY = 628f, CardWidth = 860f, CardFullWidth = 1180f, CardHeight = 108f;
        // The tiers the zoom row offers, narrow to wide (HyperSeaSky.Tiers): the first two only in full screen.
        static readonly int[] RowTiers = { 5, 4, 3, 2, 1, 0 };
        const int DeepTiers = 2;
        // Pins: names show from Sector inwards; past Region they stand down (only the picked one stays at Region).
        const int NamedFrom = 2, PinsTo = 3;

        readonly VoyageBoard _board;
        readonly VoyageKit _kit;
        readonly VoyageLines _lines;

        public RectTransform Root { get; }
        readonly RectTransform _slot;
        readonly RectTransform _frame;
        RectTransform _table, _column, _mapHost, _edgeHost, _controls, _band, _side, _card, _fullButton, _backFull,
            _crewButton, _lockedRow, _message, _pins;
        CanvasGroup _edgeGroup, _fullGroup, _backGroup;
        VoyageTableTilt _tilt;
        VoyageTableRim _rim;
        VoyageMapGraphic _map;
        TMP_Text _messageText, _crewButtonText, _lockedText;
        Image _lockedLock;

        // The full-screen layer, made on the first full screen: its own canvas over everything in the root canvas.
        RectTransform _overlay;
        Image _overlayFill;
        bool _full;
        float _fullT;
        Vector2 _laidFor = new(-1f, -1f);
        float _laidFull = -1f;
        float _e;

        readonly List<TierView> _tiers = new();
        readonly List<OpenView> _cards = new();
        readonly List<int> _openNow = new();
        readonly List<PinView> _pinViews = new();
        readonly List<VoyageBoard.Offer> _offers = new();
        int _open = -1;
        RectTransform _home;

        // The picked voyage's card.
        TMP_Text _cardKicker, _cardTitle, _cardDifficulty, _cardPay;
        VoyagePips _cardPips;
        RectTransform _cardFlower;
        Button _openButton;

        sealed class TierView
        {
            public int Tier;
            public float Width;
            public RectTransform Root;
            public CanvasGroup Group;
            public TMP_Text Name, Scale;
            public Image Bar;
        }

        sealed class OpenView
        {
            public RectTransform Root;
            public Image Ground, Line, Picture;
            public VoyageOutline Edge;
            public TMP_Text Kind, Title, Difficulty, Meta;
            public VoyagePips Pips;
            public Button Button;
        }

        sealed class PinView
        {
            public RectTransform Root, Stalk, Glow;
            public VoyageRing Ring;
            public TMP_Text Name;
        }

        public bool IsFull => _full || _fullT > 0f;

        /// <summary>The window's map, once it is lent to the table.</summary>
        public VoyageMapGraphic Map => _map;

        public VoyageSeaPage(VoyageBoard board, VoyageKit kit, RectTransform pages)
        {
            _board = board;
            _kit = kit;
            _lines = kit.Lines;
            _slot = pages;
            Root = VoyageKit.Node("HyperSea", pages);
            VoyageKit.Stretch(Root);
            _frame = VoyageKit.Node("Frame", Root);
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = Vector2.zero;

            _table = Node("Table");
            _pins = Node("Pins");
            _message = Node("Message");
            _messageText = _kit.Text(_message, 18, _kit.Sub, TextAlignmentOptions.Center);
            VoyageKit.Stretch(_messageText.rectTransform);
            _messageText.textWrappingMode = TextWrappingModes.Normal;

            BuildHome();
            BuildControls();
            BuildCard();
            BuildSide();
        }

        RectTransform Node(string name)
        {
            var rt = VoyageKit.Node(name, _frame);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            return rt;
        }

        static CanvasGroup Group(RectTransform rt) =>
            rt.TryGetComponent<CanvasGroup>(out var g) ? g : rt.gameObject.AddComponent<CanvasGroup>();

        // ── Building ─────────────────────────────────────────────────────────

        void BuildControls()
        {
            _controls = Node("Zoom");
            // The tiers stand on one dark band, 40 tall (rgba(0,1,10,.8)).
            _band = VoyageKit.Node("Band", _controls);
            _band.anchorMin = _band.anchorMax = _band.pivot = Vector2.zero;
            VoyageKit.Block(_band, VoyageKit.Fade(_kit.Popup, 0.8f));
            string[] names = { _lines?.MapTierCell, _lines?.MapTierNeighbourhood, _lines?.MapTierRegion, _lines?.MapTierSector,
                _lines?.MapTierArm, _lines?.MapTierGalaxy };
            for (int i = 0; i < RowTiers.Length; i++)
            {
                var view = new TierView { Tier = RowTiers[i] };
                view.Root = VoyageKit.Node("Tier" + view.Tier, _controls);
                view.Root.anchorMin = view.Root.anchorMax = view.Root.pivot = Vector2.zero;
                view.Group = Group(view.Root);
                var hit = VoyageKit.Block(view.Root, VoyageKit.Clear, hit: true);
                var button = view.Root.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                int tier = view.Tier;
                button.onClick.AddListener(() => { if (_map) _map.ZoomToTier(tier); });
                view.Name = _kit.Label(view.Root, 14, _kit.Sub, TextAlignmentOptions.Left);
                VoyageKit.Stretch(view.Name.rectTransform);
                view.Name.rectTransform.offsetMin = new Vector2(22f, 0f);
                VoyageKit.Set(view.Name, names[i]);
                view.Scale = _kit.Text(view.Root, 14, _kit.Sub, TextAlignmentOptions.Right);
                VoyageKit.Stretch(view.Scale.rectTransform);
                view.Scale.rectTransform.offsetMax = new Vector2(-18f, 0f);
                view.Width = (names[i] ?? string.Empty).Length > 9 ? DeepTierWidth : TierWidth;
                double ly = HyperSeaSky.Tiers[view.Tier].width;
                VoyageKit.Set(view.Scale, TemplateText.Fill(_lines?.SeaTierWidth, "ly", ly.ToString("N0", CultureInfo.InvariantCulture)));
                var bar = VoyageKit.Node("Bar", view.Root);
                VoyageKit.Foot(bar, 0f, 2f);
                view.Bar = VoyageKit.Block(bar, _kit.Cyan);
                _tiers.Add(view);
            }
            Step(_lines?.BoardZoomIn, 1.6f, "In");
            Step(_lines?.BoardZoomOut, 1f / 1.6f, "Out");

            // The board's small soft buttons: 44 tall, their words in 14.
            _fullButton = Node("FullScreen");
            _fullGroup = Group(_fullButton);
            _kit.Quiet(_fullButton, null, _lines?.SeaFullScreen, () => SetFull(true), out var fullWords);
            if (fullWords) fullWords.fontSize = 14f;
            _backFull = Node("BackFromFullScreen");
            _backGroup = Group(_backFull);
            _kit.Quiet(_backFull, _lines?.PageBackKey, _lines?.SeaFullScreenBack, () => SetFull(false), out var backWords);
            if (backWords) backWords.fontSize = 14f;

        }

        void Step(string mark, float factor, string name)
        {
            var rt = VoyageKit.Node("Zoom" + name, _controls);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            _kit.Square(rt, mark, () => { if (_map) _map.ZoomStep(factor); });
        }

        void BuildSide()
        {
            // "OPEN NOW" (caps 14, Sub) over the cards; the crew button and the locked line under them.
            _side = Node("OpenNow");
            var label = _kit.Label(_side, 14, _kit.Sub);
            VoyageKit.At(label.rectTransform, 0f, 0f, SideWidth, SideLabel);
            VoyageKit.Set(label, _lines?.SeaOpenNow);
            _crewButton = VoyageKit.Node("CrewRecommends", _side);
            _kit.Quiet(_crewButton, _lines?.SeaCrewKey, null, () => _board.OpenCrew(), out _crewButtonText);
            _lockedRow = VoyageKit.Node("Locked", _side);
            VoyageKit.Row(_lockedRow, 8f);
            var lockBox = VoyageKit.Node("Lock", _lockedRow);
            var le = VoyageKit.Le(lockBox);
            le.preferredWidth = le.minWidth = le.preferredHeight = 14f;
            _lockedLock = lockBox.gameObject.AddComponent<Image>();
            _lockedLock.preserveAspect = true;
            _lockedLock.raycastTarget = false;
            _lockedText = _kit.Text(_lockedRow, 14, _kit.Sub, TextAlignmentOptions.Left);
            VoyageKit.Le(_lockedText).flexibleWidth = 1f;
        }

        OpenView CardFor(int index)
        {
            while (_cards.Count <= index)
            {
                // A card 480 x 140, 18 in: its picture (110 x 80), its kind (caps 14, Light), its name (18) and its
                // difficulty and length (14, Sub). Picked: a faint cyan ground and one 2 unit cyan line at its left;
                // otherwise rgba(0,4,31,.6) inside a 1 unit outline at .5.
                var view = new OpenView { Root = VoyageKit.Node("Voyage" + _cards.Count, _side) };
                view.Ground = VoyageKit.Block(view.Root, VoyageKit.Fade(_kit.Popup, 0.6f), hit: true);
                var edge = VoyageKit.Node("Edge", view.Root);
                VoyageKit.Stretch(edge);
                view.Edge = edge.gameObject.AddComponent<VoyageOutline>();
                var line = VoyageKit.Node("Line", view.Root);
                line.anchorMin = Vector2.zero;
                line.anchorMax = new Vector2(0f, 1f);
                line.pivot = new Vector2(0f, 0.5f);
                line.sizeDelta = new Vector2(2f, 0f);
                view.Line = VoyageKit.Block(line, _kit.Cyan);
                view.Button = view.Root.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = view.Ground;
                view.Button.transition = Selectable.Transition.None;
                var pic = VoyageKit.Node("Picture", view.Root);
                VoyageKit.At(pic, 18f, 30f, 110f, 80f);
                view.Picture = VoyageKit.CoverBox(pic);
                var words = VoyageKit.Node("Words", view.Root);
                VoyageKit.Stretch(words);
                words.offsetMin = new Vector2(18f + 110f + 20f, 14f);
                words.offsetMax = new Vector2(-18f, -14f);
                VoyageKit.Column(words, 10f, TextAnchor.MiddleLeft);
                view.Kind = _kit.Label(words, 14, _kit.Light);
                view.Title = _kit.Text(words, 18, _kit.Light, TextAlignmentOptions.Left);
                var meta = VoyageKit.Node("Meta", words);
                VoyageKit.Row(meta, 14f);
                VoyageKit.Le(meta).preferredHeight = 18f;
                view.Pips = _kit.Pips(meta);
                view.Difficulty = _kit.Text(meta, 14, _kit.Sub, TextAlignmentOptions.Left);
                VoyageKit.Le(view.Difficulty).minWidth = VoyageKit.Le(view.Difficulty).preferredWidth = 16f;
                view.Meta = _kit.Text(meta, 14, _kit.Sub, TextAlignmentOptions.Left);
                VoyageKit.Le(view.Meta).flexibleWidth = 1f;
                _cards.Add(view);
            }
            return _cards[index];
        }

        void BuildCard()
        {
            // The picked voyage: a dark panel (rgba(0,4,31,.86), outline rgba(67,76,137,.7)), 18 by 24 in.
            _card = Node("PickedVoyage");
            _kit.Box(_card, VoyageKit.Fade(_kit.Popup, 0.86f), VoyageKit.Fade(_kit.Outline, 0.7f));
            var words = VoyageKit.Node("Words", _card);
            VoyageKit.Stretch(words);
            words.offsetMin = new Vector2(24f, 18f);
            words.offsetMax = new Vector2(-24f - 300f - 28f, -18f);
            VoyageKit.Column(words, 8f, TextAnchor.MiddleLeft);
            _cardKicker = _kit.Label(words, 14, _kit.Light);
            _cardTitle = _kit.Text(words, 20, _kit.Light, TextAlignmentOptions.Left);
            (_cardPips, _cardDifficulty, _cardFlower, _cardPay) = Meta(words);

            var cta = VoyageKit.Node("OpenVoyage", _card);
            cta.anchorMin = cta.anchorMax = new Vector2(1f, 0.5f);
            cta.pivot = new Vector2(1f, 0.5f);
            cta.sizeDelta = new Vector2(300f, 56f);
            cta.anchoredPosition = new Vector2(-24f, 0f);
            (_openButton, _) = _kit.Cta(cta, _lines?.BoardSailKey, _lines?.SeaOpenVoyage,
                () => _board.ShowPage(VoyageBoard.Page.Voyage));
        }

        /// <summary>The board's meta line: pips, the difficulty, the element's flower (18) and the pay, 14 apart in Sub.</summary>
        (VoyagePips, TMP_Text, RectTransform, TMP_Text) Meta(RectTransform parent)
        {
            var meta = VoyageKit.Node("Meta", parent);
            VoyageKit.Row(meta, 14f);
            VoyageKit.Le(meta).preferredHeight = 18f;
            var pips = _kit.Pips(meta);
            var difficulty = _kit.Text(meta, 14, _kit.Sub, TextAlignmentOptions.Left);
            VoyageKit.Le(difficulty).minWidth = VoyageKit.Le(difficulty).preferredWidth = 16f;
            var flowerBox = VoyageKit.Node("Element", meta);
            var fle = VoyageKit.Le(flowerBox);
            fle.preferredWidth = fle.minWidth = fle.preferredHeight = fle.minHeight = 18f;
            var flower = VoyageKit.Flower(flowerBox, Element.Charge, _kit.Light);
            VoyageKit.Stretch(flower);
            var pay = _kit.Text(meta, 14, _kit.Sub, TextAlignmentOptions.Left);
            VoyageKit.Le(pay).flexibleWidth = 1f;
            return (pips, difficulty, flower, pay);
        }

        void BuildHome()
        {
            // Home (where the Ark is moored): an 18 unit amber diamond with a 2 unit line.
            _home = VoyageKit.Node("Home", _pins);
            _home.anchorMin = _home.anchorMax = Vector2.zero;
            _home.pivot = new Vector2(0.5f, 0.5f);
            _home.sizeDelta = new Vector2(18f, 18f);
            var mark = _home.gameObject.AddComponent<VoyageRing>();
            mark.Diamond = true;
            mark.Width = 2f;
            mark.Set(ColorTrace.Retrace(ColorTrace.Ui, VoyageKit.ArkAuthored), VoyageKit.Clear);
            _home.gameObject.SetActive(false);
        }

        // ── The map ──────────────────────────────────────────────────────────

        /// <summary>Take the window's map column into the table: the map over its side view.</summary>
        public void Adopt(RectTransform column, RectTransform mapHost, RectTransform edgeHost)
        {
            if (!column || _column == column) return;
            _column = column;
            _mapHost = mapHost;
            _edgeHost = edgeHost;
            column.SetParent(_table, false);
            VoyageKit.Stretch(column);
            column.SetAsFirstSibling();
            foreach (var rt in new[] { mapHost, edgeHost })
            {
                if (!rt) continue;
                rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            }
            if (edgeHost) _edgeGroup = Group(edgeHost);
            Uncaption();
            _laidFor = new Vector2(-1f, -1f);
        }

        // The table's front face is the side view alone (Rev 111 Full): no caption line across it.
        void Uncaption()
        {
            if (!_edgeHost) return;
            foreach (var edge in _edgeHost.GetComponentsInChildren<VoyageEdgeGraphic>(true)) edge.Uncaptioned = true;
        }

        void Bind(VoyageMapGraphic map)
        {
            if (!map || map == _map) return;
            _map = map;
            if (!map.TryGetComponent(out _tilt)) _tilt = map.gameObject.AddComponent<VoyageTableTilt>();
            map.Tilt = _tilt;
            map.Plain = true;
            map.Unnamed = true;
            // The table zooms no closer than Region: the closer tiers cost the frame rate (2 fps, Shombith 2026-11-14).
            map.NarrowestLy = (float)HyperSeaSky.Tiers[PinsTo].width;
            // The table's rim, drawn over the map at the same tilt.
            var rim = VoyageKit.Node("Rim", map.transform);
            VoyageKit.Stretch(rim);
            _rim = rim.gameObject.AddComponent<VoyageTableRim>();
            _rim.Map = map;
            _rim.Width = 1.5f;
            _rim.color = VoyageKit.Fade(_kit.Light, 0.35f);
            _laidFor = new Vector2(-1f, -1f);
        }

        // ── Showing ──────────────────────────────────────────────────────────

        public void Show(IReadOnlyList<VoyageBoard.Offer> offers, int open, VoyageMapGraphic map, string message)
        {
            _offers.Clear();
            if (offers != null) _offers.AddRange(offers);
            _open = open;
            if (map) Bind(map);
            if (_map)
            {
                _map.Plain = true;
                _map.Unnamed = true;
            }
            Uncaption();
            VoyageKit.Set(_messageText, _offers.Count == 0 ? message : null);
            _message.gameObject.SetActive(_offers.Count == 0 && !string.IsNullOrEmpty(message));
            DrawSide();
            _laidFor = new Vector2(-1f, -1f);
            Redraw();
        }

        /// <summary>Draw the picked voyage's card again (its difficulty or pay moved).</summary>
        public void Redraw()
        {
            var offer = _open >= 0 && _open < _offers.Count ? _offers[_open] : null;
            _card.gameObject.SetActive(offer != null);
            if (offer == null) return;
            int d = _board.Difficulty;
            VoyageKit.Set(_cardKicker, string.IsNullOrEmpty(offer.Pilot) ? offer.Kind
                : TemplateText.Fill(_lines?.SeaOfferKicker, "kind", offer.Kind, "pilot", offer.Pilot));
            VoyageKit.Set(_cardTitle, offer.Title);
            ShowMeta(offer, d, _cardPips, _cardDifficulty, _cardFlower, _cardPay);
            _openButton.interactable = true;
            for (int i = 0; i < _openNow.Count && i < _cards.Count; i++)
            {
                int index = _openNow[i];
                int shown = index == _open ? d : _offers[index].Recommended;
                _cards[i].Pips.Lit = shown;
                VoyageKit.Set(_cards[i].Difficulty, shown.ToString(CultureInfo.InvariantCulture));
            }
        }

        void ShowMeta(VoyageBoard.Offer offer, int d, VoyagePips pips, TMP_Text difficulty, RectTransform flower, TMP_Text pay)
        {
            pips.Lit = d;
            VoyageKit.Set(difficulty, d.ToString(CultureInfo.InvariantCulture));
            flower.parent.gameObject.SetActive(offer.Pays);
            if (offer.Pays) Petals(flower, offer.Element);
            VoyageKit.Set(pay, offer.Pays
                ? TemplateText.Fill(_lines?.SeaOfferPay, "pay", VoyageBoard.PayAt(offer, d).ToString(CultureInfo.InvariantCulture),
                    "element", _board.ElementName(offer.Element))
                : null);
        }

        /// <summary>Swap a flower's petals to <paramref name="element"/>'s.</summary>
        internal static void Petals(RectTransform flower, Element element)
        {
            var sprite = Resources.Load<Sprite>("ElementPetals/" + element.ToString().ToLowerInvariant() + "_petal");
            int i = 0;
            foreach (var img in flower.GetComponentsInChildren<Image>(true))
            {
                ElementalBarsView.ConfigurePetal(img, sprite, i++);
                img.enabled = sprite;
            }
        }

        /// <summary>The "Open now" cards (the voyages that sail now), the crew button with how many the crew recommends,
        /// and the locked line (Rev 113 Sea).</summary>
        void DrawSide()
        {
            VoyageCrewRules.OpenNow(_offers, _openNow);
            for (int i = 0; i < _openNow.Count; i++)
            {
                int index = _openNow[i];
                var offer = _offers[index];
                var view = CardFor(i);
                view.Root.gameObject.SetActive(true);
                bool on = index == _open;
                var picture = offer.Portrait ? offer.Portrait : offer.Picture;
                VoyageKit.ShowCover(view.Picture, picture, new Vector2(110f, 80f),
                    offer.Portrait ? VoyageKit.FaceFocus : new Vector2(0.5f, 0.5f));
                VoyageKit.Set(view.Kind, offer.Kind);
                VoyageKit.Set(view.Title, offer.Title);
                view.Pips.Lit = offer.Recommended;
                VoyageKit.Set(view.Difficulty, offer.Recommended.ToString(CultureInfo.InvariantCulture));
                VoyageKit.Set(view.Meta, offer.Stops.Count > 0
                    ? TemplateText.Fill(_lines?.CrewStops, "stops", offer.Stops.Count.ToString(CultureInfo.InvariantCulture))
                    : null);
                view.Ground.color = on ? VoyageKit.Fade(_kit.Cyan, 0.06f) : VoyageKit.Fade(_kit.Popup, 0.6f);
                view.Line.enabled = on;
                view.Edge.enabled = !on;
                view.Edge.Solid(VoyageKit.Fade(_kit.Outline, 0.5f));
                view.Button.onClick.RemoveAllListeners();
                view.Button.onClick.AddListener(() => _board.Pick(index));
            }
            for (int i = _openNow.Count; i < _cards.Count; i++) _cards[i].Root.gameObject.SetActive(false);

            VoyageKit.Set(_crewButtonText, TemplateText.Fill(_lines?.SeaCrewButton, "count",
                _offers.Count.ToString(CultureInfo.InvariantCulture)));
            _crewButton.gameObject.SetActive(_offers.Count > 0);
            int locked = VoyageCrewRules.LockedCount(_offers);
            string lockedLine = locked > 0
                ? TemplateText.Fill(_lines?.SeaLockedLine, "count", locked.ToString(CultureInfo.InvariantCulture))
                : null;
            VoyageKit.Set(_lockedText, lockedLine);
            _lockedLock.sprite = _board.LockSprite;
            _lockedLock.enabled = _board.LockSprite;
            _lockedLock.color = _kit.Sub;
            _lockedRow.gameObject.SetActive(!string.IsNullOrEmpty(lockedLine));
            LayoutSide();
            DrawPins();
        }

        // The column's cards, then the button 10 lower, then the locked line, 14 apart.
        void LayoutSide()
        {
            float y = SideLabel + SideGap;
            for (int i = 0; i < _openNow.Count && i < _cards.Count; i++)
            {
                VoyageKit.At(_cards[i].Root, 0f, y, SideWidth, OpenCardHeight);
                y += OpenCardHeight + SideGap;
            }
            y += ButtonLift;
            VoyageKit.At(_crewButton, 0f, y, SideWidth, ButtonHeight);
            y += ButtonHeight + SideGap;
            VoyageKit.At(_lockedRow, 0f, y, SideWidth, LockedLine);
        }

        float SideHeight()
        {
            int n = _openNow.Count;
            return SideLabel + SideGap + n * (OpenCardHeight + SideGap) + ButtonLift + ButtonHeight + SideGap + LockedLine;
        }

        // ── The pins ─────────────────────────────────────────────────────────

        PinView PinFor(int index)
        {
            while (_pinViews.Count <= index)
            {
                int i = _pinViews.Count;
                var view = new PinView { Root = VoyageKit.Node("Pin" + i, _pins) };
                // 28 square, the middle of its foot on the place.
                view.Root.anchorMin = view.Root.anchorMax = Vector2.zero;
                view.Root.pivot = new Vector2(0.5f, 0f);
                view.Root.sizeDelta = new Vector2(28f, 28f);
                var hit = VoyageKit.Block(view.Root, VoyageKit.Clear, hit: true);
                var button = view.Root.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => _board.Pick(i));
                // The stalk: 3 wide from the ring's middle upwards, Light at its foot fading to nothing.
                view.Stalk = VoyageKit.Node("Stalk", view.Root);
                view.Stalk.anchorMin = view.Stalk.anchorMax = Vector2.zero;
                view.Stalk.pivot = Vector2.zero;
                view.Stalk.anchoredPosition = new Vector2(12f, 14f);
                var stalk = view.Stalk.gameObject.AddComponent<VoyageGradient>();
                stalk.raycastTarget = false;
                stalk.Set(VoyageKit.Fade(_kit.Light, 0f), VoyageKit.Fade(_kit.Light, 0.5f), _kit.Light, 0.5f);
                // The picked pin's halo: 6 units of cyan at .22 round the ring.
                view.Glow = VoyageKit.Node("Halo", view.Root);
                view.Glow.anchorMin = view.Glow.anchorMax = Vector2.zero;
                view.Glow.pivot = new Vector2(0.5f, 0.5f);
                view.Glow.anchoredPosition = new Vector2(14f, 10f);
                view.Glow.sizeDelta = new Vector2(32f, 32f);
                var halo = view.Glow.gameObject.AddComponent<VoyageRing>();
                halo.Width = 6f;
                halo.Set(VoyageKit.Fade(_kit.Cyan, 0.22f), VoyageKit.Clear);
                // The ring: 20 across, a 2 unit line round a dark fill.
                var ring = VoyageKit.Node("Ring", view.Root);
                ring.anchorMin = ring.anchorMax = Vector2.zero;
                ring.pivot = Vector2.zero;
                ring.anchoredPosition = new Vector2(4f, 0f);
                ring.sizeDelta = new Vector2(20f, 20f);
                view.Ring = ring.gameObject.AddComponent<VoyageRing>();
                view.Ring.Width = 2f;
                // The place's name: caps 14 in Light, 34 from the pin's left.
                view.Name = _kit.Label(view.Root, 14, _kit.Light);
                var name = view.Name.rectTransform;
                name.anchorMin = name.anchorMax = Vector2.zero;
                name.pivot = Vector2.zero;
                name.anchoredPosition = new Vector2(34f, 0f);
                name.sizeDelta = new Vector2(320f, 20f);
                view.Name.characterSpacing = 10f;
                view.Name.overflowMode = TextOverflowModes.Overflow;
                _pinViews.Add(view);
            }
            return _pinViews[index];
        }

        void DrawPins()
        {
            for (int i = 0; i < _offers.Count; i++)
            {
                var pin = PinFor(i);
                bool on = i == _open;
                pin.Ring.Set(on ? _kit.Cyan : VoyageKit.Fade(_kit.Light, 0.9f), VoyageKit.Fade(_kit.Popup, 0.6f));
                pin.Glow.gameObject.SetActive(on);
                VoyageKit.Set(pin.Name, _offers[i].Place);
            }
            for (int i = _offers.Count; i < _pinViews.Count; i++) _pinViews[i].Root.gameObject.SetActive(false);
            PlacePins();
        }

        /// <summary>Stand every pin where its place is seen on the table, smaller further back; home where the Ark is.</summary>
        void PlacePins()
        {
            if (!_map)
            {
                foreach (var p in _pinViews) p.Root.gameObject.SetActive(false);
                _home.gameObject.SetActive(false);
                return;
            }
            int tier = _map.Tier;
            float rise = 1f - _e;
            for (int i = 0; i < _offers.Count && i < _pinViews.Count; i++)
            {
                var pin = _pinViews[i];
                var offer = _offers[i];
                bool on = i == _open;
                bool shown = tier >= 0 && (tier < PinsTo || (tier == PinsTo && on)) && TryEnd(offer, out var end) &&
                             Stand(pin.Root, _map.RectPointOf(end));
                pin.Root.gameObject.SetActive(shown);
                if (!shown) continue;
                // Full screen lays the stalks down with the table.
                pin.Stalk.sizeDelta = new Vector2(3f, (40f + offer.Recommended * 14f) * rise);
                pin.Stalk.gameObject.SetActive(rise > 0.01f);
                pin.Name.gameObject.SetActive(tier >= NamedFrom);
                if (on)
                {
                    pin.Root.SetAsLastSibling();
                    // The picked pin breathes, as the board's does (its cyan glow 4 to 12 and back over 2.8s).
                    float breathe = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * (2f * Mathf.PI / 2.8f));
                    pin.Glow.sizeDelta = Vector2.one * (28f + 16f * breathe);
                }
            }
            bool home = tier >= 0 && tier <= PinsTo && !string.IsNullOrEmpty(_map.Here) &&
                        _map.PlaceRectPoint(_map.Here, out var at) && Stand(_home, at);
            _home.gameObject.SetActive(home);
        }

        static bool TryEnd(VoyageBoard.Offer offer, out Vector2 mapPoint)
        {
            mapPoint = default;
            if (offer.Stops.Count > 0) mapPoint = offer.Stops[^1].At;
            else if (offer.Course.Count > 0) mapPoint = offer.Course[^1];
            else return false;
            return true;
        }

        // Put rt where the flat map point is seen, scaled as the table scales it there; false when it is off the map.
        bool Stand(RectTransform rt, Vector2 flat)
        {
            if (!_map.InView(flat)) return false;
            var seen = _map.SeenAt(flat, out float scale);
            Vector2 local = _pins.InverseTransformPoint(_map.rectTransform.TransformPoint(seen));
            rt.anchoredPosition = local - _pins.rect.min;
            rt.localScale = new Vector3(scale, scale, 1f);
            return true;
        }

        // ── Full screen and the crew slide ───────────────────────────────────

        /// <summary>Open the full-screen map, or take it back to the table.</summary>
        public void SetFull(bool full)
        {
            if (full == _full) return;
            _full = full;
            if (full)
            {
                if (!EnsureOverlay()) { _full = false; return; }
                // The page leaves the window for the full-screen layer where it stands now, then grows from there.
                Root.SetParent(_overlay, true);
                _overlay.gameObject.SetActive(true);
                _overlay.SetAsLastSibling();
            }
            else if (_map && _map.Tier >= 4)
            {
                // Out of the foam: back to Sector on the way home.
                _map.ZoomToTier(2);
            }
            _laidFull = -1f;
            _board.RaiseScreenChanged();
        }

        /// <summary>Leave full screen; <paramref name="instant"/> skips the animation (the window closing, another page).</summary>
        public void ExitFull(bool instant)
        {
            if (!_full && _fullT <= 0f) return;
            _full = false;
            if (instant)
            {
                _fullT = 0f;
                Home();
            }
        }

        bool EnsureOverlay()
        {
            if (_overlay) return true;
            var canvas = _board.GetComponentInParent<Canvas>();
            var rootCanvas = canvas ? canvas.rootCanvas : null;
            if (!rootCanvas) return false;
            _overlay = VoyageKit.Node("VoyagesFullScreen", rootCanvas.transform);
            VoyageKit.Stretch(_overlay);
            var c = _overlay.gameObject.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = rootCanvas.sortingOrder + 500;
            _overlay.gameObject.AddComponent<GraphicRaycaster>();
            // Borderless: the sea's own ground, nothing of the window behind.
            _overlayFill = VoyageKit.Block(_overlay, _kit.Popup, hit: true);
            _overlay.gameObject.SetActive(false);
            return true;
        }

        // The page back in the window.
        void Home()
        {
            if (Root.parent != _slot)
            {
                Root.SetParent(_slot, false);
                VoyageKit.Stretch(Root);
            }
            if (_overlay) _overlay.gameObject.SetActive(false);
            _laidFull = -1f;
            _board.RaiseScreenChanged();
        }

        public void Tick(float dt)
        {
            float goalFull = _full ? 1f : 0f;
            if (!Mathf.Approximately(_fullT, goalFull))
            {
                _fullT = Mathf.MoveTowards(_fullT, goalFull, dt / FullSeconds);
                if (_fullT <= 0f && !_full) Home();
            }
            Layout();
            MarkTier();
            PlacePins();
        }

        void MarkTier()
        {
            int tier = _map ? _map.Tier : -1;
            foreach (var t in _tiers)
            {
                bool on = t.Tier == tier;
                t.Bar.enabled = on;
                t.Name.color = on ? _kit.Light : _kit.Sub;
                t.Scale.color = on ? _kit.Light : _kit.Sub;
            }
        }

        // Frogma's own timings: the full screen sweeps on cubic-bezier(.65,0,.25,1); the drawer rises on (.2,.7,.2,1).
        static float Ease(float t) => VoyageKit.Sweep(t);

        static float FitScale(Vector2 room, Vector2 design) =>
            Mathf.Max(0.05f, Mathf.Min(room.x / design.x, room.y / design.y));

        /// <summary>
        /// Lay the page out between the table (in the window's slot) and the full screen, at the full-screen ease and the
        /// crew slide as they stand. The frame holds the board's units: it is the page's room divided by the scale that fits
        /// the board's page into it, so every place below is a number from Rev 111 Full.
        /// </summary>
        void Layout()
        {
            float e = Ease(_fullT);
            _e = e;
            Rect slot = SlotRect();
            if (_fullT > 0f && _overlay)
            {
                var full = _overlay.rect;
                var r = Lerp(slot, new Rect(0f, 0f, full.width, full.height), e);
                Root.anchorMin = Root.anchorMax = Root.pivot = Vector2.zero;
                Root.anchoredPosition = r.position;
                Root.sizeDelta = r.size;
                if (_overlayFill) _overlayFill.color = VoyageKit.Fade(_kit.Popup, e);
            }
            var size = Root.rect.size;
            if (size == _laidFor && Mathf.Approximately(e, _laidFull)) return;
            _laidFor = size;
            _laidFull = e;

            var far = _overlay ? _overlay.rect.size : slot.size;
            float k = Mathf.Lerp(FitScale(slot.size, WindowSize), FitScale(far, ScreenSize), e);
            var F = size / k;
            _frame.anchorMin = _frame.anchorMax = _frame.pivot = Vector2.zero;
            _frame.anchoredPosition = Vector2.zero;
            _frame.sizeDelta = F;
            _frame.localScale = new Vector3(k, k, 1f);

            // The window's page stands centred across and at the top; the full screen runs to its edges.
            float ox = Mathf.Max(0f, (F.x - WindowSize.x) * 0.5f);
            Rect Win(float x, float y, float w, float h) => new(ox + x, F.y - y - h, w, h);
            Rect Full(float x, float y, float w, float h) => new(x, F.y - y - h, w, h);
            Rect At(Rect a, Rect b) => e <= 0f ? a : e >= 1f ? b : Lerp(a, b, e);

            // The zoom row: over the table, then inside the map's top-left corner.
            Place(_controls, At(Win(0f, 0f, ControlsWidth(0f), RowHeight), Full(Margin, Margin, ControlsWidth(1f), RowHeight)));
            LayoutTiers(e);

            Place(_fullButton, Win(FullButtonX, 0f, FullButtonWidth, RowHeight));
            _fullGroup.alpha = 1f - e;
            _fullGroup.blocksRaycasts = e < 0.5f;
            Place(_backFull, Full(F.x - Margin - BackFullWidth, Margin, BackFullWidth, RowHeight));
            _backGroup.alpha = e;
            _backGroup.blocksRaycasts = e >= 0.5f;
            _backFull.gameObject.SetActive(e > 0f);

            // Open now down the right (Rev 113 Sea), then in the full screen's right margin under Back from full screen.
            float sideH = SideHeight();
            Place(_side, At(Win(SideX, SideY, SideWidth, sideH), Full(F.x - Margin - SideWidth, 100f, SideWidth, sideH)));

            // The picked voyage: under the table's right half, then at the full screen's foot.
            Place(_card, At(Win(CardX, CardY, CardWidth, CardHeight),
                Full(Margin, F.y - Margin - CardHeight, CardFullWidth, CardHeight)));

            // The table, then the whole screen.
            var table = At(Win(TableX, TableY, TableWidth, TableHeight), Full(0f, 0f, F.x, F.y));
            Place(_table, table);
            Place(_pins, table);
            Place(_message, table);
            LayoutTable(table.size, e);

            // Back to front: the table, its pins, then the rest.
            _table.SetAsFirstSibling();
            _pins.SetSiblingIndex(1);
        }

        float ControlsWidth(float full)
        {
            float tiers = 0f;
            for (int i = 0; i < _tiers.Count; i++) tiers += _tiers[i].Width * (i < DeepTiers ? full : 1f);
            return tiers + 16f + StepSize + 8f + StepSize;
        }

        void LayoutTiers(float e)
        {
            float x = 0f, band = (RowHeight - BandHeight) * 0.5f;
            for (int i = 0; i < _tiers.Count; i++)
            {
                var t = _tiers[i];
                bool deep = i < DeepTiers;
                float w = deep ? t.Width * e : t.Width;
                Place(t.Root, new Rect(x, band, w, BandHeight));
                t.Group.alpha = deep ? e : 1f;
                t.Group.blocksRaycasts = !deep || e > 0.5f;
                t.Root.gameObject.SetActive(!deep || e > 0.01f);
                x += w;
            }
            Place(_band, new Rect(0f, band, x, BandHeight));
            _band.SetAsFirstSibling();
            var zin = (RectTransform)_controls.Find("ZoomIn");
            var zout = (RectTransform)_controls.Find("ZoomOut");
            Place(zin, new Rect(x + 16f, 0f, StepSize, StepSize));
            Place(zout, new Rect(x + 16f + StepSize + 8f, 0f, StepSize, StepSize));
        }

        /// <summary>The map and its side view in the table: the map lies back (50 degrees to flat) over the side view,
        /// which fades as the table opens to full screen.</summary>
        void LayoutTable(Vector2 size, float e)
        {
            if (!_mapHost) return;
            float edge = _edgeHost ? EdgeHeight * (1f - e) : 0f;
            float seen = Mathf.Max(1f, size.y - edge);
            Place(_mapHost, new Rect(0f, edge, size.x, seen));
            if (_edgeHost)
            {
                Place(_edgeHost, new Rect(0f, 0f, size.x, EdgeHeight));
                if (_edgeGroup)
                {
                    _edgeGroup.alpha = 1f - e;
                    _edgeGroup.blocksRaycasts = e < 0.5f;
                }
                _edgeHost.gameObject.SetActive(e < 1f && _map && _map.Chart != null);
            }
            if (!_map) return;
            float angle = VoyageTableTilt.TableAngle * (1f - e);
            if (_tilt) _tilt.Angle = angle;
            if (_rim) _rim.SetVerticesDirty();
            // The flat map is as tall as it must be for its far edge to be seen at the host's top.
            var rt = _map.rectTransform;
            float flat = angle > 0.01f ? VoyageTableTilt.FlatHeight(size.x, seen, angle) : seen;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, flat);
            rt.anchoredPosition = Vector2.zero;
            _map.Redraw();
        }

        Rect SlotRect()
        {
            if (!_overlay || Root.parent == _slot) return new Rect(0f, 0f, _slot.rect.width, _slot.rect.height);
            // The window's slot as the full-screen layer sees it.
            var corners = new Vector3[4];
            _slot.GetWorldCorners(corners);
            Vector2 lo = _overlay.InverseTransformPoint(corners[0]);
            Vector2 hi = _overlay.InverseTransformPoint(corners[2]);
            var r = _overlay.rect;
            return new Rect(lo.x - r.xMin, lo.y - r.yMin, hi.x - lo.x, hi.y - lo.y);
        }

        static Rect Lerp(Rect a, Rect b, float t) =>
            new(Vector2.Lerp(a.position, b.position, t), Vector2.Lerp(a.size, b.size, t));

        static void Place(RectTransform rt, Rect r)
        {
            if (!rt) return;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = r.position;
            rt.sizeDelta = new Vector2(Mathf.Max(0f, r.width), Mathf.Max(0f, r.height));
        }

        public void Dispose()
        {
            if (_overlay) Object.Destroy(_overlay.gameObject);
        }
    }
}

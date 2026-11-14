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
    /// The Voyages page's first step, the HyperSea (Frogma Rev 111 Full on Rev 108's table, Docs/MENU_REDESIGN_B.md sections
    /// 202-203).
    /// <list type="bullet">
    /// <item><b>The table</b>: the window's live map lying back at 50 degrees (<see cref="VoyageTableTilt"/>) on the moving
    /// sea lines, its rim drawn, the side view (<see cref="VoyageEdgeGraphic"/>) joined to its front edge and sliding with
    /// every drag. Drag pans, the wheel zooms, a press on a place picks the voyage there.</item>
    /// <item><b>The zoom row</b> above it: Region, Sector, Arm and Galaxy with their widths, + and -; the tier on show is
    /// underlined.</item>
    /// <item><b>Full screen</b>: the table lies flat and grows to a borderless full-screen map in 0.9 s; the zoom row rides
    /// into its top-left corner and gains Neighbourhood and Cell (the HyperSea's foam), the crew stays on the right, the
    /// picked voyage moves to the foot, and Back from full screen takes it home (stepping back out of the deep tiers).</item>
    /// <item><b>The crew</b>: a portrait per voyage, its difficulty under it, down the right; Crew slides the voyages out
    /// beside the portraits, Hide slides them away (Shombith on Rev 111: "a toggle with a sliding and the details only show
    /// when we want to").</item>
    /// <item><b>The picked voyage</b>: its kind and pilot, its name, its difficulty pips and pay, and Open voyage.</item>
    /// </list>
    /// Laid out in pixels each time the page's size, the full-screen ease or the crew slide moves (<see cref="Layout"/>),
    /// between the table's layout and the full screen's.
    /// </summary>
    public sealed class VoyageSeaPage
    {
        const float FullSeconds = 0.9f, SlideSeconds = 0.35f;
        const float ControlsHeight = 44f, CardHeight = 106f, CrewWidth = 112f, DetailWidth = 380f, EdgeHeight = 70f;
        const float TierWidth = 184f, Gap = 24f;
        // The tiers the zoom row offers, narrow to wide (HyperSeaSky.Tiers): the first two only in full screen.
        static readonly int[] RowTiers = { 5, 4, 3, 2, 1, 0 };
        const int DeepTiers = 2;

        readonly VoyageBoard _board;
        readonly VoyageKit _kit;
        readonly VoyageLines _lines;

        public RectTransform Root { get; }
        RectTransform _slot;
        RectTransform _table, _column, _mapHost, _edgeHost, _controls, _crewColumn, _detail, _card, _fullButton, _backFull,
            _crewToggle, _message;
        CanvasGroup _edgeGroup, _detailGroup, _fullGroup, _backGroup;
        VoyageTableTilt _tilt;
        VoyageTableRim _rim;
        VoyageMapGraphic _map;
        VoyageSeaLines _ground;
        TMP_Text _messageText, _crewToggleText;

        // The full-screen layer, made on the first full screen: its own canvas over everything in the root canvas.
        RectTransform _overlay;
        Image _overlayFill;
        bool _full, _instant;
        float _fullT;
        bool _crewOpen;
        float _slideT;
        Vector2 _laidFor = new(-1f, -1f);
        float _laidFull = -1f, _laidSlide = -1f;

        readonly List<TierView> _tiers = new();
        readonly List<CrewView> _crew = new();
        readonly List<DetailView> _details = new();
        readonly List<VoyageBoard.Offer> _offers = new();
        int _open = -1;

        // The picked voyage's card.
        TMP_Text _cardKicker, _cardTitle, _cardDifficulty, _cardPay;
        VoyagePips _cardPips;
        RectTransform _cardFlower;
        Button _openButton;

        sealed class TierView
        {
            public int Tier;
            /// <summary>Its width in the row: room for its name and its width in light years.</summary>
            public float Width01;
            public RectTransform Root;
            public CanvasGroup Group;
            public TMP_Text Name, Width;
            public Image Bar;
            public Button Button;
        }

        sealed class CrewView
        {
            public RectTransform Root;
            public Button Button;
            public Image Portrait;
            public TMP_Text Name, Difficulty;
            public Image Bar;
        }

        sealed class DetailView
        {
            public RectTransform Root;
            public VoyageKit.GlassCard Card;
            public Button Button;
            public TMP_Text Kicker, Title;
            public VoyagePips Pips;
            public RectTransform Words;
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

            // The moving sea the table stands on.
            var ground = VoyageKit.Node("SeaLines", Root);
            VoyageKit.Stretch(ground);
            _ground = ground.gameObject.AddComponent<VoyageSeaLines>();
            _ground.color = VoyageKit.Fade(_kit.Outline, 0.6f);
            _ground.Lines = 9;
            _ground.Speed = 0.01f;

            _table = Node("Table");
            _message = Node("Message");
            _messageText = _kit.Text(_message, 18, _kit.Sub, TextAlignmentOptions.Center);
            VoyageKit.Stretch(_messageText.rectTransform);
            _messageText.textWrappingMode = TextWrappingModes.Normal;

            BuildControls();
            BuildCrew();
            BuildCard();
            // The crew's voyages slide out over the picked voyage's card.
            _detail.SetAsLastSibling();
        }

        RectTransform Node(string name)
        {
            var rt = VoyageKit.Node(name, Root);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            return rt;
        }

        static CanvasGroup Group(RectTransform rt) =>
            rt.TryGetComponent<CanvasGroup>(out var g) ? g : rt.gameObject.AddComponent<CanvasGroup>();

        void BuildControls()
        {
            _controls = Node("Zoom");
            string[] names = { _lines?.MapTierCell, _lines?.MapTierNeighbourhood, _lines?.MapTierRegion, _lines?.MapTierSector,
                _lines?.MapTierArm, _lines?.MapTierGalaxy };
            for (int i = 0; i < RowTiers.Length; i++)
            {
                var view = new TierView { Tier = RowTiers[i] };
                view.Root = VoyageKit.Node("Tier" + view.Tier, _controls);
                view.Root.anchorMin = view.Root.anchorMax = view.Root.pivot = Vector2.zero;
                view.Group = Group(view.Root);
                var plate = VoyageKit.Block(view.Root, VoyageKit.Fade(_kit.Popup, 0.78f), hit: true);
                view.Button = view.Root.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = plate;
                view.Button.transition = Selectable.Transition.None;
                int tier = view.Tier;
                view.Button.onClick.AddListener(() => { if (_map) _map.ZoomToTier(tier); });
                view.Name = _kit.Label(view.Root, 14, _kit.Sub, TextAlignmentOptions.Left);
                VoyageKit.Stretch(view.Name.rectTransform);
                view.Name.rectTransform.offsetMin = new Vector2(16f, 0f);
                VoyageKit.Set(view.Name, names[i]);
                view.Width = _kit.Text(view.Root, 14, _kit.Sub, TextAlignmentOptions.Right);
                VoyageKit.Stretch(view.Width.rectTransform);
                view.Width.rectTransform.offsetMax = new Vector2(-14f, 0f);
                view.Width01 = (names[i] ?? string.Empty).Length > 9 ? TierWidth + 64f : TierWidth;
                double ly = HyperSeaSky.Tiers[view.Tier].width;
                VoyageKit.Set(view.Width, TemplateText.Fill(_lines?.SeaTierWidth, "ly", ly.ToString("N0", CultureInfo.InvariantCulture)));
                var bar = VoyageKit.Node("Bar", view.Root);
                VoyageKit.Foot(bar, 0f, 2f);
                view.Bar = VoyageKit.Block(bar, _kit.Cyan);
                _tiers.Add(view);
            }
            Step(_lines?.BoardZoomIn, 1.6f, "In");
            Step(_lines?.BoardZoomOut, 1f / 1.6f, "Out");

            _fullButton = Node("FullScreen");
            _fullGroup = Group(_fullButton);
            _kit.Quiet(_fullButton, null, _lines?.SeaFullScreen, () => SetFull(true));
            _backFull = Node("BackFromFullScreen");
            _backGroup = Group(_backFull);
            _kit.Quiet(_backFull, _lines?.PageBackKey, _lines?.SeaFullScreenBack, () => SetFull(false));

            _crewToggle = Node("CrewToggle");
            _kit.Quiet(_crewToggle, null, _lines?.SeaCrew, () => SetCrew(!_crewOpen), out _crewToggleText);
        }

        void Step(string mark, float factor, string name)
        {
            var rt = VoyageKit.Node("Zoom" + name, _controls);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            _kit.Square(rt, mark, () => { if (_map) _map.ZoomStep(factor); });
        }

        void BuildCrew()
        {
            _crewColumn = Node("Crew");
            VoyageKit.Column(_crewColumn, 18f, TextAnchor.UpperRight);
            _detail = Node("CrewVoyages");
            _detailGroup = Group(_detail);
            VoyageKit.Column(_detail, 18f, TextAnchor.UpperRight);
        }

        void BuildCard()
        {
            _card = Node("PickedVoyage");
            var glass = _kit.Glass(_card);
            var words = VoyageKit.Node("Words", glass.Content);
            VoyageKit.Stretch(words);
            words.offsetMin = new Vector2(24f, 14f);
            words.offsetMax = new Vector2(-340f, -14f);
            VoyageKit.Column(words, 8f, TextAnchor.MiddleLeft);
            _cardKicker = _kit.Label(words, 14, _kit.Light);
            _cardTitle = _kit.Text(words, 22, _kit.Light, TextAlignmentOptions.Left);
            var meta = VoyageKit.Node("Meta", words);
            VoyageKit.Row(meta, 14f);
            VoyageKit.Le(meta).preferredHeight = 20f;
            _cardPips = _kit.Pips(meta);
            _cardDifficulty = _kit.Text(meta, 15, _kit.Sub, TextAlignmentOptions.Left);
            VoyageKit.Le(_cardDifficulty).minWidth = VoyageKit.Le(_cardDifficulty).preferredWidth = 24f;
            var flowerBox = VoyageKit.Node("Element", meta);
            var fle = VoyageKit.Le(flowerBox);
            fle.preferredWidth = fle.minWidth = fle.preferredHeight = fle.minHeight = 18f;
            _cardFlower = VoyageKit.Flower(flowerBox, Element.Charge, _kit.Light);
            VoyageKit.Stretch(_cardFlower);
            _cardPay = _kit.Text(meta, 15, _kit.Sub, TextAlignmentOptions.Left);
            VoyageKit.Le(_cardPay).flexibleWidth = 1f;
            meta.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;

            var cta = VoyageKit.Node("OpenVoyage", glass.Content);
            cta.anchorMin = new Vector2(1f, 0.5f);
            cta.anchorMax = new Vector2(1f, 0.5f);
            cta.pivot = new Vector2(1f, 0.5f);
            cta.sizeDelta = new Vector2(300f, 56f);
            cta.anchoredPosition = new Vector2(-24f, 0f);
            (_openButton, _) = _kit.Cta(cta, _lines?.BoardSailKey, _lines?.SeaOpenVoyage,
                () => _board.ShowPage(VoyageBoard.Page.Voyage));
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
            _laidFor = new Vector2(-1f, -1f);
        }

        void Bind(VoyageMapGraphic map)
        {
            if (!map || map == _map) return;
            _map = map;
            if (!map.TryGetComponent(out _tilt)) _tilt = map.gameObject.AddComponent<VoyageTableTilt>();
            map.Tilt = _tilt;
            map.Plain = true;
            // The table's rim, drawn over the map at the same tilt.
            var rim = VoyageKit.Node("Rim", map.transform);
            VoyageKit.Stretch(rim);
            _rim = rim.gameObject.AddComponent<VoyageTableRim>();
            _rim.Map = map;
            _rim.color = _kit.Outline;
            _laidFor = new Vector2(-1f, -1f);
        }

        // ── Showing ──────────────────────────────────────────────────────────

        public void Show(IReadOnlyList<VoyageBoard.Offer> offers, int open, VoyageMapGraphic map, string message)
        {
            _offers.Clear();
            if (offers != null) _offers.AddRange(offers);
            _open = open;
            if (map) Bind(map);
            if (_map) _map.Plain = true;
            VoyageKit.Set(_messageText, _offers.Count == 0 ? message : null);
            _message.gameObject.SetActive(_offers.Count == 0 && !string.IsNullOrEmpty(message));
            DrawCrew();
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
            _cardPips.Lit = d;
            VoyageKit.Set(_cardDifficulty, d.ToString(CultureInfo.InvariantCulture));
            _cardFlower.parent.gameObject.SetActive(offer.Pays);
            if (offer.Pays) Petals(_cardFlower, offer.Element);
            VoyageKit.Set(_cardPay, offer.Pays
                ? TemplateText.Fill(_lines?.SeaOfferPay, "pay", VoyageBoard.PayAt(offer, d).ToString(CultureInfo.InvariantCulture),
                    "element", _board.ElementName(offer.Element))
                : null);
            _openButton.interactable = true;
            for (int i = 0; i < _crew.Count && i < _offers.Count; i++)
                _crew[i].Difficulty.text = (i == _open ? d : _offers[i].Recommended).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Swap a flower's petals to <paramref name="element"/>'s.</summary>
        static void Petals(RectTransform flower, Element element)
        {
            var sprite = Resources.Load<Sprite>("ElementPetals/" + element.ToString().ToLowerInvariant() + "_petal");
            int i = 0;
            foreach (var img in flower.GetComponentsInChildren<Image>(true))
            {
                ElementalBarsView.ConfigurePetal(img, sprite, i++);
                img.enabled = sprite;
            }
        }

        void DrawCrew()
        {
            for (int i = 0; i < _offers.Count; i++)
            {
                var offer = _offers[i];
                var crew = CrewFor(i);
                crew.Root.gameObject.SetActive(true);
                bool on = i == _open;
                VoyageKit.Show(crew.Portrait, offer.Portrait);
                VoyageKit.Set(crew.Name, offer.Portrait ? null : string.IsNullOrEmpty(offer.Pilot) ? offer.Kind : offer.Pilot);
                crew.Difficulty.text = offer.Recommended.ToString(CultureInfo.InvariantCulture);
                crew.Difficulty.color = on ? _kit.Light : _kit.Sub;
                crew.Bar.color = on ? _kit.Cyan : VoyageKit.Fade(_kit.Outline, 0.6f);
                crew.Portrait.color = on ? Color.white : new Color(1f, 1f, 1f, 0.6f);

                var detail = DetailFor(i);
                detail.Root.gameObject.SetActive(true);
                detail.Card.Pick(_kit, on);
                VoyageKit.Set(detail.Kicker, string.IsNullOrEmpty(offer.Pilot) ? offer.Kind
                    : TemplateText.Fill(_lines?.SeaCrewRecommends, "pilot", offer.Pilot));
                VoyageKit.Set(detail.Title, offer.Title);
                detail.Pips.Lit = offer.Recommended;
            }
            for (int i = _offers.Count; i < _crew.Count; i++) _crew[i].Root.gameObject.SetActive(false);
            for (int i = _offers.Count; i < _details.Count; i++) _details[i].Root.gameObject.SetActive(false);
        }

        const float CrewRow = 150f;

        CrewView CrewFor(int index)
        {
            while (_crew.Count <= index)
            {
                int i = _crew.Count;
                var view = new CrewView { Root = VoyageKit.Node("Pilot" + i, _crewColumn) };
                var le = VoyageKit.Le(view.Root);
                le.preferredHeight = le.minHeight = CrewRow;
                le.preferredWidth = CrewWidth;
                var hit = VoyageKit.Block(view.Root, VoyageKit.Clear, hit: true);
                view.Button = view.Root.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = hit;
                view.Button.transition = Selectable.Transition.None;
                view.Button.onClick.AddListener(() => _board.Pick(i));
                var face = VoyageKit.Node("Portrait", view.Root);
                VoyageKit.Anchor(face, 0f, 0.25f, 1f, 1f);
                view.Portrait = VoyageKit.Picture(face);
                view.Name = _kit.Text(face, 14, _kit.Light, TextAlignmentOptions.BottomLeft);
                VoyageKit.Stretch(view.Name.rectTransform);
                view.Name.textWrappingMode = TextWrappingModes.Normal;
                view.Difficulty = _kit.Text(view.Root, 16, _kit.Light, TextAlignmentOptions.BottomLeft);
                var d = view.Difficulty.rectTransform;
                VoyageKit.Anchor(d, 0f, 0f, 1f, 0.22f);
                d.offsetMin = new Vector2(8f, 6f);
                var bar = VoyageKit.Node("Bar", view.Root);
                VoyageKit.Foot(bar, 0f, 2f);
                view.Bar = VoyageKit.Block(bar, _kit.Cyan);
                _crew.Add(view);
            }
            return _crew[index];
        }

        DetailView DetailFor(int index)
        {
            while (_details.Count <= index)
            {
                int i = _details.Count;
                var view = new DetailView { Root = VoyageKit.Node("Voyage" + i, _detail) };
                var le = VoyageKit.Le(view.Root);
                le.preferredHeight = le.minHeight = CrewRow;
                le.preferredWidth = DetailWidth;
                view.Card = _kit.Glass(view.Root);
                view.Button = view.Root.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = view.Card.Fill;
                view.Button.transition = Selectable.Transition.None;
                view.Button.onClick.AddListener(() => _board.Pick(i));
                var words = VoyageKit.Node("Words", view.Card.Content);
                view.Words = words;
                VoyageKit.Stretch(words);
                words.offsetMin = new Vector2(20f, 16f);
                words.offsetMax = new Vector2(-20f, -16f);
                VoyageKit.Column(words, 12f, TextAnchor.MiddleLeft);
                view.Kicker = _kit.Label(words, 13, _kit.Sub);
                view.Title = _kit.Text(words, 18, _kit.Light, TextAlignmentOptions.Left);
                view.Title.textWrappingMode = TextWrappingModes.Normal;
                view.Title.overflowMode = TextOverflowModes.Ellipsis;
                VoyageKit.Le(view.Title).preferredHeight = 48f;
                view.Pips = _kit.Pips(words);
                _details.Add(view);
            }
            return _details[index];
        }

        // ── Full screen and the crew slide ───────────────────────────────────

        void SetCrew(bool open)
        {
            _crewOpen = open;
            VoyageKit.Set(_crewToggleText, open ? _lines?.SeaCrewHide : _lines?.SeaCrew);
        }

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
            float goalSlide = _crewOpen ? 1f : 0f;
            if (!Mathf.Approximately(_slideT, goalSlide)) _slideT = Mathf.MoveTowards(_slideT, goalSlide, dt / SlideSeconds);
            Layout();
            MarkTier();
        }

        void MarkTier()
        {
            int tier = _map ? _map.Tier : -1;
            foreach (var t in _tiers)
            {
                bool on = t.Tier == tier;
                t.Bar.enabled = on;
                t.Name.color = on ? _kit.Light : _kit.Sub;
                t.Width.color = on ? _kit.Light : _kit.Sub;
            }
        }

        static float Ease(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

        /// <summary>
        /// Lay the page out between the table (in the window's slot) and the full screen, at the full-screen ease and the
        /// crew slide as they stand. Only when one of those or the page's size moved.
        /// </summary>
        void Layout()
        {
            float e = Ease(_fullT), s = Ease(_slideT);
            // The page itself: in the window, or growing from the window's slot to the whole screen.
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
            if (size == _laidFor && Mathf.Approximately(e, _laidFull) && Mathf.Approximately(s, _laidSlide)) return;
            _laidFor = size;
            _laidFull = e;
            _laidSlide = s;

            var near = slot.size;
            var far = _overlay ? _overlay.rect.size : near;
            Rect At(Rect a, Rect b) => e <= 0f ? a : e >= 1f ? b : Lerp(a, b, e);
            float W0 = near.x, H0 = near.y, W1 = far.x, H1 = far.y;

            // The zoom row: over the table, then inside the map's top-left corner.
            var row0 = new Rect(0f, H0 - ControlsHeight, ControlsWidth(0f), ControlsHeight);
            var row1 = new Rect(40f, H1 - 40f - ControlsHeight, ControlsWidth(1f), ControlsHeight);
            Place(_controls, At(row0, row1));
            LayoutTiers(e);

            Place(_fullButton, new Rect(row0.xMax + Gap, H0 - ControlsHeight, 240f, ControlsHeight));
            _fullGroup.alpha = 1f - e;
            _fullGroup.blocksRaycasts = e < 0.5f;
            Place(_backFull, new Rect(W1 - 40f - 340f, H1 - 40f - ControlsHeight, 340f, ControlsHeight));
            _backGroup.alpha = e;
            _backGroup.blocksRaycasts = e >= 0.5f;
            _backFull.gameObject.SetActive(e > 0f);

            // The crew down the right: its toggle on top.
            var toggle0 = new Rect(W0 - 132f, H0 - ControlsHeight, 132f, ControlsHeight);
            var toggle1 = new Rect(W1 - 40f - 132f, H1 - 40f - ControlsHeight * 2f - 20f, 132f, ControlsHeight);
            var toggle = At(toggle0, toggle1);
            Place(_crewToggle, toggle);
            var column = new Rect(toggle.xMax - CrewWidth, 0f, CrewWidth, Mathf.Max(0f, toggle.yMin - Gap));
            Place(_crewColumn, column);
            FitRows(column.height);
            Place(_detail, new Rect(column.xMin - 16f - DetailWidth + (1f - s) * 60f, column.y, DetailWidth, column.height));
            _detailGroup.alpha = s;
            _detailGroup.blocksRaycasts = s > 0.5f;
            _detail.gameObject.SetActive(s > 0f);

            // The picked voyage: under the table's right half, then at the full screen's foot.
            var card0 = new Rect(W0 * 0.42f, 0f, Mathf.Max(420f, W0 - CrewWidth - Gap - W0 * 0.42f), CardHeight);
            var card1 = new Rect(40f, 40f, Mathf.Min(1180f, W1 - CrewWidth - 120f), CardHeight);
            Place(_card, At(card0, card1));

            // The table: between the zoom row and the card, left of the crew; then the whole screen.
            var table0 = new Rect(0f, CardHeight + Gap, W0 - CrewWidth - Gap * 2f, H0 - ControlsHeight - Gap * 2f - CardHeight);
            var table1 = new Rect(0f, 0f, W1, H1);
            var table = At(table0, table1);
            Place(_table, table);
            Place(_message, table);
            LayoutTable(table.size, e);
        }

        // Every voyage's row shares the column: as tall as the board drew them, shorter when more voyages are offered.
        void FitRows(float height)
        {
            int n = Mathf.Max(1, _offers.Count);
            float row = Mathf.Clamp((height - 18f * (n - 1)) / n, 56f, CrewRow);
            foreach (var c in _crew)
            {
                var le = VoyageKit.Le(c.Root);
                le.preferredHeight = le.minHeight = row;
            }
            // A short row keeps the voyage's name on one line and leaves its pips to the crew's number.
            bool compact = row < 110f;
            foreach (var d in _details)
            {
                var le = VoyageKit.Le(d.Root);
                le.preferredHeight = le.minHeight = row;
                d.Title.textWrappingMode = compact ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;
                VoyageKit.Le(d.Title).preferredHeight = compact ? 24f : 48f;
                d.Pips.gameObject.SetActive(!compact);
                float pad = compact ? 6f : 16f;
                d.Words.offsetMin = new Vector2(20f, pad);
                d.Words.offsetMax = new Vector2(-20f, -pad);
                d.Words.GetComponent<VerticalLayoutGroup>().spacing = compact ? 2f : 12f;
            }
        }

        float ControlsWidth(float full)
        {
            float tiers = 0f;
            for (int i = 0; i < _tiers.Count; i++) tiers += _tiers[i].Width01 * (i < DeepTiers ? full : 1f);
            return tiers + Gap + 44f + 8f + 44f;
        }

        void LayoutTiers(float e)
        {
            float x = 0f;
            for (int i = 0; i < _tiers.Count; i++)
            {
                var t = _tiers[i];
                bool deep = i < DeepTiers;
                float w = deep ? t.Width01 * e : t.Width01;
                Place(t.Root, new Rect(x, 0f, w, ControlsHeight));
                t.Group.alpha = deep ? e : 1f;
                t.Group.blocksRaycasts = !deep || e > 0.5f;
                t.Root.gameObject.SetActive(!deep || e > 0.01f);
                x += w;
            }
            var zin = (RectTransform)_controls.Find("ZoomIn");
            var zout = (RectTransform)_controls.Find("ZoomOut");
            Place(zin, new Rect(x + Gap, 0f, 44f, ControlsHeight));
            Place(zout, new Rect(x + Gap + 52f, 0f, 44f, ControlsHeight));
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

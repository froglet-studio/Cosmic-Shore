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
    /// The crew screen, pilot first (Frogma Rev 114 C2, picked by Shombith 2026-11-16; Docs/MENU_REDESIGN_B.md section
    /// 210.1), opened from the HyperSea page's "Crew recommends" button. Your crew runs across the top, one portrait tile
    /// each with their faction, first name and job; the picked tile carries one thin cyan line, and a locked one is dimmed
    /// with the gold lock. Below, the picked pilot's job is told in their own words: their scene on the left, and on the
    /// right "&lt;pilot&gt;, &lt;faction&gt;, asks", the quote, the difficulty, the worlds and the crystals it pays, with
    /// Take the job (A) and the other open voyage (Y). A locked pilot's job says it opens after Milestone 1 instead of
    /// Take the job (the lock is PR #2021's, <see cref="VoyageToyDefinitionSO.IsJobOpen"/>). XP and a pilot's trust are
    /// left out: the game has no voyage XP yet (Shombith, 2026-11-16: "not needed, remove from UI as well").
    /// <para>Drawn in the board's own units: a 1680 x 920 panel at (120, 40) of the 1920 x 1080 screen, in its own canvas
    /// over the window, as the seat popup is. Ordering is <see cref="VoyageCrewRules"/>'s.</para>
    /// </summary>
    public sealed class VoyageCrewPopup
    {
        static readonly Vector2 Screen = new(1920f, 1080f);
        const float Width = 1680f, Height = 920f, PanelX = 120f, PanelY = 40f, Pad = 40f;
        const float StripY = 120f, TileWidth = 248f, TileGap = 22f, FaceHeight = 250f, StripHeight = 350f;
        const float JobY = 560f, PictureWidth = 620f, JobHeight = 330f, WordsX = Pad + PictureWidth + 40f;
        const float LockedAlpha = 0.62f, OpenSeconds = 0.45f, DimSeconds = 0.35f;

        readonly VoyageBoard _board;
        readonly VoyageKit _kit;
        readonly VoyageLines _lines;
        readonly RectTransform _root, _frame, _panel;
        RectTransform _overlay, _strip, _tiles, _buttons, _lockedRow;
        CanvasGroup _rootGroup, _frameGroup;
        Vector2 _fitFor = new(-1f, -1f);
        float _openT = 1f, _dimT = 1f, _fitK = 1f;
        Sprite _lockSprite;

        // The picked pilot's job.
        Image _picture, _lockedIcon;
        RawImage _scene;
        Texture2D _sceneTexture;
        string _sceneResource;
        TMP_Text _asks, _quote, _difficulty, _worlds, _rewards, _lockedWords, _insteadWords;
        VoyagePips _pips;
        Button _take, _instead;

        readonly List<VoyageBoard.Offer> _offers = new();
        readonly List<int> _crew = new();
        readonly List<TileView> _tileViews = new();
        int _picked = -1;

        sealed class TileView
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Face, Lock;
            public VoyageOutline Line;
            public TMP_Text Faction, Name, Job;
            public Button Button;
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

            // "Your crew, and the work they bring" (28) over how to use it (15, Sub); Close at the top right.
            var title = _kit.Text(c, 28, _kit.Light, TextAlignmentOptions.Left);
            VoyageKit.At(title.rectTransform, Pad, 34f, 1400f, 36f);
            VoyageKit.Set(title, _lines?.CrewTitle);
            var line = _kit.Text(c, 15, _kit.Sub, TextAlignmentOptions.Left);
            VoyageKit.At(line.rectTransform, Pad, 78f, 1400f, 22f);
            VoyageKit.Set(line, _lines?.CrewLine);
            _kit.Square(At("Close", Width - Pad - 44f, 34f, 44f, 44f, c), _lines?.PickClose, Close);

            BuildStrip(c);
            BuildJob(c);
            _root.gameObject.SetActive(false);
        }

        // The board's lock gold (#FFAE00 on the board), the Ark's own amber read through the worn colour set.
        Color Gold => ColorTrace.Retrace(ColorTrace.Ui, VoyageKit.ArkAuthored);

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

        // ── Your crew, across the top ────────────────────────────────────────

        void BuildStrip(RectTransform c)
        {
            // Six tiles show at once (the board's six columns); a larger crew scrolls sideways.
            _strip = At("Crew", Pad, StripY, Width - 2f * Pad, StripHeight, c);
            _strip.gameObject.AddComponent<RectMask2D>();
            var hit = VoyageKit.Block(_strip, VoyageKit.Clear, hit: true);
            hit.raycastTarget = true;
            var scroll = _strip.gameObject.AddComponent<ScrollRect>();
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            _tiles = VoyageKit.Node("Tiles", _strip);
            _tiles.anchorMin = Vector2.zero;
            _tiles.anchorMax = new Vector2(0f, 1f);
            _tiles.pivot = new Vector2(0f, 0.5f);
            _tiles.sizeDelta = Vector2.zero;
            scroll.content = _tiles;
            scroll.viewport = _strip;
        }

        TileView TileFor(int index)
        {
            while (_tileViews.Count <= index)
            {
                int slot = _tileViews.Count;
                var view = new TileView { Root = At("Pilot" + slot, slot * (TileWidth + TileGap), 0f, TileWidth, StripHeight, _tiles) };
                view.Group = view.Root.gameObject.AddComponent<CanvasGroup>();
                var ground = VoyageKit.Block(view.Root, VoyageKit.Clear, hit: true);
                view.Button = view.Root.gameObject.AddComponent<Button>();
                view.Button.targetGraphic = ground;
                view.Button.transition = Selectable.Transition.None;
                view.Button.onClick.AddListener(() => PickSlot(slot));

                // The portrait (250 tall), the picked one inside one thin cyan line, the gold lock at its top right.
                var face = At("Face", 0f, 0f, TileWidth, FaceHeight, view.Root);
                VoyageKit.Block(face, VoyageKit.Fade(_kit.Popup, 0.6f));
                var inner = At("Picture", 3f, 3f, TileWidth - 6f, FaceHeight - 6f, face);
                view.Face = VoyageKit.CoverBox(inner);
                var edge = VoyageKit.Node("Line", face);
                VoyageKit.Stretch(edge);
                view.Line = edge.gameObject.AddComponent<VoyageOutline>();
                view.Line.raycastTarget = false;
                view.Line.Width = 2f;
                view.Line.Solid(_kit.Cyan);
                var lockRt = At("Lock", TileWidth - 10f - 20f, 10f, 20f, 20f, face);
                view.Lock = lockRt.gameObject.AddComponent<Image>();
                view.Lock.preserveAspect = true;
                view.Lock.raycastTarget = false;

                // Its faction (caps 13), first name (17) and job (14, Sub), 10 apart under the portrait.
                view.Faction = _kit.Label(At("Faction", 0f, FaceHeight + 10f, TileWidth, 18f, view.Root), 13, _kit.Sub);
                VoyageKit.Stretch(view.Faction.rectTransform);
                view.Name = _kit.Text(At("Name", 0f, FaceHeight + 38f, TileWidth, 24f, view.Root), 17, _kit.Light,
                    TextAlignmentOptions.Left);
                VoyageKit.Stretch(view.Name.rectTransform);
                view.Job = _kit.Text(At("Job", 0f, FaceHeight + 70f, TileWidth, 20f, view.Root), 14, _kit.Sub,
                    TextAlignmentOptions.Left);
                VoyageKit.Stretch(view.Job.rectTransform);
                view.Job.overflowMode = TextOverflowModes.Ellipsis;
                _tileViews.Add(view);
            }
            return _tileViews[index];
        }

        void DrawStrip()
        {
            for (int i = 0; i < _crew.Count; i++)
            {
                var view = TileFor(i);
                view.Root.gameObject.SetActive(true);
                int index = _crew[i];
                var offer = _offers[index];
                bool escort = string.IsNullOrEmpty(offer.Pilot);
                VoyageKit.ShowCover(view.Face, offer.Portrait ? offer.Portrait : offer.Picture,
                    new Vector2(TileWidth - 6f, FaceHeight - 6f), offer.Portrait ? VoyageKit.FaceFocus : new Vector2(0.5f, 0.5f));
                VoyageKit.Set(view.Faction, escort ? offer.Kind : VoyageCrewRules.FactionName(_lines, offer.House));
                VoyageKit.Set(view.Name, escort ? _lines?.CrewArk : VoyageCrewRules.FirstName(offer.Pilot));
                VoyageKit.Set(view.Job, offer.Title);
                view.Line.enabled = index == _picked;
                view.Group.alpha = offer.Locked ? LockedAlpha : 1f;
                view.Lock.sprite = _lockSprite;
                view.Lock.enabled = offer.Locked && _lockSprite;
                view.Lock.color = Gold;
            }
            for (int i = _crew.Count; i < _tileViews.Count; i++) _tileViews[i].Root.gameObject.SetActive(false);
            _tiles.sizeDelta = new Vector2(Mathf.Max(0f, _crew.Count * (TileWidth + TileGap) - TileGap), 0f);
        }

        void PickSlot(int slot)
        {
            if (slot < 0 || slot >= _crew.Count) return;
            Pick(_crew[slot]);
        }

        void Pick(int index)
        {
            _picked = index;
            DrawStrip();
            DrawJob();
        }

        // ── The picked pilot's job ───────────────────────────────────────────

        void BuildJob(RectTransform c)
        {
            // The place: the pilot's scene (the Ark for the escort), 620 x 330, inside a faint line.
            var picture = At("Place", Pad, JobY, PictureWidth, JobHeight, c);
            VoyageKit.Block(picture, VoyageKit.Fade(_kit.Popup, 0.6f));
            picture.gameObject.AddComponent<RectMask2D>();
            _picture = VoyageKit.CoverBox(At("Picture", 0f, 0f, PictureWidth, JobHeight, picture));
            var sceneRt = At("Scene", 0f, 0f, PictureWidth, JobHeight, picture);
            _scene = sceneRt.gameObject.AddComponent<RawImage>();
            _scene.raycastTarget = false;
            _scene.enabled = false;
            var edge = VoyageKit.Node("Edge", picture);
            VoyageKit.Stretch(edge);
            var outline = edge.gameObject.AddComponent<VoyageOutline>();
            outline.raycastTarget = false;
            outline.Solid(VoyageKit.Fade(_kit.Outline, 0.5f));

            // Their words: who asks (caps 14, Light), the quote (34), the difficulty, the worlds and what it pays (15, Sub).
            float w = Width - Pad - WordsX;
            _asks = _kit.Label(At("Asks", WordsX, JobY, w, 20f, c), 14, _kit.Light);
            VoyageKit.Stretch(_asks.rectTransform);
            _quote = _kit.Text(At("Quote", WordsX, JobY + 36f, w, 170f, c), 34, _kit.Light, TextAlignmentOptions.TopLeft);
            VoyageKit.Stretch(_quote.rectTransform);
            _quote.textWrappingMode = TextWrappingModes.Normal;
            _quote.overflowMode = TextOverflowModes.Ellipsis;
            _quote.lineSpacing = -6f;

            var meta = At("Meta", WordsX, JobY + 222f, w, 20f, c);
            var row = VoyageKit.Row(meta, 14f);
            row.childForceExpandWidth = false;
            _pips = _kit.Pips(meta);
            _difficulty = _kit.Text(meta, 15, _kit.Sub, TextAlignmentOptions.Left);
            VoyageKit.Le(_difficulty).minWidth = VoyageKit.Le(_difficulty).preferredWidth = 16f;
            _worlds = _kit.Text(meta, 15, _kit.Sub, TextAlignmentOptions.Left);
            _worlds.overflowMode = TextOverflowModes.Overflow;
            _rewards = _kit.Text(meta, 15, _kit.Sub, TextAlignmentOptions.Left);
            _rewards.overflowMode = TextOverflowModes.Overflow;
            VoyageKit.Le(_rewards).flexibleWidth = 1f;

            // Take the job (A) and the other open voyage (Y) at the bottom; a locked job's line in their place.
            _buttons = At("Buttons", WordsX, JobY + JobHeight - 52f, w, 52f, c);
            var take = At("Take", 0f, 0f, 260f, 52f, _buttons);
            (_take, _) = _kit.Cta(take, _lines?.BoardSailKey, _lines?.CrewTake, TakeJob);
            _lockedRow = At("Locked", 0f, 0f, 420f, 52f, _buttons);
            VoyageKit.Row(_lockedRow, 10f, TextAnchor.MiddleLeft);
            var lockBox = VoyageKit.Node("Lock", _lockedRow);
            var lle = VoyageKit.Le(lockBox);
            lle.preferredWidth = lle.minWidth = lle.preferredHeight = 18f;
            _lockedIcon = lockBox.gameObject.AddComponent<Image>();
            _lockedIcon.preserveAspect = true;
            _lockedIcon.raycastTarget = false;
            _lockedWords = _kit.Text(_lockedRow, 16, Gold, TextAlignmentOptions.Left);
            _lockedWords.overflowMode = TextOverflowModes.Overflow;
            var instead = At("Instead", 280f, 2f, 420f, 48f, _buttons);
            _instead = _kit.Quiet(instead, _lines?.CrewInsteadKey, null, PickOther, out _insteadWords);
            _insteadWords.fontSize = 15;
        }

        void DrawJob()
        {
            var offer = _picked >= 0 && _picked < _offers.Count ? _offers[_picked] : null;
            if (offer == null)
            {
                VoyageKit.Set(_asks, null);
                VoyageKit.Set(_quote, null);
                _buttons.gameObject.SetActive(false);
                ShowPlace(null);
                return;
            }
            bool escort = string.IsNullOrEmpty(offer.Pilot);
            string faction = !string.IsNullOrEmpty(offer.Faction) ? offer.Faction : VoyageCrewRules.FactionName(_lines, offer.House);
            VoyageKit.Set(_asks, escort ? _lines?.CrewArkAsks : TemplateText.Fill(_lines?.CrewAsks, "pilot", offer.Pilot, "faction", faction));
            VoyageKit.Set(_quote, string.IsNullOrEmpty(offer.Story) ? null : TemplateText.Fill(_lines?.CrewQuote, "quote", offer.Story));
            _pips.Lit = offer.Recommended;
            VoyageKit.Set(_difficulty, offer.Recommended.ToString(CultureInfo.InvariantCulture));
            VoyageKit.Set(_worlds, offer.Stops.Count > 0
                ? TemplateText.Fill(_lines?.CrewWorlds, "worlds", offer.Stops.Count.ToString(CultureInfo.InvariantCulture))
                : null);
            VoyageKit.Set(_rewards, offer.Pays
                ? TemplateText.Fill(_lines?.CrewRewards, "pay", offer.Pay.ToString(CultureInfo.InvariantCulture))
                : null);
            ShowPlace(offer);

            _buttons.gameObject.SetActive(true);
            bool sails = offer.SetSail != null;
            _take.gameObject.SetActive(sails);
            _lockedRow.gameObject.SetActive(!sails);
            _lockedIcon.sprite = _lockSprite;
            _lockedIcon.enabled = offer.Locked && _lockSprite;
            _lockedIcon.color = Gold;
            // A locked job opens after Milestone 1; a voyage under way says so.
            VoyageKit.Set(_lockedWords, offer.Locked ? TemplateText.Fill(_lines?.CrewLockedPress, "title", offer.Title) : offer.SailLabel);
            int other = VoyageCrewRules.OtherOpen(_offers, _picked);
            _instead.gameObject.SetActive(other >= 0);
            if (other >= 0) VoyageKit.Set(_insteadWords, TemplateText.Fill(_lines?.CrewInstead, "title", _offers[other].Title));
            // The other button sits after whichever stands first.
            float x = sails ? 280f : 440f;
            VoyageKit.At(_instead.GetComponent<RectTransform>(), x, 2f, Mathf.Min(420f, _buttons.rect.width - x), 48f);
        }

        void TakeJob()
        {
            if (_picked < 0 || _picked >= _offers.Count || _offers[_picked].SetSail == null) return;
            int index = _picked;
            Close();
            _board.Pick(index);
            _board.OpenPicked();
        }

        void PickOther()
        {
            int other = VoyageCrewRules.OtherOpen(_offers, _picked);
            if (other >= 0) Pick(other);
        }

        // The escort shows the Ark; a job shows its pilot's scene, cut around their face, else their portrait.
        void ShowPlace(VoyageBoard.Offer offer)
        {
            var scene = SceneOf(offer);
            if (scene != null && LoadScene(scene.resource))
            {
                _picture.enabled = false;
                _scene.enabled = true;
                _scene.texture = _sceneTexture;
                _scene.uvRect = Crop(_sceneTexture.width, _sceneTexture.height, PictureWidth / JobHeight, scene.face);
                return;
            }
            ReleaseScene();
            _scene.enabled = false;
            var sprite = offer == null ? null : offer.Picture ? offer.Picture : offer.Portrait;
            VoyageKit.ShowCover(_picture, sprite, new Vector2(PictureWidth, JobHeight),
                offer != null && !offer.Picture && offer.Portrait ? VoyageKit.FaceFocus : new Vector2(0.5f, 0.5f));
        }

        static PilotSceneCatalogSO.Entry SceneOf(VoyageBoard.Offer offer)
        {
            if (offer == null || string.IsNullOrEmpty(offer.Callsign)) return null;
            var scenes = VoyageBoardStyleSO.Load() is { } style ? style.PilotScenes : null;
            var catalog = PilotArtifactCatalogSO.Load();
            if (!scenes || !catalog || !catalog.TryGetPilot(offer.Callsign, out var pilot) || !pilot) return null;
            return scenes.TryGet(pilot.Vision, out var entry) ? entry : null;
        }

        bool LoadScene(string resource)
        {
            if (string.IsNullOrEmpty(resource)) return false;
            if (_sceneTexture && resource == _sceneResource) return true;
            var next = Resources.Load<Texture2D>(resource);
            ReleaseScene();
            _sceneTexture = next;
            _sceneResource = next ? resource : null;
            return next;
        }

        // A scene is loaded only while it is shown (PilotSceneCatalogSO names, never references, them).
        void ReleaseScene()
        {
            var old = _sceneTexture;
            _sceneTexture = null;
            _sceneResource = null;
            if (_scene) _scene.texture = null;
            if (old) Resources.UnloadAsset(old);
        }

        /// <summary>
        /// The part of a <paramref name="width"/> x <paramref name="height"/> picture that fills a box of
        /// <paramref name="aspect"/> (width over height), centred on <paramref name="face"/> (0..1 from the bottom left)
        /// as far as the picture allows.
        /// </summary>
        public static Rect Crop(int width, int height, float aspect, Rect face)
        {
            if (width <= 0 || height <= 0 || aspect <= 0f) return new Rect(0f, 0f, 1f, 1f);
            float picture = (float)width / height;
            float w = picture > aspect ? aspect / picture : 1f;
            float h = picture > aspect ? 1f : picture / aspect;
            var centre = face.width > 0f && face.height > 0f ? face.center : new Vector2(0.5f, 0.5f);
            float x = Mathf.Clamp(centre.x - w * 0.5f, 0f, 1f - w);
            float y = Mathf.Clamp(centre.y - h * 0.5f, 0f, 1f - h);
            return new Rect(x, y, w, h);
        }

        // ── Opening ──────────────────────────────────────────────────────────

        /// <summary>Open over the page with <paramref name="offers"/>, the first that sails picked;
        /// <paramref name="lockSprite"/> is the tabs' lock.</summary>
        public void Open(IReadOnlyList<VoyageBoard.Offer> offers, Sprite lockSprite)
        {
            _picked = -1;
            _lockSprite = lockSprite;
            Lift();
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            _fitFor = new Vector2(-1f, -1f);
            if (!_rootGroup && !_root.TryGetComponent(out _rootGroup)) _rootGroup = _root.gameObject.AddComponent<CanvasGroup>();
            if (!_frameGroup && !_frame.TryGetComponent(out _frameGroup)) _frameGroup = _frame.gameObject.AddComponent<CanvasGroup>();
            _openT = _dimT = 0f;
            Show(offers);
            Tick(0f);
        }

        /// <summary>Draw <paramref name="offers"/> again (the window refreshed while the popup is open).</summary>
        public void Show(IReadOnlyList<VoyageBoard.Offer> offers)
        {
            _offers.Clear();
            if (offers != null) _offers.AddRange(offers);
            VoyageCrewRules.Crew(_offers, _crew);
            if (_picked < 0 || _picked >= _offers.Count) _picked = _crew.Count > 0 ? _crew[0] : -1;
            DrawStrip();
            DrawJob();
        }

        public void Close()
        {
            ReleaseScene();
            _root.gameObject.SetActive(false);
            if (_overlay) _overlay.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            ReleaseScene();
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
    }
}

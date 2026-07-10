using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;
using CosmicShore.UI;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Vector2 = CosmicShore.Engine.Vector2;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Client
{
    /// <summary>
    /// Arc-F verification host (`--mode menushell`): the REAL ported ScreenSwitcher
    /// driving the real menu contract — five screen panels laid out to the viewport
    /// (STORE / ARK / HOME / PORT / HANGAR in the authored visual order), the nav bar
    /// as real Buttons wired to the OnClick*Nav handlers, HomeScreen living on the
    /// HOME panel, PORT/ARK disabled exactly like the shipping menu. At frame 30 a
    /// SYNTHETIC pointer click (full raycast/dispatch stack) presses the HANGAR nav
    /// button; the switcher slides one viewport per index over its 0.5s easing; the
    /// screenshot at frame 90 must show the HANGAR panel — byte-identical across
    /// runs (the loop ticks a FIXED 1/60 step, so the coroutine slide is exact).
    ///
    /// Panel content is hand-authored placeholder art (the real prefabs arrive with
    /// the Arc-E content bridge); the NAVIGATION is the shipping code path.
    /// </summary>
    public sealed class MenuShellWindow
    {
        readonly string _screenshotPath;
        readonly int _screenshotFrame;

        IWindow _window;
        GL _gl;
        UiRenderer _ui;
        GameLoop _loop;
        StandaloneInputModule _module;
        ScreenSwitcher _switcher;
        Button _hangarNavButton;
        Button _arcadeNavButton;
        CosmicShore.Core.AudioSystem _audioSystem;
        ModalWindowManager _arcadeModal;
        int _frameIndex;

        static readonly (ScreenSwitcher.MenuScreens id, string title, Color tint)[] Panels =
        {
            (ScreenSwitcher.MenuScreens.STORE, "STORE", new Color(0.20f, 0.08f, 0.30f, 1f)),
            (ScreenSwitcher.MenuScreens.ARK, "ARK", new Color(0.08f, 0.22f, 0.18f, 1f)),
            (ScreenSwitcher.MenuScreens.HOME, "HOME", new Color(0.045f, 0.03f, 0.11f, 1f)),
            (ScreenSwitcher.MenuScreens.PORT, "PORT", new Color(0.05f, 0.16f, 0.28f, 1f)),
            (ScreenSwitcher.MenuScreens.HANGAR, "HANGAR", new Color(0.16f, 0.10f, 0.05f, 1f)),
        };

        public MenuShellWindow(string screenshotPath, int screenshotFrame)
        {
            _screenshotPath = screenshotPath;
            _screenshotFrame = screenshotFrame;
        }

        public void Run()
        {
            var options = WindowOptions.Default with
            {
                Size = new Vector2D<int>(1280, 720),
                Title = "Cosmic Shore — Menu shell (port progress build)",
                VSync = true,
            };
            Console.WriteLine("[1/3] creating window (GLFW)...");
            _window = Window.Create(options);
            _window.Load += OnLoad;
            _window.Update += OnUpdate;
            _window.Render += OnRender;
            _window.Run();
            _loop?.Dispose();
        }

        void OnLoad()
        {
            Console.WriteLine("[2/3] window open — initializing GL/menu...");
            _gl = GL.GetApi(_window);
            _gl.ClearColor(0.02f, 0.015f, 0.06f, 1f);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _ui = new UiRenderer(_gl);

            Screen.width = _window.FramebufferSize.X;
            Screen.height = _window.FramebufferSize.Y;

            _loop = new GameLoop("MenuShell");
            BuildMenu();
            Console.WriteLine("[3/3] ready — menu shell.");
        }

        void BuildMenu()
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            _module = esGo.AddComponent<StandaloneInputModule>();

            // Scene transcription: Menu_Main always carries the UserActionSystem
            // singleton (the HANGAR nav completes a ViewHangarMenu action through it)
            // and the AudioSystem (modal open/close cues route through it).
            new GameObject("UserActionSystem").AddComponent<CosmicShore.Core.UserActionSystem>();
            _audioSystem = new GameObject("AudioSystem").AddComponent<CosmicShore.Core.AudioSystem>();
            new GameObject("CallToActionSystem").AddComponent<CosmicShore.Core.CallToActionSystem>();

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.AddComponent<Canvas>();
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var canvasRect = (RectTransform)canvasGo.transform;

            // The Screens container hosts the ScreenSwitcher — the switcher slides
            // THIS transform, one viewport width per screen index. Scene contract
            // (transcribed from Menu_Main): NavigateTo writes transform.position with
            // y = 0, so the container's PIVOT must rest at world (0,0) — bottom-left
            // anchor + (0,0) pivot, sized to the full canvas.
            var screensRoot = MakeChild("Screens", canvasRect);
            screensRoot.anchorMin = screensRoot.anchorMax = Vector2.zero;
            screensRoot.pivot = Vector2.zero;
            screensRoot.anchoredPosition = Vector2.zero;
            screensRoot.sizeDelta = new Vector2(canvasRect.rect.width, canvasRect.rect.height);
            var screensGroup = screensRoot.gameObject.AddComponent<CanvasGroup>();
            _switcher = screensRoot.gameObject.AddComponent<ScreenSwitcher>();

            // Clear stale PlayerPrefs return-state — the host stands in for the
            // original engine's runtime-initialize pass (data-only attribute here).
            typeof(ScreenSwitcher)
                .GetMethod("RunOnStart", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, null);

            var entries = new List<ScreenSwitcher.ScreenEntry>();
            foreach (var (id, title, tint) in Panels)
            {
                var panel = MakeChild($"Screen_{title}", screensRoot);
                var image = panel.gameObject.AddComponent<Image>();
                image.color = tint;
                image.raycastTarget = false;

                var label = MakeChild("Title", panel);
                label.anchorMin = Vector2.zero;
                label.anchorMax = Vector2.one;
                label.offsetMin = Vector2.zero;
                label.offsetMax = Vector2.zero;
                var text = label.gameObject.AddComponent<TextMeshProUGUI>();
                text.text = title;
                text.fontSize = 96f;
                text.color = new Color(0.85f, 0.95f, 1f, 0.95f);
                text.alignment = TextAlignmentOptions.Center;

                if (id == ScreenSwitcher.MenuScreens.HOME)
                {
                    // The real HomeScreen lives on the HOME panel (shipping wiring);
                    // its PlayerDataService inject stays null in the shell — the
                    // ported code null-guards exactly like the original.
                    var home = panel.gameObject.AddComponent<HomeScreen>();
                    var nameLabel = MakeChild("UserName", panel);
                    nameLabel.anchorMin = nameLabel.anchorMax = new Vector2(0.5f, 0.5f);
                    nameLabel.sizeDelta = new Vector2(600f, 40f);
                    nameLabel.anchoredPosition = new Vector2(0f, -120f);
                    var nameText = nameLabel.gameObject.AddComponent<TextMeshProUGUI>();
                    nameText.text = "PILOT";
                    nameText.fontSize = 28f;
                    nameText.color = new Color(1f, 0.45f, 0.85f, 0.9f);
                    nameText.alignment = TextAlignmentOptions.Center;
                    SetPrivateField(home, "userNameText", nameText);
                }

                entries.Add(new ScreenSwitcher.ScreenEntry { id = id, root = panel });
            }
            SetPrivateField(_switcher, "screens", entries);
            SetPrivateField(_switcher, "screensCanvasGroup", screensGroup);

            // Nav bar: five real Buttons across the bottom wired to the shipping
            // OnClick*Nav handlers (PORT/ARK stay wired — the switcher itself rejects
            // disabled screens, which the shell exercises for free).
            var navBar = MakeChild("NavBar", canvasRect);
            navBar.anchorMin = navBar.anchorMax = new Vector2(0.5f, 0f);
            navBar.pivot = new Vector2(0.5f, 0f);
            navBar.anchoredPosition = new Vector2(0f, 24f);
            navBar.sizeDelta = new Vector2(1200f, 96f);
            var navGroup = navBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            navGroup.spacing = 12f;
            navGroup.childForceExpandWidth = true;
            navGroup.childForceExpandHeight = true;

            (string label, Action<ScreenSwitcher> onClick)[] navButtons =
            {
                ("STORE", s => s.OnClickStoreNav()),
                ("ARK", s => s.OnClickArkNav()),
                ("HOME", s => s.OnClickHomeNav()),
                ("PORT", s => s.OnClickPortNav()),
                ("HANGAR", s => s.OnClickHangarNav()),
                ("ARCADE", s => s.OnClickArcadeNav()),   // shipping path: opens the arcade MODAL
            };
            foreach (var (label, onClick) in navButtons)
            {
                var buttonRect = MakeChild($"Nav_{label}", navBar);
                var buttonImage = buttonRect.gameObject.AddComponent<Image>();
                buttonImage.color = new Color(0.14f, 0.16f, 0.38f, 0.95f);
                var button = buttonRect.gameObject.AddComponent<Button>();
                var colors = button.colors;
                colors.normalColor = new Color(0.14f, 0.16f, 0.38f, 0.95f);
                colors.selectedColor = new Color(0.30f, 0.55f, 0.95f, 1f);
                colors.pressedColor = new Color(0.55f, 0.85f, 1f, 1f);
                button.colors = colors;
                var switcher = _switcher;
                button.onClick.AddListener(() => onClick(switcher));
                if (label == "HANGAR") _hangarNavButton = button;
                if (label == "ARCADE") _arcadeNavButton = button;

                var buttonLabel = MakeChild("Label", buttonRect);
                buttonLabel.anchorMin = Vector2.zero;
                buttonLabel.anchorMax = Vector2.one;
                buttonLabel.offsetMin = Vector2.zero;
                buttonLabel.offsetMax = Vector2.zero;
                var buttonText = buttonLabel.gameObject.AddComponent<TextMeshProUGUI>();
                buttonText.text = label;
                buttonText.fontSize = 22f;
                buttonText.color = new Color(0.9f, 0.97f, 1f, 1f);
                buttonText.alignment = TextAlignmentOptions.Center;
            }

            BuildArcadeModal(canvasRect);
        }

        /// <summary>
        /// The REAL arcade family as the shipping modal overlay: ModalWindowManager
        /// (ARCADE) hosting ArcadeScreen + ArcadeExploreView with a GameCard grid fed
        /// by hand-authored SO_ArcadeGame entries — the same modes the CLI proves.
        /// Prefab art arrives with the Arc-E content bridge; the WIRING is shipping code.
        /// </summary>
        void BuildArcadeModal(RectTransform canvasRect)
        {
            var gameList = ScriptableObject.CreateInstance<CosmicShore.ScriptableObjects.SO_GameList>();
            gameList.Games = new List<CosmicShore.ScriptableObjects.SO_ArcadeGame>();
            (string name, CosmicShore.Data.GameModes mode)[] games =
            {
                ("HEX RACE", CosmicShore.Data.GameModes.HexRace),
                ("JOUST", CosmicShore.Data.GameModes.MultiplayerJoust),
                ("CRYSTAL CAPTURE", CosmicShore.Data.GameModes.MultiplayerCrystalCapture),
            };
            foreach (var (name, mode) in games)
            {
                var so = ScriptableObject.CreateInstance<CosmicShore.ScriptableObjects.SO_ArcadeGame>();
                so.DisplayName = name;
                so.Mode = mode;
                gameList.Games.Add(so);
            }

            // Modal root: full-screen dim + CanvasGroup + ModalWindowManager(ARCADE).
            var modalRoot = MakeChild("ArcadeModal", canvasRect);
            modalRoot.gameObject.SetActive(false); // wire fields before Start/OnEnable
            modalRoot.anchorMin = Vector2.zero;
            modalRoot.anchorMax = Vector2.one;
            modalRoot.offsetMin = Vector2.zero;
            modalRoot.offsetMax = Vector2.zero;
            modalRoot.gameObject.AddComponent<CanvasGroup>();
            var dim = modalRoot.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.72f);
            _arcadeModal = modalRoot.gameObject.AddComponent<ModalWindowManager>();
            _arcadeModal.ModalType = ScreenSwitcher.ModalWindows.ARCADE;
            SetPrivateField(_arcadeModal, "screenSwitcher", _switcher);
            typeof(ModalWindowManager)
                .GetField("audioSystem", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(_arcadeModal, _audioSystem);

            // Panel: title + one grid row of three REAL GameCards.
            var panel = MakeChild("ArcadePanel", modalRoot);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(1360f, 640f);
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.06f, 0.20f, 0.97f);
            panelImage.raycastTarget = false;

            var title = MakeChild("Title", panel);
            title.anchorMin = new Vector2(0f, 1f);
            title.anchorMax = new Vector2(1f, 1f);
            title.pivot = new Vector2(0.5f, 1f);
            title.anchoredPosition = new Vector2(0f, -28f);
            title.sizeDelta = new Vector2(0f, 60f);
            var titleText = title.gameObject.AddComponent<TextMeshProUGUI>();
            titleText.text = "ARCADE";
            titleText.fontSize = 44f;
            titleText.color = new Color(0.55f, 0.95f, 1f, 1f);
            titleText.alignment = TextAlignmentOptions.Center;

            var screen = panel.gameObject.AddComponent<ArcadeScreen>(); // rides the modal's CanvasGroup family

            var explore = panel.gameObject.AddComponent<ArcadeExploreView>();
            SetPrivateField(explore, "GameList", gameList);
            SetPrivateField(explore, "selectedVesselClassType",
                ScriptableObject.CreateInstance<CosmicShore.ScriptableObjects.VesselClassTypeVariable>());

            var dpad = panel.gameObject.AddComponent<CosmicShore.Core.ArcadeDPadNav>();
            SetPrivateField(explore, "ArcadeDPadNav", dpad);

            // Daily-challenge card, top-left (its own Start disables it: COMING SOON).
            var daily = MakeChild("DailyChallengeCard", panel);
            daily.anchorMin = daily.anchorMax = new Vector2(0f, 1f);
            daily.pivot = new Vector2(0f, 1f);
            daily.anchoredPosition = new Vector2(36f, -110f);
            daily.sizeDelta = new Vector2(300f, 90f);
            var dailyImage = daily.gameObject.AddComponent<Image>();
            dailyImage.color = new Color(0.14f, 0.12f, 0.30f, 1f);
            daily.gameObject.AddComponent<Button>();
            var dailyCard = daily.gameObject.AddComponent<DailyChallengeCard>();
            var dailyLabel = MakeFullStretchText(daily, "DAILY CHALLENGE", 20f);
            SetPrivateField(dailyCard, "GameTitle", dailyLabel);
            var dailyTime = MakeChild("TimeRemaining", daily);
            dailyTime.anchorMin = Vector2.zero;
            dailyTime.anchorMax = new Vector2(1f, 0.4f);
            dailyTime.offsetMin = Vector2.zero;
            dailyTime.offsetMax = Vector2.zero;
            var dailyTimeText = dailyTime.gameObject.AddComponent<TextMeshProUGUI>();
            dailyTimeText.fontSize = 14f;
            dailyTimeText.color = new Color(1f, 0.45f, 0.85f, 0.9f);
            dailyTimeText.alignment = TextAlignmentOptions.Center;
            SetPrivateField(dailyCard, "TimeRemaining", dailyTimeText);
            SetPrivateField(dailyCard, "BackgroundImage", dailyImage);
            SetPrivateField(explore, "DailyChallengeCard", dailyCard);

            // Grid: one row of three cards (ExploreView walks rows → cards).
            var grid = MakeChild("GameSelectionGrid", panel);
            grid.anchorMin = new Vector2(0.5f, 0f);
            grid.anchorMax = new Vector2(0.5f, 0f);
            grid.pivot = new Vector2(0.5f, 0f);
            grid.anchoredPosition = new Vector2(0f, 60f);
            grid.sizeDelta = new Vector2(1280f, 340f);
            SetPrivateField(explore, "GameSelectionGrid", (Transform)grid);

            var row = MakeChild("Row_0", grid);
            row.anchorMin = Vector2.zero;
            row.anchorMax = Vector2.one;
            row.offsetMin = Vector2.zero;
            row.offsetMax = Vector2.zero;
            var rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowGroup.spacing = 24f;
            rowGroup.childForceExpandWidth = true;
            rowGroup.childForceExpandHeight = true;

            for (int i = 0; i < games.Length; i++)
            {
                var card = MakeChild($"GameCard_{i}", row);
                var cardImage = card.gameObject.AddComponent<Image>();
                cardImage.color = new Color(0.15f, 0.24f, 0.52f, 1f);
                card.gameObject.AddComponent<Button>();
                card.gameObject.AddComponent<CosmicShore.Core.CallToActionTarget>();
                var gameCard = card.gameObject.AddComponent<GameCard>();
                SetPrivateField(gameCard, "AllGames", gameList);
                SetPrivateField(gameCard, "BackgroundImage", cardImage);
                SetPrivateField(gameCard, "GameTitle", MakeFullStretchText(card, "", 26f));

                var star = MakeChild("Star", card);
                star.anchorMin = star.anchorMax = new Vector2(1f, 1f);
                star.pivot = new Vector2(1f, 1f);
                star.anchoredPosition = new Vector2(-10f, -10f);
                star.sizeDelta = new Vector2(28f, 28f);
                var starImage = star.gameObject.AddComponent<Image>();
                starImage.color = new Color(1f, 0.82f, 0.25f, 0.9f);
                starImage.raycastTarget = false;
                SetPrivateField(gameCard, "StarImage", starImage);
            }

            // The switcher owns the modal (OnClickArcadeNav + CloseAllModals paths).
            SetPrivateField(_switcher, "ArcadeModal", _arcadeModal);
            SetPrivateField(_switcher, "Modals", new List<ModalWindowManager> { _arcadeModal });

            modalRoot.gameObject.SetActive(true); // Start hides it via CanvasGroup until opened
        }

        static TextMeshProUGUI MakeFullStretchText(RectTransform parent, string content, float size)
        {
            var label = MakeChild("Label", parent);
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = Vector2.zero;
            label.offsetMax = Vector2.zero;
            var text = label.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.color = new Color(0.92f, 0.97f, 1f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            return text;
        }

        static RectTransform MakeChild(string name, RectTransform parent)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, worldPositionStays: false);
            return rt;
        }

        static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                        ?? throw new MissingFieldException(target.GetType().Name, fieldName);
            field.SetValue(target, value);
        }

        void OnUpdate(double dt)
        {
            // FIXED-step ticking: the 0.5s slide coroutine spans exactly 30 ticks, so
            // the frame-90 screenshot always lands on the settled HANGAR layout.
            _loop.Tick(1f / 60f);
            _frameIndex++;

            if (_frameIndex == 30 && _hangarNavButton != null)
                ClickButton(_hangarNavButton);

            // Frame 60: open the arcade MODAL through the shipping path — a real click
            // on the ARCADE nav → OnClickArcadeNav → ArcadeModal.ModalWindowIn.
            if (_frameIndex == 60 && _arcadeNavButton != null)
                ClickButton(_arcadeNavButton);
        }

        void ClickButton(Button button)
        {
            var corners = new Vector3[4];
            ((RectTransform)button.transform).GetWorldCorners(corners);
            var centre = new Vector2(
                (corners[0].x + corners[2].x) * 0.5f,
                (corners[0].y + corners[2].y) * 0.5f);
            _module.PointerDown(centre);
            _module.PointerUp(centre);
        }

        unsafe void OnRender(double dt)
        {
            _gl.Viewport(0, 0, (uint)_window.FramebufferSize.X, (uint)_window.FramebufferSize.Y);
            _gl.Clear(ClearBufferMask.ColorBufferBit);

            UiCanvasBridge.Render(_ui, _window.FramebufferSize.X, _window.FramebufferSize.Y);

            if (_screenshotPath != null && _frameIndex >= _screenshotFrame)
            {
                CaptureScreenshot();
                _window.Close();
            }
        }

        unsafe void CaptureScreenshot()
        {
            int w = _window.FramebufferSize.X, h = _window.FramebufferSize.Y;
            var pixels = new byte[w * h * 4];
            fixed (byte* p = pixels)
                _gl.ReadPixels(0, 0, (uint)w, (uint)h, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            MiniPng.Write(_screenshotPath, pixels, w, h);

            string active = "?";
            foreach (var (id, title, _) in Panels)
                if (_switcher.ScreenIsActive(id)) active = title;
            var screensRoot = _switcher.transform;
            int cardCount = 0;
            foreach (var card in Object.FindObjectsByType<GameCard>(FindObjectsSortMode.None))
                if (card.gameObject.activeInHierarchy) cardCount++;
            Console.WriteLine($"screenshot → {_screenshotPath} ({w}x{h}) frame {_frameIndex}, " +
                $"mode menushell, active {active}, slideX {screensRoot.position.x:F1}, " +
                $"modalStack {(_switcher.HasActiveModal ? "open" : "empty")}, " +
                $"arcadeModal {(_switcher.ModalIsActive(ScreenSwitcher.ModalWindows.ARCADE) ? "ARCADE" : "none")}, " +
                $"gameCards {cardCount}, paused {CosmicShore.Core.PauseSystem.Paused}");
        }
    }
}

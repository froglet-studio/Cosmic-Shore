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
    /// HOME panel, PORT/ARK disabled exactly like the shipping menu. SYNTHETIC
    /// pointer clicks (full raycast/dispatch stack) then walk the whole shipping
    /// arcade flow on the FIXED 1/60 step (byte-identical across runs):
    ///   frame  30 — HANGAR nav (screen slide)
    ///   frame  60 — ARCADE nav → arcade modal (3 real GameCards)
    ///   frame  90 — HEX RACE card → ArcadeExploreView.SelectGame →
    ///               ArcadeGameConfigureModal Screen 1 (game defaults)
    ///   frame 120 — intensity 2 (IntensitySelectButton.Select)
    ///   frame 132 — player-count “+” (IntStepper → PC 2, AI backfill 1)
    ///   frame 150 — CONFIRM CONFIGURATION → Screen 2 (domain tiles + ship + START)
    ///   frame 180 — START → no sync manager → HandleAllPlayersReady →
    ///               GameDataSO synced + OnLaunchGame raised
    ///
    /// Arc G part 2 / Arc I entry — the OnLaunchGame raise now performs the REAL
    /// menu→game handoff: the menu world's GameLoop is disposed (fresh-world statics
    /// make menu/game loops mutually exclusive — Unity scene-unload semantics), the
    /// mode host stands up the round for gameData.GameMode through the SAME
    /// IRoundDriver split the CLI proves, the round steps + renders via
    /// RoundScenePass to completion, and after a linger on the standings the menu is
    /// REBUILT from scratch (ReturnToScreen consumed from PlayerPrefs — the shipping
    /// return path; RunOnStart only runs on first boot, per-app-run semantics).
    /// The default capture shows the RETURNED menu with the loop's summary banner.
    ///
    /// Panel content is hand-authored placeholder art (the real prefabs arrive with
    /// the Arc-E content bridge); the NAVIGATION + CONFIGURE + LAUNCH flow is
    /// shipping code.
    /// </summary>
    public sealed class MenuShellWindow
    {
        enum HostPhase { Menu, Game, MenuReturned }

        const int ReturnLingerFrames = 240; // 4s on the standings before the menu returns

        readonly string _screenshotPath;
        readonly int _screenshotFrame;

        IWindow _window;
        GL _gl;
        UiRenderer _ui;
        RoundScenePass _scene;
        GameLoop _loop;
        StandaloneInputModule _module;
        ScreenSwitcher _switcher;
        Button _hangarNavButton;
        Button _arcadeNavButton;
        CosmicShore.Core.AudioSystem _audioSystem;
        ModalWindowManager _arcadeModal;
        ArcadeGameConfigureModal _configureModal;
        CosmicShore.UI.ArcadeGameConfigSO _config;
        CosmicShore.Utility.GameDataSO _gameData;
        IntensitySelectButton _intensityTwoButton;
        Button _pcIncrementButton;
        Button _confirmConfigButton;
        Button _startGameButton;
        TextMeshProUGUI _launchBanner;
        string _launchInfo = "none";
        int _frameIndex;

        // ── the menu→game→menu loop state ──────────────────────────────────
        HostPhase _phase = HostPhase.Menu;
        bool _pendingLaunch;
        CosmicShore.Cli.IRoundDriver _round;
        bool _roundDone;
        int _gameFinishFrame;
        string _lastGameSummary = "none";

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
            _round?.Dispose();
            _loop?.Dispose();
        }

        void OnLoad()
        {
            Console.WriteLine("[2/3] window open — initializing GL/menu...");
            _gl = GL.GetApi(_window);
            _gl.ClearColor(0.02f, 0.015f, 0.06f, 1f);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _gl.Enable(EnableCap.ProgramPointSize);
            _ui = new UiRenderer(_gl);
            _scene = new RoundScenePass(_gl);

            Screen.width = _window.FramebufferSize.X;
            Screen.height = _window.FramebufferSize.Y;

            _loop = new GameLoop("MenuShell");
            BuildMenu(firstBoot: true);
            Console.WriteLine("[3/3] ready — menu shell.");
        }

        void BuildMenu(bool firstBoot)
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

            // Clear stale PlayerPrefs return-state ON FIRST BOOT ONLY — the host
            // stands in for the original engine's runtime-initialize pass
            // ([RuntimeInitializeOnLoadMethod] runs once per app-run, NOT per scene
            // load). On a menu REBUILD after a game, the persisted ReturnToScreen is
            // consumed by ScreenSwitcher.Start — the shipping return path.
            if (firstBoot)
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
            (string name, CosmicShore.Data.GameModes mode, string scene, int minDomains)[] games =
            {
                ("HEX RACE", CosmicShore.Data.GameModes.HexRace, "MinigameHexRace", 1),
                ("JOUST", CosmicShore.Data.GameModes.MultiplayerJoust, "MinigameJoust_Gameplay", 2),
                ("CRYSTAL CAPTURE", CosmicShore.Data.GameModes.MultiplayerCrystalCapture, "MinigameCrystalCaptureMultiplayer_Gameplay", 2),
            };
            foreach (var (name, mode, scene, minDomains) in games)
            {
                var so = ScriptableObject.CreateInstance<CosmicShore.ScriptableObjects.SO_ArcadeGame>();
                so.DisplayName = name;
                so.Mode = mode;
                so.SceneName = scene;
                so.IsMultiplayer = true;
                so.Description = $"{name} - the same mode the CLI gate proves headlessly.";
                so.MinPlayersAllowed = 1;
                so.MaxPlayersAllowed = 4;
                so.MinDomainsAllowed = minDomains;
                so.MaxDomainsAllowed = 3;
                var dolphin = ScriptableObject.CreateInstance<SO_Vessel>(); // SO_Vessel is global-namespace (as upstream)
                dolphin.Class = CosmicShore.Data.VesselClassType.Dolphin;
                dolphin.Name = "Dolphin";
                so.Vessels = new List<SO_Vessel> { dolphin };
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

            // ArcadeScreen's Explore/Loadout pair: the explore view IS this panel's
            // view; the loadout view exists inactive (its screen is a future unit)
            // so Awake's EnsureCanvasGroup and Start's LoadoutButton.Select() run
            // on real references instead of guarded NREs.
            var loadoutGo = MakeChild("LoadoutView", panel);
            loadoutGo.gameObject.SetActive(false);
            var loadoutView = loadoutGo.gameObject.AddComponent<ArcadeLoadoutView>();
            var loadoutToggle = MakeChild("LoadoutButton", panel).gameObject.AddComponent<Toggle>();
            var exploreToggle = MakeChild("ExploreButton", panel).gameObject.AddComponent<Toggle>();
            SetPrivateField(screen, "ExploreView", explore);
            SetPrivateField(screen, "LoadoutView", loadoutView);
            SetPrivateField(screen, "LoadoutButton", loadoutToggle);
            SetPrivateField(screen, "ExploreButton", exploreToggle);

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

            BuildConfigureModal(canvasRect, gameList, explore);

            // The switcher owns the modals (OnClickArcadeNav + CloseAllModals paths).
            SetPrivateField(_switcher, "ArcadeModal", _arcadeModal);
            SetPrivateField(_switcher, "Modals", new List<ModalWindowManager> { _arcadeModal, _configureModal });

            modalRoot.gameObject.SetActive(true); // Start hides it via CanvasGroup until opened
        }

        /// <summary>
        /// The REAL ArcadeGameConfigureModal on the shipping SOLO path (no
        /// ArcadeConfigSyncManager, no NetworkManager): card click → Screen 1
        /// (intensity buttons + PC/DC IntSteppers + Confirm) → Screen 2 (domain
        /// tiles + ship summary + Start) → HandleAllPlayersReady → GameDataSO
        /// synced + OnLaunchGame raised. The host subscribes OnLaunchGame and
        /// shows the LAUNCHING banner where SceneLoader would load the scene.
        /// </summary>
        void BuildConfigureModal(RectTransform canvasRect,
                                 CosmicShore.ScriptableObjects.SO_GameList gameList,
                                 ArcadeExploreView explore)
        {
            // The shared game data the launch seam writes into (the same SOAP
            // container the CLI rounds and SceneLoader read).
            _gameData = ScriptableObject.CreateInstance<CosmicShore.Utility.GameDataSO>();
            _gameData.OnLaunchGame = ScriptableObject.CreateInstance<CosmicShore.Engine.Soap.ScriptableEventNoParam>();
            _gameData.SelectedIntensity = ScriptableObject.CreateInstance<CosmicShore.Engine.Soap.IntVariable>();
            _gameData.SelectedPlayerCount = ScriptableObject.CreateInstance<CosmicShore.Engine.Soap.IntVariable>();
            _gameData.selectedVesselClass = ScriptableObject.CreateInstance<CosmicShore.ScriptableObjects.VesselClassTypeVariable>();
            _gameData.VesselClassSelectedIndex = ScriptableObject.CreateInstance<CosmicShore.Engine.Soap.IntVariable>();
            _gameData.OnLaunchGame.OnRaised += () =>
            {
                _launchInfo = $"{_gameData.GameMode}@{_gameData.SceneName} " +
                              $"players={_gameData.SelectedPlayerCount.Value} ai={_gameData.RequestedAIBackfillCount} " +
                              $"intensity={_gameData.SelectedIntensity.Value} dc={_gameData.RequestedDomainCount}";
                if (_launchBanner != null)
                {
                    _launchBanner.text = $"LAUNCHING {_gameData.GameMode} -> {_gameData.SceneName}";
                    _launchBanner.gameObject.SetActive(true);
                }

                // The SceneLoader seam: the raise arrives from INSIDE _loop.Tick
                // (modal HandleAllPlayersReady), so the actual world swap is
                // deferred to the next OnUpdate — never tear a loop down mid-tick.
                _pendingLaunch = true;
            };

            _config = ScriptableObject.CreateInstance<CosmicShore.UI.ArcadeGameConfigSO>();

            // Modal root: full-screen dim + CanvasGroup + the ported modal itself.
            var modalRoot = MakeChild("ArcadeGameConfigureModal", canvasRect);
            modalRoot.gameObject.SetActive(false); // wire fields before Awake/OnEnable/Start
            modalRoot.anchorMin = Vector2.zero;
            modalRoot.anchorMax = Vector2.one;
            modalRoot.offsetMin = Vector2.zero;
            modalRoot.offsetMax = Vector2.zero;
            modalRoot.gameObject.AddComponent<CanvasGroup>();
            var dim = modalRoot.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.82f);
            _configureModal = modalRoot.gameObject.AddComponent<ArcadeGameConfigureModal>();
            _configureModal.ModalType = ScreenSwitcher.ModalWindows.ARCADE_GAME_CONFIGURE;
            typeof(ModalWindowManager)
                .GetField("screenSwitcher", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(_configureModal, _switcher);
            typeof(ModalWindowManager)
                .GetField("audioSystem", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(_configureModal, _audioSystem);

            var panel = MakeChild("ConfigurePanel", modalRoot);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(1400f, 720f);
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.055f, 0.045f, 0.16f, 0.98f);
            panelImage.raycastTarget = false;

            // ── Game meta (left side — always visible) ─────────────────────────
            var nameLabel = MakeChild("SelectedGameName", panel);
            nameLabel.anchorMin = new Vector2(0f, 1f);
            nameLabel.anchorMax = new Vector2(0.42f, 1f);
            nameLabel.pivot = new Vector2(0.5f, 1f);
            nameLabel.anchoredPosition = new Vector2(0f, -36f);
            nameLabel.sizeDelta = new Vector2(0f, 64f);
            var nameText = nameLabel.gameObject.AddComponent<TextMeshProUGUI>();
            nameText.fontSize = 40f;
            nameText.color = new Color(0.55f, 0.95f, 1f, 1f);
            nameText.alignment = TextAlignmentOptions.Center;

            var descLabel = MakeChild("SelectedGameDescription", panel);
            descLabel.anchorMin = new Vector2(0f, 1f);
            descLabel.anchorMax = new Vector2(0.42f, 1f);
            descLabel.pivot = new Vector2(0.5f, 1f);
            descLabel.anchoredPosition = new Vector2(0f, -120f);
            descLabel.sizeDelta = new Vector2(-48f, 120f);
            var descText = descLabel.gameObject.AddComponent<TextMeshProUGUI>();
            descText.fontSize = 16f;
            descText.color = new Color(0.85f, 0.9f, 1f, 0.85f);
            descText.alignment = TextAlignmentOptions.Center;

            // ── Screen 1: ConfigurationDetailView ──────────────────────────────
            var screen1 = MakeChild("ConfigurationDetailView", panel);
            screen1.anchorMin = new Vector2(0.42f, 0f);
            screen1.anchorMax = Vector2.one;
            screen1.offsetMin = Vector2.zero;
            screen1.offsetMax = Vector2.zero;

            MakeSectionLabel(screen1, "INTENSITY", new Vector2(0f, -40f));
            var intensityRow = MakeChild("IntensityRow", screen1);
            intensityRow.anchorMin = intensityRow.anchorMax = new Vector2(0.5f, 1f);
            intensityRow.pivot = new Vector2(0.5f, 1f);
            intensityRow.anchoredPosition = new Vector2(0f, -80f);
            intensityRow.sizeDelta = new Vector2(520f, 110f);
            var intensityGroup = intensityRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            intensityGroup.spacing = 20f;
            intensityGroup.childForceExpandWidth = true;
            intensityGroup.childForceExpandHeight = true;

            var sharedIntensityVariable = ScriptableObject.CreateInstance<CosmicShore.Engine.Soap.IntVariable>();
            var intensityButtons = new List<IntensitySelectButton>();
            for (int level = 1; level <= 4; level++)
            {
                var isb = MakeIntensityButton(intensityRow, sharedIntensityVariable);
                intensityButtons.Add(isb);
                if (level == 2) _intensityTwoButton = isb;
            }

            MakeSectionLabel(screen1, "PLAYERS", new Vector2(0f, -230f));
            var (pcStepper, pcIncrement) = MakeStepper(screen1, "PlayerCountStepper", new Vector2(0f, -270f));
            _pcIncrementButton = pcIncrement;

            MakeSectionLabel(screen1, "DOMAINS", new Vector2(0f, -380f));
            var (dcStepper, _) = MakeStepper(screen1, "DomainCountStepper", new Vector2(0f, -420f));

            _confirmConfigButton = MakeActionButton(screen1, "ConfirmConfigurationButton", "CONFIRM CONFIGURATION",
                new Vector2(0f, 56f), new Vector2(420f, 76f));
            _confirmConfigButton.onClick.AddListener(() => _configureModal.OnConfirmConfiguration());

            // ── Screen 2: GameDetailView ───────────────────────────────────────
            var screen2 = MakeChild("GameDetailView", panel);
            screen2.anchorMin = new Vector2(0.42f, 0f);
            screen2.anchorMax = Vector2.one;
            screen2.offsetMin = Vector2.zero;
            screen2.offsetMax = Vector2.zero;

            MakeSectionLabel(screen2, "PICK YOUR DOMAIN", new Vector2(0f, -40f));
            var tileRow = MakeChild("DomainTileRow", screen2);
            tileRow.anchorMin = tileRow.anchorMax = new Vector2(0.5f, 1f);
            tileRow.pivot = new Vector2(0.5f, 1f);
            tileRow.anchoredPosition = new Vector2(0f, -80f);
            tileRow.sizeDelta = new Vector2(640f, 160f);
            var tileGroup = tileRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            tileGroup.spacing = 24f;
            tileGroup.childForceExpandWidth = true;
            tileGroup.childForceExpandHeight = true;

            var domainTiles = new List<DomainInfoData>
            {
                MakeDomainTile(tileRow, CosmicShore.Data.Domains.Jade, new Color(0.15f, 0.62f, 0.38f, 1f)),
                MakeDomainTile(tileRow, CosmicShore.Data.Domains.Ruby, new Color(0.68f, 0.16f, 0.28f, 1f)),
                MakeDomainTile(tileRow, CosmicShore.Data.Domains.Gold, new Color(0.75f, 0.58f, 0.14f, 1f)),
            };

            MakeSectionLabel(screen2, "VESSEL", new Vector2(0f, -290f));
            var shipLabel = MakeChild("ShipNameText", screen2);
            shipLabel.anchorMin = shipLabel.anchorMax = new Vector2(0.5f, 1f);
            shipLabel.pivot = new Vector2(0.5f, 1f);
            shipLabel.anchoredPosition = new Vector2(0f, -330f);
            shipLabel.sizeDelta = new Vector2(500f, 48f);
            var shipText = shipLabel.gameObject.AddComponent<TextMeshProUGUI>();
            shipText.fontSize = 30f;
            shipText.color = new Color(1f, 0.45f, 0.85f, 1f);
            shipText.alignment = TextAlignmentOptions.Center;

            _startGameButton = MakeActionButton(screen2, "StartGameButton", "START",
                new Vector2(0f, 56f), new Vector2(320f, 76f));
            _startGameButton.onClick.AddListener(() => _configureModal.OnStartGameClicked());

            var waitingLabel = MakeChild("WaitingForOthersLabel", screen2);
            waitingLabel.anchorMin = waitingLabel.anchorMax = new Vector2(0.5f, 0f);
            waitingLabel.pivot = new Vector2(0.5f, 0f);
            waitingLabel.anchoredPosition = new Vector2(0f, 56f);
            waitingLabel.sizeDelta = new Vector2(500f, 48f);
            var waitingText = waitingLabel.gameObject.AddComponent<TextMeshProUGUI>();
            waitingText.text = "WAITING FOR OTHERS...";
            waitingText.fontSize = 24f;
            waitingText.color = new Color(0.9f, 0.9f, 0.6f, 1f);
            waitingText.alignment = TextAlignmentOptions.Center;
            waitingLabel.gameObject.SetActive(false);

            // ── Field wiring (the prefab's inspector references) ───────────────
            SetPrivateField(_configureModal, "config", _config);
            SetPrivateField(_configureModal, "gameData", _gameData);
            SetPrivateField(_configureModal, "shipClassTypeVariable",
                ScriptableObject.CreateInstance<CosmicShore.Engine.Soap.IntVariable>());
            SetPrivateField(_configureModal, "arcadeExploreView", explore);
            SetPrivateField(_configureModal, "selectedGameName", nameText);
            SetPrivateField(_configureModal, "selectedGameDescription", descText);
            SetPrivateField(_configureModal, "configurationDetailView", screen1.gameObject);
            SetPrivateField(_configureModal, "gameDetailView", screen2.gameObject);
            SetPrivateField(_configureModal, "intensityButtons", intensityButtons);
            SetPrivateField(_configureModal, "pcStepper", pcStepper);
            SetPrivateField(_configureModal, "dcStepper", dcStepper);
            SetPrivateField(_configureModal, "domainInfoItems", domainTiles);
            SetPrivateField(_configureModal, "shipNameText", shipText);
            SetPrivateField(_configureModal, "startGameButton", _startGameButton);
            SetPrivateField(_configureModal, "waitingForOthersLabel", waitingLabel.gameObject);
            SetPrivateField(_configureModal, "confirmConfigurationButton", _confirmConfigButton);

            // ExploreView.SelectGame opens THIS modal — the restored 2b-iii seam.
            SetPrivateField(explore, "ArcadeGameConfigureModal", _configureModal);

            // Launch banner (host-side stand-in for SceneLoader's loading splash).
            var banner = MakeChild("LaunchBanner", canvasRect);
            banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 0f);
            banner.pivot = new Vector2(0.5f, 0f);
            banner.anchoredPosition = new Vector2(0f, 140f);
            banner.sizeDelta = new Vector2(1400f, 72f);
            _launchBanner = banner.gameObject.AddComponent<TextMeshProUGUI>();
            _launchBanner.fontSize = 40f;
            _launchBanner.color = new Color(0.4f, 1f, 0.6f, 1f);
            _launchBanner.alignment = TextAlignmentOptions.Center;
            banner.gameObject.SetActive(false);

            modalRoot.gameObject.SetActive(true); // Start hides it via CanvasGroup until opened
        }

        void MakeSectionLabel(RectTransform parent, string content, Vector2 anchoredPosition)
        {
            var label = MakeChild($"Label_{content}", parent);
            label.anchorMin = label.anchorMax = new Vector2(0.5f, 1f);
            label.pivot = new Vector2(0.5f, 1f);
            label.anchoredPosition = anchoredPosition;
            label.sizeDelta = new Vector2(600f, 32f);
            var text = label.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = 20f;
            text.color = new Color(0.7f, 0.8f, 1f, 0.9f);
            text.alignment = TextAlignmentOptions.Center;
        }

        IntensitySelectButton MakeIntensityButton(RectTransform row,
            CosmicShore.Engine.Soap.IntVariable sharedIntensityVariable)
        {
            var rect = MakeChild("IntensityButton", row);
            var border = rect.gameObject.AddComponent<Image>();
            border.color = new Color(0.2f, 0.25f, 0.55f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            var isb = rect.gameObject.AddComponent<IntensitySelectButton>();

            var fill = MakeChild("IntensityImage", rect);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(6f, 6f);
            fill.offsetMax = new Vector2(-6f, -6f);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(0.1f, 0.12f, 0.3f, 1f);
            fillImage.raycastTarget = false;

            var label = MakeFullStretchText(rect, "", 34f);

            SetPrivateField(isb, "BorderImage", border);
            SetPrivateField(isb, "IntensityImage", fillImage);
            SetPrivateField(isb, "IntensityText", label);
            SetPrivateField(isb, "selectedIntensityCount", sharedIntensityVariable);
            SetPrivateField(isb, "IntensityColorSelected", (Color32)new Color(1f, 1f, 1f, 1f));
            SetPrivateField(isb, "IntensityColorUnselected", (Color32)new Color(0.55f, 0.6f, 0.8f, 1f));
            SetPrivateField(isb, "IntensityColorActive", (Color32)new Color(0.9f, 0.95f, 1f, 1f));
            SetPrivateField(isb, "IntensityColorInactive", (Color32)new Color(0.3f, 0.32f, 0.4f, 1f));

            // IntensitySelectButton owns the border/fill colors — turn off the
            // Selectable's color transition so its white normalColor can't stamp
            // them (OnEnable lazily re-adopts a target graphic, so nulling the
            // reference wouldn't stick — Transition.None is the prefab-faithful way).
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(isb.Select); // prefab wiring: Button → Select()
            return isb;
        }

        (IntStepper stepper, Button increment) MakeStepper(RectTransform parent, string name, Vector2 anchoredPosition)
        {
            var rect = MakeChild(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(340f, 84f);
            var group = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 16f;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = true;

            var decrement = MakeStepperButton(rect, "Decrement", "-");
            var countLabel = MakeChild("Count", rect);
            var countText = countLabel.gameObject.AddComponent<TextMeshProUGUI>();
            countText.fontSize = 40f;
            countText.color = new Color(1f, 1f, 1f, 1f);
            countText.alignment = TextAlignmentOptions.Center;
            var increment = MakeStepperButton(rect, "Increment", "+");

            var stepper = rect.gameObject.AddComponent<IntStepper>();
            SetPrivateField(stepper, "decrementButton", decrement);
            SetPrivateField(stepper, "incrementButton", increment);
            SetPrivateField(stepper, "countText", countText);
            return (stepper, increment);
        }

        Button MakeStepperButton(RectTransform row, string name, string glyph)
        {
            var rect = MakeChild(name, row);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.22f, 0.5f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.18f, 0.22f, 0.5f, 1f);
            colors.pressedColor = new Color(0.45f, 0.6f, 0.95f, 1f);
            colors.disabledColor = new Color(0.12f, 0.13f, 0.22f, 1f);
            button.colors = colors;
            MakeFullStretchText(rect, glyph, 40f);
            return button;
        }

        DomainInfoData MakeDomainTile(RectTransform row, CosmicShore.Data.Domains domain, Color tint)
        {
            var rect = MakeChild($"Tile_{domain}", row);
            var background = rect.gameObject.AddComponent<Image>();
            background.color = tint;
            var button = rect.gameObject.AddComponent<Button>();
            // DomainInfoData owns the tile's tint + dim — turn off the Selectable's
            // color transition so its white normalColor can't stamp the domain hue.
            button.transition = Selectable.Transition.None;
            var label = MakeFullStretchText(rect, domain.ToString().ToUpperInvariant(), 26f);
            var tile = rect.gameObject.AddComponent<DomainInfoData>();
            SetPrivateField(tile, "domain", domain);
            SetPrivateField(tile, "button", button);
            SetPrivateField(tile, "backgroundImage", background);
            SetPrivateField(tile, "labelText", label);
            return tile;
        }

        Button MakeActionButton(RectTransform parent, string name, string caption,
            Vector2 anchoredPosition, Vector2 size)
        {
            var rect = MakeChild(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.16f, 0.42f, 0.28f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.16f, 0.42f, 0.28f, 1f);
            colors.pressedColor = new Color(0.35f, 0.8f, 0.5f, 1f);
            button.colors = colors;
            MakeFullStretchText(rect, caption, 26f);
            return button;
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
            // ── game phase: step the round (it owns its own GameLoop) ──────────
            if (_phase == HostPhase.Game)
            {
                _frameIndex++;
                if (_round == null) return;

                if (!_roundDone)
                {
                    if (_round.StepFrame())
                    {
                        _roundDone = true;
                        _round.FinishAndScore();
                        _gameFinishFrame = _frameIndex;
                    }
                    else if (_round.FramesStepped >= _round.MaxFrames)
                    {
                        _roundDone = true;
                        _round.CompleteStepping();
                        _gameFinishFrame = _frameIndex;
                    }
                }
                else if (_frameIndex - _gameFinishFrame >= ReturnLingerFrames)
                {
                    ReturnToMenu();
                }
                return;
            }

            // ── menu phases: FIXED-step ticking — the 0.5s slide/hide coroutines
            // span exactly 30 ticks, so every scripted click lands on settled
            // layout and captures are byte-identical across runs.
            _loop.Tick(1f / 60f);
            _frameIndex++;

            // OnLaunchGame fired inside the tick above — swap worlds now.
            if (_pendingLaunch)
            {
                _pendingLaunch = false;
                EnterGame();
                return;
            }

            // The scripted click flow drives only the FIRST menu visit.
            if (_phase != HostPhase.Menu) return;

            if (_frameIndex == 30 && _hangarNavButton != null)
                ClickButton(_hangarNavButton);

            // Frame 60: open the arcade MODAL through the shipping path — a real click
            // on the ARCADE nav → OnClickArcadeNav → ArcadeModal.ModalWindowIn.
            if (_frameIndex == 60 && _arcadeNavButton != null)
                ClickButton(_arcadeNavButton);

            // Frame 90: click the HEX RACE card → ArcadeExploreView.SelectGame →
            // configure modal opens at Screen 1 with the game's defaults.
            if (_frameIndex == 90)
            {
                foreach (var card in Object.FindObjectsByType<GameCard>(FindObjectsSortMode.None))
                {
                    if (card.GameMode != CosmicShore.Data.GameModes.HexRace) continue;
                    ClickButton(card.GetComponent<Button>());
                    break;
                }
            }

            // Frame 120: pick intensity 2 (IntensitySelectButton.Select via Button).
            if (_frameIndex == 120 && _intensityTwoButton != null)
                ClickButton(_intensityTwoButton.GetComponent<Button>());

            // Frame 132: player-count “+” — IntStepper → HandlePlayerCountSelected(2)
            // (one AI backfill slot at launch: 2 desired − 1 human).
            if (_frameIndex == 132 && _pcIncrementButton != null)
                ClickButton(_pcIncrementButton);

            // Frame 150: CONFIRM CONFIGURATION → single-shot commit → Screen 2.
            if (_frameIndex == 150 && _confirmConfigButton != null)
                ClickButton(_confirmConfigButton);

            // Frame 180: START → solo path (no sync manager) → HandleAllPlayersReady
            // → GameDataSO synced + OnLaunchGame raised (LAUNCHING banner shows).
            if (_frameIndex == 180 && _startGameButton != null)
                ClickButton(_startGameButton);
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

        // ── the menu→game→menu handoff (the SceneLoader.LaunchGame seam) ──────

        /// <summary>
        /// Tear down the MENU world and stand up the mode host for
        /// gameData.GameMode — the windowed counterpart of the host/server Netcode
        /// scene load. The launch parameters are captured from the SAME GameDataSO
        /// the configure modal synced (ScriptableObjects outlive the GameLoop).
        /// </summary>
        void EnterGame()
        {
            var mode = _gameData.GameMode;
            int players = Math.Max(1, _gameData.SelectedPlayerCount.Value);
            string game = mode switch
            {
                CosmicShore.Data.GameModes.MultiplayerCrystalCapture => "crystalcapture",
                CosmicShore.Data.GameModes.MultiplayerJoust => "joust",
                CosmicShore.Data.GameModes.AstroLeague => "astroleague",
                _ => "hexrace",
            };

            Console.WriteLine($"[menushell] LAUNCH — tearing down the menu world, standing up {mode} ({players} pilots)");

            // Unity scene-unload semantics: the menu world dies with its loop; the
            // fresh-world statics the round's GameLoop resets are exactly why the
            // two worlds are mutually exclusive.
            _loop.Dispose();
            _loop = null;
            _switcher = null;
            _module = null;
            _hangarNavButton = null;
            _arcadeNavButton = null;
            _arcadeModal = null;
            _configureModal = null;
            _intensityTwoButton = null;
            _pcIncrementButton = null;
            _confirmConfigButton = null;
            _startGameButton = null;
            _launchBanner = null;

            _round = ModeHostWindow.CreateDriver(game, seed: 42, players, crystalTarget: 6,
                line => Console.WriteLine("  " + line));
            _roundDone = false;
            _phase = HostPhase.Game;
        }

        /// <summary>
        /// Dispose the finished round and REBUILD the menu world from scratch —
        /// the windowed counterpart of returning to Menu_Main. ScreenSwitcher.Start
        /// consumes the persisted ReturnToScreen (game modals never auto-reopen).
        /// </summary>
        void ReturnToMenu()
        {
            _lastGameSummary = _round.Finished
                ? $"{ModeHostWindow.DiagName(_round)} winner {_round.WinnerName} ({_round.WinnerDomain})"
                : $"{ModeHostWindow.DiagName(_round)} timeout";
            Console.WriteLine($"[menushell] RETURN — {_lastGameSummary}; rebuilding the menu world");

            _round.Dispose();
            _round = null;

            _loop = new GameLoop("MenuShell");
            BuildMenu(firstBoot: false);
            _phase = HostPhase.MenuReturned;

            // The loop's summary where the loading splash would land (host-level).
            if (_launchBanner != null)
            {
                _launchBanner.text = $"RETURNED - {_lastGameSummary}";
                _launchBanner.gameObject.SetActive(true);
            }
        }

        unsafe void OnRender(double dt)
        {
            if (_phase == HostPhase.Game && _round != null)
            {
                _scene.Render(_round, _window.FramebufferSize.X, _window.FramebufferSize.Y);
                _scene.DrawHud(_ui, _round, _window.FramebufferSize.X, _window.FramebufferSize.Y);
            }
            else
            {
                _gl.Viewport(0, 0, (uint)_window.FramebufferSize.X, (uint)_window.FramebufferSize.Y);
                _gl.ClearColor(0.02f, 0.015f, 0.06f, 1f); // RoundScenePass clears indigo — restore the menu's own
                _gl.Clear(ClearBufferMask.ColorBufferBit);

                UiCanvasBridge.Render(_ui, _window.FramebufferSize.X, _window.FramebufferSize.Y);
            }

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

            if (_phase == HostPhase.Game && _round != null)
            {
                // Mid-game capture: the menu world is disposed — report the round.
                Console.WriteLine($"screenshot → {_screenshotPath} ({w}x{h}) frame {_frameIndex}, " +
                    $"mode menushell, phase game, game {ModeHostWindow.DiagName(_round)}, " +
                    $"t {RoundScenePass.Clock(_round):0.00}, claims {_round.TotalClaims}, " +
                    $"jade {RoundScenePass.DomainSum(_round, CosmicShore.Data.Domains.Jade)} " +
                    $"ruby {RoundScenePass.DomainSum(_round, CosmicShore.Data.Domains.Ruby)} " +
                    $"gold {RoundScenePass.DomainSum(_round, CosmicShore.Data.Domains.Gold)}, " +
                    $"state {(_round.Finished ? "Finished" : "Racing")}, " +
                    $"winner {(_round.Finished ? _round.WinnerName : "none")}, " +
                    $"launch {_launchInfo}");
                return;
            }

            string active = "?";
            foreach (var (id, title, _) in Panels)
                if (_switcher.ScreenIsActive(id)) active = title;
            var screensRoot = _switcher.transform;
            int cardCount = 0;
            foreach (var card in Object.FindObjectsByType<GameCard>(FindObjectsSortMode.None))
                if (card.gameObject.activeInHierarchy) cardCount++;
            Console.WriteLine($"screenshot → {_screenshotPath} ({w}x{h}) frame {_frameIndex}, " +
                $"mode menushell, phase {(_phase == HostPhase.MenuReturned ? "menuReturned" : "menu")}, " +
                $"active {active}, slideX {screensRoot.position.x:F1}, " +
                $"modalStack {(_switcher.HasActiveModal ? "open" : "empty")}, " +
                $"arcadeModal {(_switcher.ModalIsActive(ScreenSwitcher.ModalWindows.ARCADE) ? "ARCADE" : "none")}, " +
                $"gameCards {cardCount}, " +
                $"configIntensity {_config.Intensity}, configPlayers {_config.PlayerCount}, configDomains {_config.DomainCount}, " +
                $"launch {_launchInfo}, lastGame {_lastGameSummary}, paused {CosmicShore.Core.PauseSystem.Paused}");
        }
    }
}

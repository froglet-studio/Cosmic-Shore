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
// Silk.NET.Input aliased (not opened) — its Button would collide with the engine's UI Button.
using IInputContext = Silk.NET.Input.IInputContext;
using Key = Silk.NET.Input.Key;
using InputWindowExtensions = Silk.NET.Input.InputWindowExtensions;

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

        // Arc H: Tab in the game phase hands the stick to the human (and back).
        IInputContext _inputContext;
        readonly HumanPilotBridge _bridge = new();
        bool _prevTab;

        // Arc I: the real in-round UI (Ready button + scoreboard panel).
        RoundUiOverlay _overlay;
        CosmicShore.ScriptableObjects.SO_GameList _gameList;
        readonly string _screenPeek;
        Button _portNavButton;
        Button _storeNavButton;
        PurchaseConfirmationModal _purchaseModal;

        static readonly (ScreenSwitcher.MenuScreens id, string title, Color tint)[] Panels =
        {
            (ScreenSwitcher.MenuScreens.STORE, "STORE", new Color(0.20f, 0.08f, 0.30f, 1f)),
            (ScreenSwitcher.MenuScreens.ARK, "ARK", new Color(0.08f, 0.22f, 0.18f, 1f)),
            (ScreenSwitcher.MenuScreens.HOME, "HOME", new Color(0.045f, 0.03f, 0.11f, 1f)),
            (ScreenSwitcher.MenuScreens.PORT, "PORT", new Color(0.05f, 0.16f, 0.28f, 1f)),
            (ScreenSwitcher.MenuScreens.HANGAR, "HANGAR", new Color(0.16f, 0.10f, 0.05f, 1f)),
        };

        public MenuShellWindow(string screenshotPath, int screenshotFrame, string screenPeek = null)
        {
            _screenshotPath = screenshotPath;
            _screenshotFrame = screenshotFrame;
            _screenPeek = screenPeek;
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

            // Arc H: hardware keyboard for the game phase (extension called statically —
            // opening Silk.NET.Input would collide Button with the engine's UI Button).
            _inputContext = InputWindowExtensions.CreateInput(_window);

            _loop = new GameLoop("MenuShell");
            BuildMenu(firstBoot: true);
            Console.WriteLine("[3/3] ready — menu shell. In a game: Tab toggles human/autopilot, WASD+arrows fly, Space boost, Shift drift.");
        }

        void BuildMenu(bool firstBoot)
        {
            _gameList = null; // fresh SO fixture per menu world (matches the old per-build behavior)
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            _module = esGo.AddComponent<StandaloneInputModule>();

            // Scene transcription: Menu_Main always carries the UserActionSystem
            // singleton (the HANGAR nav completes a ViewHangarMenu action through it)
            // and the AudioSystem (modal open/close cues route through it).
            new GameObject("UserActionSystem").AddComponent<CosmicShore.Core.UserActionSystem>();
            BuildAudioSystem();
            new GameObject("CallToActionSystem").AddComponent<CosmicShore.Core.CallToActionSystem>();
            BuildProgressionService();

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

                if (id == ScreenSwitcher.MenuScreens.STORE)
                {
                    // The real StoreScreen on the STORE panel — header shrinks like
                    // the other real screens.
                    label.anchorMin = new Vector2(0f, 0.88f);
                    label.anchorMax = Vector2.one;
                    text.fontSize = 40f;
                    BuildStoreScreen(panel, canvasRect);
                }

                if (id == ScreenSwitcher.MenuScreens.PORT)
                {
                    // The real LeaderboardsMenu on the PORT panel — header shrinks
                    // like the hangar's so the board owns the panel.
                    label.anchorMin = new Vector2(0f, 0.88f);
                    label.anchorMax = Vector2.one;
                    text.fontSize = 40f;
                    BuildLeaderboardsScreen(panel);
                }

                if (id == ScreenSwitcher.MenuScreens.HANGAR)
                {
                    // The real HangarScreen (grid of vessel cards + detail view with
                    // the crystal unlock flow) — the placeholder title shrinks to a
                    // header so the grid owns the panel.
                    label.anchorMin = new Vector2(0f, 0.88f);
                    label.anchorMax = Vector2.one;
                    text.fontSize = 40f;
                    BuildHangarScreen(panel);
                }

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
            // PORT hosts the real LeaderboardsMenu now — only ARK stays disabled
            // (the scene-serialized override the Unity build would carry).
            SetPrivateField(_switcher, "disabledScreens",
                new List<ScreenSwitcher.MenuScreens> { ScreenSwitcher.MenuScreens.ARK });

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
                if (label == "PORT") _portNavButton = button;
                if (label == "STORE") _storeNavButton = button;

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
        /// <summary>
        /// The REAL AudioSystem, LIVE (AudioSystem unit): a fully-wired rig
        /// modelling the Unity scene's inspector authoring — a GameSetting
        /// singleton (PlayerPrefs-backed levels/toggles), the master AudioMixer,
        /// and the three legacy AudioSources. Start pulls the setting state and
        /// drives the FMOD SFX bus (engine placeholder) exactly like the wire;
        /// modal open/close cues route through PlayMenuAudio. No FMOD events
        /// exist in the port fixture yet, so the migration warn flag is off —
        /// the authored-scene posture once all slots are filled. Statics
        /// (AudioSystem.Instance, SingletonPersistent&lt;GameSetting&gt;.Instance)
        /// are cleared first: the previous menu world's objects are not
        /// destroyed by GameLoop disposal, so the duplicate-instance guards
        /// would otherwise kill every rebuilt world's components.
        /// </summary>
        void BuildAudioSystem()
        {
            _audioSystem = CosmicShore.Cli.AudioSystemRig.Create();
        }

        /// <summary>
        /// The REAL GameModeProgressionService, LIVE (Progression unit): a quest
        /// chain over the same three arcade modes — HexRace is first-in-chain
        /// (free), Joust and Crystal Capture are locked behind it. The arcade
        /// cards show real LOCKED overlays through ArcadeExploreView's
        /// progression check, the configure modal clamps + gates intensities
        /// (the locked-intensity toast lane is live), and the hangar gate runs
        /// the real quest-prefix rule. Persistence rides a local UGSDataService
        /// rig (repos real, auth dormant — readiness flipped like a fresh
        /// signed-out boot that already loaded local data).
        /// </summary>
        void BuildProgressionService()
        {
            var authVar = ScriptableObject.CreateInstance<CosmicShore.ScriptableObjects.AuthenticationDataVariable>();
            authVar.Value = new CosmicShore.ScriptableObjects.AuthenticationData
            {
                OnSignedIn = ScriptableObject.CreateInstance<CosmicShore.Engine.Soap.ScriptableEventNoParam>(),
            };
            var vesselList = ScriptableObject.CreateInstance<SO_VesselList>();
            vesselList.VesselList = new List<SO_Vessel>();
            var ugsGo = new GameObject("UGSDataService");
            ugsGo.SetActive(false);
            var ugs = ugsGo.AddComponent<CosmicShore.Core.UGSDataService>();
            SetPrivateField(ugs, "vesselList", vesselList);
            SetPrivateField(ugs, "_authData", authVar);
            ugsGo.SetActive(true); // Awake: repos created
            typeof(CosmicShore.Core.UGSDataService).GetProperty("IsInitialized")!.SetValue(ugs, true);

            CosmicShore.ScriptableObjects.SO_GameModeQuestData MakeQuest(
                CosmicShore.Data.GameModes mode, string displayName,
                CosmicShore.ScriptableObjects.QuestTargetType targetType, float targetValue)
            {
                var quest = ScriptableObject.CreateInstance<CosmicShore.ScriptableObjects.SO_GameModeQuestData>();
                quest.GameMode = mode;
                quest.DisplayName = displayName;
                quest.TargetType = targetType;
                quest.TargetValue = targetValue;
                return quest;
            }
            var questList = ScriptableObject.CreateInstance<CosmicShore.ScriptableObjects.SO_GameModeQuestList>();
            questList.Quests = new List<CosmicShore.ScriptableObjects.SO_GameModeQuestData>
            {
                MakeQuest(CosmicShore.Data.GameModes.HexRace, "HEX RACE",
                    CosmicShore.ScriptableObjects.QuestTargetType.CrystalsCollected, 3f),
                MakeQuest(CosmicShore.Data.GameModes.MultiplayerJoust, "JOUST",
                    CosmicShore.ScriptableObjects.QuestTargetType.JoustsWon, 3f),
                MakeQuest(CosmicShore.Data.GameModes.MultiplayerCrystalCapture, "CRYSTAL CAPTURE",
                    CosmicShore.ScriptableObjects.QuestTargetType.CrystalsCollected, 3f),
            };

            var serviceGo = new GameObject("GameModeProgressionService");
            serviceGo.SetActive(false);
            var progression = serviceGo.AddComponent<CosmicShore.Core.GameModeProgressionService>();
            SetPrivateField(progression, "questList", questList);
            SetPrivateField(progression, "_ugsDataService", ugs);
            serviceGo.SetActive(true); // Awake: Instance + first-mode (HexRace) unlock

            // The participation-XP companion (inert without a PlayerDataService —
            // its AddXP lane null-guards, exactly the upstream fresh-boot posture).
            var xpGo = new GameObject("ParticipationXpAwarder");
            xpGo.AddComponent<CosmicShore.Core.ParticipationXpAwarder>();
        }

        /// <summary>
        /// The REAL StoreScreen on the STORE panel (Store unit): crystal + ticket
        /// balances, the captain purchase grid, the daily-challenge ticket card, and
        /// the purchase-confirmation modal — all running the CatalogManager's LOCAL
        /// economy lanes (upstream's PlayFab lanes are inert there too). Fixture: a
        /// 500-crystal wallet, three encountered captains priced 150/300/450, and
        /// the 25-crystal ticket — seeded through the same internal landing lanes
        /// the PlayFab callbacks funneled into.
        /// </summary>
        void BuildStoreScreen(RectTransform panel, RectTransform canvasRect)
        {
            panel.gameObject.SetActive(false); // wire fields before OnEnable/Start

            // ── economy singletons + deterministic fixture ─────────────────────
            CosmicShore.Core.CatalogManager.ResetLocalEconomy();
            var captainManagerGo = new GameObject("CaptainManager");
            var captainManager = captainManagerGo.AddComponent<CosmicShore.Core.CaptainManager>();
            new GameObject("DailyRewardHandler").AddComponent<CosmicShore.Core.DailyRewardHandler>();

            var catalogGo = new GameObject("CatalogManager");
            catalogGo.SetActive(false);
            var catalog = catalogGo.AddComponent<CosmicShore.Core.CatalogManager>();
            var netVariable = ScriptableObject.CreateInstance<CosmicShore.ScriptableObjects.NetworkMonitorDataVariable>();
            netVariable.Value = new CosmicShore.ScriptableObjects.NetworkMonitorData
            {
                OnNetworkFound = ScriptableObject.CreateInstance<CosmicShore.Engine.Soap.ScriptableEventNoParam>(),
                OnNetworkLost = ScriptableObject.CreateInstance<CosmicShore.Engine.Soap.ScriptableEventNoParam>(),
            };
            SetPrivateField(catalog, "_networkMonitorDataVariable", netVariable);
            SetPrivateField(catalog, "_captainManager", captainManager);
            catalogGo.SetActive(true);

            (string name, CosmicShore.Data.VesselClassType cls, CosmicShore.Data.Element element, int cost, string flavor)[] roster =
            {
                ("AURELIA", CosmicShore.Data.VesselClassType.Manta, CosmicShore.Data.Element.Space, 150, "Cartographer of the open sea."),
                ("KORVAX", CosmicShore.Data.VesselClassType.Rhino, CosmicShore.Data.Element.Mass, 300, "Breaks lines. Keeps promises."),
                ("SIRRA", CosmicShore.Data.VesselClassType.Dolphin, CosmicShore.Data.Element.Time, 450, "Arrives before the wave does."),
            };

            var captains = new List<CosmicShore.Data.Captain>();
            var shelveItems = new List<CosmicShore.Core.VirtualItem>();
            var crystal = new CosmicShore.Core.VirtualItem
            {
                ItemId = "crystal-omni",
                Name = "Omni Crystal",
                ContentType = "Crystal",
                Tags = new List<string> { "Omni" },
                Price = new List<CosmicShore.Core.ItemPrice>(),
                Amount = 500, // the wallet — one shared instance in shelve + inventory
            };
            shelveItems.Add(crystal);

            int captainIndex = 0;
            foreach (var (name, cls, element, cost, flavor) in roster)
            {
                var vessel = ScriptableObject.CreateInstance<SO_Vessel>(); // global namespace (as upstream)
                vessel.Name = cls.ToString();
                vessel.Class = cls;
                var soCaptain = ScriptableObject.CreateInstance<CosmicShore.ScriptableObjects.SO_Captain>();
                soCaptain.Name = name;
                soCaptain.Description = flavor;
                soCaptain.Vessel = vessel;
                soCaptain.PrimaryElement = element;
                captains.Add(new CosmicShore.Data.Captain(soCaptain) { Encountered = true });

                shelveItems.Add(new CosmicShore.Core.VirtualItem
                {
                    ItemId = $"captain-{++captainIndex}",
                    Name = name,
                    Description = flavor,
                    ContentType = "Captain",
                    Tags = new List<string>(),
                    Price = new List<CosmicShore.Core.ItemPrice>
                    {
                        new() { ItemId = "crystal-omni", Amount = cost, UnitAmount = 1 },
                    },
                    Amount = 1, // PlayFab inventory items carry >=1 — the over-purchase guard reads it
                });
            }

            shelveItems.Add(new CosmicShore.Core.VirtualItem
            {
                ItemId = "ticket-dc",
                Name = "Daily Challenge Ticket",
                Description = "One entry into today's challenge.",
                ContentType = "Ticket",
                Tags = new List<string>(),
                Price = new List<CosmicShore.Core.ItemPrice>
                {
                    new() { ItemId = "crystal-omni", Amount = 25, UnitAmount = 1 },
                },
                Amount = 0,
            });

            captainManager.LoadLocalCaptains(captains);
            catalog.LoadLocalCatalog(shelveItems);
            catalog.LoadLocalInventory(new List<CosmicShore.Core.VirtualItem> { crystal });

            // ── balances header (top right) ────────────────────────────────────
            TMP_Text MakeBalance(string balanceName, float anchorX)
            {
                var cell = MakeChild(balanceName, panel);
                cell.anchorMin = new Vector2(anchorX, 0.88f);
                cell.anchorMax = new Vector2(anchorX + 0.14f, 0.96f);
                cell.offsetMin = Vector2.zero;
                cell.offsetMax = Vector2.zero;
                var cellImage = cell.gameObject.AddComponent<Image>();
                cellImage.color = new Color(0.12f, 0.06f, 0.20f, 1f);
                cellImage.raycastTarget = false;
                var labelText = MakeChild("Label", cell);
                labelText.anchorMin = new Vector2(0f, 0.5f);
                labelText.anchorMax = Vector2.one;
                labelText.offsetMin = Vector2.zero;
                labelText.offsetMax = Vector2.zero;
                var caption = labelText.gameObject.AddComponent<TextMeshProUGUI>();
                caption.text = balanceName == "CrystalBalance" ? "CRYSTALS" : "TICKETS";
                caption.fontSize = 14f;
                caption.color = new Color(0.7f, 0.8f, 0.95f, 1f);
                caption.alignment = TextAlignmentOptions.Center;
                var valueRect = MakeChild("Value", cell);
                valueRect.anchorMin = Vector2.zero;
                valueRect.anchorMax = new Vector2(1f, 0.5f);
                valueRect.offsetMin = Vector2.zero;
                valueRect.offsetMax = Vector2.zero;
                var value = valueRect.gameObject.AddComponent<TextMeshProUGUI>();
                value.fontSize = 22f;
                value.color = new Color(0.55f, 0.95f, 1f, 1f);
                value.alignment = TextAlignmentOptions.Center;
                return value;
            }
            var crystalBalanceText = MakeBalance("CrystalBalance", 0.66f);
            var ticketBalanceText = MakeBalance("TicketBalance", 0.82f);

            // Templates live ACTIVE under an inactive holder: upstream's prefabs are
            // active assets, so Instantiate must yield active clones (an inactive
            // template would clone inactive and StoreScreen never re-activates).
            var templateHolder = MakeChild("Templates", panel);
            templateHolder.gameObject.SetActive(false);

            // ── purchase card template builder (captain / game / ticket share it) ──
            T MakeCardTemplate<T>(string cardName, RectTransform parent) where T : PurchaseItemCard
            {
                var card = MakeChild(cardName, parent);
                card.sizeDelta = new Vector2(420f, 240f);
                var cardBg = card.gameObject.AddComponent<Image>();
                cardBg.color = new Color(0.16f, 0.09f, 0.26f, 1f);
                var cardButton = card.gameObject.AddComponent<Button>();
                cardButton.transition = Selectable.Transition.None;

                var nameText = MakeChild("Name", card);
                nameText.anchorMin = new Vector2(0f, 0.74f);
                nameText.anchorMax = Vector2.one;
                nameText.offsetMin = Vector2.zero;
                nameText.offsetMax = Vector2.zero;
                var nameLabel = nameText.gameObject.AddComponent<TextMeshProUGUI>();
                nameLabel.fontSize = 24f;
                nameLabel.color = new Color(0.95f, 0.85f, 0.6f, 1f);
                nameLabel.alignment = TextAlignmentOptions.Center;

                var descText = MakeChild("Description", card);
                descText.anchorMin = new Vector2(0.04f, 0.36f);
                descText.anchorMax = new Vector2(0.96f, 0.72f);
                descText.offsetMin = Vector2.zero;
                descText.offsetMax = Vector2.zero;
                var descLabel = descText.gameObject.AddComponent<TextMeshProUGUI>();
                descLabel.fontSize = 15f;
                descLabel.color = new Color(0.85f, 0.88f, 0.95f, 1f);
                descLabel.alignment = TextAlignmentOptions.Center;

                var itemImageRect = MakeChild("ItemImage", card);
                itemImageRect.anchorMin = new Vector2(0.4f, 0.36f);
                itemImageRect.anchorMax = new Vector2(0.6f, 0.7f);
                itemImageRect.offsetMin = Vector2.zero;
                itemImageRect.offsetMax = Vector2.zero;
                var itemImage = itemImageRect.gameObject.AddComponent<Image>();
                itemImage.color = new Color(1f, 1f, 1f, 0f); // sprite art arrives with Arc E
                itemImage.raycastTarget = false;

                (RectTransform rect, TextMeshProUGUI label, Image image) MakeStateButton(string stateName, string caption, Color color, bool active)
                {
                    var state = MakeChild(stateName, card);
                    state.anchorMin = new Vector2(0.2f, 0.06f);
                    state.anchorMax = new Vector2(0.8f, 0.30f);
                    state.offsetMin = Vector2.zero;
                    state.offsetMax = Vector2.zero;
                    var stateImage = state.gameObject.AddComponent<Image>();
                    stateImage.color = color;
                    stateImage.raycastTarget = false;
                    var stateLabel = MakeFullStretchText(state, caption, 20f);
                    state.gameObject.SetActive(active);
                    return (state, stateLabel, stateImage);
                }
                var (_, priceLabel, priceImage) = MakeStateButton("PriceButton", "", new Color(0.16f, 0.42f, 0.28f, 1f), active: true);
                var (_, unavailableLabel, unavailableImage) = MakeStateButton("UnavailableButton", "", new Color(0.4f, 0.16f, 0.14f, 1f), active: false);
                var (_, ownedLabel, ownedImage) = MakeStateButton("PurchasedButton", "OWNED", new Color(0.2f, 0.26f, 0.5f, 1f), active: false);

                var component = card.gameObject.AddComponent<T>();
                SetPrivateField(component, "PriceLabel", priceLabel);
                SetPrivateField(component, "UnavailablePriceLabel", unavailableLabel);
                SetPrivateField(component, "ItemNameLabel", nameLabel);
                SetPrivateField(component, "ItemDescriptionLabel", descLabel);
                SetPrivateField(component, "ItemImage", itemImage);
                SetPrivateField(component, "PriceButton", priceImage);
                SetPrivateField(component, "UnavailableButton", unavailableImage);
                SetPrivateField(component, "PurchasedButton", ownedImage);
                SetPrivateField(component, "BackgroundImage", cardBg);
                // Prefab-persistent listener stand-in: a template-captured delegate
                // would survive cloning pointing at the TEMPLATE card — the binding
                // component re-wires each clone to ITS OWN card on Awake.
                card.gameObject.AddComponent<PurchaseCardClickBinding>();
                return component;
            }

            // ── captain purchase section: two rows + inactive template ─────────
            var captainSection = MakeChild("CaptainPurchaseSection", panel);
            captainSection.anchorMin = new Vector2(0.04f, 0.30f);
            captainSection.anchorMax = new Vector2(0.72f, 0.86f);
            captainSection.offsetMin = Vector2.zero;
            captainSection.offsetMax = Vector2.zero;
            var captainRows = new List<HorizontalLayoutGroup>();
            for (int r = 0; r < 2; r++)
            {
                var row = MakeChild($"CaptainRow_{r}", captainSection);
                row.anchorMin = new Vector2(0f, r == 0 ? 0.52f : 0.02f);
                row.anchorMax = new Vector2(1f, r == 0 ? 0.98f : 0.48f);
                row.offsetMin = Vector2.zero;
                row.offsetMax = Vector2.zero;
                var rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                rowGroup.spacing = 20f;
                rowGroup.childForceExpandWidth = true;
                rowGroup.childForceExpandHeight = true;
                if (r > 0) row.gameObject.SetActive(false); // upstream activates on overflow
                captainRows.Add(rowGroup);
            }
            var captainTemplate = MakeCardTemplate<PurchaseCaptainCard>("CaptainCardTemplate", templateHolder);
            SetPrivateField(captainTemplate, "_captainManager", captainManager); // clones inherit the inject

            // ── daily-challenge ticket card (fixed scene instance) ─────────────
            var ticketArea = MakeChild("TicketArea", panel);
            ticketArea.anchorMin = new Vector2(0.75f, 0.44f);
            ticketArea.anchorMax = new Vector2(0.96f, 0.86f);
            ticketArea.offsetMin = Vector2.zero;
            ticketArea.offsetMax = Vector2.zero;
            var ticketCard = MakeCardTemplate<PurchaseGameplayTicketCard>("DailyChallengeTicketCard", ticketArea);
            var ticketRect = (RectTransform)ticketCard.transform;
            ticketRect.anchorMin = Vector2.zero;
            ticketRect.anchorMax = Vector2.one;
            ticketRect.offsetMin = Vector2.zero;
            ticketRect.offsetMax = Vector2.zero;
            var ticketName = (TMP_Text)typeof(PurchaseItemCard)
                .GetField("ItemNameLabel", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(ticketCard);
            ticketName.text = "DAILY CHALLENGE TICKET";
            var ticketDesc = (TMP_Text)typeof(PurchaseItemCard)
                .GetField("ItemDescriptionLabel", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(ticketCard);
            ticketDesc.text = "One entry into today's challenge.";

            // ── game purchase section (ShowGamePurchasing defaults FALSE upstream) ──
            var gameSection = MakeChild("GamePurchaseSection", panel);
            gameSection.gameObject.SetActive(false);
            var gameRow0 = MakeChild("GameRow_0", gameSection);
            var gameRowGroup = gameRow0.gameObject.AddComponent<HorizontalLayoutGroup>();
            var gameTemplate = MakeCardTemplate<PurchaseGameCard>("GameCardTemplate", templateHolder);

            // ── purchase confirmation modal (canvas-level, like the arcade modal) ──
            var modalRoot = MakeChild("PurchaseConfirmationModal", canvasRect);
            modalRoot.gameObject.SetActive(false);
            modalRoot.anchorMin = Vector2.zero;
            modalRoot.anchorMax = Vector2.one;
            modalRoot.offsetMin = Vector2.zero;
            modalRoot.offsetMax = Vector2.zero;
            modalRoot.gameObject.AddComponent<CanvasGroup>();
            var modalDim = modalRoot.gameObject.AddComponent<Image>();
            modalDim.color = new Color(0f, 0f, 0f, 0.72f);
            _purchaseModal = modalRoot.gameObject.AddComponent<PurchaseConfirmationModal>();
            _purchaseModal.ModalType = ScreenSwitcher.ModalWindows.PURCHASE_ITEM_CONFIRMATION;
            SetPrivateField(_purchaseModal, "screenSwitcher", _switcher);
            typeof(ModalWindowManager)
                .GetField("audioSystem", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(_purchaseModal, _audioSystem);
            SetPrivateField(_purchaseModal, "_captainManager", captainManager);

            var modalPanel = MakeChild("Panel", modalRoot);
            modalPanel.anchorMin = modalPanel.anchorMax = new Vector2(0.5f, 0.5f);
            modalPanel.sizeDelta = new Vector2(640f, 420f);
            var modalPanelImage = modalPanel.gameObject.AddComponent<Image>();
            modalPanelImage.color = new Color(0.10f, 0.06f, 0.22f, 0.98f);
            modalPanelImage.raycastTarget = false;

            TMP_Text MakeModalText(string textName, float yMin, float yMax, float size, Color color)
            {
                var rect = MakeChild(textName, modalPanel);
                rect.anchorMin = new Vector2(0.05f, yMin);
                rect.anchorMax = new Vector2(0.95f, yMax);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var modalText = rect.gameObject.AddComponent<TextMeshProUGUI>();
                modalText.fontSize = size;
                modalText.color = color;
                modalText.alignment = TextAlignmentOptions.Center;
                return modalText;
            }
            var modalPrice = MakeModalText("PriceLabel", 0.62f, 0.80f, 40f, new Color(0.55f, 0.95f, 1f, 1f));
            var modalUnlock = MakeModalText("UnlockText", 0.46f, 0.60f, 22f, new Color(0.9f, 0.92f, 0.95f, 1f));
            var modalCrystalBalance = MakeModalText("CrystalBalance", 0.32f, 0.44f, 20f, new Color(0.7f, 0.8f, 0.95f, 1f));
            var modalTicketBalance = MakeModalText("TicketBalance", 0.20f, 0.32f, 20f, new Color(0.7f, 0.8f, 0.95f, 1f));

            Button MakeModalButton(string buttonName, string caption, float xMin, float xMax, Color color)
            {
                var rect = MakeChild(buttonName, modalPanel);
                rect.anchorMin = new Vector2(xMin, 0.04f);
                rect.anchorMax = new Vector2(xMax, 0.17f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var buttonImage = rect.gameObject.AddComponent<Image>();
                buttonImage.color = color;
                var modalButton = rect.gameObject.AddComponent<Button>();
                modalButton.transition = Selectable.Transition.None;
                MakeFullStretchText(rect, caption, 22f);
                return modalButton;
            }
            var confirmButton = MakeModalButton("ConfirmButton", "CONFIRM", 0.54f, 0.94f, new Color(0.16f, 0.42f, 0.28f, 1f));
            confirmButton.onClick.AddListener(_purchaseModal.Confirm);
            var cancelButton = MakeModalButton("CancelButton", "CANCEL", 0.06f, 0.46f, new Color(0.30f, 0.14f, 0.14f, 1f));
            cancelButton.onClick.AddListener(_purchaseModal.ModalWindowOut);

            var modalEmitter = modalRoot.gameObject.AddComponent<IconEmitter>();
            var modalImages = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var imageRect = MakeChild(i == 0 ? "CaptainImage" : i == 1 ? "GameImage" : "TicketImage", modalPanel);
                imageRect.anchorMin = new Vector2(0.42f, 0.82f);
                imageRect.anchorMax = new Vector2(0.58f, 0.96f);
                imageRect.offsetMin = Vector2.zero;
                imageRect.offsetMax = Vector2.zero;
                modalImages[i] = imageRect.gameObject.AddComponent<Image>();
                modalImages[i].color = new Color(0.8f, 0.85f, 1f, 0.25f);
                modalImages[i].raycastTarget = false;
                imageRect.gameObject.SetActive(false);
            }
            SetPrivateField(_purchaseModal, "PriceLabel", modalPrice);
            SetPrivateField(_purchaseModal, "UnlockText", modalUnlock);
            SetPrivateField(_purchaseModal, "CrystalBalanceText", modalCrystalBalance);
            SetPrivateField(_purchaseModal, "TicketBalanceText", modalTicketBalance);
            SetPrivateField(_purchaseModal, "ConfirmButton", confirmButton);
            SetPrivateField(_purchaseModal, "IconEmitter", modalEmitter);
            SetPrivateField(_purchaseModal, "CaptainImage", modalImages[0]);
            SetPrivateField(_purchaseModal, "GameImage", modalImages[1]);
            SetPrivateField(_purchaseModal, "TicketImage", modalImages[2]);
            modalRoot.gameObject.SetActive(true); // Start hides via CanvasGroup until opened

            // ── the screen itself ───────────────────────────────────────────────
            panel.gameObject.AddComponent<MenuAudio>();
            var store = panel.gameObject.AddComponent<StoreScreen>();
            SetPrivateField(store, "_captainManager", captainManager); // the [Inject] (no DI scope in the shell)
            SetPrivateField(store, "CrystalBalance", crystalBalanceText);
            SetPrivateField(store, "TicketBalance", ticketBalanceText);
            SetPrivateField(store, "CaptainPurchaseSection", captainSection.gameObject);
            SetPrivateField(store, "PurchaseCaptainPrefab", captainTemplate);
            SetPrivateField(store, "CaptainPurchaseRows", captainRows);
            SetPrivateField(store, "PurchaseConfirmationModal", _purchaseModal);
            SetPrivateField(store, "PurchaseConfirmationButton", confirmButton);
            SetPrivateField(store, "GamePurchaseSection", gameSection.gameObject);
            SetPrivateField(store, "PurchaseGamePrefab", gameTemplate);
            SetPrivateField(store, "GamePurchaseRows", new List<HorizontalLayoutGroup> { gameRowGroup });
            SetPrivateField(store, "DailyChallengeTicketCard", ticketCard);

            panel.gameObject.SetActive(true); // fields wired — Start's CatalogLoaded lane populates
        }

        /// <summary>
        /// The REAL LeaderboardsMenu on the PORT panel (Leaderboards unit): three
        /// game-select buttons, the vessel-class dropdown, and the high-score board.
        /// Upstream's manager runs its "[PLAYFAB DISABLED]" offline lane, so the rows
        /// come from the DataAccessor-cached lists — seeded here with a deterministic
        /// fixture board per game (identical every boot → byte-stable captures). The
        /// local pilot's PlayFabAccount.ID matches one seeded row, exercising the
        /// player-highlight lane.
        /// </summary>
        void BuildLeaderboardsScreen(RectTransform panel)
        {
            panel.gameObject.SetActive(false); // wire fields before Start runs

            var gameList = EnsureGameList();

            // The offline manager singleton the screen fetches through (upstream: a
            // scene object with the NetworkMonitor variable serialized in).
            if (CosmicShore.Core.LeaderboardManager.Instance == null)
            {
                var managerGo = new GameObject("LeaderboardManager");
                managerGo.SetActive(false);
                var manager = managerGo.AddComponent<CosmicShore.Core.LeaderboardManager>();
                var netVariable = ScriptableObject.CreateInstance<CosmicShore.ScriptableObjects.NetworkMonitorDataVariable>();
                netVariable.Value = new CosmicShore.ScriptableObjects.NetworkMonitorData
                {
                    OnNetworkFound = ScriptableObject.CreateInstance<CosmicShore.Engine.Soap.ScriptableEventNoParam>(),
                    OnNetworkLost = ScriptableObject.CreateInstance<CosmicShore.Engine.Soap.ScriptableEventNoParam>(),
                };
                SetPrivateField(manager, "_networkMonitorDataVariable", netVariable);
                managerGo.SetActive(true);
            }

            // Deterministic cached boards: five pilots per game, the local pilot on
            // rank 3 (the screen paints that row cyan via PlayFabAccount.ID).
            CosmicShore.Core.AuthenticationManager.PlayFabAccount.ID = "pilot-local";
            string[] rivals = { "VELA", "CRUX", "YOU", "LYRA", "DRACO" };
            foreach (var game in gameList.Games)
            {
                var entries = new List<CosmicShore.Core.LeaderboardManager.LeaderboardEntry>();
                int baseScore = 120 + game.DisplayName.Length; // per-game flavor, constant
                for (int i = 0; i < rivals.Length; i++)
                    entries.Add(new CosmicShore.Core.LeaderboardManager.LeaderboardEntry(
                        rivals[i] == "YOU" ? "YOU" : rivals[i],
                        rivals[i] == "YOU" ? "pilot-local" : $"pilot-{rivals[i]}",
                        baseScore - i * 7,
                        i,
                        avatarUrl: null));
                CosmicShore.Utility.DataAccessor.Save(
                    $"leaderboard_{game.Mode.ToString().ToUpper()}_DOLPHIN.data", entries);
            }

            // Game-select buttons (Image + Button per slot, the shape SelectGame reads).
            var gameRow = MakeChild("GameSelectionContainer", panel);
            gameRow.anchorMin = new Vector2(0.06f, 0.72f);
            gameRow.anchorMax = new Vector2(0.66f, 0.84f);
            gameRow.offsetMin = Vector2.zero;
            gameRow.offsetMax = Vector2.zero;
            var gameRowGroup = gameRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            gameRowGroup.spacing = 18f;
            gameRowGroup.childForceExpandWidth = true;
            gameRowGroup.childForceExpandHeight = true;
            for (int i = 0; i < gameList.Games.Count; i++)
            {
                var slot = MakeChild($"GameSelect_{i}", gameRow);
                var slotImage = slot.gameObject.AddComponent<Image>();
                slotImage.color = new Color(0.10f, 0.24f, 0.38f, 1f);
                var slotButton = slot.gameObject.AddComponent<Button>();
                slotButton.transition = Selectable.Transition.None;
                var slotLabel = MakeFullStretchText(slot, gameList.Games[i].DisplayName, 18f);
                slotLabel.color = new Color(0.8f, 0.95f, 1f, 1f);
            }

            // Vessel-class dropdown (top right).
            var dropdownRect = MakeChild("ShipClassSelection", panel);
            dropdownRect.anchorMin = new Vector2(0.70f, 0.74f);
            dropdownRect.anchorMax = new Vector2(0.94f, 0.82f);
            dropdownRect.offsetMin = Vector2.zero;
            dropdownRect.offsetMax = Vector2.zero;
            var dropdownImage = dropdownRect.gameObject.AddComponent<Image>();
            dropdownImage.color = new Color(0.12f, 0.20f, 0.30f, 1f);
            var dropdown = dropdownRect.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.transition = Selectable.Transition.None;
            var caption = MakeFullStretchText(dropdownRect, "", 20f);
            dropdown.captionText = caption;

            // High-score board: six rows of [rank | pilot | score].
            var board = MakeChild("HighScoresContainer", panel);
            board.anchorMin = new Vector2(0.06f, 0.10f);
            board.anchorMax = new Vector2(0.94f, 0.68f);
            board.offsetMin = Vector2.zero;
            board.offsetMax = Vector2.zero;
            var boardImage = board.gameObject.AddComponent<Image>();
            boardImage.color = new Color(0.03f, 0.10f, 0.18f, 0.9f);
            boardImage.raycastTarget = false;
            var boardGroup = board.gameObject.AddComponent<VerticalLayoutGroup>();
            boardGroup.spacing = 8f;
            boardGroup.childForceExpandWidth = true;
            boardGroup.childForceExpandHeight = true;
            for (int i = 0; i < 6; i++)
            {
                var row = MakeChild($"ScoreRow_{i}", board);
                var rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                rowGroup.spacing = 12f;
                rowGroup.childForceExpandWidth = true;
                rowGroup.childForceExpandHeight = true;
                for (int c = 0; c < 3; c++)
                {
                    var cell = MakeChild(c == 0 ? "Rank" : c == 1 ? "Pilot" : "Score", row);
                    var cellText = cell.gameObject.AddComponent<TextMeshProUGUI>();
                    cellText.fontSize = 20f;
                    cellText.color = Color.white;
                    cellText.alignment = TextAlignmentOptions.Center;
                }
            }

            panel.gameObject.AddComponent<MenuAudio>(); // [RequireComponent] partner (engine doesn't auto-add)
            var leaderboards = panel.gameObject.AddComponent<LeaderboardsMenu>();
            SetPrivateField(leaderboards, "allGames", gameList);
            SetPrivateField(leaderboards, "GameSelectionContainer", (Transform)gameRow);
            SetPrivateField(leaderboards, "HighScoresContainer", board.gameObject);
            SetPrivateField(leaderboards, "ShipClassSelection", dropdown);

            panel.gameObject.SetActive(true); // fields wired — Start may run
        }

        /// <summary>
        /// The hand-authored 3-game SO fixture (the same modes the CLI proves). One
        /// instance feeds BOTH the arcade modal's cards and the PORT leaderboards
        /// screen (which copies the list before filtering, per the upstream comment).
        /// </summary>
        CosmicShore.ScriptableObjects.SO_GameList EnsureGameList()
        {
            if (_gameList != null) return _gameList;

            _gameList = ScriptableObject.CreateInstance<CosmicShore.ScriptableObjects.SO_GameList>();
            _gameList.Games = new List<CosmicShore.ScriptableObjects.SO_ArcadeGame>();
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
                _gameList.Games.Add(so);
            }
            return _gameList;
        }

        void BuildArcadeModal(RectTransform canvasRect)
        {
            var gameList = EnsureGameList();

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

            for (int i = 0; i < gameList.Games.Count; i++)
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

                // Lock overlay: shown by GameCard.SetLocked when the quest chain
                // hasn't reached this mode (the progression service is LIVE).
                var lockOverlay = MakeChild("LockOverlay", card);
                lockOverlay.anchorMin = Vector2.zero;
                lockOverlay.anchorMax = Vector2.one;
                lockOverlay.offsetMin = Vector2.zero;
                lockOverlay.offsetMax = Vector2.zero;
                var lockImage = lockOverlay.gameObject.AddComponent<Image>();
                lockImage.color = new Color(0.02f, 0.02f, 0.06f, 0.55f);
                lockImage.raycastTarget = false;
                var lockLabel = MakeChild("Label", lockOverlay);
                lockLabel.anchorMin = Vector2.zero;
                lockLabel.anchorMax = new Vector2(1f, 0.3f); // low strip — clears the card title
                lockLabel.offsetMin = Vector2.zero;
                lockLabel.offsetMax = Vector2.zero;
                var lockText = lockLabel.gameObject.AddComponent<TextMeshProUGUI>();
                lockText.text = "LOCKED";
                lockText.fontSize = 22f;
                lockText.color = new Color(1f, 0.55f, 0.35f, 0.95f);
                lockText.alignment = TextAlignmentOptions.Center;
                lockOverlay.gameObject.SetActive(false);
                SetPrivateField(gameCard, "lockOverlay", lockOverlay.gameObject);
            }

            BuildConfigureModal(canvasRect, gameList, explore);

            // The switcher owns the modals (OnClickArcadeNav + CloseAllModals paths).
            SetPrivateField(_switcher, "ArcadeModal", _arcadeModal);
            SetPrivateField(_switcher, "Modals", new List<ModalWindowManager> { _arcadeModal, _configureModal, _purchaseModal });

            modalRoot.gameObject.SetActive(true); // Start hides it via CanvasGroup until opened
        }

        /// <summary>
        /// The REAL HangarScreen on the HANGAR panel (Hangar unit): a grid of
        /// vessel cards (unlocked sort first, staggered fade-in, eye toggles the
        /// names) and the detail view (description tab + ability tabs + the
        /// crystal unlock flow — the confirm branch stays dormant in the shell
        /// because no PlayerDataService instance means a zero wallet, exactly the
        /// upstream fresh-boot posture). Roster is a hand-authored SO fixture;
        /// prefab art arrives with the Arc-E content bridge — the WIRING is
        /// shipping code. Panel deactivates during wiring so OnEnable/Awake see
        /// their serialized fields (the scene-load ordering the original engine
        /// guarantees).
        /// </summary>
        void BuildHangarScreen(RectTransform panel)
        {
            panel.gameObject.SetActive(false); // wire fields before OnEnable/Start

            var vesselList = ScriptableObject.CreateInstance<SO_VesselList>();
            vesselList.VesselList = new List<SO_Vessel>();
            (string name, CosmicShore.Data.VesselClassType cls, string desc, int cost, bool locked)[] roster =
            {
                ("Manta", CosmicShore.Data.VesselClassType.Manta, "Feature-complete flagship - sweeping trails and skimmer play.", 0, false),
                ("Dolphin", CosmicShore.Data.VesselClassType.Dolphin, "Feature-complete racer - momentum and flow.", 0, false),
                ("Rhino", CosmicShore.Data.VesselClassType.Rhino, "Feature-complete bruiser - mass and momentum.", 0, false),
                ("Squirrel", CosmicShore.Data.VesselClassType.Squirrel, "Vaporwave drift racer - ride the trails.", 0, false),
                ("Urchin", CosmicShore.Data.VesselClassType.Urchin, "Spiky area-denial specialist.", 150, true),
                ("Grizzly", CosmicShore.Data.VesselClassType.Grizzly, "Heavy support - shields and sustain.", 300, true),
                ("Serpent", CosmicShore.Data.VesselClassType.Serpent, "Sinuous striker with a dedicated HUD.", 300, true),
                ("Sparrow", CosmicShore.Data.VesselClassType.Sparrow, "Arcade space combat - guns and missiles.", 500, true),
            };
            foreach (var (name, cls, desc, cost, locked) in roster)
            {
                var vessel = ScriptableObject.CreateInstance<SO_Vessel>(); // global namespace (as upstream)
                vessel.Name = name;
                vessel.Class = cls;
                vessel.Description = desc;
                vessel.UnlockCost = cost;
                if (locked) vessel.Lock();
                var abilityA = ScriptableObject.CreateInstance<SO_VesselAbility>();
                abilityA.Name = "BOOST";
                abilityA.Description = $"{name}: channel skim energy into a burst of speed.";
                var abilityB = ScriptableObject.CreateInstance<SO_VesselAbility>();
                abilityB.Name = "DRIFT";
                abilityB.Description = $"{name}: decouple heading from velocity for wide arcs.";
                vessel.Abilities = new List<SO_VesselAbility> { abilityA, abilityB };
                vesselList.VesselList.Add(vessel);
            }

            var screen = panel.gameObject.AddComponent<HangarScreen>();
            SetPrivateField(screen, "ShipList", vesselList);

            // ── grid panel: container + card template + eye toggle ─────────────
            var gridPanel = MakeChild("GridPanel", panel);
            gridPanel.anchorMin = new Vector2(0.04f, 0.08f);
            gridPanel.anchorMax = new Vector2(0.96f, 0.86f);
            gridPanel.offsetMin = Vector2.zero;
            gridPanel.offsetMax = Vector2.zero;

            var gridContainer = MakeChild("GridContainer", gridPanel);
            gridContainer.anchorMin = Vector2.zero;
            gridContainer.anchorMax = Vector2.one;
            gridContainer.offsetMin = Vector2.zero;
            gridContainer.offsetMax = Vector2.zero;
            var grid = gridContainer.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(420f, 180f);
            grid.spacing = new Vector2(24f, 56f); // tall gutter: name strips clear the next row

            // Template lives OUTSIDE gridContainer (PopulateGrid clears the container)
            // and stays inactive — engine Instantiate clones it per vessel.
            var template = MakeChild("GridCardTemplate", panel);
            template.gameObject.SetActive(false);
            template.sizeDelta = new Vector2(420f, 180f);
            template.gameObject.AddComponent<CanvasGroup>();
            var cardBg = template.gameObject.AddComponent<Image>();
            cardBg.color = new Color(0.24f, 0.16f, 0.09f, 1f);
            var cardButton = template.gameObject.AddComponent<Button>();
            cardButton.transition = Selectable.Transition.None; // card owns its visuals
            var cardName = MakeChild("Name", template);
            cardName.anchorMin = Vector2.zero;
            cardName.anchorMax = new Vector2(1f, 0.34f);
            cardName.offsetMin = Vector2.zero;
            cardName.offsetMax = Vector2.zero;
            var cardNameText = cardName.gameObject.AddComponent<TextMeshProUGUI>();
            cardNameText.fontSize = 26f;
            cardNameText.color = new Color(0.95f, 0.85f, 0.6f, 1f);
            cardNameText.alignment = TextAlignmentOptions.Center;
            var lockOverlay = MakeChild("LockOverlay", template);
            lockOverlay.anchorMin = Vector2.zero;
            lockOverlay.anchorMax = Vector2.one;
            lockOverlay.offsetMin = Vector2.zero;
            lockOverlay.offsetMax = Vector2.zero;
            var lockImage = lockOverlay.gameObject.AddComponent<Image>();
            lockImage.color = new Color(0.02f, 0.02f, 0.05f, 0.62f);
            lockImage.raycastTarget = false;
            var lockText = MakeFullStretchText(lockOverlay, "LOCKED", 30f);
            lockText.color = new Color(1f, 0.55f, 0.35f, 0.95f);
            var gridCard = template.gameObject.AddComponent<HangarVesselGridCard>();
            SetPrivateField(gridCard, "vesselIcon", cardBg);
            SetPrivateField(gridCard, "vesselName", cardNameText);
            SetPrivateField(gridCard, "lockOverlay", lockOverlay.gameObject);
            SetPrivateField(gridCard, "cardButton", cardButton);

            var eye = MakeChild("EyeButton", panel);
            eye.anchorMin = eye.anchorMax = new Vector2(0.96f, 0.88f);
            eye.pivot = new Vector2(1f, 0.5f);
            eye.sizeDelta = new Vector2(120f, 48f);
            var eyeImage = eye.gameObject.AddComponent<Image>();
            eyeImage.color = new Color(0.30f, 0.22f, 0.10f, 1f);
            var eyeButton = eye.gameObject.AddComponent<Button>();
            eyeButton.transition = Selectable.Transition.None;
            MakeFullStretchText(eye, "NAMES", 20f);

            SetPrivateField(screen, "gridPanel", gridPanel.gameObject);
            SetPrivateField(screen, "gridContainer", (Transform)gridContainer);
            SetPrivateField(screen, "gridCardPrefab", gridCard);
            SetPrivateField(screen, "eyeButton", eyeButton);

            // ── detail panel: title/back + general & ability tabs + unlock flow ─
            var detailPanel = MakeChild("DetailPanel", panel);
            detailPanel.gameObject.SetActive(false); // Awake sees wired fields on first show
            detailPanel.anchorMin = new Vector2(0.04f, 0.06f);
            detailPanel.anchorMax = new Vector2(0.96f, 0.86f);
            detailPanel.offsetMin = Vector2.zero;
            detailPanel.offsetMax = Vector2.zero;
            var detailBg = detailPanel.gameObject.AddComponent<Image>();
            detailBg.color = new Color(0.10f, 0.07f, 0.04f, 0.98f);
            detailBg.raycastTarget = false;
            var detailView = detailPanel.gameObject.AddComponent<HangarVesselDetailView>();

            var detailTitle = MakeChild("VesselName", detailPanel);
            detailTitle.anchorMin = new Vector2(0f, 0.88f);
            detailTitle.anchorMax = Vector2.one;
            detailTitle.offsetMin = Vector2.zero;
            detailTitle.offsetMax = Vector2.zero;
            var detailTitleText = detailTitle.gameObject.AddComponent<TextMeshProUGUI>();
            detailTitleText.fontSize = 40f;
            detailTitleText.color = new Color(0.95f, 0.85f, 0.6f, 1f);
            detailTitleText.alignment = TextAlignmentOptions.Center;

            var back = MakeChild("BackButton", detailPanel);
            back.anchorMin = back.anchorMax = new Vector2(0f, 1f);
            back.pivot = new Vector2(0f, 1f);
            back.anchoredPosition = new Vector2(18f, -14f);
            back.sizeDelta = new Vector2(140f, 52f);
            var backImage = back.gameObject.AddComponent<Image>();
            backImage.color = new Color(0.30f, 0.22f, 0.10f, 1f);
            var backButton = back.gameObject.AddComponent<Button>();
            backButton.transition = Selectable.Transition.None;
            MakeFullStretchText(back, "< BACK", 22f);

            // Tab strip: GENERAL + the ability tabs (SetVessel hides unused ones).
            var tabs = MakeChild("Tabs", detailPanel);
            tabs.anchorMin = new Vector2(0.05f, 0.74f);
            tabs.anchorMax = new Vector2(0.95f, 0.85f);
            tabs.offsetMin = Vector2.zero;
            tabs.offsetMax = Vector2.zero;
            var tabRow = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabRow.spacing = 16f;
            tabRow.childForceExpandWidth = true;
            tabRow.childForceExpandHeight = true;

            (Button button, GameObject bg) MakeTab(string tabLabel)
            {
                var tab = MakeChild($"Tab_{tabLabel}", tabs);
                var tabImage = tab.gameObject.AddComponent<Image>();
                tabImage.color = new Color(0.20f, 0.15f, 0.08f, 1f);
                var tabButton = tab.gameObject.AddComponent<Button>();
                tabButton.transition = Selectable.Transition.None;
                var bg = MakeChild("BG", tab);
                bg.anchorMin = Vector2.zero;
                bg.anchorMax = Vector2.one;
                bg.offsetMin = Vector2.zero;
                bg.offsetMax = Vector2.zero;
                var bgImage = bg.gameObject.AddComponent<Image>();
                bgImage.color = new Color(0.55f, 0.40f, 0.16f, 1f);
                bgImage.raycastTarget = false;
                bg.gameObject.SetActive(false);
                MakeFullStretchText(tab, tabLabel, 20f);
                return (tabButton, bg.gameObject);
            }

            var (generalButton, generalBG) = MakeTab("GENERAL");
            var abilityButtons = new Button[4];
            var abilityBGs = new GameObject[4];
            for (int i = 0; i < 4; i++)
                (abilityButtons[i], abilityBGs[i]) = MakeTab($"ABILITY{i + 1}");

            // General tab content: description + the unlock button.
            var descriptionPanel = MakeChild("DescriptionPanel", detailPanel);
            descriptionPanel.anchorMin = new Vector2(0.05f, 0.10f);
            descriptionPanel.anchorMax = new Vector2(0.95f, 0.72f);
            descriptionPanel.offsetMin = Vector2.zero;
            descriptionPanel.offsetMax = Vector2.zero;
            var descText = MakeFullStretchText(descriptionPanel, "", 24f);
            descText.color = new Color(0.9f, 0.92f, 0.95f, 1f);

            var unlock = MakeChild("UnlockButton", descriptionPanel);
            unlock.anchorMin = unlock.anchorMax = new Vector2(0.5f, 0f);
            unlock.pivot = new Vector2(0.5f, 0f);
            unlock.anchoredPosition = new Vector2(0f, 10f);
            unlock.sizeDelta = new Vector2(320f, 60f);
            var unlockImage = unlock.gameObject.AddComponent<Image>();
            unlockImage.color = new Color(0.16f, 0.42f, 0.28f, 1f);
            var unlockButton = unlock.gameObject.AddComponent<Button>();
            unlockButton.transition = Selectable.Transition.None;
            var unlockText = MakeFullStretchText(unlock, "", 24f);

            // Ability tab content.
            var abilitiesPanel = MakeChild("AbilitiesPanel", detailPanel);
            abilitiesPanel.gameObject.SetActive(false);
            abilitiesPanel.anchorMin = new Vector2(0.05f, 0.10f);
            abilitiesPanel.anchorMax = new Vector2(0.95f, 0.72f);
            abilitiesPanel.offsetMin = Vector2.zero;
            abilitiesPanel.offsetMax = Vector2.zero;
            var abilityTitle = MakeChild("AbilityTitle", abilitiesPanel);
            abilityTitle.anchorMin = new Vector2(0f, 0.8f);
            abilityTitle.anchorMax = Vector2.one;
            abilityTitle.offsetMin = Vector2.zero;
            abilityTitle.offsetMax = Vector2.zero;
            var abilityTitleText = abilityTitle.gameObject.AddComponent<TextMeshProUGUI>();
            abilityTitleText.fontSize = 30f;
            abilityTitleText.color = new Color(0.95f, 0.85f, 0.6f, 1f);
            abilityTitleText.alignment = TextAlignmentOptions.Center;
            var abilityBody = MakeChild("AbilityBody", abilitiesPanel);
            abilityBody.anchorMin = Vector2.zero;
            abilityBody.anchorMax = new Vector2(1f, 0.8f);
            abilityBody.offsetMin = Vector2.zero;
            abilityBody.offsetMax = Vector2.zero;
            var abilityBodyText = abilityBody.gameObject.AddComponent<TextMeshProUGUI>();
            abilityBodyText.fontSize = 22f;
            abilityBodyText.color = new Color(0.9f, 0.92f, 0.95f, 1f);
            abilityBodyText.alignment = TextAlignmentOptions.Center;

            // Unlock confirmation (spend-crystals) panel.
            var unlockPanel = MakeChild("UnlockPanel", detailPanel);
            unlockPanel.gameObject.SetActive(false);
            unlockPanel.anchorMin = Vector2.zero;
            unlockPanel.anchorMax = Vector2.one;
            unlockPanel.offsetMin = Vector2.zero;
            unlockPanel.offsetMax = Vector2.zero;
            var unlockDim = unlockPanel.gameObject.AddComponent<Image>();
            unlockDim.color = new Color(0f, 0f, 0f, 0.6f);
            var spendPanel = MakeChild("SpendCrystalsPanel", unlockPanel);
            spendPanel.anchorMin = spendPanel.anchorMax = new Vector2(0.5f, 0.5f);
            spendPanel.sizeDelta = new Vector2(520f, 300f);
            var spendImage = spendPanel.gameObject.AddComponent<Image>();
            spendImage.color = new Color(0.10f, 0.08f, 0.22f, 0.98f);
            spendImage.raycastTarget = false;
            var spendDetail = MakeChild("Detail", spendPanel);
            spendDetail.anchorMin = new Vector2(0f, 0.55f);
            spendDetail.anchorMax = Vector2.one;
            spendDetail.offsetMin = Vector2.zero;
            spendDetail.offsetMax = Vector2.zero;
            var spendDetailText = spendDetail.gameObject.AddComponent<TextMeshProUGUI>();
            spendDetailText.fontSize = 22f;
            spendDetailText.color = new Color(0.9f, 0.92f, 0.95f, 1f);
            spendDetailText.alignment = TextAlignmentOptions.Center;
            var crystalAmount = MakeChild("CrystalAmount", spendPanel);
            crystalAmount.anchorMin = new Vector2(0f, 0.36f);
            crystalAmount.anchorMax = new Vector2(1f, 0.55f);
            crystalAmount.offsetMin = Vector2.zero;
            crystalAmount.offsetMax = Vector2.zero;
            var crystalAmountText = crystalAmount.gameObject.AddComponent<TextMeshProUGUI>();
            crystalAmountText.fontSize = 26f;
            crystalAmountText.color = new Color(0.55f, 0.95f, 1f, 1f);
            crystalAmountText.alignment = TextAlignmentOptions.Center;
            var confirm = MakeChild("ConfirmButton", spendPanel);
            confirm.anchorMin = confirm.anchorMax = new Vector2(0.5f, 0f);
            confirm.pivot = new Vector2(0.5f, 0f);
            confirm.anchoredPosition = new Vector2(0f, 24f);
            confirm.sizeDelta = new Vector2(240f, 60f);
            var confirmImage = confirm.gameObject.AddComponent<Image>();
            confirmImage.color = new Color(0.16f, 0.42f, 0.28f, 1f);
            var confirmButton = confirm.gameObject.AddComponent<Button>();
            confirmButton.transition = Selectable.Transition.None;
            MakeFullStretchText(confirm, "CONFIRM", 22f);

            SetPrivateField(detailView, "vesselNameText", detailTitleText);
            SetPrivateField(detailView, "backButton", backButton);
            SetPrivateField(detailView, "generalButton", generalButton);
            SetPrivateField(detailView, "abilityButtons", abilityButtons);
            SetPrivateField(detailView, "generalButtonBG", generalBG);
            SetPrivateField(detailView, "abilityButtonBGs", abilityBGs);
            SetPrivateField(detailView, "descriptionPanel", descriptionPanel.gameObject);
            SetPrivateField(detailView, "abilitiesPanel", abilitiesPanel.gameObject);
            SetPrivateField(detailView, "vesselDescriptionText", descText);
            SetPrivateField(detailView, "unlockButton", unlockButton);
            SetPrivateField(detailView, "unlockButtonText", unlockText);
            SetPrivateField(detailView, "abilitiesPreviewTitle", abilityTitleText);
            SetPrivateField(detailView, "abilitiesPreviewText", abilityBodyText);
            SetPrivateField(detailView, "unlockPanel", unlockPanel.gameObject);
            SetPrivateField(detailView, "spendCrystalsPanel", spendPanel.gameObject);
            SetPrivateField(detailView, "confirmButton", confirmButton);
            SetPrivateField(detailView, "spendCrystalsDetailText", spendDetailText);
            SetPrivateField(detailView, "crystalAmountText", crystalAmountText);

            SetPrivateField(screen, "detailPanel", detailPanel.gameObject);
            SetPrivateField(screen, "detailView", detailView);

            panel.gameObject.SetActive(true); // fields wired — OnEnable/Start may run
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

        /// <summary>
        /// Stand-in for the upstream prefab's PERSISTENT Button.onClick listener
        /// (inspector-serialized, remapped per clone by Unity). Runtime AddListener
        /// delegates capture the template instance and survive engine cloning still
        /// bound to it — so each clone re-wires its own Button → its own card here.
        /// </summary>
        sealed class PurchaseCardClickBinding : MonoBehaviour
        {
            void Awake()
            {
                var button = GetComponent<Button>();
                var card = GetComponent<PurchaseCard>();
                button.onClick.RemoveAllListeners(); // drop the template-bound stale delegate
                button.onClick.AddListener(card.OnClickBuy);
            }
        }

        static void SetPrivateField(object target, string fieldName, object value)
        {
            // Private fields don't surface through GetField on derived types —
            // walk the base chain (a subclass wiring a base [SerializeField]).
            FieldInfo field = null;
            for (var t = target.GetType(); t != null && field == null; t = t.BaseType)
                field = t.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            (field ?? throw new MissingFieldException(target.GetType().Name, fieldName)).SetValue(target, value);
        }

        void OnUpdate(double dt)
        {
            // ── game phase: step the round (it owns its own GameLoop) ──────────
            if (_phase == HostPhase.Game)
            {
                _frameIndex++;
                if (_round == null) return;

                // Arc H: Tab toggles human/autopilot on Players[0] (edge-detected);
                // the human writes land before the tick, like any hardware pilot.
                bool tab = false;
                foreach (var keyboard in _inputContext.Keyboards)
                    if (keyboard.IsKeyPressed(Key.Tab)) tab = true;
                if (tab && !_prevTab)
                {
                    if (_bridge.Active) _bridge.Detach();
                    else _bridge.Attach(_round);
                    Console.WriteLine($"[menushell] pilot: {(_bridge.Active ? "HUMAN" : "autopilot")}");
                }
                _prevTab = tab;
                if (_bridge.Active)
                    _bridge.Drive(_inputContext);

                _overlay?.Update();
                if (_screenshotPath == null)
                    _overlay?.DriveMouse(_inputContext);

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
                    if (_bridge.Active) _bridge.Detach(); // hand back before the world dies
                    _overlay = null;                       // dies with the round world
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

            // `--screen <name>` capture runs: navigate straight to that screen and
            // hold (no game flow) — a dedicated verify lane that leaves the
            // canonical choreography (and its pinned diags) untouched.
            if (_screenPeek != null)
            {
                if (_frameIndex == 6)
                {
                    var peek = _screenPeek.ToUpperInvariant() switch
                    {
                        "PORT" => _portNavButton,
                        "HANGAR" => _hangarNavButton,
                        "STORE" => _storeNavButton,
                        _ => null,
                    };
                    if (peek != null) ClickButton(peek);
                }
                return;
            }

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

            // Arc I: the real in-round UI lives in the round's world. Screenshot runs
            // keep the deterministic auto-ready; interactive runs get the REAL button.
            _round.AutoReady = _screenshotPath != null;
            _overlay = new RoundUiOverlay(_round);

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
                _scene.DrawHud(_ui, _round, _window.FramebufferSize.X, _window.FramebufferSize.Y,
                    _bridge.Active ? _round.Players[0].Name : null,
                    standingsPanelShown: _overlay?.ScoreboardShown ?? false);
                UiCanvasBridge.Render(_ui, _window.FramebufferSize.X, _window.FramebufferSize.Y);
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

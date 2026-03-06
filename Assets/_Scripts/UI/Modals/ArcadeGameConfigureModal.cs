using CosmicShore.ScriptableObjects;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using CosmicShore.Data;
using CosmicShore.Utility;
using Obvious.Soap;
using Reflex.Attributes;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace CosmicShore.UI
{
    public class ArcadeGameConfigureModal : ModalWindowManager
    {
        [Inject] CaptainManager _captainManager;

        // TEMP for legacy systems (e.g. DailyChallengeSystem)
        public static ArcadeGameConfigureModal Instance { get; private set; }

        [Header("Config State")]
        [SerializeField] private ArcadeGameConfigSO  config;
        [SerializeField] private ScriptableEventNoParam configChangedEvent;
        [SerializeField] private ScriptableEventNoParam startGameRequestedEvent;

        [Header("Shared Game Data")]
        [Inject] private GameDataSO gameData;
        [Inject] private HostConnectionDataSO hostConnectionData;
        [SerializeField] private ScriptableVariable<int> shipClassTypeVariable; // broadcast class index

        [Header("External Views")]
        [SerializeField] private ArcadeExploreView arcadeExploreView;

        [Header("Game Meta UI (left side – always visible)")]
        [SerializeField] private TMP_Text    selectedGameName;
        [SerializeField] private TMP_Text    selectedGameDescription;
        [SerializeField] private GameObject  selectedGamePreviewWindow;
        [SerializeField] private FavoriteIcon selectedGameFavoriteIcon;

        [Header("Screens (right side)")]
        [SerializeField] private GameObject configurationDetailView; // Screen 1
        [SerializeField] private GameObject gameDetailView;          // Screen 2
        [SerializeField] private GameObject vesselSelectionView;     // Screen 3
        [SerializeField] private GameObject squadMateSelectionView;  // Screen 4

        [Header("Screen 1 – Configuration Controls")]
        [SerializeField] private PlayerCountStepper          playerCountStepper;
        [SerializeField] private List<IntensitySelectButton> intensityButtons   = new(4);
        [SerializeField] private TMP_Text teamsValueText;
        [SerializeField] private TeamSelectionPanel teamSelectionPanel;

        [Tooltip("If true, will filter out unowned ships from being available to play")]
        [SerializeField] private bool respectInventoryForShipSelection = false;

        [Header("Screen 2 – Selected Vessel Summary")]
        [SerializeField] private Image    shipPlaceholderIcon;
        [SerializeField] private TMP_Text shipNameText;
        [SerializeField] private TMP_Text shipConfigurationText;
        [SerializeField] private TMP_Text shipVesselNameText;

        [Tooltip("Optional secondary icon (e.g. config screen).")]
        [SerializeField] private Image iconInConfigurationSelectionView;

        [Tooltip("Optional icon in the game-detail view.")]
        [SerializeField] private Image iconInGameDetailView;

        [Header("Ready-Up UI")]
        [Tooltip("Start/Confirm button — all players press this to lock in their choices.")]
        [SerializeField] private Button startGameButton;

        [Tooltip("'Waiting for others...' label — shown after a player confirms, hidden when choosing.")]
        [SerializeField] private GameObject waitingForOthersLabel;

        [Header("Network Sync")]
        [SerializeField] private ArcadeConfigSyncManager arcadeConfigSyncManager;

        // Runtime state
        SO_ArcadeGame _selectedGame;
        VideoPlayer   _previewVideo;
        bool _isClientMode;

        readonly List<SO_Ship> _availableShips = new();
        int _currentShipIndex = -1;

        /// <summary>
        /// True when this modal is being shown on a non-host client via RPC.
        /// Host-only controls (intensity, player count, start button) are read-only.
        /// </summary>
        bool IsClientMode => _isClientMode;

        #region Unity lifecycle

        void Awake()
        {
            if (Instance != null && Instance != this)
                return;

            Instance = this;
        }

        void OnEnable()
        {
            foreach (var intensityButton in intensityButtons)
                intensityButton.OnSelect += HandleIntensitySelected;

            if (playerCountStepper)
                playerCountStepper.OnValueChanged += HandlePlayerCountSelected;

            if (configChangedEvent != null)
                configChangedEvent.OnRaised += HandleConfigChangedExternal;

            if (teamSelectionPanel)
                teamSelectionPanel.OnTeamSelected += HandleTeamSelected;

            if (arcadeConfigSyncManager)
            {
                arcadeConfigSyncManager.OnConfigOpenedOnClient += HandleConfigOpenedOnClient;
                arcadeConfigSyncManager.OnConfigClosedOnClient += HandleConfigClosedOnClient;
                arcadeConfigSyncManager.OnConfigUpdatedOnClient += HandleConfigUpdatedOnClient;
                arcadeConfigSyncManager.OnScreenChangedOnClient += HandleScreenChangedOnClient;
                arcadeConfigSyncManager.OnAllPlayersReady += HandleAllPlayersReady;
            }
        }

        void OnDisable()
        {
            foreach (var intensityButton in intensityButtons)
                intensityButton.OnSelect -= HandleIntensitySelected;

            if (playerCountStepper)
                playerCountStepper.OnValueChanged -= HandlePlayerCountSelected;

            if (configChangedEvent != null)
                configChangedEvent.OnRaised -= HandleConfigChangedExternal;

            if (teamSelectionPanel)
                teamSelectionPanel.OnTeamSelected -= HandleTeamSelected;

            if (arcadeConfigSyncManager)
            {
                arcadeConfigSyncManager.OnConfigOpenedOnClient -= HandleConfigOpenedOnClient;
                arcadeConfigSyncManager.OnConfigClosedOnClient -= HandleConfigClosedOnClient;
                arcadeConfigSyncManager.OnConfigUpdatedOnClient -= HandleConfigUpdatedOnClient;
                arcadeConfigSyncManager.OnScreenChangedOnClient -= HandleScreenChangedOnClient;
                arcadeConfigSyncManager.OnAllPlayersReady -= HandleAllPlayersReady;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Entry point from ArcadeExploreView when a game tile is selected (host path).
        /// </summary>
        public void SetSelectedGame(SO_ArcadeGame selectedGame)
        {
            _isClientMode = false;
            _selectedGame = selectedGame;

            config.ResetState();
            config.SelectedGame = selectedGame;
            config.TeamCount    = 1; // number of teams disabled for now

            BuildAvailableShips(selectedGame);
            InitializeConfigFromGameDefaults(selectedGame);
            InitializeGameMetaView(selectedGame);
            InitializeScreen1Controls(selectedGame);
            InitializeDefaultShipFromAvailable();
            ApplyHostOnlyInteractability();
            ResetReadyUpUI();

            ShowConfigurationScreen();
            RaiseConfigChanged();

            // Notify all clients to open their own modal with team + vessel selection
            if (arcadeConfigSyncManager)
            {
                arcadeConfigSyncManager.NotifyConfigOpened(
                    (int)selectedGame.Mode,
                    config.Intensity,
                    config.PlayerCount,
                    selectedGame.MaxPlayersAllowed,
                    CurrentPartyHumanCount);
            }
        }

        #endregion

        #region Initialization helpers

        int CurrentPartyHumanCount
        {
            get
            {
                // Prefer Netcode connected client count — it's the ground truth for
                // human players and avoids stale PartyMembers (polled every 3s).
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsServer)
                    return Mathf.Max(1, nm.ConnectedClientsIds.Count);

                return hostConnectionData != null && hostConnectionData.PartyMembers != null
                    ? Mathf.Max(1, hostConnectionData.PartyMembers.Count)
                    : 1;
            }
        }

        void InitializeConfigFromGameDefaults(SO_ArcadeGame game)
        {
            config.Intensity   = game.MinIntensity;
            config.PlayerCount = Mathf.Max(game.MinPlayersAllowed, CurrentPartyHumanCount);

            SyncGameDataConfig();
        }

        void InitializeGameMetaView(SO_ArcadeGame game)
        {
            if (selectedGameName)
                selectedGameName.text = game.DisplayName;

            if (selectedGameDescription)
                selectedGameDescription.text = game.Description;

            if (selectedGameFavoriteIcon)
                selectedGameFavoriteIcon.Favorited = FavoriteSystem.IsFavorited(game.Mode);

            if (!game.PreviewClip || !selectedGamePreviewWindow)
                return;

            if (!_previewVideo)
            {
                _previewVideo = Instantiate(game.PreviewClip, selectedGamePreviewWindow.transform, false);
                var rt = _previewVideo.GetComponent<RectTransform>();
                if (rt)
                    rt.sizeDelta = new Vector2(300, 152);
            }
            else
            {
                _previewVideo.clip = game.PreviewClip.clip;
            }
        }

        void InitializeScreen1Controls(SO_ArcadeGame game)
        {
            for (int i = 0; i < intensityButtons.Count; i++)
            {
                var button = intensityButtons[i];
                if (!button) continue;

                int level = i + 1;
                button.SetIntensityLevel(level);

                bool active = level >= game.MinIntensity && level <= game.MaxIntensity;
                button.SetActive(active);
                button.SetSelected(level == config.Intensity);
            }

            // Player count — enforce minimum = party size so host can't select
            // fewer total players than there are humans in the lobby.
            int effectiveMin = Mathf.Max(game.MinPlayersAllowed, CurrentPartyHumanCount);

            // Stepper UI (preferred — supports 1-12 range)
            if (playerCountStepper)
                playerCountStepper.Initialize(effectiveMin, game.MaxPlayersAllowed, config.PlayerCount);

            if (teamsValueText)
                teamsValueText.text = "3";

            if (teamSelectionPanel && gameData.LocalPlayer is Player localPlayer)
                teamSelectionPanel.SetSelection(localPlayer.NetDomain.Value);
        }

        void BuildAvailableShips(SO_ArcadeGame game)
        {
            _availableShips.Clear();

            if (!game) return;

            var ships = game.Captains
                .Where(c => c && c.Ship)
                .Select(c => c.Ship)
                .ToList();

            if (respectInventoryForShipSelection && _captainManager)
            {
                var unlocked = _captainManager.UnlockedShips;
                ships = ships.Where(s => unlocked.Contains(s)).ToList();
            }

            _availableShips.AddRange(ships);
        }
        
        void InitializeDefaultShipFromAvailable()
        {
            if (_availableShips.Count == 0)
            {
                _currentShipIndex = -1;
                SetSelectedShipInternal(null);
                return;
            }

            SO_Ship chosen = null;

            if (gameData && gameData.selectedVesselClass)
            {
                var prevType = gameData.selectedVesselClass.Value;
                if (prevType != VesselClassType.Any && prevType != VesselClassType.Random)
                    chosen = _availableShips.FirstOrDefault(s => s.Class == prevType);
            }

            // 2) saved loadout vessel type
            if (!chosen && _selectedGame)
            {
                var loadout   = LoadoutSystem.LoadGameLoadout(_selectedGame.Mode, _selectedGame.IsMultiplayer).Loadout;
                var loadoutVT = loadout.VesselType;

                if (loadoutVT != VesselClassType.Random)
                    chosen = _availableShips.FirstOrDefault(s => s.Class == loadoutVT);
            }

            // 3) Dolphin is the default ship
            if (!chosen)
                chosen = _availableShips.FirstOrDefault(s => s.Class == VesselClassType.Dolphin);

            // 4) fallback
            if (!chosen)
                chosen = _availableShips[0];

            _currentShipIndex = Mathf.Max(0, _availableShips.IndexOf(chosen));
            SetSelectedShipInternal(chosen);
        }

        #endregion

        #region Screen switching

        // All screen GameObjects and their cached CanvasGroups.
        // Screens may start inactive in the scene — we activate them on first use
        // and rely exclusively on CanvasGroup for visibility after that.
        readonly struct ScreenSlot
        {
            public readonly GameObject Go;
            public readonly CanvasGroup Cg;
            public ScreenSlot(GameObject go, CanvasGroup cg) { Go = go; Cg = cg; }
        }

        ScreenSlot[] _screens;
        bool _screensInitialized;

        void EnsureScreensInitialized()
        {
            if (_screensInitialized) return;
            _screensInitialized = true;

            _screens = new ScreenSlot[]
            {
                InitSlot(configurationDetailView),
                InitSlot(gameDetailView),
                InitSlot(vesselSelectionView),
                InitSlot(squadMateSelectionView),
            };
        }

        static ScreenSlot InitSlot(GameObject go)
        {
            if (go == null) return default;

            // Activate the GO so CanvasGroup can control visibility.
            if (!go.activeSelf) go.SetActive(true);

            if (!go.TryGetComponent(out CanvasGroup cg))
                cg = go.AddComponent<CanvasGroup>();

            // Start hidden — the caller will show the right screen.
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            return new ScreenSlot(go, cg);
        }

        void ShowScreen(GameObject target)
        {
            EnsureScreensInitialized();

            foreach (var slot in _screens)
            {
                if (slot.Cg == null) continue;
                bool show = slot.Go == target;
                slot.Cg.alpha = show ? 1f : 0f;
                slot.Cg.blocksRaycasts = show;
                slot.Cg.interactable = show;
            }
        }

        void ShowConfigurationScreen()
        {
            ShowScreen(configurationDetailView);
        }

        void ShowGameDetailScreen()
        {
            ShowScreen(gameDetailView);
            RefreshShipSummaryView();
        }

        void ShowVesselSelectionScreen()
        {
            ShowScreen(vesselSelectionView);
            RefreshShipSummaryView();
        }

        void ShowSquadMateSelectionScreen()
        {
            ShowScreen(squadMateSelectionView);
        }

        #endregion

        #region Config change handlers

        void HandleIntensitySelected(int intensity)
        {
            if (_selectedGame == null || config == null) return;
            if (IsClientMode) return; // Clients cannot change intensity

            intensity        = Mathf.Clamp(intensity, _selectedGame.MinIntensity, _selectedGame.MaxIntensity);
            config.Intensity = intensity;

            foreach (var button in intensityButtons)
            {
                if (!button) continue;
                button.SetSelected(button.Intensity == intensity);
            }

            SyncGameDataConfig();
            RaiseConfigChanged();

            // Sync intensity + player count to clients so they see updated read-only values
            if (arcadeConfigSyncManager)
                arcadeConfigSyncManager.NotifyConfigUpdated(config.Intensity, config.PlayerCount);
        }

        void HandlePlayerCountSelected(int playerCount)
        {
            if (_selectedGame == null || config == null) return;
            if (IsClientMode) return; // Clients cannot change player count

            int effectiveMin = Mathf.Max(_selectedGame.MinPlayersAllowed, CurrentPartyHumanCount);
            playerCount        = Mathf.Clamp(playerCount, effectiveMin, _selectedGame.MaxPlayersAllowed);
            config.PlayerCount = playerCount;

            if (playerCountStepper)
                playerCountStepper.SetValue(playerCount);

            SyncGameDataConfig();
            RaiseConfigChanged();

            // Sync intensity + player count to clients so they see updated read-only values
            if (arcadeConfigSyncManager)
                arcadeConfigSyncManager.NotifyConfigUpdated(config.Intensity, config.PlayerCount);
        }

        void HandleTeamSelected(Domains domain)
        {
            if (gameData.LocalPlayer is not Player player) return;
            if (!player.IsOwner) return;
            player.NetDomain.Value = domain;
        }

        void HandleConfigChangedExternal()
        {
            if (!gameObject.activeInHierarchy || !config) return;
            if (config.SelectedGame != _selectedGame) return;

            RefreshShipSummaryView();
        }

        void RaiseConfigChanged()
        {
            configChangedEvent?.Raise();
        }

        #endregion

        #region Ship selection (Prev / Next)

        public void OnNextShipClicked()
        {
            if (_availableShips.Count == 0) return;

            if (_currentShipIndex < 0)
                _currentShipIndex = 0;
            else
                _currentShipIndex = (_currentShipIndex + 1) % _availableShips.Count;

            var ship = _availableShips[_currentShipIndex];
            SetSelectedShipInternal(ship);
            RaiseConfigChanged();
        }

        public void OnPreviousShipClicked()
        {
            if (_availableShips.Count == 0) return;

            if (_currentShipIndex < 0)
                _currentShipIndex = 0;
            else
                _currentShipIndex = (_currentShipIndex - 1 + _availableShips.Count) % _availableShips.Count;

            var ship = _availableShips[_currentShipIndex];
            SetSelectedShipInternal(ship);
            RaiseConfigChanged();
        }

        void SetSelectedShipInternal(SO_Ship ship)
        {
            if (config)
                config.SelectedShip = ship;

            SyncGameDataShip(ship);

            // Write the selected vessel class to the local player's NetworkVariable
            // so the server spawns the correct vessel for this client.
            SyncLocalPlayerVesselType(ship);

            // Also broadcast via ScriptableVariable<int> so other Views can react
            if (shipClassTypeVariable != null)
            {
                var classIndex = ship ? (int)ship.Class : (int)VesselClassType.Dolphin;
                shipClassTypeVariable.Value = classIndex;
            }

            RefreshShipSummaryView();
        }

        #endregion

        #region Ship summary & actions (Screen 2)

        void RefreshShipSummaryView()
        {
            RefreshShipSummaryView(config ? config.SelectedShip : null);
        }

        void RefreshShipSummaryView(SO_Ship ship)
        {
            // Icons
            Sprite icon = ship && ship.IconActive ? ship.IconActive : null;

            if (shipPlaceholderIcon)
            {
                if (icon != null)
                {
                    shipPlaceholderIcon.enabled = true;
                    shipPlaceholderIcon.sprite  = icon;
                }
                else
                {
                    shipPlaceholderIcon.enabled = false;
                }
            }

            if (iconInConfigurationSelectionView)
                iconInConfigurationSelectionView.sprite = icon;

            if (iconInGameDetailView)
                iconInGameDetailView.sprite = icon;

            // Text
            string nameText = ship ? ship.Name : "SELECT SHIP";

            if (shipNameText)
                shipNameText.text = nameText;

            if (shipConfigurationText)
                shipConfigurationText.text = nameText;

            if (shipVesselNameText)
                shipVesselNameText.text = nameText;
        }

        // Screen 1 → Screen 2
        public void OnConfirmConfiguration()
        {
            audioSystem.PlayMenuAudio(MenuAudioCategory.Confirmed);
            ShowGameDetailScreen();

            if (!IsClientMode && arcadeConfigSyncManager)
                arcadeConfigSyncManager.NotifyScreenChanged(1);
        }

        // Screen 2 → Screen 1 (Back button)
        public void OnBackFromGameSelectView()
        {
            ShowConfigurationScreen();

            if (!IsClientMode && arcadeConfigSyncManager)
                arcadeConfigSyncManager.NotifyScreenChanged(0);
        }

        // Screen 2 → Screen 3 (Vessel Selection)
        public void OnOpenVesselSelectionClicked()
        {
            ShowVesselSelectionScreen();

            if (!IsClientMode && arcadeConfigSyncManager)
                arcadeConfigSyncManager.NotifyScreenChanged(2);
        }

        // Screen 3 → Screen 2 (Back from Vessel Selection)
        public void OnBackFromVesselSelectionClicked()
        {
            ShowGameDetailScreen();

            if (!IsClientMode && arcadeConfigSyncManager)
                arcadeConfigSyncManager.NotifyScreenChanged(1);
        }

        // Screen 4 → Screen 2 (Back from Squad Mate Selection)
        public void OnBackFromSquadMateSelectionClicked()
        {
            ShowGameDetailScreen();

            if (!IsClientMode && arcadeConfigSyncManager)
                arcadeConfigSyncManager.NotifyScreenChanged(1);
        }

        /// <summary>
        /// Modal close (back/cancel) — host notifies clients to close too.
        /// Wire ALL close/back buttons to this method instead of ModalWindowOut() directly.
        /// </summary>
        public void OnCloseModal()
        {
            CloseAndNotifyClients();
        }

        void CloseAndNotifyClients()
        {
            if (arcadeConfigSyncManager && !IsClientMode)
                arcadeConfigSyncManager.NotifyConfigClosed();

            _isClientMode = false;
            ModalWindowOut();
        }

        /// <summary>
        /// Start/Confirm button — called by ALL players (host and clients).
        /// Confirms the player's team + vessel choices and enters the waiting state.
        /// When all human players have confirmed, the host auto-launches the game.
        /// </summary>
        public void OnStartGameClicked()
        {
            Debug.Log("<color=#FFD700>[FLOW-2] [ArcadeConfigModal] OnStartGameClicked (confirming ready)</color>");
            audioSystem.PlayMenuAudio(MenuAudioCategory.Confirmed);

            // Show "Waiting for others..." and hide the Start button
            if (startGameButton)
                startGameButton.gameObject.SetActive(false);
            if (waitingForOthersLabel)
                waitingForOthersLabel.SetActive(true);
            else
                Debug.LogWarning("[ArcadeConfigModal] waitingForOthersLabel is not assigned in the inspector.");

            // Tell the server this player is ready
            if (arcadeConfigSyncManager)
                arcadeConfigSyncManager.ConfirmLocalPlayerReady();
        }

        /// <summary>
        /// Called on ALL instances (host + clients) when every human player
        /// has pressed Start/Confirm. The host launches the game; clients
        /// close their modal (they'll be pulled into the game scene via Netcode).
        /// </summary>
        void HandleAllPlayersReady()
        {
            Debug.Log("<color=#FFD700>[FLOW-2] [ArcadeConfigModal] All players ready!</color>");

            if (hostConnectionData != null && hostConnectionData.IsHost)
            {
                audioSystem.PlayMenuAudio(MenuAudioCategory.LetsGo);
                SyncAllGameDataForLaunch();
                Debug.Log("<color=#FFD700>[FLOW-2] [ArcadeConfigModal] Calling gameData.InvokeGameLaunch()</color>");
                gameData.InvokeGameLaunch();
            }

            // Close the modal on all instances
            ModalWindowOut();
        }

        void SyncAllGameDataForLaunch()
        {
            if (!gameData || config?.SelectedGame == null)
            {
                Debug.LogError("<color=#FF0000>[FLOW-2] [ArcadeConfigModal] SyncAllGameDataForLaunch — gameData or config.SelectedGame is NULL!</color>");
                return;
            }

            var selectedGame = config.SelectedGame;
            gameData.SyncFromArcadeGame(selectedGame);

            int humanCount = CurrentPartyHumanCount;

            // Single source of truth — GameDataSO owns the player count computation
            gameData.ConfigurePlayerCounts(config.PlayerCount, humanCount);

            Debug.Log($"<color=#FFD700>[FLOW-2] [ArcadeConfigModal] SyncAllGameDataForLaunch — " +
                      $"Scene={selectedGame.SceneName}, Mode={selectedGame.Mode}, IsMultiplayer={selectedGame.IsMultiplayer}, " +
                      $"HumanCount={humanCount}, ConfigPlayerCount={config.PlayerCount}, " +
                      $"AIBackfill={gameData.RequestedAIBackfillCount}, " +
                      $"Vessel={gameData.selectedVesselClass.Value}, Intensity={gameData.SelectedIntensity.Value}</color>");

            // Hand off the party session so MultiplayerSetup in the game scene
            // knows to reuse the existing Relay connection instead of tearing it down.
            if (HostConnectionService.Instance?.PartySession != null)
                gameData.ActiveSession = HostConnectionService.Instance.PartySession;
        }

        #endregion

        #region Favorites

        public void ToggleFavorite()
        {
            if (_selectedGame == null) return;

            if (arcadeExploreView != null)
                arcadeExploreView.ToggleFavorite();

            if (selectedGameFavoriteIcon != null)
                selectedGameFavoriteIcon.Favorited = FavoriteSystem.IsFavorited(_selectedGame.Mode);
        }

        #endregion

        #region GameData sync helpers

        void SyncGameDataConfig()
        {
            if (!gameData) return;

            if (gameData.SelectedIntensity)
                gameData.SelectedIntensity.Value = config.Intensity;

            // SelectedPlayerCount is NOT synced here — during modal interaction,
            // config.PlayerCount (ArcadeGameConfigSO) is the interim store.
            // Player count is only committed to gameData at launch via ConfigurePlayerCounts().
        }

        void SyncGameDataShip(SO_Ship ship)
        {
            if (!gameData || !gameData.selectedVesselClass)
                return;

            VesselClassType targetClass = ship ? ship.Class : VesselClassType.Dolphin;

            gameData.selectedVesselClass.Value = targetClass;

            if (gameData.VesselClassSelectedIndex)
                gameData.VesselClassSelectedIndex.Value = (int)targetClass;

            if (shipClassTypeVariable != null)
                shipClassTypeVariable.Value = (int)targetClass;
        }

        /// <summary>
        /// Writes the selected vessel class directly to the local Player's
        /// NetDefaultVesselType NetworkVariable (owner-writable). This ensures
        /// each client's vessel choice is propagated to the server independently
        /// of gameData.selectedVesselClass (which carries the host's choice).
        /// </summary>
        void SyncLocalPlayerVesselType(SO_Ship ship)
        {
            if (gameData.LocalPlayer is not Player localPlayer) return;
            if (!localPlayer.IsOwner) return;

            var vesselType = ship ? ship.Class : VesselClassType.Dolphin;
            localPlayer.NetDefaultVesselType.Value = vesselType;
        }

        #endregion

        #region Client-side RPC handlers

        /// <summary>
        /// Called on non-host clients when the host opens the arcade config modal.
        /// Opens the same modal in client mode with host-only controls disabled.
        /// </summary>
        void HandleConfigOpenedOnClient(int gameModeInt, int intensity, int playerCount, int maxPlayers)
        {
            _isClientMode = true;

            // Look up the SO_ArcadeGame by mode so we can show the same game info
            SO_ArcadeGame game = arcadeConfigSyncManager.FindGameByMode(gameModeInt);
            if (game == null)
            {
                Debug.LogWarning($"[ArcadeConfigModal] Client could not find game for mode {gameModeInt}");
                return;
            }

            _selectedGame = game;

            config.ResetState();
            config.SelectedGame = game;
            config.TeamCount    = 1;
            config.Intensity    = intensity;
            config.PlayerCount  = playerCount;

            BuildAvailableShips(game);
            InitializeGameMetaView(game);
            InitializeScreen1Controls(game);
            InitializeDefaultShipFromAvailable();
            ApplyHostOnlyInteractability();
            ResetReadyUpUI();

            ModalWindowIn();
            ShowConfigurationScreen();
        }

        /// <summary>
        /// Called on non-host clients when the host closes the modal or starts a game.
        /// </summary>
        void HandleConfigClosedOnClient()
        {
            _isClientMode = false;
            ModalWindowOut();
        }

        /// <summary>
        /// Called on non-host clients when the host navigates between modal screens.
        /// Clients follow the same screen transitions so they can see vessel/domain selection.
        /// </summary>
        void HandleScreenChangedOnClient(int screenIndex)
        {
            switch (screenIndex)
            {
                case 0: ShowConfigurationScreen(); break;
                case 1: ShowGameDetailScreen(); break;
                case 2: ShowVesselSelectionScreen(); break;
                case 3: ShowSquadMateSelectionScreen(); break;
            }
        }

        /// <summary>
        /// Called on non-host clients when the host changes intensity or player count.
        /// Updates the read-only display values.
        /// </summary>
        void HandleConfigUpdatedOnClient(int intensity, int playerCount)
        {
            if (_selectedGame == null || config == null) return;

            config.Intensity   = intensity;
            config.PlayerCount = playerCount;

            // Update intensity button visuals (read-only — buttons are not interactable)
            foreach (var button in intensityButtons)
            {
                if (!button) continue;
                button.SetSelected(button.Intensity == intensity);
            }

            // Update player count display (read-only — stepper is not interactable)
            if (playerCountStepper)
                playerCountStepper.SetValue(playerCount);
        }

        /// <summary>
        /// Disables host-only controls when in client mode.
        /// Intensity buttons and player count stepper become non-interactable.
        /// Team selection, vessel selection, and the Start/Confirm button remain
        /// interactive for all players (host and clients).
        /// </summary>
        void ApplyHostOnlyInteractability()
        {
            bool isHost = !IsClientMode;

            // Intensity buttons — read-only for clients
            foreach (var button in intensityButtons)
            {
                if (!button) continue;
                var uiButton = button.GetComponent<Button>();
                if (uiButton) uiButton.interactable = isHost;
            }

            // Player count stepper — visible for all, but only host can change it
            if (playerCountStepper)
                playerCountStepper.SetInteractable(isHost);
        }

        /// <summary>
        /// Resets the ready-up UI to its initial state: Start button visible,
        /// "Waiting for others..." label hidden.
        /// </summary>
        void ResetReadyUpUI()
        {
            if (startGameButton)
                startGameButton.gameObject.SetActive(true);
            if (waitingForOthersLabel)
                waitingForOthersLabel.SetActive(false);
        }

        #endregion
    }
}

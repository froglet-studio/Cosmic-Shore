using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
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
        // TEMP for legacy systems (e.g. DailyChallengeSystem)
        public static ArcadeGameConfigureModal Instance { get; private set; }

        [Header("Config State")]
        [SerializeField] private ArcadeGameConfigSO  config;
        [SerializeField] private ScriptableEventNoParam configChangedEvent;
        [SerializeField] private ScriptableEventNoParam startGameRequestedEvent;

        [Header("Shared Game Data")]
        [Inject] private GameDataSO gameData;
        [SerializeField] private ScriptableVariable<int> shipClassTypeVariable; // broadcast class index

        [Header("Host / Party Data")]
        [Inject] private HostConnectionDataSO hostConnectionData;

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

        [Header("Screen 1 – Intensity Controls")]
        [SerializeField] private List<IntensitySelectButton> intensityButtons   = new(4);

        [Header("Screen 1 – Player Count (Component Stepper)")]
        [SerializeField] private PlayerCountStepper playerCountStepper;

        [Header("Screen 1 – Player Count (Inline Stepper)")]
        [SerializeField] private Button playerCountDecrementButton;
        [SerializeField] private Button playerCountIncrementButton;
        [SerializeField] private TMP_Text playerCountValueText;

        [Header("Screen 1 – Team Count Stepper")]
        [SerializeField] private Button teamCountDecrementButton;
        [SerializeField] private Button teamCountIncrementButton;
        [SerializeField] private TMP_Text teamsValueText;

        [Header("Screen 1 – Team Selection")]
        [SerializeField] private TeamSelectionPanel teamSelectionPanel;

        [Header("Screen 2 – Domain (Team) Selection")]
        [Tooltip("One TeamInfoData per selectable team. First entry should be the RANDOM option (Domain = Unassigned).")]
        [SerializeField] private List<TeamInfoData> teamInfoItems = new();

        [Header("Screen 2 – Selected Vessel Summary")]
        [SerializeField] private Image    shipPlaceholderIcon;
        [SerializeField] private TMP_Text shipNameText;
        [SerializeField] private TMP_Text shipConfigurationText;
        [SerializeField] private TMP_Text shipVesselNameText;

        [Tooltip("Optional secondary icon (e.g. config screen).")]
        [SerializeField] private Image iconInConfigurationSelectionView;

        [Tooltip("Optional icon in the game-detail view.")]
        [SerializeField] private Image iconInGameDetailView;

        [Header("Vessel Navigation")]
        [Tooltip("Button to cycle to the previous vessel. Hidden when only one vessel available.")]
        [SerializeField] private Button previousShipButton;
        [Tooltip("Button to cycle to the next vessel. Hidden when only one vessel available.")]
        [SerializeField] private Button nextShipButton;

        /// <summary>Fired when a locked intensity button is clicked. Args: (lockedIntensity)</summary>
        public event Action<int> OnLockedIntensityClicked;

        [Header("Ready-Up UI")]
        [Tooltip("Start/Confirm button — all players press this to lock in their choices.")]
        [SerializeField] private Button startGameButton;

        [Tooltip("'Waiting for others...' label — shown after a player confirms, hidden when choosing.")]
        [SerializeField] private GameObject waitingForOthersLabel;

        [Header("Network Sync")]
        [SerializeField] private ArcadeConfigSyncManager arcadeConfigSyncManager;

        // Hard cap on the number of players/teams the game supports
        const int MaxSupportedPlayers = 4;
        const int MaxSupportedTeams = 3;
        const int MinTeams = 1;

        // Runtime state
        SO_ArcadeGame _selectedGame;
        VideoPlayer   _previewVideo;
        bool _isClientMode;

        readonly List<SO_Vessel> _availableShips = new();
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

        protected override void Start()
        {
            base.Start();

            // Ensure no stale game selection from a previous session or scene load.
            // ArcadeGameConfigSO is a ScriptableObject that persists in memory across
            // scene transitions — if SelectedGame was set before a game launched, it
            // would still be set when Menu_Main reloads.
            _selectedGame = null;
            if (config) config.ResetState();
        }

        void OnEnable()
        {
            foreach (var intensityButton in intensityButtons)
            {
                intensityButton.OnSelect += HandleIntensitySelected;
                intensityButton.OnLockedSelect += HandleLockedIntensitySelected;
            }

            if (playerCountStepper)
                playerCountStepper.OnValueChanged += HandlePlayerCountSelected;

            // Inline player count buttons (development UI)
            if (playerCountDecrementButton)
                playerCountDecrementButton.onClick.AddListener(OnPlayerCountDecrement);
            if (playerCountIncrementButton)
                playerCountIncrementButton.onClick.AddListener(OnPlayerCountIncrement);

            // Team count buttons
            if (teamCountDecrementButton)
                teamCountDecrementButton.onClick.AddListener(OnTeamCountDecrement);
            if (teamCountIncrementButton)
                teamCountIncrementButton.onClick.AddListener(OnTeamCountIncrement);

            // Domain / team info buttons
            foreach (var item in teamInfoItems)
            {
                if (!item || !item.Button) continue;
                var captured = item.Domain;
                item.Button.onClick.AddListener(() => HandleDomainSelected(captured));
            }

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
                Debug.Log($"[ArcadeConfigModal] OnEnable — subscribed to ArcadeConfigSyncManager events (instance={GetInstanceID()})");
            }
            else
            {
                Debug.LogWarning($"[ArcadeConfigModal] OnEnable — arcadeConfigSyncManager is NULL, cannot subscribe (instance={GetInstanceID()})");
            }
        }

        void OnDisable()
        {
            foreach (var intensityButton in intensityButtons)
            {
                intensityButton.OnSelect -= HandleIntensitySelected;
                intensityButton.OnLockedSelect -= HandleLockedIntensitySelected;
            }

            if (playerCountStepper)
                playerCountStepper.OnValueChanged -= HandlePlayerCountSelected;

            if (playerCountDecrementButton)
                playerCountDecrementButton.onClick.RemoveListener(OnPlayerCountDecrement);
            if (playerCountIncrementButton)
                playerCountIncrementButton.onClick.RemoveListener(OnPlayerCountIncrement);

            if (teamCountDecrementButton)
                teamCountDecrementButton.onClick.RemoveListener(OnTeamCountDecrement);
            if (teamCountIncrementButton)
                teamCountIncrementButton.onClick.RemoveListener(OnTeamCountIncrement);

            foreach (var item in teamInfoItems)
            {
                if (item && item.Button)
                    item.Button.onClick.RemoveAllListeners();
            }

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
            config.TeamCount    = 1; // default to 1 team — user can adjust via stepper

            BuildAvailableShips(selectedGame);
            InitializeConfigFromGameDefaults(selectedGame);
            InitializeGameMetaView(selectedGame);
            InitializeScreen1Controls(selectedGame);
            InitializeDefaultShipFromAvailable();
            InitializeDomainSelection();
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
            // Clamp default intensity to what the player has actually unlocked
            var progressionService = GameModeProgressionService.Instance;
            int maxUnlocked = progressionService != null
                ? progressionService.GetMaxUnlockedIntensity(game.Mode)
                : game.MaxIntensity;

            config.Intensity   = Mathf.Clamp(game.MinIntensity, game.MinIntensity, maxUnlocked);
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
            var progressionService = GameModeProgressionService.Instance;

            for (int i = 0; i < intensityButtons.Count; i++)
            {
                var button = intensityButtons[i];
                if (!button) continue;

                int level = i + 1;
                button.SetIntensityLevel(level);

                bool active = level >= game.MinIntensity && level <= game.MaxIntensity;
                button.SetActive(active);

                // Lock intensity 3 and 4 if the player hasn't unlocked them yet
                if (active && progressionService != null)
                {
                    bool unlocked = progressionService.IsIntensityUnlocked(game.Mode, level);
                    button.SetLocked(!unlocked);
                }

                button.SetSelected(active && level == config.Intensity);
            }

            // Player count — enforce minimum = party size so host can't select
            // fewer total players than there are humans in the lobby.
            int effectiveMin = Mathf.Max(game.MinPlayersAllowed, CurrentPartyHumanCount);

            // Component stepper UI (preferred — supports 1-12 range)
            if (playerCountStepper)
                playerCountStepper.Initialize(effectiveMin, game.MaxPlayersAllowed, config.PlayerCount);

            // Inline stepper UI (development UI)
            RefreshPlayerCountStepper();

            // Team count stepper
            RefreshTeamCountStepper();

            if (teamSelectionPanel && gameData != null && gameData.LocalPlayer is Player localPlayer)
                teamSelectionPanel.SetSelection(localPlayer.NetDomain.Value);
        }

        void BuildAvailableShips(SO_ArcadeGame game)
        {
            _availableShips.Clear();

            if (!game || game.Vessels == null) return;

            _availableShips.AddRange(game.Vessels.Where(s => s != null && !s.IsLocked));
            UpdateShipNavigationButtons();
        }

        void UpdateShipNavigationButtons()
        {
            bool canCycle = _availableShips.Count > 1;

            if (previousShipButton)
                previousShipButton.gameObject.SetActive(canCycle);

            if (nextShipButton)
                nextShipButton.gameObject.SetActive(canCycle);
        }
        
        void InitializeDefaultShipFromAvailable()
        {
            if (_availableShips.Count == 0)
            {
                _currentShipIndex = -1;
                SetSelectedShipInternal(null);
                return;
            }

            SO_Vessel chosen = null;

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

        void SetScreenActive(GameObject configScreen, GameObject gameDetailScreen)
        {
            if (configurationDetailView)
                configurationDetailView.SetActive(configurationDetailView == configScreen);

            if (gameDetailView)
                gameDetailView.SetActive(gameDetailView == gameDetailScreen);
        }

        void ShowConfigurationScreen()
        {
            SetScreenActive(configurationDetailView, null);
        }

        void ShowGameDetailScreen()
        {
            SetScreenActive(null, gameDetailView);
            RefreshShipSummaryView();
        }

        void ShowVesselSelectionScreen()
        {
            ShowGameDetailScreen();
        }

        void ShowSquadMateSelectionScreen()
        {
            ShowGameDetailScreen();
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
            if (IsClientMode) return;

            int effectiveMin = Mathf.Max(_selectedGame.MinPlayersAllowed, CurrentPartyHumanCount);
            playerCount        = Mathf.Clamp(playerCount, effectiveMin, _selectedGame.MaxPlayersAllowed);
            config.PlayerCount = playerCount;

            if (playerCountStepper)
                playerCountStepper.SetValue(playerCount);
            RefreshPlayerCountStepper();

            SyncGameDataConfig();
            RaiseConfigChanged();

            if (arcadeConfigSyncManager)
                arcadeConfigSyncManager.NotifyConfigUpdated(config.Intensity, config.PlayerCount);
        }

        void HandleTeamSelected(Domains domain)
        {
            if (gameData.LocalPlayer is not Player player) return;
            if (!player.IsOwner) return;
            player.NetDomain.Value = domain;
        }

        #endregion

        #region Inline player count stepper

        public void OnPlayerCountIncrement()
        {
            if (_selectedGame == null || config == null) return;
            if (IsClientMode) return;

            int max = Mathf.Min(_selectedGame.MaxPlayersAllowed, MaxSupportedPlayers);
            int next = Mathf.Min(config.PlayerCount + 1, max);
            SetPlayerCount(next);
        }

        public void OnPlayerCountDecrement()
        {
            if (_selectedGame == null || config == null) return;
            if (IsClientMode) return;

            int effectiveMin = Mathf.Max(_selectedGame.MinPlayersAllowed, CurrentPartyHumanCount);
            int next = Mathf.Max(config.PlayerCount - 1, effectiveMin);
            SetPlayerCount(next);
        }

        void SetPlayerCount(int playerCount)
        {
            if (config.PlayerCount == playerCount) return;

            config.PlayerCount = playerCount;

            // Update both stepper UIs
            if (playerCountStepper)
                playerCountStepper.SetValue(playerCount);
            RefreshPlayerCountStepper();

            SyncGameDataConfig();
            RaiseConfigChanged();

            if (arcadeConfigSyncManager)
                arcadeConfigSyncManager.NotifyConfigUpdated(config.Intensity, config.PlayerCount);
        }

        void RefreshPlayerCountStepper()
        {
            if (playerCountValueText)
                playerCountValueText.text = config.PlayerCount.ToString();

            if (_selectedGame == null) return;

            int effectiveMin = Mathf.Max(_selectedGame.MinPlayersAllowed, CurrentPartyHumanCount);
            int max = Mathf.Min(_selectedGame.MaxPlayersAllowed, MaxSupportedPlayers);

            if (playerCountDecrementButton)
                playerCountDecrementButton.interactable = config.PlayerCount > effectiveMin && !IsClientMode;

            if (playerCountIncrementButton)
                playerCountIncrementButton.interactable = config.PlayerCount < max && !IsClientMode;
        }

        #endregion

        #region Team count stepper

        public void OnTeamCountIncrement()
        {
            if (config == null) return;

            int next = Mathf.Min(config.TeamCount + 1, MaxSupportedTeams);
            SetTeamCount(next);
        }

        public void OnTeamCountDecrement()
        {
            if (config == null) return;

            int next = Mathf.Max(config.TeamCount - 1, MinTeams);
            SetTeamCount(next);
        }

        void SetTeamCount(int teamCount)
        {
            if (config.TeamCount == teamCount) return;

            config.TeamCount = teamCount;
            RefreshTeamCountStepper();
            SyncGameDataConfig();
            RaiseConfigChanged();
        }

        void RefreshTeamCountStepper()
        {
            if (teamsValueText)
                teamsValueText.text = config.TeamCount.ToString();

            if (teamCountDecrementButton)
                teamCountDecrementButton.interactable = config.TeamCount > MinTeams;

            if (teamCountIncrementButton)
                teamCountIncrementButton.interactable = config.TeamCount < MaxSupportedTeams;
        }

        #endregion

        #region Domain (team) selection via TeamInfoData

        void InitializeDomainSelection()
        {
            if (config != null)
                config.SelectedDomain = Domains.Unassigned;
            RefreshDomainButtons();
        }

        void HandleDomainSelected(Domains domain)
        {
            if (config != null)
                config.SelectedDomain = domain;

            // Write to local player's NetworkVariable
            if (gameData != null && gameData.LocalPlayer is Player player && player.IsOwner)
                player.NetDomain.Value = domain;

            SyncGameDataDomain();
            RefreshDomainButtons();
            RaiseConfigChanged();
        }

        void RefreshDomainButtons()
        {
            var selected = config ? config.SelectedDomain : Domains.Unassigned;

            Sprite avatarSprite = null;
            var dataService = PlayerDataService.Instance;
            if (dataService != null)
                avatarSprite = dataService.GetAvatarSprite(dataService.CurrentProfile.avatarId);

            foreach (var item in teamInfoItems)
            {
                if (!item) continue;
                item.SetSelected(item.Domain == selected);
                item.SetAvatarSprite(avatarSprite);
            }
        }

        void SyncGameDataDomain()
        {
            // Domain is synced via Player.NetDomain.Value in HandleDomainSelected.
            // No additional GameDataSO field needed.
        }

        #endregion

        #region Config change handlers (intensity, locked, external)

        void HandleLockedIntensitySelected(int intensity)
        {
            OnLockedIntensityClicked?.Invoke(intensity);

            if (_selectedGame == null) return;

            var service = GameModeProgressionService.Instance;
            if (service == null) return;

            var quest = service.GetQuestForMode(_selectedGame.Mode);
            if (quest == null) return;

            string goalDescription = intensity == 3
                ? quest.Intensity3GoalDescription
                : quest.Intensity4GoalDescription;

            ToastNotificationAPI.Show(goalDescription);
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

        void SetSelectedShipInternal(SO_Vessel ship)
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

        void RefreshShipSummaryView(SO_Vessel ship)
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
            AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.Confirmed);
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

            // Clear stale state so the modal never reopens showing a
            // previously-selected game (e.g. after returning from a game scene).
            _selectedGame = null;
            if (config) config.ResetState();

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

            // Tell the server this player is ready
            if (arcadeConfigSyncManager)
            {
                arcadeConfigSyncManager.ConfirmLocalPlayerReady();
            }
            else
            {
                // No sync manager — single player or no multiplayer setup.
                // Launch directly.
                HandleAllPlayersReady();
            }
        }

        /// <summary>
        /// Called on ALL instances (host + clients) when every human player
        /// has pressed Start/Confirm. The host launches the game; clients
        /// close their modal (they'll be pulled into the game scene via Netcode).
        /// </summary>
        void HandleAllPlayersReady()
        {
            Debug.Log("<color=#FFD700>[FLOW-2] [ArcadeConfigModal] All players ready!</color>");

            // If there's no sync manager, this is a solo/local launch — always proceed.
            // If there IS a sync manager, only the host should trigger the launch.
            bool shouldLaunch = !arcadeConfigSyncManager
                || (hostConnectionData != null && hostConnectionData.IsHost);

            if (shouldLaunch)
            {
                audioSystem.PlayMenuAudio(MenuAudioCategory.LetsGo);
                SyncAllGameDataForLaunch();
                Debug.Log("<color=#FFD700>[FLOW-2] [ArcadeConfigModal] Calling gameData.InvokeGameLaunch()</color>");
                gameData.InvokeGameLaunch();
            }

            // Clear runtime state so it can't resurface after returning to menu
            _selectedGame = null;
            if (config) config.ResetState();

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

            // Team count — controls how many teams AI can be assigned to
            gameData.RequestedTeamCount = config.TeamCount;

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

            if (gameData.SelectedPlayerCount)
                gameData.SelectedPlayerCount.Value = config.PlayerCount;
        }

        void SyncGameDataShip(SO_Vessel ship)
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
        void SyncLocalPlayerVesselType(SO_Vessel ship)
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
            Debug.Log($"[ArcadeConfigModal] HandleConfigOpenedOnClient — mode={gameModeInt}, intensity={intensity}, " +
                      $"players={playerCount}, max={maxPlayers}");

            _isClientMode = true;

            // Look up the SO_ArcadeGame by mode so we can show the same game info
            SO_ArcadeGame game = arcadeConfigSyncManager.FindGameByMode(gameModeInt);
            if (game == null)
            {
                Debug.LogWarning($"[ArcadeConfigModal] Client could not find game for mode {gameModeInt}. " +
                                 $"gameList injected={arcadeConfigSyncManager != null}");
                return;
            }

            _selectedGame = game;

            config.ResetState();
            config.SelectedGame = game;
            config.TeamCount    = 1; // clients inherit host's team count via UI sync
            config.Intensity    = intensity;
            config.PlayerCount  = playerCount;

            BuildAvailableShips(game);
            InitializeGameMetaView(game);
            InitializeScreen1Controls(game);
            InitializeDefaultShipFromAvailable();
            InitializeDomainSelection();
            ApplyHostOnlyInteractability();
            ResetReadyUpUI();

            Debug.Log("[ArcadeConfigModal] Calling ModalWindowIn on client");
            ModalWindowIn();

            // Clients skip the config screen (intensity/player count) and go
            // directly to domain + vessel selection since only the host controls those.
            ShowGameDetailScreen();
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
            RefreshPlayerCountStepper();
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

            // Player count stepper component — visible for all, but only host can change it
            if (playerCountStepper)
                playerCountStepper.SetInteractable(isHost);

            // Inline player count buttons
            if (playerCountDecrementButton)
                playerCountDecrementButton.interactable = isHost;
            if (playerCountIncrementButton)
                playerCountIncrementButton.interactable = isHost;

            // Team count buttons
            if (teamCountDecrementButton)
                teamCountDecrementButton.interactable = isHost;
            if (teamCountIncrementButton)
                teamCountIncrementButton.interactable = isHost;
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

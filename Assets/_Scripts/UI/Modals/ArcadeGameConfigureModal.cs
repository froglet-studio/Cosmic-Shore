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
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.InputSystem;
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
        [SerializeField] private GameObject vesselSelectionView;     // Screen 3

        [Header("Screen 3 – Vessel Selection Grid")]
        [Tooltip("Fixed pool of vessel cards inside the ShipSelectGrid. Configured from " +
                 "the game's unlocked vessels on screen entry; surplus cards are hidden.")]
        [SerializeField] private List<ShipSelectionItemView> vesselSelectionItems = new(6);

        [Header("Screen 1 – Intensity Controls")]
        [SerializeField] private List<IntensitySelectButton> intensityButtons   = new(4);

        [Header("Screen 1 – Player Count Stepper")]
        [FormerlySerializedAs("playerCountStepper")]
        [SerializeField] private IntStepper pcStepper;

        [Header("Screen 1 – Domain Count Stepper")]
        [SerializeField] private IntStepper dcStepper;

        [Header("Screen 2 – Domain Selection")]
        [Tooltip("One DomainInfoData per selectable domain (Jade, Ruby, Gold). " +
                 "Any Blue tile in this list is hidden at runtime - Random is gone, " +
                 "Jade is the unpicked default. Tiles outside ActiveDomains[0..DC-1] " +
                 "are dimmed and non-interactable.")]
        [FormerlySerializedAs("domainInfoItems")]
        [SerializeField] private List<DomainInfoData> domainInfoItems = new();

        [Tooltip("Avatar chip prefab. One instance is created per human player when the " +
                 "modal opens, parented to the player's currently-picked tile (Jade by default). " +
                 "Reparented to the new tile's strip on each player's NetDomain.OnValueChanged.")]
        [SerializeField] private DomainAvatarChip chipPrefab;

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
        [Tooltip("Start/Confirm button - all players press this to lock in their choices.")]
        [SerializeField] private Button startGameButton;

        [Tooltip("'Waiting for others...' label - shown after a player confirms, hidden when choosing.")]
        [SerializeField] private GameObject waitingForOthersLabel;

        [Header("Network Sync")]
        [SerializeField] private ArcadeConfigSyncManager arcadeConfigSyncManager;

        [Header("Screen 1 → Screen 2 transition")]
        [Tooltip("Confirm Configuration button on Screen 1. Disabled after the first click " +
                 "to defend against spam-clicks (commit fires exactly once per modal session).")]
        [SerializeField] private Button confirmConfigurationButton;

        [Tooltip("Optional: the Screen-2 Back button. Hidden on Screen-2 entry - the " +
                 "commit-once flow has no back path. Wire in the inspector if a back " +
                 "button still exists in the prefab.")]
        [SerializeField] private GameObject backFromGameSelectButton;

        [Header("D-pad Row Highlights")]
        [Tooltip("Background or border Image on each Screen 1 row, indexed 0-3: " +
                 "Intensity, Player Count, Domain Count, Confirm. " +
                 "Tinted to show which row the D-pad currently targets.")]
        [SerializeField] private List<Image> dpadRowHighlights = new(4);
        [SerializeField] private Color dpadFocusColor = new(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color dpadUnfocusColor = new(1f, 1f, 1f, 0f);

        // D-pad navigation for Screen 1 - rows: 0=intensity, 1=player count, 2=domain count, 3=confirm
        bool _dpadHighlightActive;
        int _dpadFocusRow;
        const int DpadRowIntensity = 0;
        const int DpadRowPlayerCount = 1;
        const int DpadRowDomainCount = 2;
        const int DpadRowConfirm = 3;
        const int DpadRowCount = 4;

        // Hard cap on the number of players/domains the game supports
        const int MaxSupportedPlayers = 12;
        const int MaxSupportedDomains = 3;
        const int MinDomains = 1;
        const int DefaultDomainCount = 1;

        // Per-game minimum domain (team) count, from SO_ArcadeGame.MinDomainsAllowed.
        // Modes that need opposing teams (e.g. Joust) set it to 2 so the domain stepper
        // and the computed default can never collapse to a single domain - which would
        // put every player on one team, leaving the AI/humans with no opponents.
        int MinDomainsForGame =>
            _selectedGame != null
                ? Mathf.Clamp(_selectedGame.MinDomainsAllowed, MinDomains, MaxSupportedDomains)
                : MinDomains;

        // Runtime state
        SO_ArcadeGame _selectedGame;
        VideoPlayer   _previewVideo;
        bool _isClientMode;

        // Modal-side single-shot guard for the host's "Confirm Configuration"
        // button. Set true on first click; gates re-entry into OnConfirmConfiguration
        // so a host spam-click does not re-trigger the chip respawn, audio, or
        // server commit. Reset on modal-open (SetSelectedGame) and modal-close
        // (CloseAndNotifyClients) so the next session starts clean.
        bool _isConfigurationCommitted;

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
            // scene transitions - if SelectedGame was set before a game launched, it
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

            if (pcStepper)
                pcStepper.OnValueChanged += HandlePlayerCountSelected;

            if (dcStepper)
                dcStepper.OnValueChanged += HandleDomainCountChanged;

            // Domain info buttons
            foreach (var item in domainInfoItems)
            {
                if (!item || !item.Button) continue;
                var captured = item.Domain;
                item.Button.onClick.AddListener(() => HandleDomainSelected(captured));
            }

            if (configChangedEvent != null)
                configChangedEvent.OnRaised += HandleConfigChangedExternal;

            if (arcadeConfigSyncManager)
            {
                arcadeConfigSyncManager.OnConfigOpenedOnClient += HandleConfigOpenedOnClient;
                arcadeConfigSyncManager.OnConfigClosedOnClient += HandleConfigClosedOnClient;
                arcadeConfigSyncManager.OnScreenChangedOnClient += HandleScreenChangedOnClient;
                arcadeConfigSyncManager.OnAllPlayersReady += HandleAllPlayersReady;
                Debug.Log($"[ArcadeConfigModal] OnEnable - subscribed to ArcadeConfigSyncManager events (instance={GetInstanceID()})");
            }
            else
            {
                Debug.LogWarning($"[ArcadeConfigModal] OnEnable - arcadeConfigSyncManager is NULL, cannot subscribe (instance={GetInstanceID()})");
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            foreach (var intensityButton in intensityButtons)
            {
                intensityButton.OnSelect -= HandleIntensitySelected;
                intensityButton.OnLockedSelect -= HandleLockedIntensitySelected;
            }

            if (pcStepper)
                pcStepper.OnValueChanged -= HandlePlayerCountSelected;

            if (dcStepper)
                dcStepper.OnValueChanged -= HandleDomainCountChanged;

            foreach (var item in domainInfoItems)
            {
                if (item && item.Button)
                    item.Button.onClick.RemoveAllListeners();
            }

            if (configChangedEvent != null)
                configChangedEvent.OnRaised -= HandleConfigChangedExternal;

            if (arcadeConfigSyncManager)
            {
                arcadeConfigSyncManager.OnConfigOpenedOnClient -= HandleConfigOpenedOnClient;
                arcadeConfigSyncManager.OnConfigClosedOnClient -= HandleConfigClosedOnClient;
                arcadeConfigSyncManager.OnScreenChangedOnClient -= HandleScreenChangedOnClient;
                arcadeConfigSyncManager.OnAllPlayersReady -= HandleAllPlayersReady;
            }

            // Drop NetDomain / NetAvatarId subscriptions if the modal is being
            // disabled while still open (scene transition, OnDestroy, etc).
            DespawnAllChips();
        }

        protected override void Update()
        {
            base.Update();

            var pad = Gamepad.current;
            if (pad == null) return;
            if (IsClientMode) return;
            if (!configurationDetailView || !configurationDetailView.activeSelf) return;

            if (pad.dpad.up.wasPressedThisFrame)
            {
                ActivateDpadHighlight();
                MoveDpadFocusRow(-1);
            }
            else if (pad.dpad.down.wasPressedThisFrame)
            {
                ActivateDpadHighlight();
                MoveDpadFocusRow(1);
            }
            else if (pad.dpad.left.wasPressedThisFrame)
            {
                ActivateDpadHighlight();
                HandleDpadHorizontal(-1);
            }
            else if (pad.dpad.right.wasPressedThisFrame)
            {
                ActivateDpadHighlight();
                HandleDpadHorizontal(1);
            }
            else if (pad.buttonSouth.wasPressedThisFrame && _dpadFocusRow == DpadRowConfirm)
            {
                OnConfirmConfiguration();
            }
        }

        void MoveDpadFocusRow(int direction)
        {
            _dpadFocusRow = Mathf.Clamp(_dpadFocusRow + direction, 0, DpadRowCount - 1);
            RefreshDpadRowHighlights();
        }

        void RefreshDpadRowHighlights()
        {
            if (!_dpadHighlightActive) return;
            for (int i = 0; i < dpadRowHighlights.Count; i++)
            {
                if (!dpadRowHighlights[i]) continue;
                dpadRowHighlights[i].color = i == _dpadFocusRow ? dpadFocusColor : dpadUnfocusColor;
            }
        }

        void ClearDpadRowHighlights()
        {
            _dpadHighlightActive = false;
            for (int i = 0; i < dpadRowHighlights.Count; i++)
            {
                if (!dpadRowHighlights[i]) continue;
                dpadRowHighlights[i].color = dpadUnfocusColor;
            }
        }

        void ActivateDpadHighlight()
        {
            if (_dpadHighlightActive) return;
            _dpadHighlightActive = true;
            RefreshDpadRowHighlights();
        }

        void HandleDpadHorizontal(int direction)
        {
            switch (_dpadFocusRow)
            {
                case DpadRowIntensity:
                    CycleIntensity(direction);
                    break;
                case DpadRowPlayerCount:
                    if (pcStepper)
                    {
                        if (direction > 0) pcStepper.Increment();
                        else pcStepper.Decrement();
                    }
                    break;
                case DpadRowDomainCount:
                    if (dcStepper)
                    {
                        if (direction > 0) dcStepper.Increment();
                        else dcStepper.Decrement();
                    }
                    break;
            }
        }

        void CycleIntensity(int direction)
        {
            if (config == null) return;

            int currentIdx = -1;
            for (int i = 0; i < intensityButtons.Count; i++)
            {
                if (intensityButtons[i] && intensityButtons[i].Intensity == config.Intensity)
                {
                    currentIdx = i;
                    break;
                }
            }

            int nextIdx = currentIdx;
            while (true)
            {
                nextIdx += direction;
                if (nextIdx < 0 || nextIdx >= intensityButtons.Count) return;
                var btn = intensityButtons[nextIdx];
                if (!btn) continue;
                var uiBtn = btn.GetComponent<Button>();
                if (uiBtn && uiBtn.enabled)
                {
                    btn.Select();
                    return;
                }
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

            // Fresh modal session - re-arm the commit guard so OnConfirmConfiguration
            // can fire again. The Confirm button is re-enabled below in
            // ResetCommitGuard().
            ResetCommitGuard();

            config.ResetState();
            config.SelectedGame = selectedGame;

            BuildAvailableShips(selectedGame);
            InitializeConfigFromGameDefaults(selectedGame);
            // Compute the default domain count AFTER player count is set: the DC bound
            // depends on PC (DC <= PC) and ResetState() leaves PlayerCount at 0. For modes
            // with MinDomainsAllowed >= 2 (Joust) this defaults the stepper to 2, not 1.
            config.DomainCount = ComputeDefaultDomainCount();
            InitializeGameMetaView(selectedGame);
            InitializeScreen1Controls(selectedGame);
            InitializeDefaultShipFromAvailable();
            InitializeDomainSelection();
            ApplyHostOnlyInteractability();
            ResetReadyUpUI();

            _dpadFocusRow = DpadRowIntensity;
            ClearDpadRowHighlights();

            // Host configures privately on Screen 1. No client involvement until
            // the host clicks Confirm Configuration → CommitConfiguration RPC fires.
            ShowConfigurationScreen();
            RaiseConfigChanged();
        }

        #endregion

        #region Initialization helpers

        int CurrentPartyHumanCount
        {
            get
            {
                // Prefer Netcode connected client count - it's the ground truth for
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
                {
                    // Stretch to fill the preview window. The prefab's authored fixed
                    // size predates the canvas resolution upgrade and no longer matches
                    // the parent, leaving the video floating small in its frame.
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }
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

            // Player count - enforce minimum = party size so host can't select
            // fewer total players than there are humans in the lobby.
            int effectiveMin = Mathf.Max(game.MinPlayersAllowed, CurrentPartyHumanCount);
            int pcMax = Mathf.Min(game.MaxPlayersAllowed, MaxSupportedPlayers);

            if (pcStepper)
                pcStepper.Initialize(effectiveMin, pcMax, config.PlayerCount);

            // Domain count stepper - max bound depends on current PC (DC <= PC);
            // min bound is the per-game minimum (2 for opposing-team modes like Joust).
            if (dcStepper)
                dcStepper.Initialize(MinDomainsForGame, ComputeMaxDomainCount(), config.DomainCount);
        }

        int ComputeMaxDomainCount()
        {
            // DC <= PC, capped at the hard max and at the per-game MaxDomainsAllowed
            // (modes with a fixed team shape - e.g. Astro League - pin this to 2). Fall
            // back to the hard max when PC isn't set yet (ResetState leaves it 0), and
            // never drop below the per-game minimum.
            int pc = config != null && config.PlayerCount > 0 ? config.PlayerCount : MaxSupportedDomains;
            int gameMax = _selectedGame
                ? Mathf.Clamp(_selectedGame.MaxDomainsAllowed, MinDomains, MaxSupportedDomains)
                : MaxSupportedDomains;
            int max = Mathf.Min(Mathf.Min(pc, MaxSupportedDomains), gameMax);
            return Mathf.Max(max, MinDomainsForGame);
        }

        int ComputeDefaultDomainCount() =>
            Mathf.Clamp(DefaultDomainCount, MinDomainsForGame, ComputeMaxDomainCount());

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

        void SetScreenActive(GameObject configScreen, GameObject gameDetailScreen, GameObject vesselScreen = null)
        {
            if (configurationDetailView)
                configurationDetailView.SetActive(configurationDetailView == configScreen);

            if (gameDetailView)
                gameDetailView.SetActive(gameDetailView == gameDetailScreen);

            if (vesselSelectionView)
                vesselSelectionView.SetActive(vesselSelectionView == vesselScreen);
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
            // Prefab variants without the vessel screen wired keep the old
            // stay-on-game-detail behavior instead of blanking the right side.
            if (!vesselSelectionView)
            {
                ShowGameDetailScreen();
                return;
            }

            SetScreenActive(null, null, vesselSelectionView);
            RefreshVesselSelectionItems();
            RefreshShipSummaryView(); // keeps the screen-3 header (shipVesselNameText) current
        }

        void ShowSquadMateSelectionScreen()
        {
            // No squad-mate screen exists in the prefab yet.
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
        }

        void HandlePlayerCountSelected(int playerCount)
        {
            if (_selectedGame == null || config == null) return;
            if (IsClientMode) return;

            int effectiveMin = Mathf.Max(_selectedGame.MinPlayersAllowed, CurrentPartyHumanCount);
            int pcMax = Mathf.Min(_selectedGame.MaxPlayersAllowed, MaxSupportedPlayers);
            playerCount        = Mathf.Clamp(playerCount, effectiveMin, pcMax);
            config.PlayerCount = playerCount;

            if (pcStepper)
                pcStepper.SetValue(playerCount);

            // PC change may shrink the DC bound (DC <= PC). Re-clamp + re-bound the DC stepper.
            int newDcMax = ComputeMaxDomainCount();
            int newDc = Mathf.Clamp(config.DomainCount, MinDomainsForGame, newDcMax);
            if (newDc != config.DomainCount)
                config.DomainCount = newDc;
            if (dcStepper)
                dcStepper.Initialize(MinDomainsForGame, newDcMax, config.DomainCount);

            RefreshTileVisibility();
            SyncGameDataConfig();
            RaiseConfigChanged();
        }

        #endregion

        #region Domain count stepper

        void HandleDomainCountChanged(int newDomainCount)
        {
            if (config == null) return;
            if (IsClientMode) return;

            // Pre-commit, host-local DC change. No snap-back: nobody has picked
            // a domain yet (CommitConfiguration resets all humans to Jade), so
            // there's nothing to protect against. No client broadcast either -
            // clients don't open the modal until commit.
            int proposed = Mathf.Clamp(newDomainCount, MinDomainsForGame, ComputeMaxDomainCount());

            if (proposed == config.DomainCount)
            {
                if (dcStepper) dcStepper.SetValue(config.DomainCount);
                return;
            }

            config.DomainCount = proposed;
            RefreshTileVisibility();
            SyncGameDataConfig();
            RaiseConfigChanged();
        }

        #endregion

        #region Domain selection via DomainInfoData

        void InitializeDomainSelection()
        {
            if (config != null)
                config.SelectedDomain = Domains.Jade;
            RefreshTileVisibility();
        }

        /// <summary>
        /// Resolves the local human's own Player for owner-writes (domain pick RPC,
        /// vessel type). Primary source is gameData.LocalPlayer; falls back to
        /// NetworkManager.LocalClient.PlayerObject because LocalPlayer can be null or
        /// stale on a client whose menu pair-init hasn't completed (game→menu return),
        /// or after RemovePlayerData's Players[0] repair pointed it at another player
        /// (on the host that can even be an AI, which shares the host's client id -
        /// hence the IsInitializedAsAI guard).
        /// Returns null only when no owned Player exists - callers must treat that as
        /// an error, not skip silently: a swallowed pick leaves the player on a stale
        /// domain for the whole next game while the tile UI claims otherwise.
        /// </summary>
        Player ResolveLocalOwnedPlayer()
        {
            if (gameData != null
                && gameData.LocalPlayer is Player cached
                && cached.IsOwner && !cached.IsInitializedAsAI)
                return cached;

            var nm = NetworkManager.Singleton;
            var playerObj = nm != null ? nm.LocalClient?.PlayerObject : null;
            if (playerObj != null && playerObj.TryGetComponent<Player>(out var resolved) && resolved.IsOwner)
            {
                Debug.LogWarning("[ArcadeConfigModal] gameData.LocalPlayer was null/stale - " +
                                 "resolved local Player via NetworkManager.LocalClient instead.");
                return resolved;
            }

            return null;
        }

        void HandleDomainSelected(Domains domain)
        {
            // Resolve BEFORE touching any UI state: if the pick cannot reach the server,
            // the tile must not highlight - the UI shown to the player always matches the
            // server's truth (chip movement is already NetDomain-event-driven).
            var player = ResolveLocalOwnedPlayer();
            if (player == null)
            {
                Debug.LogError($"[ArcadeConfigModal] Domain pick '{domain}' DROPPED - no owned local " +
                               "Player resolved (pair-init incomplete after scene return?). " +
                               "Pick not sent to server; tile selection unchanged.");
                return;
            }

            if (config != null)
                config.SelectedDomain = domain;

            // Request a server-authoritative domain update for the local player.
            // The chip movement is purely event-driven - Player.NetDomain.OnValueChanged
            // fires on every client (including the host) and triggers the surgical
            // reparent in HandlePlayerDomainChanged. No refresh-everything-each-event.
            player.RequestSetDomain_ServerRpc(domain);

            SyncGameDataDomain();
            RefreshTileVisibility();
            RaiseConfigChanged();
        }

        // ── Per-player chip lifecycle ─────────────────────────────────────────
        // One DomainAvatarChip is instantiated per human player when the modal
        // opens, parented to the Blue tile's strip. Each player's own chip is
        // reparented to whichever tile they pick on NetDomain.OnValueChanged.
        // Chips are destroyed on modal close.

        readonly Dictionary<Player, DomainAvatarChip> _playerChips = new();
        readonly Dictionary<Player, NetworkVariable<Domains>.OnValueChangedDelegate> _domainHandlers = new();
        bool _watchingPlayerSpawnEvent;

        void SpawnChipsForAllPlayers()
        {
            DespawnAllChips();
            if (gameData == null) return;

            if (chipPrefab == null)
            {
                Debug.LogWarning("[DomainPicker] Chip Prefab is not wired on ArcadeGameConfigureModal - cannot spawn chips.");
                return;
            }

            ClearStaleChipsFromAllStrips();

            ulong localId = NetworkManager.Singleton ? NetworkManager.Singleton.LocalClientId : 0UL;
            var dataService = PlayerDataService.Instance;

            foreach (var ip in gameData.Players)
            {
                if (ip is Player p && !p.NetIsAI.Value)
                    SpawnChipForPlayer(p, localId, dataService);
            }

            // Late-joiner support - new humans get a chip as soon as their Player object replicates.
            if (gameData.OnPlayerNetworkSpawnedUlong != null && !_watchingPlayerSpawnEvent)
            {
                gameData.OnPlayerNetworkSpawnedUlong.OnRaised += HandlePlayerSpawnedDuringModal;
                _watchingPlayerSpawnEvent = true;
            }
        }

        void SpawnChipForPlayer(Player p, ulong localId, PlayerDataService dataService)
        {
            if (p == null || _playerChips.ContainsKey(p)) return;

            var startTile = FindTileForDomain(p.NetDomain.Value) ?? FindTileForDomain(Domains.Jade);
            if (startTile == null || startTile.AvatarStripTransform == null)
            {
                Debug.LogWarning($"[DomainPicker] No suitable tile (or strip) found for player {p.Name} - chip not spawned.");
                return;
            }

            var chip = Instantiate(chipPrefab, startTile.AvatarStripTransform);
            Sprite sprite = dataService != null ? dataService.GetAvatarSprite(p.NetAvatarId.Value) : null;
            chip.Set(sprite, p.OwnerClientId == localId);
            _playerChips[p] = chip;

            // Hook for future domain changes - closure captures the player so we know
            // whose chip to move when this fires.
            NetworkVariable<Domains>.OnValueChangedDelegate handler =
                (_, newDomain) => HandlePlayerDomainChanged(p, newDomain);
            p.NetDomain.OnValueChanged += handler;
            _domainHandlers[p] = handler;
        }

        void HandlePlayerDomainChanged(Player p, Domains newDomain)
        {
            if (!_playerChips.TryGetValue(p, out var chip) || chip == null) return;
            var tile = FindTileForDomain(newDomain) ?? FindTileForDomain(Domains.Jade);
            if (tile == null || tile.AvatarStripTransform == null) return;
            chip.transform.SetParent(tile.AvatarStripTransform, worldPositionStays: false);
        }

        void DespawnAllChips()
        {
            foreach (var kv in _domainHandlers)
            {
                if (kv.Key != null && kv.Key.NetDomain != null)
                    kv.Key.NetDomain.OnValueChanged -= kv.Value;
            }
            _domainHandlers.Clear();

            foreach (var chip in _playerChips.Values)
                if (chip) Destroy(chip.gameObject);
            _playerChips.Clear();

            if (_watchingPlayerSpawnEvent && gameData != null
                && gameData.OnPlayerNetworkSpawnedUlong != null)
            {
                gameData.OnPlayerNetworkSpawnedUlong.OnRaised -= HandlePlayerSpawnedDuringModal;
            }
            _watchingPlayerSpawnEvent = false;
        }

        void HandlePlayerSpawnedDuringModal(ulong ownerClientId)
        {
            if (gameData == null) return;
            ulong localId = NetworkManager.Singleton ? NetworkManager.Singleton.LocalClientId : 0UL;
            var dataService = PlayerDataService.Instance;

            foreach (var ip in gameData.Players)
            {
                if (ip is Player p && p.OwnerClientId == ownerClientId && !p.NetIsAI.Value)
                {
                    SpawnChipForPlayer(p, localId, dataService);
                    break;
                }
            }
        }

        DomainInfoData FindTileForDomain(Domains d)
        {
            foreach (var item in domainInfoItems)
                if (item && item.Domain == d) return item;
            return null;
        }

        /// <summary>
        /// Destroys any DomainAvatarChip GameObjects already parented under tile strips
        /// (e.g. from a previous modal session, or hand-placed editor chips). Keeps
        /// the strip clean for our managed chip set.
        /// </summary>
        void ClearStaleChipsFromAllStrips()
        {
            foreach (var item in domainInfoItems)
            {
                if (!item || item.AvatarStripTransform == null) continue;
                for (int i = item.AvatarStripTransform.childCount - 1; i >= 0; i--)
                {
                    var child = item.AvatarStripTransform.GetChild(i);
                    if (child.TryGetComponent<DomainAvatarChip>(out _))
                        Destroy(child.gameObject);
                }
            }
        }

        /// <summary>
        /// Refreshes the selected-vs-unselected sprite and interactability on each tile.
        /// The Blue tile is always hidden (Random is gone; Jade is the unpicked default).
        /// Tiles outside <c>ActiveDomains[0..DC-1]</c> are dimmed and non-interactable.
        /// Does not touch chip lifecycle.
        /// </summary>
        void RefreshTileVisibility()
        {
            if (config == null) return;

            var selected = config.SelectedDomain;
            int dc = config.DomainCount;

            foreach (var item in domainInfoItems)
            {
                if (!item) continue;

                // Blue is the "no team" sentinel; never expose it in the modal.
                if (item.Domain == Domains.Blue)
                {
                    item.gameObject.SetActive(false);
                    continue;
                }

                item.gameObject.SetActive(true);
                bool active = GameDataSO.IsActiveDomain(item.Domain, dc);
                item.SetInteractable(active);
                item.SetSelected(item.Domain == selected);
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

        /// <summary>
        /// Screen 3 - maps the game's unlocked vessels onto the fixed card pool.
        /// Cards past the roster are hidden. Clicking a card selects that vessel
        /// and re-renders so the active/inactive icons track the selection.
        /// </summary>
        void RefreshVesselSelectionItems()
        {
            for (int i = 0; i < vesselSelectionItems.Count; i++)
            {
                var item = vesselSelectionItems[i];
                if (!item) continue;

                if (i < _availableShips.Count)
                {
                    int captured = i;
                    item.Configure(_availableShips[i], i == _currentShipIndex,
                        () => HandleVesselItemClicked(captured));
                }
                else
                {
                    item.Clear();
                }
            }
        }

        void HandleVesselItemClicked(int index)
        {
            if (index < 0 || index >= _availableShips.Count) return;

            _currentShipIndex = index;
            SetSelectedShipInternal(_availableShips[index]);
            RefreshVesselSelectionItems();
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

        // Screen 1 → Screen 2 - host commits PC + DC + intensity.
        //
        // This is the single commit point in the lava-lamp arcade flow. Before
        // this fires, clients are flying in freestyle and have no modal open.
        // After it fires, every client opens the modal at GameDetailView with
        // chips on Jade, tiles dimmed per DC, and back-navigation removed.
        //
        // Idempotent - repeated clicks (button mash, repeated input) short-circuit
        // at the _isConfigurationCommitted gate. The Confirm button is also
        // disabled visually for snappy feedback.
        public void OnConfirmConfiguration()
        {
            if (_isConfigurationCommitted) return;
            _isConfigurationCommitted = true;
            SetConfirmButtonInteractable(false);

            AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.Confirmed);

            if (!IsClientMode && arcadeConfigSyncManager && _selectedGame != null)
            {
                arcadeConfigSyncManager.CommitConfiguration(
                    (int)_selectedGame.Mode,
                    config.Intensity,
                    config.PlayerCount,
                    _selectedGame.MaxPlayersAllowed,
                    CurrentPartyHumanCount,
                    config.DomainCount);
            }

            // Local: spawn chips (after server reset to Jade), refresh tiles, open
            // Screen 2, hide the back button. SpawnChipsForAllPlayers is idempotent
            // - it calls DespawnAllChips first - so even if guard #1 is bypassed
            // somehow, no duplicate chips leak.
            ClearDpadRowHighlights();
            SpawnChipsForAllPlayers();
            RefreshTileVisibility();
            ShowGameDetailScreen();
            HideBackFromGameSelectButton();
        }

        // Screen 2 → Screen 1 (Back button) - DEPRECATED.
        //
        // The new commit-once flow has no Screen 2 → Screen 1 transition. This
        // method is retained as a no-op stub so prefab UnityEvent wiring doesn't
        // surface a missing-method warning. The button itself is hidden via
        // HideBackFromGameSelectButton() on Screen-2 entry.
        public void OnBackFromGameSelectView() { }

        void SetConfirmButtonInteractable(bool interactable)
        {
            if (confirmConfigurationButton)
                confirmConfigurationButton.interactable = interactable;
        }

        void HideBackFromGameSelectButton()
        {
            if (backFromGameSelectButton)
                backFromGameSelectButton.SetActive(false);
        }

        void ResetCommitGuard()
        {
            _isConfigurationCommitted = false;
            SetConfirmButtonInteractable(true);
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
        /// Modal close (back/cancel) - host notifies clients to close too.
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

            // Re-arm the modal-side commit guard so the next session's
            // OnConfirmConfiguration is allowed to fire.
            ResetCommitGuard();

            ModalWindowOut();
        }

        /// <summary>
        /// Start/Confirm button - called by ALL players (host and clients).
        /// Confirms the player's domain + vessel choices and enters the waiting state.
        /// When all human players have confirmed, the host auto-launches the game.
        /// </summary>
        public void OnStartGameClicked()
        {
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#FFD700>[FLOW-2] [ArcadeConfigModal] OnStartGameClicked (confirming ready)</color>");
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
                // No sync manager - single player or no multiplayer setup.
                // Launch directly.
                HandleAllPlayersReady();
            }
        }

        /// <summary>
        /// True if the local player is the launch authority - i.e. they sync the
        /// authoritative launch config into GameDataSO and their SceneLoader
        /// performs the actual scene load. Three cases hold launch authority:
        /// (a) no sync manager at all (legacy solo path),
        /// (b) sync manager exists but the local player is not in a multi-human
        ///     party session (PartyMembers <= 1, i.e. just self - presence-lobby
        ///     membership is irrelevant),
        /// (c) the local player is the host of an active multi-human party session.
        ///
        /// Non-host party clients return false: they skip the data sync but still
        /// raise InvokeGameLaunch locally so SceneLoader shows the loading splash
        /// and enters LoadingGame - its connected-client guard defers the actual
        /// scene load to the server's Netcode scene replication.
        /// </summary>
        internal static bool ShouldLocalPlayerLaunch(HostConnectionDataSO data, bool hasSyncManager)
        {
            if (!hasSyncManager) return true;
            if (data == null) return true;

            bool inActiveParty = data.PartyMembers != null && data.PartyMembers.Count > 1;
            if (!inActiveParty) return true;

            return data.IsPartyHost;
        }

        /// <summary>
        /// Called on ALL instances (host + clients) when every human player
        /// has pressed Start/Confirm. The host syncs launch config and loads the
        /// scene; clients show the loading splash and close their modal (they'll
        /// be pulled into the game scene via Netcode scene replication).
        /// </summary>
        void HandleAllPlayersReady()
        {
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#FFD700>[FLOW-2] [ArcadeConfigModal] All players ready!</color>");

            bool shouldLaunch = ShouldLocalPlayerLaunch(hostConnectionData, arcadeConfigSyncManager != null);

            if (shouldLaunch)
            {
                audioSystem.PlayMenuAudio(MenuAudioCategory.LetsGo);
                SyncAllGameDataForLaunch();
            }

            // Every instance raises the launch event. On the launch authority,
            // SceneLoader.LaunchGame loads the scene; on non-host party clients
            // it shows the loading splash, enters LoadingGame, and arms the
            // splash fade, while its connected-client guard defers the actual
            // scene load to the server's Netcode scene replication. Without
            // this, clients sit on the menu/modal with no transition visual
            // until the network scene load arrives.
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#FFD700>[FLOW-2] [ArcadeConfigModal] Calling gameData.InvokeGameLaunch() (launchAuthority={shouldLaunch})</color>");
            gameData.InvokeGameLaunch();

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
                Debug.LogError("<color=#FF0000>[FLOW-2] [ArcadeConfigModal] SyncAllGameDataForLaunch - gameData or config.SelectedGame is NULL!</color>");
                return;
            }

            var selectedGame = config.SelectedGame;
            gameData.SyncFromArcadeGame(selectedGame);

            int humanCount = CurrentPartyHumanCount;

            // Single source of truth - GameDataSO owns the player count computation
            gameData.ConfigurePlayerCounts(config.PlayerCount, humanCount);

            // Domain count - controls how many domains AI can be assigned to
            gameData.RequestedDomainCount = config.DomainCount;

            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#FFD700>[FLOW-2] [ArcadeConfigModal] SyncAllGameDataForLaunch - " +
                      $"Scene={selectedGame.SceneName}, Mode={selectedGame.Mode}, IsMultiplayer={selectedGame.IsMultiplayer}, " +
                      $"HumanCount={humanCount}, ConfigPlayerCount={config.PlayerCount}, " +
                      $"AIBackfill={gameData.RequestedAIBackfillCount}, " +
                      $"Vessel={gameData.selectedVesselClass.Value}, Intensity={gameData.SelectedIntensity.Value}</color>");

            // gameData.ActiveSession IS HCS.PartySession (single backing field
            // - see Docs/PartySystem/ARCHITECTURE.md Q4). No hand-off needed.
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
        /// NetDefaultVesselType NetworkVariable (owner-writable - legal from the
        /// owning client, unlike NetDomain). This ensures each client's vessel
        /// choice is propagated to the server independently of
        /// gameData.selectedVesselClass (which carries the host's choice).
        /// </summary>
        void SyncLocalPlayerVesselType(SO_Vessel ship)
        {
            var localPlayer = ResolveLocalOwnedPlayer();
            if (localPlayer == null)
            {
                Debug.LogError("[ArcadeConfigModal] Vessel selection DROPPED - no owned local Player " +
                               "resolved. NetDefaultVesselType not updated; spawn would use a stale class.");
                return;
            }

            localPlayer.NetDefaultVesselType.Value = ship ? ship.Class : VesselClassType.Dolphin;
        }

        #endregion

        #region Client-side RPC handlers

        /// <summary>
        /// Called on non-host clients when the host opens the arcade config modal.
        /// Opens the same modal in client mode with host-only controls disabled.
        /// </summary>
        void HandleConfigOpenedOnClient(int gameModeInt, int intensity, int playerCount, int maxPlayers, int domainCount)
        {
            Debug.Log($"[ArcadeConfigModal] HandleConfigOpenedOnClient - mode={gameModeInt}, intensity={intensity}, " +
                      $"players={playerCount}, max={maxPlayers}, domains={domainCount}");

            _isClientMode = true;

            // Re-arm the commit guard. Clients never invoke OnConfirmConfiguration
            // (the Confirm button lives on Screen 1, which clients never see), but
            // a player who was previously the party host might have a stale
            // _isConfigurationCommitted=true. Reset for hygiene.
            ResetCommitGuard();

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
            config.DomainCount  = Mathf.Clamp(domainCount, MinDomainsForGame, MaxSupportedDomains);
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

            // Clients skip Screen 1 entirely - modal opens straight at GameDetailView
            // with the back button hidden. Host has already committed PC + DC + intensity.
            ShowGameDetailScreen();
            HideBackFromGameSelectButton();

            // Same chip-spawn pattern as the host path so clients see live
            // chip movement when any player picks. Server has reset every human's
            // NetDomain to Jade as part of CommitConfiguration, so all chips spawn
            // on the Jade tile.
            SpawnChipsForAllPlayers();
            RefreshTileVisibility();
        }

        /// <summary>
        /// Called on non-host clients when the host closes the modal or starts a game.
        /// </summary>
        void HandleConfigClosedOnClient()
        {
            _isClientMode = false;
            DespawnAllChips();
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
        /// Disables host-only controls when in client mode.
        /// Intensity buttons and player count stepper become non-interactable.
        /// Domain selection, vessel selection, and the Start/Confirm button remain
        /// interactive for all players (host and clients).
        /// </summary>
        void ApplyHostOnlyInteractability()
        {
            bool isHost = !IsClientMode;

            // Intensity buttons - read-only for clients
            foreach (var button in intensityButtons)
            {
                if (!button) continue;
                var uiButton = button.GetComponent<Button>();
                if (uiButton) uiButton.interactable = isHost;
            }

            // Steppers - visible for all, but only host can change them
            if (pcStepper) pcStepper.SetInteractable(isHost);
            if (dcStepper) dcStepper.SetInteractable(isHost);
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

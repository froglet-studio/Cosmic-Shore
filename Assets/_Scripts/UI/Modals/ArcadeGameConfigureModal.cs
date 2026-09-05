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

namespace CosmicShore.UI
{
    public class ArcadeGameConfigureModal : ModalWindowManager
    {
        // TEMP for legacy systems (e.g. WeeklyChallengeSystem)
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

        [Header("Mode Preview")]
        [Tooltip("Which modes have a playable preview. Leave empty to load " +
                 "Resources/ModePreviewLibrary. A mode with no entry shows 'LEVEL PREVIEW NOT " +
                 "AVAILABLE' in the window - there is no video fallback any more.")]
        [SerializeField] private ModePreviewLibrarySO previewLibrary;

        [Tooltip("The preview window itself: an idle scale model of the mode's arena, and - once " +
                 "clicked - the live game playing in the same frame at the same size. Optional; " +
                 "without it the legacy video path still runs.")]
        [SerializeField] private ModePreviewWindow previewWindow;

        [Tooltip("Owns the windowed preview. Leave empty to find the one in the scene.")]
        [SerializeField] private ModePreviewSession previewSession;

        [Header("Maelstrom")]
        [Tooltip("Names the Maelstrom's own arcade card, so OpenMaelstrom can find it. Optional - " +
                 "without it the card is looked up in SO_GameList by mode.")]
        [SerializeField] private MaelstromDataSO tournamentData;

        [Tooltip("Fallback roster for that lookup. Optional.")]
        [SerializeField] private SO_GameList gameList;

        [Header("Launch Panels (the one-panel layout)")]
        [Tooltip("One panel per KIND of card - MinigameLaunchPanel for a mode with an arena of " +
                 "its own, MaelstromLaunchPanel for the meta-mode that draws other modes. The " +
                 "first whose Handles() accepts the card is used and the rest are hidden.\n\n" +
                 "Wiring ANY panel here switches the modal to the one-panel layout: the " +
                 "configure-then-pick-a-vessel pair of screens is skipped entirely and the " +
                 "config is committed the moment the card opens, because there is no longer a " +
                 "separate Confirm step for the host to press. Leave EMPTY to keep the legacy " +
                 "two-screen layout running off the Screen 1 / Screen 2 fields above.")]
        [SerializeField] private List<ArcadeLaunchPanel> launchPanels = new();

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
        // Every domain tile is always pickable now, so the default MATCH spreads across all
        // three - a default of 1 made GetBalancedDomain's active set [Jade] alone, which seated
        // every backfilled AI on one team (and the preview chips honestly showed it). A card
        // whose rules need fewer still clamps through ComputeMaxDomainCount.
        const int DefaultDomainCount = 3;

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
        bool _isClientMode;

        // The launch panel currently drawing the selected card, or null on the legacy layout.
        ArcadeLaunchPanel _activePanel;
        bool _activePanelWired;

        // A panel in its own window closes that window from inside CloseAndNotifyClients, and the
        // window then reports the close back here. Without this the two would call each other.
        bool _closing;

        // The local player's own ready state - known exactly here, unlike the replicated ready
        // COUNT, which says how many confirmed but not which (see LobbySlotRow).
        bool _localPlayerReady;
        int _readyCount;

        /// <summary>
        /// True when the one-panel layout is in use. Wiring any panel switches the modal over
        /// wholesale - there is no half-way state where some controls come from a panel and some
        /// from the legacy screens, because two sources for one control is how a stale widget ends
        /// up driving live config.
        /// </summary>
        bool UsesLaunchPanels => launchPanels != null && launchPanels.Count > 0;

        // Every control the modal drives resolves through ONE of these, and each answers from the
        // ACTIVE PANEL on the one-panel layout and from the legacy serialized field otherwise -
        // never a mix. A per-control fallback would look harmless and be the bug: the Maelstrom
        // panel deliberately has no preview window, so falling back would arm a live arena into a
        // leftover Screen-1 frame the player cannot see, and a panel that simply forgot to wire its
        // Start button would silently drive the legacy one instead of reporting the hole.
        static readonly IntensitySelectButton[] NoIntensityButtons = new IntensitySelectButton[0];
        static readonly DomainInfoData[] NoDomainTiles = new DomainInfoData[0];

        IReadOnlyList<IntensitySelectButton> ActiveIntensityButtons =>
            UsesLaunchPanels
                ? (_activePanel ? _activePanel.IntensityButtons : NoIntensityButtons)
                : intensityButtons;

        IReadOnlyList<DomainInfoData> ActiveDomainTiles =>
            UsesLaunchPanels
                ? (_activePanel ? _activePanel.DomainTiles : NoDomainTiles)
                : (IReadOnlyList<DomainInfoData>)domainInfoItems;

        Button ActiveStartButton =>
            UsesLaunchPanels ? (_activePanel ? _activePanel.StartButton : null) : startGameButton;

        GameObject ActiveWaitingLabel =>
            UsesLaunchPanels
                ? (_activePanel ? _activePanel.WaitingForOthersLabel : null)
                : waitingForOthersLabel;

        ModePreviewWindow ActivePreviewWindow =>
            UsesLaunchPanels ? (_activePanel ? _activePanel.PreviewWindow : null) : previewWindow;

        ModePreviewSession _resolvedPreviewSession;
        bool _previewSessionSubscribed;

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
            // On the one-panel layout the intensity row and the domain tiles live INSIDE whichever
            // panel the card selects, so they are wired when that panel becomes active (see
            // WireActivePanel) rather than here. Wiring both would double-subscribe every handler.
            if (!UsesLaunchPanels)
            {
                foreach (var intensityButton in intensityButtons)
                {
                    intensityButton.OnSelect += HandleIntensitySelected;
                    intensityButton.OnLockedSelect += HandleLockedIntensitySelected;
                }

                // Domain info buttons
                foreach (var item in domainInfoItems)
                {
                    if (!item || !item.Button) continue;
                    var captured = item.Domain;
                    item.Button.onClick.AddListener(() => HandleDomainSelected(captured));
                }
            }

            if (pcStepper)
                pcStepper.OnValueChanged += HandlePlayerCountSelected;

            if (dcStepper)
                dcStepper.OnValueChanged += HandleDomainCountChanged;

            if (configChangedEvent != null)
                configChangedEvent.OnRaised += HandleConfigChangedExternal;

            if (arcadeConfigSyncManager)
            {
                arcadeConfigSyncManager.OnConfigOpenedOnClient += HandleConfigOpenedOnClient;
                arcadeConfigSyncManager.OnConfigClosedOnClient += HandleConfigClosedOnClient;
                arcadeConfigSyncManager.OnScreenChangedOnClient += HandleScreenChangedOnClient;
                arcadeConfigSyncManager.OnIntensityChangedOnClient += HandleIntensityChangedOnClient;
                arcadeConfigSyncManager.OnRosterChangedOnClient += HandleRosterChangedOnClient;
                arcadeConfigSyncManager.OnAllPlayersReady += HandleAllPlayersReady;
                arcadeConfigSyncManager.OnPlayerReadyCountChanged += HandleReadyCountChanged;
                Debug.Log($"[ArcadeConfigModal] OnEnable - subscribed to ArcadeConfigSyncManager events (instance={GetInstanceID()})");

                // The lobby is replicated state, so a modal that subscribes AFTER the host's open
                // landed (re-enabled mid-lobby) asks for it back rather than waiting for the next
                // change. No-op on the host, when nothing is open, and on the panel-less
                // Maelstrom copy of this component (HandleConfigOpenedOnClient's own gate).
                if (UsesLaunchPanels && ArcadeConfigSyncManager.IsPartyClient)
                    arcadeConfigSyncManager.ReplayLobbyToSubscribers();
            }
            else
            {
                Debug.LogWarning($"[ArcadeConfigModal] OnEnable - arcadeConfigSyncManager is NULL, cannot subscribe (instance={GetInstanceID()})");
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            UnwireActivePanel();

            if (!UsesLaunchPanels)
            {
                foreach (var intensityButton in intensityButtons)
                {
                    intensityButton.OnSelect -= HandleIntensitySelected;
                    intensityButton.OnLockedSelect -= HandleLockedIntensitySelected;
                }

                foreach (var item in domainInfoItems)
                {
                    if (item && item.Button)
                        item.Button.onClick.RemoveAllListeners();
                }
            }

            if (pcStepper)
                pcStepper.OnValueChanged -= HandlePlayerCountSelected;

            if (dcStepper)
                dcStepper.OnValueChanged -= HandleDomainCountChanged;

            ShutDownPreview();

            if (configChangedEvent != null)
                configChangedEvent.OnRaised -= HandleConfigChangedExternal;

            if (arcadeConfigSyncManager)
            {
                arcadeConfigSyncManager.OnConfigOpenedOnClient -= HandleConfigOpenedOnClient;
                arcadeConfigSyncManager.OnConfigClosedOnClient -= HandleConfigClosedOnClient;
                arcadeConfigSyncManager.OnScreenChangedOnClient -= HandleScreenChangedOnClient;
                arcadeConfigSyncManager.OnIntensityChangedOnClient -= HandleIntensityChangedOnClient;
                arcadeConfigSyncManager.OnRosterChangedOnClient -= HandleRosterChangedOnClient;
                arcadeConfigSyncManager.OnAllPlayersReady -= HandleAllPlayersReady;
                arcadeConfigSyncManager.OnPlayerReadyCountChanged -= HandleReadyCountChanged;
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

            // While the preview window holds focus the pad belongs to the VESSEL - the d-pad
            // and A button must not silently drive the intensity rows behind the game the
            // player is flying. Same gate the base applies to its B-to-close.
            if (ModePreviewWindow.AnyHasFocus) return;

            // On the one-panel layout the panel IS the config surface; on the legacy one it is
            // Screen 1. Either way the d-pad only drives the rows the player can actually see.
            if (UsesLaunchPanels)
            {
                if (!_activePanel || !_activePanel.gameObject.activeInHierarchy) return;
            }
            else if (!configurationDetailView || !configurationDetailView.activeSelf) return;

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

            var row = ActiveIntensityButtons;

            int currentIdx = -1;
            for (int i = 0; i < row.Count; i++)
            {
                if (row[i] && row[i].Intensity == config.Intensity)
                {
                    currentIdx = i;
                    break;
                }
            }

            int nextIdx = currentIdx;
            while (true)
            {
                nextIdx += direction;
                if (nextIdx < 0 || nextIdx >= row.Count) return;
                var btn = row[nextIdx];
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
        /// Open the launch surface for a card - the single entry point a card tile should call.
        ///
        /// <para>It exists because a panel may live in its OWN window (the Maelstrom's), and which
        /// window opens has to be decided BEFORE anything is shown. Opening this modal first and
        /// letting the panel selection close it again would flash the wrong window for a frame,
        /// every time a player picks the Maelstrom.</para>
        /// </summary>
        public void OpenFor(SO_ArcadeGame selectedGame)
        {
            // A party CLIENT does not configure a match - only the host does. Their press is a
            // REQUEST to play this card, which lands as their avatar on it for the whole party
            // (and toggles off if they press the same card again).
            //
            // The gate lives here rather than at the callers because this is the one chokepoint
            // BOTH routes pass through: the arcade grid's cards and the Maelstrom's own button,
            // which is parameterless and would otherwise have kept opening the panel for a
            // client. Solo and offline players are the server under the EAGER-Relay design, so
            // they fall straight through.
            if (ArcadeConfigSyncManager.IsPartyClient)
            {
                // Local acknowledgement: the chip is the real confirmation but it is a server
                // round trip away, and on the long links this party system has to survive that
                // silence reads as a dead button.
                AudioSystem.Instance?.PlayMenuAudio(MenuAudioCategory.OptionClick);

                if (arcadeConfigSyncManager && hostConnectionData != null)
                    arcadeConfigSyncManager.RequestGamePick(
                        (int)selectedGame.Mode, hostConnectionData.LocalAvatarId);
                return;
            }

            // A panel with a window of its own opens it in SelectLaunchPanel; anything else lives
            // in this one.
            var panel = ResolvePanelFor(selectedGame);
            if (!panel || !panel.HostModal) ModalWindowIn();

            SetSelectedGame(selectedGame);
        }

        /// <summary>
        /// Open the launch surface for TODAY'S WEEKLY CHALLENGE.
        ///
        /// <para>Identical to <see cref="OpenFor"/> except that the challenge's terms are PINNED:
        /// intensity is the challenge's, and the player count is the card's own
        /// <see cref="SO_ArcadeGame.MinPlayersAllowed"/> (never below the humans actually in the
        /// party - the existing clamp still wins, because the party is a fact on the ground while
        /// the challenge's seat count is a preference). Everything else - vessel choice, domain
        /// tiles, the live preview, ready-up, the launch - is the ordinary arcade flow, which is
        /// the point: the weekly challenge is a mode you already know with one objective attached,
        /// not a second launch pipeline.</para>
        ///
        /// <para>The lock is cleared by the next ordinary <see cref="SetSelectedGame"/>, so a
        /// player who backs out and picks a normal card gets full control back.</para>
        /// </summary>
        public void OpenForWeeklyChallenge(SO_ArcadeGame selectedGame, WeeklyChallenge challenge)
        {
            if (!selectedGame) return;

            _pendingWeeklyChallenge = challenge.IsValid;
            _weeklyChallengeIntensity = Mathf.Clamp(
                challenge.Intensity, selectedGame.MinIntensity, selectedGame.MaxIntensity);
            _weeklyChallengeDomain = challenge.Domain == Domains.Blue ? Domains.Jade : challenge.Domain;
            _weeklyChallengeObjective = challenge.ObjectiveText;

            OpenFor(selectedGame);
        }

        /// <summary>
        /// Open the Maelstrom's launch panel. Parameterless so a Button's onClick can call it: the
        /// meta-mode is deliberately NOT one of the arcade grid's cards (it draws the others, so
        /// listing it beside them invites "play this one" when it means "play several of these"),
        /// which leaves it needing an entry point of its own.
        ///
        /// <para>The card is resolved from the tournament asset that names it, so there is nothing
        /// to keep in step with the roster.</para>
        /// </summary>
        public void OpenMaelstrom()
        {
            var card = tournamentData ? tournamentData.ModeCard : null;
            if (!card && gameList != null && gameList.Games != null)
            {
                foreach (var game in gameList.Games)
                    if (game && game.Mode == GameModes.Maelstrom) { card = game; break; }
            }

            if (!card)
            {
                Debug.LogError("[ArcadeConfigModal] OpenMaelstrom found no Maelstrom card - wire " +
                               "MaelstromData on the modal, or keep the card in SO_GameList.");
                return;
            }

            OpenFor(card);
        }

        /// <summary>
        /// Entry point from ArcadeExploreView when a game tile is selected (host path).
        /// </summary>
        public void SetSelectedGame(SO_ArcadeGame selectedGame)
        {
            _isClientMode = false;
            _selectedGame = selectedGame;

            // Consume the arm-flag from OpenForWeeklyChallenge. Consumed rather than read so a
            // later ordinary card selection cannot inherit the lock.
            _weeklyChallengeLocked = _pendingWeeklyChallenge;
            _pendingWeeklyChallenge = false;

            // Fresh modal session - re-arm the commit guard so OnConfirmConfiguration
            // can fire again. The Confirm button is re-enabled below in
            // ResetCommitGuard().
            ResetCommitGuard();

            config.ResetState();
            config.SelectedGame = selectedGame;

            _localPlayerReady = false;
            _readyCount = 0;

            // Before anything reads a control: the panel decides WHICH intensity row, domain tiles
            // and Start button the rest of this method is talking about.
            SelectLaunchPanel(selectedGame);

            BuildAvailableShips(selectedGame);
            InitializeConfigFromGameDefaults(selectedGame);
            // Compute the default domain count AFTER player count is set: the DC bound
            // depends on PC (DC <= PC) and ResetState() leaves PlayerCount at 0. For modes
            // with MinDomainsAllowed >= 2 (Joust) this defaults the stepper to 2, not 1.
            config.DomainCount = ComputeDefaultDomainCount();
            InitializeGameMetaView(selectedGame);
            ApplyWeeklyChallengePresentation();
            InitializeScreen1Controls(selectedGame);
            InitializeDefaultShipFromAvailable();
            InitializeDomainSelection();
            ApplyHostOnlyInteractability();
            ResetReadyUpUI();

            _dpadFocusRow = DpadRowIntensity;
            ClearDpadRowHighlights();

            if (UsesLaunchPanels)
            {
                // ONE panel means there is no separate Confirm step for the host to press, so the
                // config is committed here instead: that is the call that publishes the domain
                // count, resets every human to Jade, spawns the chips and opens the same panel on
                // the clients. Deferring it would leave the domain tiles inert on a panel that is
                // already showing them.
                CommitConfiguration(playSound: false);
                RefreshRoster();
            }
            else
            {
                // Legacy two-screen layout: the host configures privately on Screen 1 and no
                // client is involved until Confirm Configuration fires the commit RPC.
                ShowConfigurationScreen();
            }

            RaiseConfigChanged();
        }

        #endregion

        #region Launch panels (the one-panel layout)

        /// <summary>
        /// Bring up the panel that draws this card and take the others down.
        ///
        /// <para>Selection is by <see cref="ArcadeLaunchPanel.Handles"/>, first match wins - a card
        /// asks the panels which of them draws it rather than the modal switching on a mode enum,
        /// so a third kind of card is a new subclass and one list entry, with nothing here to
        /// edit.</para>
        /// </summary>
        /// <summary>
        /// Which panel draws this card, with NO side effects - so the routing question can be asked
        /// before anything is shown or hidden.
        /// </summary>
        ArcadeLaunchPanel ResolvePanelFor(SO_ArcadeGame game)
        {
            if (!UsesLaunchPanels) return null;

            foreach (var panel in launchPanels)
                if (panel && panel.Handles(game)) return panel;

            return null;
        }

        void SelectLaunchPanel(SO_ArcadeGame game)
        {
            if (!UsesLaunchPanels) return;

            var chosen = ResolvePanelFor(game);
            foreach (var panel in launchPanels)
                if (panel && panel != chosen) panel.Hide();

            if (chosen == null)
            {
                // Not a fault we can paper over: with no panel there is no intensity row, no
                // domain tiles and no Start button, so the modal would open blank. Say which card
                // and stop, rather than showing an empty frame the player cannot act on.
                CSDebug.LogWarning($"[ArcadeLaunch] No launch panel accepts " +
                                   $"'{(game ? game.DisplayName : "null")}' " +
                                   $"({(game ? game.Mode.ToString() : "-")}). The modal has nothing to draw.", this);
                UnwireActivePanel();
                _activePanel = null;
                return;
            }

            bool panelChanged = _activePanel != chosen;
            if (panelChanged)
            {
                UnwireActivePanel();
                _activePanel = chosen;
                WireActivePanel();
            }

            _activePanel.Show();

            // Each panel carries its OWN domain tiles, and every chip is parented under a tile
            // strip - so chips spawned while the other panel was active are stranded under
            // strips nobody can see. Re-home them the moment the panel changes; harmless when
            // no chips exist yet (SpawnChipsForAllPlayers despawns first and spawns from the
            // live player list).
            if (panelChanged && _playerChips.Count > 0)
            {
                SpawnChipsForAllPlayers();
                RefreshTileVisibility();
            }
        }

        void WireActivePanel()
        {
            if (!_activePanel || _activePanelWired) return;

            _activePanel.OnHostModalClosed += HandleHostModalClosed;

            foreach (var button in _activePanel.IntensityButtons)
            {
                if (!button) continue;
                button.OnSelect += HandleIntensitySelected;
                button.OnLockedSelect += HandleLockedIntensitySelected;
            }

            foreach (var tile in _activePanel.DomainTiles)
            {
                if (!tile || !tile.Button) continue;
                var captured = tile.Domain;
                tile.Button.onClick.AddListener(() => HandleDomainSelected(captured));
            }

            if (_activePanel.StartButton)
                _activePanel.StartButton.onClick.AddListener(OnStartGameClicked);

            if (_activePanel.LeaderboardButton)
                _activePanel.LeaderboardButton.onClick.AddListener(_activePanel.RequestLeaderboard);

            _activePanel.OnKickAIRequested += HandleKickAIRequested;
            _activePanel.OnAddAIModeChanged += HandleAddAIModeChanged;
            _activePanel.OnLeaderboardRequested += OpenWeeklyLeaderboard;

            _activePanelWired = true;
        }

        void UnwireActivePanel()
        {
            if (!_activePanel || !_activePanelWired) return;

            _activePanel.OnHostModalClosed -= HandleHostModalClosed;

            foreach (var button in _activePanel.IntensityButtons)
            {
                if (!button) continue;
                button.OnSelect -= HandleIntensitySelected;
                button.OnLockedSelect -= HandleLockedIntensitySelected;
            }

            foreach (var tile in _activePanel.DomainTiles)
            {
                if (tile && tile.Button) tile.Button.onClick.RemoveAllListeners();
            }

            if (_activePanel.StartButton)
                _activePanel.StartButton.onClick.RemoveListener(OnStartGameClicked);

            if (_activePanel.LeaderboardButton)
                _activePanel.LeaderboardButton.onClick.RemoveListener(_activePanel.RequestLeaderboard);

            _activePanel.OnKickAIRequested -= HandleKickAIRequested;
            _activePanel.OnAddAIModeChanged -= HandleAddAIModeChanged;
            _activePanel.OnLeaderboardRequested -= OpenWeeklyLeaderboard;

            _activePanelWired = false;
        }

        /// <summary>
        /// The ✕ on an AI seat. There is no AI object to remove yet - the bots are spawned in the
        /// game scene from <c>GameDataSO.RequestedAIDomains</c> (+ balanced top-up) - so kicking
        /// one is removing its entry from the placement list, which is both what the player means
        /// and the only representation that cannot go out of step with what actually spawns. The
        /// seat count then re-derives (never below the card's minimum: a kicked seat the match
        /// still needs simply turns EMPTY, to be topped up balanced at launch).
        /// </summary>
        void HandleKickAIRequested(int aiOrdinal)
        {
            if (IsClientMode || config == null || _selectedGame == null) return;
            if (aiOrdinal < 0 || aiOrdinal >= config.AIDomains.Count) return;

            config.AIDomains.RemoveAt(aiOrdinal);
            HandlePlayerCountSelected(BaseSeats + config.AIDomains.Count);
            BroadcastRosterToClients();
        }

        /// <summary>
        /// Seats the match holds BEFORE any hand-placed AI: the humans present, floored at the
        /// card's minimum. The floor matters - a min-2 card played solo already carries one
        /// balanced auto-AI in that base, and a placement must stack a NEW seat on top of it
        /// rather than replace it (the auto seat stays, at the balanced pick's domain, and it
        /// carries no ✕ because there is no placement to remove).
        /// </summary>
        int BaseSeats => _selectedGame
            ? Mathf.Max(_selectedGame.MinPlayersAllowed, CurrentPartyHumanCount)
            : CurrentPartyHumanCount;

        /// <summary>
        /// The Add AI toggle. While armed, tapping a domain tile PLACES an AI on that domain
        /// (see <see cref="HandleDomainSelected"/>) instead of picking the local player's own;
        /// toggling off ends placement. Host only - a client's toggle is hidden by the roster row
        /// and this guard makes the rule structural.
        /// </summary>
        void HandleAddAIModeChanged(bool armed)
        {
            if (IsClientMode || config == null || _selectedGame == null) return;

            // A weekly challenge seats the card's minimum, so there is no seat for a bot to take.
            // The control is hidden, but the event is public API on the panel - refuse rather than
            // trust that nothing can raise it.
            if (_weeklyChallengeLocked) { _addAiArmed = false; return; }

            _addAiArmed = armed;
            RefreshRoster();
        }

        /// <summary>Add AI placement mode (the repurposed fill toggle). Host-only, reset per card.</summary>
        bool _addAiArmed;

        /// <summary>
        /// Place one AI on <paramref name="domain"/> - the armed Add AI mode's answer to a domain
        /// tile tap. Capacity is the HOUSE match size (4 seats total, humans included) further
        /// clamped by the card; a full house refuses quietly (the roster already shows every seat
        /// taken).
        /// </summary>
        void AddAiToDomain(Domains domain)
        {
            if (IsClientMode || config == null || _selectedGame == null) return;

            // Placements stack ON TOP of the base seats (humans, floored at the card's minimum) -
            // a min-2 card played solo keeps its balanced auto-AI and a tap adds the THIRD seat.
            int ceiling = Mathf.Min(Mathf.Min(_selectedGame.MaxPlayersAllowed, MaxSupportedPlayers),
                                    MaxMatchSeats);
            if (BaseSeats + config.AIDomains.Count >= ceiling)
            {
                CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                    $"[ArcadeLaunch] Add AI refused - {BaseSeats} base seats + " +
                    $"{config.AIDomains.Count} placed AI already fill the {ceiling} seats.");
                return;
            }

            config.AIDomains.Add(domain);

            // A placement is the host declaring that domain IS in the match, so the domain count
            // rises to cover it where the DC<=PC clamp allows (HandlePlayerCountSelected re-clamps
            // against the grown seat count). The spawner and the tile chips honour an explicit
            // placement even when the prefix cannot stretch that far.
            config.DomainCount = Mathf.Max(config.DomainCount, DomainPrefixCount(config.AIDomains));

            HandlePlayerCountSelected(BaseSeats + config.AIDomains.Count);
            BroadcastRosterToClients();
        }

        /// <summary>
        /// Push the host's live roster shape to every client, so their chips redraw in real time
        /// instead of freezing at what the open RPC carried. Host-only; a no-op with no sync
        /// manager (offline / single player - there is nobody to tell).
        /// </summary>
        void BroadcastRosterToClients()
        {
            if (IsClientMode || config == null || arcadeConfigSyncManager == null) return;

            var placed = new int[config.AIDomains.Count];
            for (int i = 0; i < placed.Length; i++) placed[i] = (int)config.AIDomains[i];
            arcadeConfigSyncManager.NotifyRosterChanged(config.PlayerCount, config.DomainCount, placed);
        }

        /// <summary>
        /// The host reshaped the roster (placed or kicked an AI). Mirror it: counts, domain
        /// count, and the placement list land in this client's config, and the chips redraw -
        /// placed bots on their exact tiles (no ✕ here: kicking is the host's), the balanced
        /// remainder recomputed over the same replicated player data the host used.
        /// </summary>
        void HandleRosterChangedOnClient(int playerCount, int domainCount, int[] placedAiDomains)
        {
            // Same two-instance gate as HandleConfigOpenedOnClient.
            if (!UsesLaunchPanels) return;
            if (!IsClientMode || config == null) return;

            config.PlayerCount = Mathf.Max(1, playerCount);
            config.DomainCount = Mathf.Clamp(domainCount, MinDomainsForGame, MaxSupportedDomains);

            config.AIDomains.Clear();
            if (placedAiDomains != null)
                foreach (var d in placedAiDomains)
                    config.AIDomains.Add((Domains)d);

            RefreshTileVisibility();
            RefreshRoster();
        }

        /// <summary>Total seats a match holds, humans included - the house party size. The old
        /// fill toggle used the same four; a lobby of twelve bots is not what Add AI means.</summary>
        const int MaxMatchSeats = 4;

        /// <summary>How many of the Jade→Ruby→Gold prefix the placed list needs (a Gold placement
        /// needs all three). Blue never occurs here - the tiles only offer real domains.</summary>
        static int DomainPrefixCount(List<Domains> placed)
        {
            int needed = 1;
            foreach (var d in placed)
                needed = Mathf.Max(needed, d == Domains.Gold ? 3 : d == Domains.Ruby ? 2 : 1);
            return needed;
        }

        /// <summary>Redraw the roster from the live config. Cheap; call it whenever either moves.</summary>
        void RefreshRoster()
        {
            if (!_activePanel || config == null) return;

            int humans = CurrentPartyHumanCount;
            int total = Mathf.Max(humans, config.PlayerCount);

            // Clients pass their (empty) placement list: placed AI are host-side state, so a
            // client's roster shows the synced seat count with the AI seats drawn EMPTY - honest,
            // since the client cannot see who the host placed where.
            _activePanel.RefreshRoster(gameData, total, humans, config.AIDomains,
                                       _readyCount, _localPlayerReady, !IsClientMode,
                                       _addAiArmed && !IsClientMode && !_weeklyChallengeLocked);

            RefreshAIPreviewChips(total - humans);
        }

        /// <summary>
        /// A panel's own window was closed by its own controls - its X, or gamepad B. That window
        /// animating out is not the same event as the SESSION ending: the clients still have the
        /// modal open, and a satellite arena is still standing. Route it through the real close.
        /// </summary>
        void HandleHostModalClosed()
        {
            if (_closing) return;      // this close IS the one we started
            CloseAndNotifyClients();
        }

        void HandleReadyCountChanged(int readyCount, int totalExpected)
        {
            _readyCount = readyCount;
            RefreshRoster();
        }

        #endregion

        #region Mode preview (diorama + Test Flight)

        /// <summary>
        /// The preview definition for <paramref name="mode"/>, or null when the mode has none.
        /// Null is the ordinary answer: the arcade lists 42 games while only ~15 have a scene,
        /// and Maelstrom is excluded on principle (it draws OTHER modes, so it has no arena of
        /// its own to shrink).
        /// </summary>
        ModePreviewDefinitionSO ResolvePreviewDefinition(GameModes mode)
        {
            if (!previewLibrary)
                previewLibrary = Resources.Load<ModePreviewLibrarySO>(ModePreviewLibrarySO.ResourcePath);

            return previewLibrary ? previewLibrary.Resolve(mode) : null;
        }

        /// <summary>
        /// Arm the session for this card: bind it to the window, hand it the definition, and give
        /// it the hull the mode locks to. The window is the affordance - there is no separate
        /// button, because the thing you click to play is the thing the game plays in.
        /// </summary>
        void ArmPreviewForGame(SO_ArcadeGame game, ModePreviewDefinitionSO definition)
        {
            var window = ActivePreviewWindow;

            // A panel with no preview window is a designed state, not a fault: Maelstrom draws
            // OTHER modes, so it has no arena of its own to stand up and shows a clip instead.
            if (!window)
            {
                var idle = previewSession ? previewSession : _resolvedPreviewSession;
                if (idle) { idle.Stop(); idle.Detach(); }
                return;
            }

            var session = PreviewSession;
            if (!session)
            {
                // No session in the scene: the window still owes the player an answer, and the
                // honest one is the label - never a stale image or an empty frame.
                window.ShowUnavailable();
                return;
            }

            session.Attach(window);
            session.SetDefinition(definition && definition.CanTestFlight ? definition : null,
                                  ResolveModeVessel(game),
                                  config != null ? config.Intensity : 1,
                                  sparringPartner: game && game.MinPlayersAllowed >= 2);

            if (_activePanel is MinigameLaunchPanel minigamePanel)
            {
                // A weekly challenge shows no objective box at all - its ask is the briefing. Said
                // again here because the definition's line would put the box back every time the
                // preview re-arms, which an intensity change alone does.
                if (_weeklyChallengeLocked)
                    minigamePanel.HideObjective();
                else if (definition)
                    minigamePanel.BindObjective(definition.ObjectiveMetric, definition.ObjectiveText);
            }
        }

        /// <summary>
        /// Stop anything running in the window and let go of it. Reached from exactly TWO routes -
        /// the ✕ (<see cref="CloseAndNotifyClients"/>) and the modal being disabled.
        ///
        /// <para>This comment used to claim a third, "a launch", and that route does not exist:
        /// <see cref="HandleAllPlayersReady"/> closes through <c>ModalWindowOut</c>, which fades a
        /// CanvasGroup and never deactivates the GameObject, so <c>OnDisable</c> never fires. The
        /// window is taken down on that route by <see cref="HandlePreviewEnded"/> instead, off the
        /// session's own end event - which is the better place for it anyway, since it holds on
        /// every stop rather than on the ones the modal happens to hear about.</para>
        /// </summary>
        void ShutDownPreview()
        {
            UnsubscribeFromPreviewSession();

            var session = previewSession ? previewSession : _resolvedPreviewSession;
            if (session)
            {
                session.Stop();
                session.Detach();
            }

            var window = ActivePreviewWindow;
            if (window) window.Hide();
        }

        /// <summary>The scene's preview session, resolved once and cached.</summary>
        ModePreviewSession PreviewSession
        {
            get
            {
                if (previewSession)
                {
                    SubscribeToPreviewSession(previewSession);
                    return previewSession;
                }
                if (_resolvedPreviewSession) return _resolvedPreviewSession;

                // One lookup per modal lifetime, not per frame - this is the sanctioned shape
                // for a scene-singleton the inspector forgot, not a hot path.
                _resolvedPreviewSession = FindFirstObjectByType<ModePreviewSession>(FindObjectsInactive.Exclude);
                if (_resolvedPreviewSession) SubscribeToPreviewSession(_resolvedPreviewSession);
                return _resolvedPreviewSession;
            }
        }

        void SubscribeToPreviewSession(ModePreviewSession session)
        {
            if (!session || _previewSessionSubscribed) return;

            session.OnPreviewEnded += HandlePreviewEnded;
            session.OnObjectiveProgress += HandleObjectiveProgress;
            _previewSessionSubscribed = true;
        }

        void UnsubscribeFromPreviewSession()
        {
            if (!_previewSessionSubscribed) return;

            var session = previewSession ? previewSession : _resolvedPreviewSession;
            if (session)
            {
                session.OnPreviewEnded -= HandlePreviewEnded;
                session.OnObjectiveProgress -= HandleObjectiveProgress;
            }
            _previewSessionSubscribed = false;
        }

        /// <summary>
        /// The hull a mode locks to, or <see cref="VesselClassType.Any"/> when it allows several
        /// (in which case the preview keeps whatever the player is already flying). Four of the
        /// live modes are single-vessel, and this is the list they already declare.
        /// </summary>
        static VesselClassType ResolveModeVessel(SO_ArcadeGame game)
        {
            if (game == null || game.Vessels == null || game.Vessels.Count != 1 || !game.Vessels[0])
                return VesselClassType.Any;

            return game.Vessels[0].Class;
        }

        /// <summary>
        /// A preview stopped - by the player clicking away, by the card changing, by entering
        /// freestyle, or by a LAUNCH. Whatever ended it, the arena is struck and the camera loan
        /// is returned, so <b>the window has nothing left to draw</b> and must come down here.
        ///
        /// <para>This method used to be empty, on the reasoning that "the modal never went
        /// anywhere, so there is nothing to restore". That answers the wrong question: the modal
        /// is not what was showing the arena - the RawImage is, and it stays enabled over a
        /// RenderTexture nothing renders into any more. The visible result is the launch panel
        /// sitting over live gameplay with a frozen picture of an arena that no longer exists.</para>
        ///
        /// <para>Taking it down HERE rather than at each stop site is the point: the window's
        /// visibility now follows the SESSION's lifetime, so it cannot depend on modal-close
        /// choreography. That mattered because <see cref="ShutDownPreview"/> - the only other
        /// caller of <c>Hide</c> - is reached from just two routes (the modal being disabled, and
        /// the ✕), and <b>neither fires on a launch</b>: <see cref="HandleAllPlayersReady"/> closes
        /// through <c>ModalWindowOut</c>, which fades a CanvasGroup and never deactivates the
        /// GameObject, so <c>OnDisable</c> never runs.</para>
        ///
        /// <para>Ordering is safe on every route, because a stop that is followed by a new state
        /// already re-asserts it AFTER the stop: a card change calls <c>ShowLoading</c> next, and
        /// a failed stand calls <c>ShowUnavailable</c> next (deliberately last, for exactly this
        /// reason). Hiding an already-hidden window is a no-op.</para>
        /// </summary>
        void HandlePreviewEnded(GameModes mode, ModePreviewOutcome outcome)
        {
            var window = ActivePreviewWindow;
            if (window) window.Hide();
        }

        // One beat, two views: the objective box pulses its counter and the micro toast pops a
        // "+N", off the same session event - which is what makes the pair teach. The flash wears
        // the LOCAL PLAYER'S DOMAIN colour, resolved live at each event (never snapshotted, per
        // the domain-sync rule) - change domain on the roster and the very next tick flashes the
        // new colour.
        void HandleObjectiveProgress(int delta, int total)
        {
            // A weekly challenge's box states the CHALLENGE, and the preview is the AI playing the
            // mode - letting its score tick that counter would show the player progress they have
            // not made, against an objective they have not started.
            if (_weeklyChallengeLocked) return;

            (_activePanel as MinigameLaunchPanel)?.NotifyObjectiveProgress(delta, total,
                                                                           ResolveDomainFlash());
        }

        /// <summary>
        /// The local player's live domain signal colour (<see cref="SO_ColorSet
        /// .GetDomainSignalColor"/> - the accessor every domain-tinted UI reads), or null to keep
        /// the authored colours when the theme is not resolvable.
        /// </summary>
        Color? ResolveDomainFlash()
        {
            var colorSet = gameData && gameData.ThemeManagerData ? gameData.ThemeManagerData.ColorSet : null;
            if (colorSet == null) return null;
            var domain = gameData.LocalPlayer != null ? gameData.LocalPlayer.Domain
                                                      : CosmicShore.Data.Domains.Blue;
            return colorSet.GetDomainSignalColor(domain);
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

        // ── Weekly challenge lock ───────────────────────────────────────────────
        //
        // Set by OpenForWeeklyChallenge and consumed by the very next SetSelectedGame, which then
        // latches _weeklyChallengeLocked for the life of that modal session. Two flags rather than
        // one because OpenFor -> SetSelectedGame is the only ordering that can distinguish "this
        // card was opened AS the weekly challenge" from "this card happens to be the weekly
        // challenge's mode and the player picked it off the grid" - the second must stay fully
        // configurable.
        bool _pendingWeeklyChallenge;
        int _weeklyChallengeIntensity = 1;
        Domains _weeklyChallengeDomain = Domains.Jade;
        string _weeklyChallengeObjective = "";
        bool _weeklyChallengeLocked;

        [SerializeField, Tooltip("The weekly challenge's leaderboard window, opened by the " +
                                 "leaderboard button on a challenge card. Left empty it is found " +
                                 "in the scene on first use.")]
        WeeklyChallengeLeaderboardModal leaderboardModal;

        /// <summary>True while this modal session is configuring the weekly challenge.</summary>
        public bool IsWeeklyChallengeSession => _weeklyChallengeLocked;

        void InitializeConfigFromGameDefaults(SO_ArcadeGame game)
        {
            // Clamp default intensity to what the player has actually unlocked
            var progressionService = GameModeProgressionService.Instance;
            int maxUnlocked = progressionService != null
                ? progressionService.GetMaxUnlockedIntensity(game.Mode)
                : game.MaxIntensity;

            config.Intensity   = _weeklyChallengeLocked
                // The challenge's intensity is the same ask for every player, so it is NOT
                // clamped to what this player has unlocked - the weekly challenge is a curated
                // invitation into a mode, and an unlock gate would make two players in the same
                // week face different objectives.
                ? Mathf.Clamp(_weeklyChallengeIntensity, game.MinIntensity, game.MaxIntensity)
                : Mathf.Clamp(game.MinIntensity, game.MinIntensity, maxUnlocked);

            // Humans only: the card opens with no AI placed (by design call, 2026-08-27) - the
            // host seats every bot by hand through Add AI. Seats the card's MINIMUM still owes
            // beyond the humans draw EMPTY and are topped up domain-balanced at launch, so an
            // un-configured lobby still starts legally.
            config.AIDomains.Clear();
            _addAiArmed = false;
            // The weekly challenge seats the card's MINIMUM - it is a personal objective, and every
            // extra seat is one more pilot competing for the same crystals. The party's humans
            // still win the clamp below (a fact on the ground beats a preference).
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

            if (_activePanel)
            {
                _activePanel.Bind(game, config != null ? config.Intensity : game.MinIntensity);
                _activePanel.RefreshFavorite(FavoriteSystem.IsFavorited(game.Mode));
            }

            // The session drives the window through its three states - 'LEVEL PREVIEW NOT
            // AVAILABLE' (no definition, or the build failed), 'LOADING' (the arena standing
            // up), and live (the mode already playing under AI until the player taps in).
            // Every card lands in exactly one of those; the video path is gone, so nothing
            // stale, white, or leaked-through can ever draw in the frame.
            ArmPreviewForGame(game, ResolvePreviewDefinition(game.Mode));
        }

        void InitializeScreen1Controls(SO_ArcadeGame game)
        {
            var progressionService = GameModeProgressionService.Instance;

            var intensityRow = ActiveIntensityButtons;
            for (int i = 0; i < intensityRow.Count; i++)
            {
                var button = intensityRow[i];
                if (!button) continue;

                int level = i + 1;
                button.SetIntensityLevel(level);

                bool active = level >= game.MinIntensity && level <= game.MaxIntensity;

                // Weekly challenge: only the challenge's own intensity is offered. Deactivating the
                // rest rather than disabling the row keeps the pinned number readable as a choice
                // that has already been made.
                if (_weeklyChallengeLocked)
                    active = level == config.Intensity;

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

            // Pinned for the weekly challenge: min == max, so the stepper renders with both arrows
            // disabled by its own bounds logic and there is no second code path to keep in step.
            if (_weeklyChallengeLocked)
                pcMax = effectiveMin;

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
            if (_weeklyChallengeLocked) return; // The challenge's terms are not negotiable

            intensity        = Mathf.Clamp(intensity, _selectedGame.MinIntensity, _selectedGame.MaxIntensity);
            bool changed     = config.Intensity != intensity;
            config.Intensity = intensity;

            foreach (var button in ActiveIntensityButtons)
            {
                if (!button) continue;
                button.SetSelected(button.Intensity == intensity);
            }

            if (_activePanel) _activePanel.HandleIntensityChanged(intensity);

            // The preview IS the mode's real cell at this intensity, so a changed number has to
            // rebuild the arena - leaving the old world under the new label would be the stale
            // frame the whole preview design exists to make impossible.
            if (changed) ArmPreviewForGame(_selectedGame, ResolvePreviewDefinition(_selectedGame.Mode));

            // Clients follow: their modal shows the host's number and their own LOCAL preview
            // rebuilds to it (kicking them out of flight when the arena differs - the session's
            // SetDefinition unwinds an active flight before standing the new world).
            if (changed && arcadeConfigSyncManager)
                arcadeConfigSyncManager.NotifyIntensityChanged(intensity);

            SyncGameDataConfig();
            RaiseConfigChanged();
        }

        /// <summary>
        /// The host moved the intensity row while this client's lobby is open. Mirror the host's
        /// own handler minus authority: clamp, reflect on the row, and re-arm the LOCAL preview -
        /// the session rebuilds only when the arena actually differs, and a client who is IN the
        /// microgame at the old intensity is unwound out of it first (SetDefinition stops an
        /// active flight), exactly the "host changed it, I leave and can tap back in" flow.
        /// </summary>
        void HandleIntensityChangedOnClient(int intensity)
        {
            if (!IsClientMode || _selectedGame == null || config == null) return;

            intensity        = Mathf.Clamp(intensity, _selectedGame.MinIntensity, _selectedGame.MaxIntensity);
            bool changed     = config.Intensity != intensity;
            config.Intensity = intensity;

            foreach (var button in ActiveIntensityButtons)
            {
                if (!button) continue;
                button.SetSelected(button.Intensity == intensity);
            }

            if (_activePanel) _activePanel.HandleIntensityChanged(intensity);

            if (changed) ArmPreviewForGame(_selectedGame, ResolvePreviewDefinition(_selectedGame.Mode));
        }

        void HandlePlayerCountSelected(int playerCount)
        {
            if (_selectedGame == null || config == null) return;
            if (IsClientMode) return;

            int effectiveMin = Mathf.Max(_selectedGame.MinPlayersAllowed, CurrentPartyHumanCount);
            int pcMax = Mathf.Min(_selectedGame.MaxPlayersAllowed, MaxSupportedPlayers);
            // A party can be LARGER than the card allows (four humans on a 3-player card), and
            // then effectiveMin > pcMax. Mathf.Clamp resolves an inverted range by returning the
            // MAX, so the count silently came back as fewer players than are actually present -
            // and SelectedPlayerCount is what sizes the spawn ring, so two pilots spawned on the
            // same pose, interpenetrating. The party is the fact on the ground; the card's
            // ceiling is a preference, so the party wins and the stepper is simply pinned.
            playerCount        = effectiveMin > pcMax
                ? effectiveMin
                : Mathf.Clamp(playerCount, effectiveMin, pcMax);
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
            RefreshRoster();
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
            // Jade is the ordinary default AND what MenuServerPlayerVesselInitializer resets every
            // player to on spawn, so it is the one value that is already true without asking.
            var domain = _weeklyChallengeLocked ? _weeklyChallengeDomain : Domains.Jade;

            if (config != null)
                config.SelectedDomain = domain;

            // A pinned domain other than Jade has to be REQUESTED, not just shown selected: the
            // tiles reflect Player.NetDomain, and highlighting one without the server round trip
            // is exactly the "UI claims a domain the server never got" case HandleDomainSelected
            // refuses to create.
            if (_weeklyChallengeLocked && domain != Domains.Jade)
            {
                var player = ResolveLocalOwnedPlayer();
                if (player != null) player.RequestSetDomain_ServerRpc(domain);
                else
                    Debug.LogError($"[ArcadeConfigModal] Weekly challenge domain '{domain}' DROPPED " +
                                   "- no owned local Player resolved. The run would be flown on " +
                                   "whatever domain the player already had.");
            }

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
            // The challenge's terms are not negotiable. The tiles are already non-interactable, so
            // this only catches a tap that arrives some other way (a gamepad Submit landing on a
            // dimmed tile, a stray inspector onClick).
            if (_weeklyChallengeLocked) return;

            // Armed Add AI mode captures the tap: the tile names WHERE the bot goes, not where
            // the local player goes. Host-only by construction (_addAiArmed can only arm on the
            // host), and the mode stays armed so several taps place several bots - the toggle
            // is the off switch.
            if (_addAiArmed && !IsClientMode)
            {
                AddAiToDomain(domain);
                return;
            }

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

            // ClearStaleChipsFromAllStrips above removes EVERY chip under a strip, AI included -
            // it cannot tell one apart from a hand-placed leftover, and should not have to. So the
            // AI chips are rebuilt here rather than left to whichever call happened to run last.
            if (config != null)
                RefreshAIPreviewChips(Mathf.Max(0, config.PlayerCount - CurrentPartyHumanCount));
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

        readonly List<DomainAvatarChip> _aiChips = new();

        // What the AI chips currently show, so a RefreshRoster that changes nothing does not
        // destroy and re-roll them - chips that reshuffle their avatars on every refresh read
        // as the roster changing when it has not. Cleared with the chips.
        string _aiChipSignature;

        /// <summary>
        /// Show the AI the match will seat, as chips under the domain each will actually join.
        ///
        /// <para>The bots do not exist yet - <c>ServerPlayerVesselInitializerWithAI</c> spawns them
        /// in the game scene from <c>GameDataSO.RequestedAIBackfillCount</c> - so these are a
        /// PREVIEW of a roster, not a view of one. That is exactly why the placement runs the
        /// spawner's own <c>GetBalancedDomain</c> over the same counts it will use: a preview that
        /// distributed them its own way would be a promise the match then breaks.</para>
        ///
        /// <para>The avatar is random per chip because a bot has no profile to read one from, and
        /// four seats showing icon 0 read as one player repeated.</para>
        /// </summary>
        void RefreshAIPreviewChips(int aiCount)
        {
            if (chipPrefab == null || gameData == null) return;

            // The LIVE domain count off the config, never gameData.RequestedDomainCount - that
            // is only written at commit, so reading it here placed every chip against a stale
            // count (its default of 1 made the active set [Jade] alone: every AI under one tile).
            int domainCount = config != null ? config.DomainCount : DefaultDomainCount;
            var activeDomains = ServerPlayerVesselInitializerWithAI.BuildActiveDomains(domainCount);
            if (activeDomains == null || activeDomains.Count == 0) return;

            var humans = new List<Player>();
            foreach (var ip in gameData.Players)
                if (ip is Player p && !p.NetIsAI.Value) humans.Add(p);

            var humanCounts = GameDataSO.BuildHumanCounts(humans, activeDomains);

            // Rebuild only when what the chips SAY changes. A refresh that changes nothing must
            // not destroy and re-roll them - reshuffling avatars on every ready-count tick reads
            // as the roster changing when it has not.
            var signature = new System.Text.StringBuilder();
            signature.Append(aiCount).Append('/').Append(domainCount);
            foreach (var d in activeDomains)
                signature.Append('/').Append(d).Append(':')
                         .Append(humanCounts.TryGetValue(d, out var hc) ? hc : 0);
            if (config != null)
                foreach (var placed in config.AIDomains)
                    signature.Append('#').Append(placed);
            string sig = signature.ToString();
            if (sig == _aiChipSignature && _aiChips.Count == Mathf.Max(0, aiCount)) return;
            _aiChipSignature = sig;

            foreach (var chip in _aiChips)
                if (chip) Destroy(chip.gameObject);
            _aiChips.Clear();

            if (aiCount <= 0) return;

            var totalCounts = new Dictionary<Domains, int>(humanCounts);
            var dataService = PlayerDataService.Instance;

            for (int i = 0; i < aiCount; i++)
            {
                // A PLACED bot sits exactly where the host put it - ALWAYS, even when the domain
                // sits past the current DomainCount prefix (placing on Gold in a two-domain lobby
                // is the host widening the match, and the spawner honours it the same way). Only
                // the top-up seats past the placement list draw balanced - and a placed domain is
                // never written into a count dict that lacks its key, because GetBalancedDomain
                // requires a domain in BOTH dicts and a half-known key starves its tie-break.
                bool placedChip = config != null && i < config.AIDomains.Count &&
                                  config.AIDomains[i] != Domains.Blue;
                Domains domain;
                if (placedChip)
                {
                    domain = config.AIDomains[i];
                    if (totalCounts.ContainsKey(domain)) totalCounts[domain]++;
                }
                else
                {
                    domain = ServerPlayerVesselInitializerWithAI.GetBalancedDomain(totalCounts, humanCounts);
                    totalCounts[domain] = totalCounts.TryGetValue(domain, out var t) ? t + 1 : 1;
                }

                var tile = FindTileForDomain(domain);
                if (tile == null || tile.AvatarStripTransform == null) continue;

                var seat = Instantiate(chipPrefab, tile.AvatarStripTransform);
                seat.Set(dataService != null ? dataService.GetRandomAvatarSprite() : null, false);

                // The ✕ lives ON the chip - this strip IS the roster the player looks at. Only a
                // placed bot is kickable (a balanced top-up seat has no placement to remove), and
                // only for the host. The ordinal names the placement, not the seat.
                int ordinal = i;
                seat.SetKickable(placedChip && !IsClientMode,
                                 () => HandleKickAIRequested(ordinal));
                _aiChips.Add(seat);
            }
        }

        void HandlePlayerDomainChanged(Player p, Domains newDomain)
        {
            if (!_playerChips.TryGetValue(p, out var chip) || chip == null)
            {
                // The chip is GONE - destroyed by a modal close, a panel switch, or a strip
                // sweep - while the NetDomain subscription survived. Returning here is how a
                // player's second domain pick "vanished" their avatar: the event that should
                // move the chip is the one moment we know a chip is missing, so respawn it
                // (SpawnChipForPlayer parents it under the player's current-domain tile).
                _playerChips.Remove(p);
                SpawnChipForPlayer(p,
                    NetworkManager.Singleton ? NetworkManager.Singleton.LocalClientId : 0UL,
                    PlayerDataService.Instance);
                return;
            }

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

            foreach (var chip in _aiChips)
                if (chip) Destroy(chip.gameObject);
            _aiChips.Clear();
            _aiChipSignature = null;

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
            foreach (var item in ActiveDomainTiles)
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
            foreach (var item in ActiveDomainTiles)
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

            foreach (var item in ActiveDomainTiles)
            {
                if (!item) continue;

                // Blue is the "no team" sentinel; never expose it in the modal.
                if (item.Domain == Domains.Blue)
                {
                    item.gameObject.SetActive(false);
                    continue;
                }

                item.gameObject.SetActive(true);

                // EVERY domain is pickable, always. The domain count is a property of how the
                // MATCH is scored, not a gate on which colour a player may fly: dimming Gold
                // because this card was configured for two domains reads as "Gold is locked",
                // which is a progression claim the game does not make anywhere else.
                //
                // The ONE exception is a weekly challenge, where the domain is part of the fixed
                // ask like the intensity - and there the tiles are dimmed precisely BECAUSE the
                // choice has already been made, which is the honest reading.
                item.SetInteractable(!_weeklyChallengeLocked);
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
        public void OnConfirmConfiguration() => CommitConfiguration(playSound: true);

        /// <summary>
        /// The commit itself. <paramref name="playSound"/> is false on the one-panel layout's
        /// automatic commit: the sting acknowledges a BUTTON PRESS, and there is no press there -
        /// the player opened a card, and a confirmation sound for that reads as having agreed to
        /// something.
        /// </summary>
        void CommitConfiguration(bool playSound)
        {
            if (_isConfigurationCommitted) return;
            _isConfigurationCommitted = true;
            SetConfirmButtonInteractable(false);

            if (playSound)
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

            // The one-panel layout has nowhere to go: the panel showing the domain tiles is
            // already up, and the chips just spawned into it.
            if (!UsesLaunchPanels)
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
            if (_closing) return;
            _closing = true;

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

            _localPlayerReady = false;
            _readyCount = 0;

            // A satellite arena is the expensive half of the preview - it must never outlive the
            // window somebody was looking at it through.
            ShutDownPreview();

            // Hide() closes the panel's OWN window when it has one; ModalWindowOut below is a
            // no-op on a modal that was never opened, so both routes end with everything shut.
            if (_activePanel) _activePanel.Hide();

            ModalWindowOut();
            _closing = false;
        }

        /// <summary>
        /// Start/Confirm button - called by ALL players (host and clients).
        /// Confirms the player's domain + vessel choices and enters the waiting state.
        /// When all human players have confirmed, the host auto-launches the game.
        /// </summary>
        public void OnStartGameClicked()
        {
            // The one-panel layout subscribes this to the panel's Start button, and a prefab may
            // ALSO carry an inspector onClick to it - plus a player can simply double-click. Ready
            // is a latch, so the second call is a no-op rather than a second sting and a second
            // ConfirmLocalPlayerReady.
            if (_localPlayerReady) return;

            // A DISABLED button is not the whole gate. `OnStartGameClicked` is public, a prefab may
            // carry its own onClick to it, and the modal's gamepad path drives rows rather than
            // the button - so the one place that must refuse a spent attempt is the handler, not
            // the control.
            if (!CanStartWeeklyChallenge())
            {
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow,
                    "[ArcadeConfigModal] Start refused - this week's challenge attempt is spent.");
                return;
            }

            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#FFD700>[FLOW-2] [ArcadeConfigModal] OnStartGameClicked (confirming ready)</color>");
            audioSystem.PlayMenuAudio(MenuAudioCategory.Confirmed);

            // Show "Waiting for others..." and hide the Start button
            _localPlayerReady = true;
            if (ActiveStartButton)
                ActiveStartButton.gameObject.SetActive(false);
            if (ActiveWaitingLabel)
                ActiveWaitingLabel.SetActive(true);
            RefreshRoster();

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
            // The scene now holds TWO of this component (the arcade modal and the Maelstrom's own
            // window), and BOTH subscribe to the sync manager - so this fires on the instance that
            // was never opened too, where _selectedGame is null and the launch sync can only log
            // its red NULL error. An instance with no game selected has nothing to launch and
            // nothing to close; the open instance handles everything, including InvokeGameLaunch.
            if (_selectedGame == null)
            {
                CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                    "[ArcadeConfigModal] AllPlayersReady ignored - no game selected on this " +
                    "modal instance (the open instance launches).");
                return;
            }

            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#FFD700>[FLOW-2] [ArcadeConfigModal] All players ready!</color>");

            bool shouldLaunch = ShouldLocalPlayerLaunch(hostConnectionData, arcadeConfigSyncManager != null);

            if (shouldLaunch)
            {
                audioSystem.PlayMenuAudio(MenuAudioCategory.LetsGo);
                SyncAllGameDataForLaunch();
            }

            // Arm (or stand down) the weekly challenge on EVERY instance, not just the launch
            // authority: IsWeeklyChallenge and the attempt clock are LOCAL state each machine
            // evaluates against its own pilot's stats, and the objective is personal. Standing it
            // down explicitly matters as much as arming it - nothing else clears the flag, so an
            // ordinary launch after a challenge would otherwise still be running the attempt.
            ArmWeeklyChallengeForLaunch();

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
            _weeklyChallengeLocked = false;
            _pendingWeeklyChallenge = false;
            if (config) config.ResetState();

            // Close the modal on all instances.
            //
            // ModalWindowOut() is NOT the whole close, and on this modal it is barely half of it.
            // It fades the modal PREFAB's own root (CanvasGroup + "Window Out"), and the launch
            // panel is not in that prefab - MinigameLaunchPanel is a Menu_Main SCENE object the
            // modal drives - so nothing in that call reaches the panel, its controls block, its
            // objective box, or the live preview window sitting in it. The ✕ route hides all of
            // that through CloseAndNotifyClients; the launch route never did, which left the panel
            // and a frozen RenderTexture on screen over the match it had just started.
            //
            // Deliberately NOT CloseAndNotifyClients(): that tells the party the config was
            // DISMISSED, which is the opposite of what just happened. This takes the same two
            // teardown steps and leaves the notification alone.
            //
            // Safe after InvokeGameLaunch above: the session already unwound itself on the launch
            // event (ModePreviewSession.HandleLaunchRequested), so Stop() inside ShutDownPreview
            // no-ops on an Idle session and this is just the frame coming down.
            ShutDownPreview();
            if (_activePanel) _activePanel.Hide();

            ModalWindowOut();
        }

        /// <summary>
        /// Dress the panel for a weekly challenge — or undress it for an ordinary card, which is
        /// just as important: the panel is a shared scene object, so every override applied here
        /// has to be handed back when the next card opens.
        ///
        /// <para>Four things change, and each is a consequence of the same fact — a weekly
        /// challenge is a FIXED ask with one attempt, not a lobby:</para>
        /// <list type="bullet">
        /// <item>The BRIEFING is the challenge's objective ("Score 20 combat points in 1:30").
        /// The card's own copy sells the mode, which is not what this run is about.</item>
        /// <item>The objective BOX is hidden. It exists to pair a mode's win condition with a live
        /// counter, and here there is nothing to count until the run starts - a box repeating the
        /// briefing beside a 0 says the same thing twice, and one of them wrongly.</item>
        /// <item>Add AI is gone: the seat count is pinned to the card's minimum, so there is no
        /// seat for a bot to take. Intensity and domain are pinned with it.</item>
        /// <item>The preview stays live but stops offering the stick — a free flight in the same
        /// arena on the way in is a rehearsal, and a way to spend the pad with the modal open.</item>
        /// </list>
        ///
        /// <para><b>What is NOT pinned is the mode's END CONDITIONS.</b> The run plays the mode at
        /// its own authored race target — a weekly challenge is a full match of that mode with a
        /// personal objective on top, not a shortened variant. The controls are locked because the
        /// ASK is fixed; the mode is not altered.</para>
        /// </summary>
        void ApplyWeeklyChallengePresentation()
        {
            if (!_activePanel) return;

            bool weekly = _weeklyChallengeLocked;

            _activePanel.SetBriefingOverride(weekly ? _weeklyChallengeObjective : null);

            // Passed either way, never only switched OFF: the panel is a shared scene object, so an
            // override applied for a challenge has to be handed back when the next ordinary card
            // opens, or Add AI is gone from every card after it.
            _activePanel.SetAddAIAvailable(!weekly);
            _activePanel.SetLeaderboardAvailable(weekly);

            // A SPENT challenge still opens - that is the point. Today's run is gone, so Start is
            // dead, but everything else the window shows (the objective, this week's mode, and the
            // leaderboard the button beside it opens) is still worth reading. Closing the card
            // outright, which is what it used to do, made the board unreachable between runs.
            var service = WeeklyChallengeService.Instance;
            bool canStart = !weekly || service == null || service.CanAttempt;

            // Deliberately NO countdown in the reason. It is written once, when the card opens,
            // and a modal can sit open for minutes - a ticking value that does not tick is worse
            // than no value. The card in the grid behind this one already counts down.
            _activePanel.SetStartAvailable(canStart,
                canStart ? null
                : service != null && service.CompletedThisWeek
                    ? "COMPLETED - BEAT YOUR TIME TOMORROW"
                    : "PLAYED TODAY - COME BACK TOMORROW");

            if (_activePanel is MinigameLaunchPanel minigamePanel)
            {
                minigamePanel.SetPreviewFocusEnabled(!weekly);

                // Stated here as well as in ArmPreviewForGame: the preview arms asynchronously and
                // may not have a definition at all, and the box must never be left showing the
                // previous card's win condition in the meantime.
                if (weekly) minigamePanel.HideObjective();
            }
        }

        /// <summary>
        /// Hands the launch to <see cref="WeeklyChallengeService"/> when this session is the weekly
        /// challenge, and clears the flag when it is not. Safe with no service present (the
        /// feature is simply off) and safe on a card that merely happens to be this week's mode.
        /// </summary>
        /// <summary>
        /// False only when this is a weekly-challenge session whose attempt is already spent.
        /// Every ordinary card, and a challenge with an attempt left, answers true.
        /// </summary>
        bool CanStartWeeklyChallenge()
        {
            if (!_weeklyChallengeLocked) return true;

            var service = WeeklyChallengeService.Instance;
            return service == null || service.CanAttempt;
        }

        /// <summary>
        /// Open this week's leaderboard over the launch panel.
        ///
        /// <para>The arcade modal is NOT closed first. The board is a detour, not a destination -
        /// the player is mid-decision about a card, and closing the card underneath them means
        /// re-opening it to get back. The leaderboard window sits on top and hands focus back when
        /// it closes.</para>
        /// </summary>
        void OpenWeeklyLeaderboard()
        {
            var window = ResolveLeaderboardModal();
            if (!window)
            {
                Debug.LogWarning("[ArcadeConfigModal] No WeeklyChallengeLeaderboardModal in the " +
                                 "scene - wire one on this modal, or run FrogletTools > Interface " +
                                 "> Wire Weekly Challenge Leaderboard.");
                return;
            }

            window.Open();
        }

        WeeklyChallengeLeaderboardModal ResolveLeaderboardModal()
        {
            if (leaderboardModal) return leaderboardModal;

            // Inactive included: the window is authored switched off, which is exactly the state
            // it is in every time this runs.
            leaderboardModal = FindAnyObjectByType<WeeklyChallengeLeaderboardModal>(
                FindObjectsInactive.Include);
            return leaderboardModal;
        }

        void ArmWeeklyChallengeForLaunch()
        {
            if (!gameData) return;

            var service = WeeklyChallengeService.Instance;

            if (_weeklyChallengeLocked && service != null)
            {
                service.BeginAttempt(gameData);
                return;
            }

            gameData.IsWeeklyChallenge = false;
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

            // The host's hand-placed AI domains (Add AI mode). The spawner seats bot i in entry i
            // and falls back to its balanced pick past the end - covering the empty seats the
            // card's minimum topped up.
            gameData.SetRequestedAIDomains(config.AIDomains);

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

            bool favorited = FavoriteSystem.IsFavorited(_selectedGame.Mode);
            if (selectedGameFavoriteIcon != null)
                selectedGameFavoriteIcon.Favorited = favorited;
            if (_activePanel) _activePanel.RefreshFavorite(favorited);
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
            // TWO instances of this component live in the scene and BOTH subscribe to these
            // broadcast events: the paneled arcade modal, and the Maelstrom panel's own window -
            // which carries this component only as its ModalWindowManager and authors NO launch
            // panels. Without this gate the panel-less instance also "opened" on every client
            // and drew its LEGACY detail view - the retired video path's solid white rectangle -
            // over the real panel, with its chip spawn producing the "No suitable tile"
            // warnings. A modal that cannot draw a launch panel does not follow remote opens;
            // the legacy client screens died with the one-panel layout.
            if (!UsesLaunchPanels) return;

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

            SelectLaunchPanel(game);

            BuildAvailableShips(game);
            InitializeGameMetaView(game);
            InitializeScreen1Controls(game);
            InitializeDefaultShipFromAvailable();
            InitializeDomainSelection();
            ApplyHostOnlyInteractability();
            ResetReadyUpUI();

            // Move the APP SHELL to the arcade screen first, so the card the host opened is
            // drawn on the screen it belongs to rather than over whatever the guest was looking
            // at - and so closing the modal leaves them there instead of somewhere unrelated.
            // ScreenSwitcher.NavigateTo refuses ARK for a guest on purpose (no browsing the
            // arcade in someone else's party); FollowHostToArcadeScreen is the host-driven
            // entry point past that guard, and nothing on the guest's own UI calls it.
            if (Switcher) Switcher.FollowHostToArcadeScreen();

            Debug.Log("[ArcadeConfigModal] Calling ModalWindowIn on client");
            ModalWindowIn();

            // Clients skip Screen 1 entirely - modal opens straight at GameDetailView
            // with the back button hidden. Host has already committed PC + DC + intensity.
            // The one-panel layout has no second screen to move to - the panel SelectLaunchPanel
            // brought up is the whole surface, and it is already showing.
            if (!UsesLaunchPanels)
                ShowGameDetailScreen();

            HideBackFromGameSelectButton();

            // Same chip-spawn pattern as the host path so clients see live
            // chip movement when any player picks. Server has reset every human's
            // NetDomain to Jade as part of CommitConfiguration, so all chips spawn
            // on the Jade tile.
            SpawnChipsForAllPlayers();
            RefreshTileVisibility();
            RefreshRoster();
        }

        /// <summary>
        /// Called on non-host clients when the host closes the modal or starts a game.
        /// </summary>
        void HandleConfigClosedOnClient()
        {
            // Same two-instance gate as HandleConfigOpenedOnClient: the Maelstrom window's copy
            // of this component never opened, so it has nothing to close - and ModalWindowOut on
            // it would play a close animation on a window the player may be using.
            if (!UsesLaunchPanels) return;

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
            // There are no screens to follow on the one-panel layout, and the host never sends
            // these there - it has no navigation left to broadcast. The !_isClientMode half is
            // the two-instance gate: the Maelstrom window's panel-less copy of this component
            // never opens in client mode, so it must not page through its legacy screens either.
            if (UsesLaunchPanels || !_isClientMode) return;

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
            foreach (var button in ActiveIntensityButtons)
            {
                if (!button) continue;
                var uiButton = button.GetComponent<Button>();
                if (uiButton) uiButton.interactable = isHost;
            }

            if (_activePanel) _activePanel.SetHostControlsInteractable(isHost);

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
            _localPlayerReady = false;
            _readyCount = 0;
            if (ActiveStartButton)
                ActiveStartButton.gameObject.SetActive(true);
            if (ActiveWaitingLabel)
                ActiveWaitingLabel.SetActive(false);
        }

        #endregion
    }
}

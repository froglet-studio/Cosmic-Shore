using System.Collections.Generic;
using System.Linq;
using CosmicShore.App.Systems.Favorites;
using CosmicShore.App.Systems.Loadout;
using CosmicShore.App.UI.Views;
using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.SOAP;                     // ⬅️ NEW: for GameDataSO
using Obvious.Soap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace CosmicShore.App.UI.Modals
{
    public class ArcadeGameConfigureModal : ModalWindowManager
    {
        // TEMP for legacy systems (e.g. DailyChallengeSystem)
        public static ArcadeGameConfigureModal Instance { get; private set; }

        [Header("Config State")]
        [SerializeField] private ArcadeGameConfigSO  config;
        [SerializeField] private ScriptableEventNoParam configChangedEvent;
        [SerializeField] private ScriptableEventNoParam openShipSelectionScreenEvent;
        [SerializeField] private ScriptableEventNoParam startGameRequestedEvent;

        [Header("Shared Game Data")]
        [SerializeField] private GameDataSO gameData;   // ⬅️ NEW: hook into GameDataSO

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
        [SerializeField] private GameObject squadMateSelectionView;  // Screen 3
        [SerializeField] private GameObject vesselSelectionView;     // Screen 4

        [Header("Screen 1 – Configuration Controls")]
        [SerializeField] private List<PlayerCountButton>     playerCountButtons = new(4);
        [SerializeField] private List<IntensitySelectButton> intensityButtons   = new(4);
        [SerializeField] private TMP_Text teamsValueText;

        [Tooltip("If true, will filter out unowned ships from being available to play")]
        [SerializeField] private bool respectInventoryForShipSelection = false;

        [Header("Screen 2 – Selected Vessel Summary")]
        [SerializeField] private Image    shipPlaceholderIcon;
        [SerializeField] private TMP_Text shipNameText;
        [SerializeField] private TMP_Text shipConfigurationText;
        [SerializeField] private TMP_Text shipVesselNameText;
        
        [SerializeField] private Image iconInVesselSelectionView;
        [SerializeField] private Image iconInConfigurationSelectionView;

        [Header("Screen 4 – Vessel Selection Grid")]
        [SerializeField] private ShipSelectionView vesselShipSelectionView;

        SO_ArcadeGame _selectedGame;
        VideoPlayer   _previewVideo;

        readonly List<SO_Ship> _availableShips = new();

        #region Unity lifecycle

        void Awake()
        {
            if (Instance != null && Instance != this)
                return;

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void OnEnable()
        {
            foreach (var intensityButton in intensityButtons)
                intensityButton.OnSelect += HandleIntensitySelected;

            foreach (var playerCountButton in playerCountButtons)
                playerCountButton.OnSelect += HandlePlayerCountSelected;

            if (vesselShipSelectionView != null)
                vesselShipSelectionView.OnSelect += HandleShipSelectedFromGrid;

            if (configChangedEvent != null)
                configChangedEvent.OnRaised += HandleConfigChangedExternal;
        }

        void OnDisable()
        {
            foreach (var intensityButton in intensityButtons)
                intensityButton.OnSelect -= HandleIntensitySelected;

            foreach (var playerCountButton in playerCountButtons)
                playerCountButton.OnSelect -= HandlePlayerCountSelected;

            if (vesselShipSelectionView != null)
                vesselShipSelectionView.OnSelect -= HandleShipSelectedFromGrid;

            if (configChangedEvent != null)
                configChangedEvent.OnRaised -= HandleConfigChangedExternal;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Entry point from ArcadeExploreView when a game tile is selected.
        /// </summary>
        public void SetSelectedGame(SO_ArcadeGame selectedGame)
        {
            _selectedGame = selectedGame;

            if (config == null)
            {
                Debug.LogError("[ArcadeGameConfigureModal] Missing ArcadeGameConfigSO.");
                return;
            }

            config.ResetState();
            config.SelectedGame = selectedGame;
            config.TeamCount    = 1; // number of teams disabled for now

            BuildAvailableShips(selectedGame);
            InitializeConfigFromGameDefaults(selectedGame);
            InitializeGameMetaView(selectedGame);
            InitializeScreen1Controls(selectedGame);
            InitializeDefaultShipFromAvailable();   // ⬅️ now also syncs to GameData

            SetupVesselSelectionViewModels();

            ShowConfigurationScreen();
            RaiseConfigChanged();
        }

        #endregion

        #region Initialization helpers

        void InitializeConfigFromGameDefaults(SO_ArcadeGame game)
        {
            config.Intensity   = game.MinIntensity;
            config.PlayerCount = game.MinPlayers;

            SyncGameDataConfig();   // ⬅️ NEW: mirror to GameData
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
            // Intensity
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

            // Player count
            for (int i = 0; i < playerCountButtons.Count; i++)
            {
                var button = playerCountButtons[i];
                if (!button) continue;

                int count = i + 1;
                button.SetPlayerCount(count);

                bool active = count >= game.MinPlayers && count <= game.MaxPlayers;
                button.SetActive(active);
                button.SetSelected(count == config.PlayerCount);
            }

            if (teamsValueText)
                teamsValueText.text = "1"; // fixed for now
        }

        void BuildAvailableShips(SO_ArcadeGame game)
        {
            _availableShips.Clear();

            if (!game) return;

            var ships = game.Captains
                .Where(c => c && c.Ship)
                .Select(c => c.Ship)
                .ToList();

            if (respectInventoryForShipSelection && CaptainManager.Instance)
            {
                var unlocked = CaptainManager.Instance.UnlockedShips;
                ships = ships.Where(s => unlocked.Contains(s)).ToList();
            }

            _availableShips.AddRange(ships);
        }

        /// <summary>
        /// Decide which ship is selected by default:
        /// 1) Last selected (GameDataSO.selectedVesselClass) if available
        /// 2) Loadout's VesselType if available
        /// 3) Dolphin if available
        /// 4) First available ship
        /// </summary>
        void InitializeDefaultShipFromAvailable()
        {
            if (_availableShips.Count == 0)
            {
                config.SelectedShip = null;
                SyncGameDataShip(null);
                RefreshShipSummaryView();
                return;
            }

            SO_Ship chosen = null;
            if (gameData && gameData.selectedVesselClass)
            {
                var prevType = gameData.selectedVesselClass.Value;
                if (prevType != VesselClassType.Any && prevType != VesselClassType.Random)
                    chosen = _availableShips.FirstOrDefault(s => s.Class == prevType);
            }

            if (!chosen && _selectedGame)
            {
                var loadout   = LoadoutSystem.LoadGameLoadout(_selectedGame.Mode).Loadout;
                var loadoutVT = loadout.VesselType;

                if (loadoutVT != VesselClassType.Random)
                {
                    chosen = _availableShips.FirstOrDefault(s => s.Class == loadoutVT);
                }
            }

            if (!chosen)
                chosen = _availableShips.FirstOrDefault(s => s.Class == VesselClassType.Dolphin);

            // 4) Fallback: first available
            if (!chosen)
                chosen = _availableShips[0];

            config.SelectedShip = chosen;
            SyncGameDataShip(chosen);
            RefreshShipSummaryView();
        }

        void SetupVesselSelectionViewModels()
        {
            if (!vesselShipSelectionView) return;

            var models = _availableShips.Cast<ScriptableObject>().ToList();

            vesselShipSelectionView.AssignModels(models);
            vesselShipSelectionView.UpdateView();
        }

        #endregion

        #region Screen switching

        void SetScreenActive(GameObject configScreen, GameObject gameDetailScreen,
                             GameObject squadScreen, GameObject vesselScreen)
        {
            if (configurationDetailView)
                configurationDetailView.SetActive(configurationDetailView == configScreen);

            if (gameDetailView)
                gameDetailView.SetActive(gameDetailView == gameDetailScreen);

            if (squadMateSelectionView)
                squadMateSelectionView.SetActive(squadMateSelectionView == squadScreen);

            if (vesselSelectionView)
                vesselSelectionView.SetActive(vesselSelectionView == vesselScreen);
        }

        void ShowConfigurationScreen()
        {
            SetScreenActive(configurationDetailView, null, null, null);
        }

        void ShowGameDetailScreen()
        {
            SetScreenActive(null, gameDetailView, null, null);
            RefreshShipSummaryView();
        }

        void ShowSquadMateSelectionScreen()
        {
            SetScreenActive(null, null, squadMateSelectionView, null);
        }

        void ShowVesselSelectionScreen()
        {
            SetScreenActive(null, null, null, vesselSelectionView);
            SetupVesselSelectionViewModels();
        }

        #endregion

        #region Config change handlers

        void HandleIntensitySelected(int intensity)
        {
            if (_selectedGame == null || config == null) return;

            intensity        = Mathf.Clamp(intensity, _selectedGame.MinIntensity, _selectedGame.MaxIntensity);
            config.Intensity = intensity;

            foreach (var button in intensityButtons)
            {
                if (!button) continue;
                button.SetSelected(button.Intensity == intensity);
            }

            SyncGameDataConfig();   // ⬅️ mirror to GameData
            RaiseConfigChanged();
        }

        void HandlePlayerCountSelected(int playerCount)
        {
            if (_selectedGame == null || config == null) return;

            playerCount        = Mathf.Clamp(playerCount, _selectedGame.MinPlayers, _selectedGame.MaxPlayers);
            config.PlayerCount = playerCount;

            foreach (var button in playerCountButtons)
            {
                if (!button) continue;
                button.SetSelected(button.Count == playerCount);
            }

            SyncGameDataConfig();   // ⬅️ mirror to GameData
            RaiseConfigChanged();
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

        #region Vessel selection (Screen 4)

        void HandleShipSelectedFromGrid(SO_Ship ship)
        {
            if (!config) return;

            config.SelectedShip = ship;
            SyncGameDataShip(ship);     // ⬅️ keep GameData in sync
            RefreshShipSummaryView();
            RaiseConfigChanged();
        }

        #endregion

        #region Ship summary & actions (Screen 2)

        void RefreshShipSummaryView()
        {
            RefreshShipSummaryView(config ? config.SelectedShip : null);
        }

        void RefreshShipSummaryView(SO_Ship ship)
        {
            if (shipPlaceholderIcon)
            {
                if (ship && ship.IconActive)
                {
                    shipPlaceholderIcon.enabled = true;
                    shipPlaceholderIcon.sprite  = ship.IconActive;

                    if (iconInConfigurationSelectionView)
                        iconInConfigurationSelectionView.sprite = ship.IconActive;
                    if (iconInVesselSelectionView)
                        iconInVesselSelectionView.sprite = ship.IconActive;
                }
                else
                {
                    shipPlaceholderIcon.enabled = false;
                }
            }

            var nameText = ship ? ship.Name : "SELECT SHIP";

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
            ShowGameDetailScreen();
        }

        // Screen 2 → Screen 3
        public void OnOpenSquadMateSelectionClicked()
        {
            ShowSquadMateSelectionScreen();
        }

        // Screen 3 → Screen 4
        public void OnOpenVesselSelectionClicked()
        {
            // Optional hook if you want another view to react
            openShipSelectionScreenEvent?.Raise();
            ShowVesselSelectionScreen();
        }

        // Screen 4 → Screen 3
        public void OnBackFromVesselSelectionClicked()
        {
            ShowSquadMateSelectionScreen();
        }

        // Screen 3 → Screen 2
        public void OnBackFromSquadMateSelectionClicked()
        {
            ShowGameDetailScreen();
        }

        public void OnBackFromGameSelectView()
        {
            ShowConfigurationScreen();
        }

        // Start Game button on Screen 2
        public void OnStartGameClicked()
        {
            startGameRequestedEvent?.Raise();
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

        void SyncGameDataShip(SO_Ship ship)
        {
            if (!gameData || !gameData.selectedVesselClass)
                return;

            if (ship)
            {
                gameData.selectedVesselClass.Value = ship.Class;

                if (gameData.VesselClassSelectedIndex)
                    gameData.VesselClassSelectedIndex.Value = (int)ship.Class;
            }
            else
            {
                // fallback to Dolphin if nothing is selected
                gameData.selectedVesselClass.Value = VesselClassType.Dolphin;

                if (gameData.VesselClassSelectedIndex)
                    gameData.VesselClassSelectedIndex.Value = (int)VesselClassType.Dolphin;
            }
        }

        #endregion
    }
}

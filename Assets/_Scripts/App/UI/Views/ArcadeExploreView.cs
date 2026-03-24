using CosmicShore.App.Systems.Audio;
using CosmicShore.Soap;
using CosmicShore.App.Systems.CTA;
using CosmicShore.App.Systems.Favorites;
using CosmicShore.App.Systems.Loadout;
using CosmicShore.App.UI.Elements;
using CosmicShore.App.UI.Modals;
using CosmicShore.Core;
using CosmicShore.Game.Arcade;
using CosmicShore.Game.Progression;
using CosmicShore.Integrations.PlayFab.Economy;
using System;
using System.Collections.Generic;
using System.Linq;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using CosmicShore.Models.Enums;
using CosmicShore.Utility;

namespace CosmicShore.App.UI.Views
{
    public class ArcadeExploreView : MonoBehaviour
    {
        [Header("Game Selection View")]
        [SerializeField] SO_GameList GameList;
        [SerializeField] GameObject GameSelectionView;
        [SerializeField] Transform GameSelectionGrid;
        [SerializeField] ArcadeDPadNav ArcadeDPadNav;
        [SerializeField] DailyChallengeCard DailyChallengeCard;
        [SerializeField] Button QuickPlayButton;
        [Header("Game Detail View")]
        [SerializeField] ArcadeGameConfigureModal ArcadeGameConfigureModal;
        [Header("Test Settings")]
        [Tooltip("If true, will filter out unowned games from being available to play (MUST BE TRUE ON FOR PRODUCTION BUILDS")]
        [SerializeField] bool RespectInventoryForGameSelection = false;

        [SerializeField] VesselClassTypeVariable selectedVesselClassType;
        [SerializeField] GameDataSO gameData;

        SO_ArcadeGame SelectedGame;
        List<GameCard> GameCards;

        void OnEnable()
        {
            CatalogManager.OnLoadInventory += PopulateGameSelectionList;

            if (GameModeProgressionService.Instance != null)
                GameModeProgressionService.Instance.OnProgressionChanged += OnProgressionChanged;
        }

        void OnDisable()
        {
            CatalogManager.OnLoadInventory -= PopulateGameSelectionList;

            if (GameModeProgressionService.Instance != null)
                GameModeProgressionService.Instance.OnProgressionChanged -= OnProgressionChanged;
        }

        void Start()
        {
            LoadoutSystem.Init();

            // Show Daily Challenge card but disable interaction
            if (DailyChallengeCard != null)
                DailyChallengeCard.SetComingSoon();

            // Wire QuickPlay button
            if (QuickPlayButton != null)
                QuickPlayButton.onClick.AddListener(LaunchQuickPlay);

            PopulateGameSelectionList();
        }

        public void PopulateGameSelectionList()
        {
            GameCards = new List<GameCard>();
            ArcadeDPadNav.AddRow(new List<Button>());

            // Add top-row buttons to nav row 0
            if (DailyChallengeCard != null && DailyChallengeCard.gameObject.activeSelf
                && DailyChallengeCard.TryGetComponent<Button>(out var dcButton))
                ArcadeDPadNav.AddButtonToRow(dcButton, 0);
            if (QuickPlayButton != null)
                ArcadeDPadNav.AddButtonToRow(QuickPlayButton, 0);

            // Deactivate all game cards and add them to the list of game cards
            for (var i = 0; i < GameSelectionGrid.transform.childCount; i++)
            {
                ArcadeDPadNav.AddRow(new List<Button>());

                var gameSelectionRow = GameSelectionGrid.GetChild(i);
                for (var j = 0; j < gameSelectionRow.childCount; j++)
                {
                    gameSelectionRow.GetChild(j).gameObject.SetActive(false);
                    GameCards.Add(gameSelectionRow.GetChild(j).GetComponent<GameCard>());

                    ArcadeDPadNav.AddButtonToRow(gameSelectionRow.GetChild(j).GetComponent<Button>(), i+1);
                }
            }

            var progressionService = GameModeProgressionService.Instance;

            // Sort unlocked first, then favorited, then alphabetically
            var filteredGames = RespectInventoryForGameSelection ? GameList.Games.Where(x => CatalogManager.Inventory.ContainsGame(x.DisplayName)).ToList() : GameList.Games;
            var sortedGames = filteredGames;
            sortedGames.Sort((x, y) =>
            {
                // Unlocked games before locked games
                bool xLocked = progressionService != null && !progressionService.IsGameModeUnlocked(x.Mode);
                bool yLocked = progressionService != null && !progressionService.IsGameModeUnlocked(y.Mode);
                int lockComparison = xLocked.CompareTo(yLocked);
                if (lockComparison != 0)
                    return lockComparison;

                int flagComparison = FavoriteSystem.IsFavorited(y.Mode).CompareTo(FavoriteSystem.IsFavorited(x.Mode));
                if (flagComparison == 0)
                    return string.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal);

                return flagComparison;
            });

            for (var i = 0; i < GameCards.Count && i < GameList.Games.Count && i < sortedGames.Count; i++)
            {
                var game = sortedGames[i];

                CSDebug.Log($"ExploreMenu - Populating Game Select List: {game.DisplayName}");

                var gameCard = GameCards[i];
                gameCard.GameMode = game.Mode;
                gameCard.Favorited = FavoriteSystem.IsFavorited(game.Mode);
                gameCard.GetComponent<Button>().onClick.RemoveAllListeners();
                gameCard.ExploreView = this;

                // Check if this game mode is unlocked via the quest progression system
                bool isLocked = progressionService != null && !progressionService.IsGameModeUnlocked(game.Mode);
                gameCard.SetLocked(isLocked);

                if (!isLocked)
                {
                    gameCard.GetComponent<Button>().onClick.AddListener(() => SelectGame(game));
                }

                if (gameCard.TryGetComponent(out CallToActionTarget target))
                {
                    target.TargetID = game.CallToActionTargetType;
                }
                else
                {
                    CSDebug.LogWarningFormat("{0} - The {1} game card does not have Call To Action Target Component. Please attach it.",
                        nameof(ArcadeExploreView), game.CallToActionTargetType.ToString());
                }

                gameCard.gameObject.SetActive(true);
            }
        }

        void OnProgressionChanged(GameModeProgressionData data)
        {
            PopulateGameSelectionList();
        }

        public void SelectGame(SO_ArcadeGame selectedGame)
        {
            SelectedGame = selectedGame;
            ArcadeGameConfigureModal.ModalWindowIn();
            ArcadeGameConfigureModal.SetSelectedGame(SelectedGame);
            // TODO: is is throwing a key not found exception
            //UserActionSystem.Instance.CompleteAction(SelectedGame.ViewUserAction);
        }

        public void SelectShip(SO_Vessel selectedShip)
        {
            CSDebug.Log($"SelectShip: {selectedShip.Name}");

            selectedVesselClassType.Value = selectedShip.Class;
            // TODO - Remove statics from MiniGame, use SOAP Data Container
            // notify the mini game engine that this is the vessel to play
            // MiniGame.PlayerShipType = selectedShip.Class;

            // Set resource levels from the vessel's config
            MiniGame.ResourceCollection = selectedShip.InitialResourceLevels;
        }

        public void PlaySelectedGame()
        {
            if (SelectedGame == null) return;

            var vesselType = gameData && gameData.selectedVesselClass
                ? gameData.selectedVesselClass.Value
                : VesselClassType.Dolphin;

            // Validate vessel: must exist in the game's vessel list and be unlocked
            var validVessels = SelectedGame.Vessels?.Where(v => v != null && !v.IsLocked).ToList();
            if (validVessels == null || validVessels.Count == 0) return;

            var matchedVessel = validVessels.FirstOrDefault(v => v.Class == vesselType);
            if (matchedVessel == null)
            {
                // Selected vessel not available for this game mode — fall back to first unlocked
                matchedVessel = validVessels[0];
                vesselType = matchedVessel.Class;
            }

            var resources = matchedVessel.InitialResourceLevels;

            int playerCount = gameData && gameData.SelectedPlayerCount ? gameData.SelectedPlayerCount.Value : 1;
            int intensity = gameData && gameData.SelectedIntensity ? gameData.SelectedIntensity.Value : 1;
            bool isMultiplayer = SelectedGame.IsMultiplayer && playerCount > 1;

            AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.LetsGo);
            LoadoutSystem.SaveGameLoadOut(SelectedGame.Mode, new Loadout(intensity, playerCount, vesselType, SelectedGame.Mode, isMultiplayer));
            Arcade.Instance.LaunchArcadeGame(SelectedGame.Mode, vesselType, resources, intensity, playerCount, isMultiplayer, false);
        }

        public void ToggleFavorite()
        {
            FavoriteSystem.ToggleFavorite(SelectedGame.Mode);
            PopulateGameSelectionList();
        }

        /// <summary>
        /// Launches the latest unlocked game in the quest chain at the highest unlocked intensity.
        /// Always single-player. Randomly selects from unlocked vessels only.
        /// </summary>
        void LaunchQuickPlay()
        {
            var progressionService = GameModeProgressionService.Instance;
            if (progressionService == null || progressionService.QuestList == null) return;

            var quests = progressionService.QuestList.Quests;
            if (quests == null || quests.Count == 0) return;

            // Walk the quest chain backwards to find the latest unlocked non-placeholder game mode
            SO_ArcadeGame latestGame = null;
            for (int i = quests.Count - 1; i >= 0; i--)
            {
                var quest = quests[i];
                if (quest == null || quest.IsPlaceholder) continue;
                if (!progressionService.IsGameModeUnlocked(quest.GameMode)) continue;

                latestGame = GameList.Games.FirstOrDefault(g => g.Mode == quest.GameMode);
                if (latestGame != null) break;
            }

            if (latestGame == null) return;

            int maxIntensity = progressionService.GetMaxUnlockedIntensity(latestGame.Mode);
            if (maxIntensity <= 0) maxIntensity = latestGame.MinIntensity;

            // Collect only unlocked vessels and pick one at random
            var unlockedVessels = latestGame.Vessels?.Where(v => v != null && !v.IsLocked).ToList();
            if (unlockedVessels == null || unlockedVessels.Count == 0) return;

            var vessel = unlockedVessels[UnityEngine.Random.Range(0, unlockedVessels.Count)];

            AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.LetsGo);
            Arcade.Instance.LaunchArcadeGame(latestGame.Mode, vessel.Class, vessel.InitialResourceLevels, maxIntensity, 1, false, false);
        }
    }
}
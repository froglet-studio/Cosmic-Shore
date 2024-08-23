using CosmicShore.App.Systems.CTA;
using CosmicShore.App.Systems.Favorites;
using CosmicShore.App.Systems.Loadout;
using CosmicShore.App.Systems.UserActions;
using CosmicShore.App.UI.Elements;
using CosmicShore.App.UI.Modals;
using CosmicShore.Core;
using CosmicShore.Game.Arcade;
using CosmicShore.Integrations.PlayFab.Economy;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace CosmicShore.App.UI.Views
{
    public class ArcadeExploreView : MonoBehaviour
    {
        [Header("Game Selection View")]
        [SerializeField] SO_GameList GameList;
        [SerializeField] GameObject GameSelectionView;
        [SerializeField] Transform GameSelectionGrid;

        [Header("Game Detail View")]
        [SerializeField] ArcadeGameConfigureModal ArcadeGameConfigureModal;
        [SerializeField] GameObject GameDetailView;
        [SerializeField] TMPro.TMP_Text SelectedGameName;
        [SerializeField] TMPro.TMP_Text SelectedGameDescription;
        [SerializeField] GameObject SelectedGamePreviewWindow;
        [SerializeField] Transform ShipSelectionGrid;
        [SerializeField] FavoriteIcon SelectedGameFavoriteIcon;
        [SerializeField] ShipSelectionView ShipSelectionView;
        
        [Header("Test Settings")]
        [Tooltip("If true, will filter out unowned games from being available to play (MUST BE TRUE ON FOR PRODUCTION BUILDS")]
        [SerializeField] bool RespectInventoryForGameSelection = false;
        [Tooltip("If true, will filter out unowned ships from being available to play (MUST BE TRUE ON FOR PRODUCTION BUILDS")]
        [SerializeField] bool RespectInventoryForShipSelection = false;

        SO_Ship SelectedShip;
        SO_ArcadeGame SelectedGame;
        List<GameCard> GameCards;
        VideoPlayer PreviewVideo;

        void Start()
        {
            LoadoutSystem.Init();
            PopulateGameSelectionList();
        }

        void OpenGameDetailModal()
        {
            ArcadeGameConfigureModal.ModalWindowIn();

            UserActionSystem.Instance.CompleteAction(SelectedGame.ViewUserAction);
        }

        public void PopulateGameSelectionList()
        {
            GameCards = new List<GameCard>();

            // Deactivate all game cards and add them to the list of game cards
            for (var i = 0; i < GameSelectionGrid.transform.childCount; i++)
            {
                var gameSelectionRow = GameSelectionGrid.GetChild(i);
                for (var j = 0; j < gameSelectionRow.childCount; j++)
                {
                    gameSelectionRow.GetChild(j).gameObject.SetActive(false);
                    GameCards.Add(gameSelectionRow.GetChild(j).GetComponent<GameCard>());
                }
            }

            // Sort favorited first, then alphabetically
            var filteredGames = RespectInventoryForGameSelection ? GameList.GameList.Where(x => CatalogManager.Inventory.ContainsGame(x.DisplayName)).ToList() : GameList.GameList;
            var sortedGames = filteredGames;
            sortedGames.Sort((x, y) =>
            {
                int flagComparison = FavoriteSystem.IsFavorited(y.Mode).CompareTo(FavoriteSystem.IsFavorited(x.Mode));
                if (flagComparison == 0)
                    return string.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal); // Sort alphabetically by Name if they're tied

                return flagComparison;
            });

            for (var i = 0; i < GameCards.Count && i < GameList.GameList.Count; i++)
            {
                var game = sortedGames[i];

                Debug.Log($"ExploreMenu - Populating Game Select List: {game.DisplayName}");

                var gameCard = GameCards[i];
                gameCard.GameMode = game.Mode;
                gameCard.Favorited = FavoriteSystem.IsFavorited(game.Mode);
                gameCard.GetComponent<Button>().onClick.RemoveAllListeners();
                gameCard.GetComponent<Button>().onClick.AddListener(() => SelectGame(game));
                gameCard.GetComponent<Button>().onClick.AddListener(() => GameSelectionGrid.GetComponent<MenuAudio>().PlayAudio());
                gameCard.ExploreView = this;
                
                if (gameCard.TryGetComponent(out CallToActionTarget target))
                {
                    target.TargetID = game.CallToActionTargetType;
                }
                else
                {
                    Debug.LogWarningFormat("{0} - The {1} game card does not have Call To Action Target Component. Please attach it.", 
                        nameof(ArcadeExploreView), game.CallToActionTargetType.ToString());
                }
                
                gameCard.gameObject.SetActive(true);
            }
        }

        public void SelectGame(SO_ArcadeGame selectedGame)
        {
            SelectedGame = selectedGame;

            // Populate configuration using loadout or default
            var loadout = LoadoutSystem.LoadGameLoadout(SelectedGame.Mode).Loadout;
            ArcadeGameConfigureModal.SetSelectedGame(SelectedGame);
            ArcadeGameConfigureModal.SetIntensity(loadout.Intensity == 0 ? SelectedGame.MinIntensity : loadout.Intensity);
            ArcadeGameConfigureModal.SetPlayerCount(loadout.PlayerCount == 0 ? SelectedGame.MinPlayers : loadout.PlayerCount);

            if (RespectInventoryForShipSelection)
            {
                List<SO_Captain> filteredCaptains = SelectedGame.Captains.Where(x => CaptainManager.Instance.UnlockedShips.Contains(x.Ship)).ToList();
                ShipSelectionView.AssignModels(filteredCaptains.ConvertAll(x => (ScriptableObject)x.Ship));
                ShipSelectionView.OnSelect += SelectShip;
            }
            else
            {
                ShipSelectionView.AssignModels(SelectedGame.Captains.ConvertAll(x => (ScriptableObject)x.Ship));
                ShipSelectionView.OnSelect += SelectShip;
            }

            // Populate game data and show view
            PopulateGameDetails();
            OpenGameDetailModal();
        }

        void PopulateGameDetails()
        {
            Debug.Log($"Populating Game Details List: {SelectedGame.DisplayName}");
            Debug.Log($"Populating Game Details List: {SelectedGame.Description}");
            Debug.Log($"Populating Game Details List: {SelectedGame.Icon}");
            Debug.Log($"Populating Game Details List: {SelectedGame.PreviewClip}");

            // Set Game Detail Meta Data
            SelectedGameName.text = SelectedGame.DisplayName;
            SelectedGameDescription.text = SelectedGame.Description;
            SelectedGameFavoriteIcon.Favorited = FavoriteSystem.IsFavorited(SelectedGame.Mode);

            // Load Preview Video
            if (PreviewVideo == null)
            {
                PreviewVideo = Instantiate(SelectedGame.PreviewClip);
                PreviewVideo.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 152);
                PreviewVideo.transform.SetParent(SelectedGamePreviewWindow.transform, false);
            }
            else
                PreviewVideo.clip = SelectedGame.PreviewClip.clip;
        }

        IEnumerator SelectCaptainCoroutine(SO_Captain captain)
        {
            yield return new WaitForEndOfFrame();
            SelectShip(captain.Ship);
        }

        public void SelectShip(SO_Ship selectedShip)
        {
            Debug.Log($"SelectShip: {selectedShip.Name}");

            // notify the mini game engine that this is the ship to play
            MiniGame.PlayerShipType = selectedShip.Class;

            // if game.captains matches selectedShip.captains, that's the one
            foreach (var captain in selectedShip.Captains)
            {
                if (SelectedGame.Captains.Contains(captain))
                    MiniGame.ShipResources = captain.InitialResourceLevels;
            }
        }

        public void SetPlayerCount(int playerCount)
        {
            Debug.Log($"SetPlayerCount: {playerCount}");

            // notify the mini game engine that this is the number of players
            MiniGame.NumberOfPlayers = playerCount;
        }

        public void SetIntensity(int intensity)
        {
            Debug.Log($"ArcadeMenu - SetIntensity: {intensity}");

            Hangar.Instance.SetAiDifficultyLevel(intensity);

            // notify the mini game engine that this is the difficulty
            MiniGame.IntensityLevel = intensity;
        }

        public void PlaySelectedGame()
        {
            LoadoutSystem.SaveGameLoadOut(SelectedGame.Mode, new Loadout(MiniGame.IntensityLevel, MiniGame.NumberOfPlayers, MiniGame.PlayerShipType, SelectedGame.Mode));

            Arcade.Instance.LaunchArcadeGame(SelectedGame.Mode, MiniGame.PlayerShipType, MiniGame.ShipResources, MiniGame.IntensityLevel, MiniGame.NumberOfPlayers, false);
        }

        public void ToggleFavorite()
        {
            SelectedGameFavoriteIcon.Favorited = !SelectedGameFavoriteIcon.Favorited;
            FavoriteSystem.ToggleFavorite(SelectedGame.Mode);
            PopulateGameSelectionList();
        }
    }
}
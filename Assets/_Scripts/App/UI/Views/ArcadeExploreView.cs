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
        [Header("Test Settings")]
        [Tooltip("If true, will filter out unowned games from being available to play (MUST BE TRUE ON FOR PRODUCTION BUILDS")]
        [SerializeField] bool RespectInventoryForGameSelection = false;

        SO_ArcadeGame SelectedGame;
        List<GameCard> GameCards;

        void Start()
        {
            LoadoutSystem.Init();
            PopulateGameSelectionList();
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
            ArcadeGameConfigureModal.SetSelectedGame(SelectedGame);
            ArcadeGameConfigureModal.ModalWindowIn();
            // TODO: is is throwing a key not found exception
            //UserActionSystem.Instance.CompleteAction(SelectedGame.ViewUserAction);
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
            FavoriteSystem.ToggleFavorite(SelectedGame.Mode);
            PopulateGameSelectionList();
        }
    }
}
using CosmicShore.Systems.CTA;
using CosmicShore.Systems.Favorites;
using CosmicShore.Systems.Loadout;
using CosmicShore.App.UI.Elements;
using CosmicShore.App.UI.Modals;
using CosmicShore.Game.Arcade;
using CosmicShore.Integrations.PlayFab.Economy;
using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Soap;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;

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
        [Header("Game Detail View")]
        [SerializeField] ArcadeGameConfigureModal ArcadeGameConfigureModal;
        [Header("Test Settings")]
        [Tooltip("If true, will filter out unowned games from being available to play (MUST BE TRUE ON FOR PRODUCTION BUILDS")]
        [SerializeField] bool RespectInventoryForGameSelection = false;

        [SerializeField] VesselClassTypeVariable selectedVesselClassType;
        
        SO_ArcadeGame SelectedGame;
        List<GameCard> GameCards;
        
        [Inject] GameDataSO gameData;

        void OnEnable()
        {
            CatalogManager.OnLoadInventory += PopulateGameSelectionList;
        }

        void OnDisable()
        {
            CatalogManager.OnLoadInventory -= PopulateGameSelectionList;
        }

        void Start()
        {
            LoadoutSystem.Init();
            PopulateGameSelectionList();
        }

        public void PopulateGameSelectionList()
        {
            GameCards = new List<GameCard>();
            ArcadeDPadNav.AddRow(new List<Button>());
            ArcadeDPadNav.AddButtonToRow(DailyChallengeCard.GetComponent<Button>(), 0);

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

            // Sort favorited first, then alphabetically
            var filteredGames = RespectInventoryForGameSelection ? GameList.Games.Where(x => CatalogManager.Inventory.ContainsGame(x.DisplayName)).ToList() : GameList.Games;
            var sortedGames = filteredGames;
            sortedGames.Sort((x, y) =>
            {
                int flagComparison = FavoriteSystem.IsFavorited(y.Mode).CompareTo(FavoriteSystem.IsFavorited(x.Mode));
                if (flagComparison == 0)
                    return string.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal); // Sort alphabetically by Name if they're tied

                return flagComparison;
            });

            for (var i = 0; i < GameCards.Count && i < GameList.Games.Count && i < sortedGames.Count; i++)
            {
                var game = sortedGames[i];

                // Debug.Log($"ExploreMenu - Populating Game Select List: {game.DisplayName}");

                var gameCard = GameCards[i];
                gameCard.GameMode = game.Mode;
                gameCard.Favorited = FavoriteSystem.IsFavorited(game.Mode);
                gameCard.GetComponent<Button>().onClick.RemoveAllListeners();
                gameCard.GetComponent<Button>().onClick.AddListener(() => SelectGame(game));
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
            ArcadeGameConfigureModal.ModalWindowIn();
            ArcadeGameConfigureModal.SetSelectedGame(SelectedGame);
            // TODO: is is throwing a key not found exception
            //UserActionSystem.Instance.CompleteAction(SelectedGame.ViewUserAction);
        }

        public void SelectShip(SO_Ship selectedShip)
        {
            Debug.Log($"SelectShip: {selectedShip.Name}");

            selectedVesselClassType.Value = selectedShip.Class;
            // TODO - Remove statics from MiniGame, use SOAP Data Container
            // notify the mini game engine that this is the vessel to play
            // MiniGame.PlayerShipType = selectedShip.Class;

            // if game.captains matches selectedShip.captains, that's the one
            foreach (var captain in selectedShip.Captains)
            {
                if (SelectedGame.Captains.Contains(captain))
                    //MiniGame.ShipResources = captain.InitialResourceLevels;
                    gameData.ResourceCollection = captain.InitialResourceLevels;
            }
        }

        public void PlaySelectedGame()
        {
            // LoadoutSystem.SaveGameLoadOut(SelectedGame.Mode, new Loadout(MiniGame.IntensityLevel, MiniGame.NumberOfPlayers, MiniGame.PlayerVesselType, SelectedGame.Mode, SelectedGame.IsMultiplayer));
            // Arcade.Instance.LaunchArcadeGame(SelectedGame.Mode, MiniGame.PlayerVesselType, MiniGame.ResourceCollection, MiniGame.IntensityLevel, MiniGame.NumberOfPlayers, SelectedGame.IsMultiplayer, false);
            gameData.InvokeGameLaunch();
        }

        public void ToggleFavorite()
        {
            FavoriteSystem.ToggleFavorite(SelectedGame.Mode);
            PopulateGameSelectionList();
        }
    }
}
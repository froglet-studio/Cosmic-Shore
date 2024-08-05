using CosmicShore.App.Systems.CTA;
using CosmicShore.App.Systems.Favorites;
using CosmicShore.App.Systems.Loadout;
using CosmicShore.App.Systems.UserActions;
using CosmicShore.App.UI.Elements;
using CosmicShore.Core;
using CosmicShore.Game.Arcade;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace CosmicShore.App.UI.Menus
{
    public class ExploreMenu : MonoBehaviour
    {
        [Header("Game Selection View")]
        [SerializeField] SO_GameList GameList;
        [SerializeField] GameObject GameSelectionView;
        [SerializeField] Transform GameSelectionGrid;

        [Header("Game Detail View")]
        [SerializeField] ModalWindowManager GameDetailModal;
        [SerializeField] GameObject GameDetailView;
        [SerializeField] TMPro.TMP_Text SelectedGameName;
        [SerializeField] TMPro.TMP_Text SelectedGameDescription;
        [SerializeField] GameObject SelectedGamePreviewWindow;
        [SerializeField] Transform ShipSelectionGrid;
        [SerializeField] FavoriteIcon SelectedGameFavoriteIcon;
        [SerializeField] ShipSelectionView ShipSelectionView;

        [Header("Game Play Settings")]
        [SerializeField] GameObject PlayerCountButtonContainer;
        [SerializeField] GameObject IntensityButtonContainer;
        [SerializeField] List<Sprite> IntensityIcons = new(4);
        [SerializeField] List<Sprite> PlayerCountIcons = new(4);

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
            GameDetailModal.ModalWindowIn();

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
            var sortedGames = GameList.GameList;
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
                gameCard.ExploreMenu = this;
                
                if (gameCard.TryGetComponent(out CallToActionTarget target))
                {
                    target.TargetID = game.CallToActionTargetType;
                }
                else
                {
                    Debug.LogWarningFormat("{0} - The {1} game card does not have Call To Action Target Component. Please attach it.", 
                        nameof(ExploreMenu), game.CallToActionTargetType.ToString());
                }
                
                gameCard.gameObject.SetActive(true);
            }
        }

        public void SelectGame(SO_ArcadeGame selectedGame)
        {
            SelectedGame = selectedGame;

            // Show/Hide Player Count buttons
            for (var i = 0; i < PlayerCountButtonContainer.transform.childCount; i++)
                PlayerCountButtonContainer.transform.GetChild(i).gameObject.SetActive(i < SelectedGame.MaxPlayers && i >= SelectedGame.MinPlayers - 1);

            // Enable/Disable Intensity Buttons
            var theFonzColor = new Color(.66f, .66f, .66f); // #AAAAAA
            for (var i = 0; i < IntensityButtonContainer.transform.childCount; i++)
            {
                var intensity = i + 1;
                if (intensity >= SelectedGame.MinIntensity && intensity <= SelectedGame.MaxIntensity)
                {
                    IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().enabled = true;
                    IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Image>().color = Color.white;
                }
                else
                {
                    IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().enabled = false;
                    IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Image>().color = theFonzColor;
                }
            }

            // Populate configuration using loadout or default
            var loadout = LoadoutSystem.LoadGameLoadout(SelectedGame.Mode).Loadout;
            SetPlayerCount(loadout.PlayerCount == 0 ? SelectedGame.MinPlayers : loadout.PlayerCount);
            SetIntensity(loadout.Intensity == 0 ? SelectedGame.MinIntensity : loadout.Intensity);


            ShipSelectionView.AssignModels(SelectedGame.Captains.ConvertAll(x => (ScriptableObject)x.Ship));
            ShipSelectionView.OnSelect += SelectShip;


            //StartCoroutine(SelectCaptainCoroutine(SelectedGame.Captains[0]));

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

            for (var i = 0; i < PlayerCountButtonContainer.transform.childCount; i++)
                PlayerCountButtonContainer.transform.GetChild(i).gameObject.GetComponent<Image>().sprite = PlayerCountIcons[i];

            PlayerCountButtonContainer.transform.GetChild(playerCount - 1).gameObject.GetComponent<Image>().sprite = PlayerCountButtonContainer.transform.GetChild(playerCount - 1).gameObject.GetComponent<Button>().spriteState.selectedSprite;

            // notify the mini game engine that this is the number of players
            MiniGame.NumberOfPlayers = playerCount;
        }

        public void SetIntensity(int intensity)
        {
            Debug.Log($"ArcadeMenu - SetIntensity: {intensity}");

            for (var i = 0; i < IntensityButtonContainer.transform.childCount; i++)
                IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Image>().sprite = IntensityIcons[i];

            IntensityButtonContainer.transform.GetChild(intensity - 1).gameObject.GetComponent<Image>().sprite = IntensityButtonContainer.transform.GetChild(intensity - 1).gameObject.GetComponent<Button>().spriteState.selectedSprite;

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
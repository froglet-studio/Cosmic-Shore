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

            var loadout = LoadoutSystem.LoadGameLoadout(SelectedGame.Mode).Loadout;

            // TODO: this is kludgy
            for (var i = 0; i < PlayerCountButtonContainer.transform.childCount; i++)
            {
                Debug.Log($"SelectGame - SelectedGame.MaxPlayers:{SelectedGame.MaxPlayers}, i:{i}, i < SelectedGame.MaxPlayers:{i < SelectedGame.MaxPlayers}");
                var playerCount = i + 1;
                PlayerCountButtonContainer.transform.GetChild(i).gameObject.SetActive(i < SelectedGame.MaxPlayers && i >= SelectedGame.MinPlayers - 1);
                PlayerCountButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.RemoveAllListeners();
                PlayerCountButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.AddListener(() => SetPlayerCount(playerCount));
                PlayerCountButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.AddListener(() => PlayerCountButtonContainer.GetComponent<MenuAudio>().PlayAudio());
            }
            SetPlayerCount(loadout.PlayerCount == 0 ? SelectedGame.MinPlayers : loadout.PlayerCount);

            var theFonzColor = new Color(.66f, .66f, .66f); // #AAAAAA
            // TODO: this is kludgy
            for (var i = 0; i < IntensityButtonContainer.transform.childCount; i++)
            {
                var intensity = i + 1;
                IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.RemoveAllListeners();
                if (intensity >= SelectedGame.MinIntensity && intensity <= SelectedGame.MaxIntensity)
                {
                    IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().enabled = true;
                    IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Image>().color = Color.white;
                    IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.AddListener(() => SetIntensity(intensity));
                    IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.AddListener(() => IntensityButtonContainer.GetComponent<MenuAudio>().PlayAudio());
                }
                else
                {
                    IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().enabled = false;
                    IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Image>().color = theFonzColor;
                }
            }
            SetIntensity(loadout.Intensity == 0 ? SelectedGame.MinIntensity : loadout.Intensity);
            SetIntensity(SelectedGame.MinIntensity);

            PopulateGameDetails();
            PopulateShipSelectionList(loadout.ShipType);
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

        void PopulateShipSelectionList(ShipTypes shipClass = ShipTypes.Any)
        {
            Debug.Log($"MiniGamesMenu - Populating Ship Select List - shipClass: {shipClass}");

            var selectedCaptain = SelectedGame.Captains[0];

            for (var i = 0; i < ShipSelectionGrid.childCount; i++)
            {
                Debug.Log($"MiniGamesMenu - Populating Ship Select List: {i}");
                var shipSelectionRow = ShipSelectionGrid.transform.GetChild(i);
                for (var j = 0; j < shipSelectionRow.transform.childCount; j++)
                {
                    Debug.Log($"MiniGamesMenu - Populating Ship Select List: {i},{j}");
                    var selectionIndex = (i * 3) + j;
                    // TODO: convert this to take a CaptainCard prefab and instantiate one rather than using the placeholder objects
                    var shipSelection = shipSelectionRow.transform.GetChild(j).gameObject;
                    if (selectionIndex < SelectedGame.Captains.Count)
                    {
                        var ship = SelectedGame.Captains[selectionIndex].Ship;
                        var captain = SelectedGame.Captains[selectionIndex];

                        if (ship.Class == shipClass)
                            selectedCaptain = captain;

                        Debug.Log($"MiniGamesMenu - Populating Ship Select List: {ship.Name}");

                        shipSelection.SetActive(true);
                        shipSelection.GetComponent<Image>().sprite = captain.Ship.CardSilohoutte;
                        shipSelection.GetComponent<Button>().onClick.RemoveAllListeners();
                        shipSelection.GetComponent<Button>().onClick.AddListener(() => SelectCaptain(captain));
                        shipSelection.GetComponent<Button>().onClick.AddListener(() => ShipSelectionGrid.GetComponent<MenuAudio>().PlayAudio());

                    }
                    else
                    {
                        // Deactive remaining
                        shipSelection.SetActive(false);
                    }
                }
            }

            StartCoroutine(SelectCaptainCoroutine(SelectedGame.Captains[0]));
        }

        IEnumerator SelectCaptainCoroutine(SO_Captain captain)
        {
            yield return new WaitForEndOfFrame();
            SelectCaptain(captain);
        }

        public void SelectCaptain(SO_Captain selectedCaptain)
        {
            Debug.Log($"SelectCaptain: {selectedCaptain.Name}");
            Debug.Log($"ShipSelectionContainer.childCount: {ShipSelectionGrid.childCount}");
            Debug.Log($"Ships.Count: {SelectedGame.Captains.Count}");

            SelectedShip = selectedCaptain.Ship;

            for (var i = 0; i < ShipSelectionGrid.childCount; i++)
            {
                var shipSelectionRow = ShipSelectionGrid.GetChild(i);
                for (var j = 0; j < shipSelectionRow.childCount; j++)
                {
                    var shipIndex = (i * 3) + j;
                    var shipButton = shipSelectionRow.GetChild(j).gameObject;

                    if (shipIndex >= SelectedGame.Captains.Count)
                        continue;
                    
                    if (SelectedGame.Captains[shipIndex] == selectedCaptain)
                        shipButton.GetComponent<Image>().sprite = selectedCaptain.Ship.CardSilohoutteActive;
                    else if (shipIndex < SelectedGame.Captains.Count)
                        shipButton.GetComponent<Image>().sprite = SelectedGame.Captains[shipIndex].Ship.CardSilohoutte;
                }
            }

            // notify the mini game engine that this is the ship to play
            MiniGame.PlayerShipType = SelectedShip.Class;
            MiniGame.ShipResources = selectedCaptain.InitialResourceLevels;
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
using CosmicShore.App.Systems.Favorites;
using CosmicShore.App.Systems.Loadout;
using CosmicShore.App.UI.Views;
using CosmicShore.Integrations.PlayFab.Economy;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Video;

namespace CosmicShore.App.UI.Modals
{
    public class ArcadeGameConfigureModal : ModalWindowManager
    {
        [SerializeField] ArcadeExploreView ArcadeExploreView;
        [SerializeField] ShipSelectionView ShipSelectionView;
        
        [SerializeField] TMPro.TMP_Text SelectedGameName;
        [SerializeField] TMPro.TMP_Text SelectedGameDescription;
        [SerializeField] GameObject SelectedGamePreviewWindow;
        [SerializeField] FavoriteIcon SelectedGameFavoriteIcon;

        [SerializeField] List<PlayerCountButton> PlayerCountButtons = new(4);
        [SerializeField] List<IntensitySelectButton> IntensityButtons = new(4);
        [Tooltip("If true, will filter out unowned ships from being available to play (MUST BE TRUE ON FOR PRODUCTION BUILDS")]
        [SerializeField] bool RespectInventoryForShipSelection = false;

        SO_ArcadeGame SelectedGame;
        VideoPlayer PreviewVideo;

        void OnEnable()
        {
            foreach (var intensityButton in IntensityButtons)
                intensityButton.OnSelect += SetIntensity;

            foreach (var playerCountButton in PlayerCountButtons)
                playerCountButton.OnSelect += SetPlayerCount;

            ShipSelectionView.OnSelect += SelectShip;
        }

        void OnDisable()
        {
            foreach (var intensityButton in IntensityButtons)
                intensityButton.OnSelect -= SetIntensity;

            foreach (var playerCountButton in PlayerCountButtons)
                playerCountButton.OnSelect -= SetPlayerCount;

            ShipSelectionView.OnSelect -= SelectShip;
        }

        public void SetSelectedGame(SO_ArcadeGame selectedGame)
        {
            SelectedGame = selectedGame;
            InitialializeIntensityButtons();
            InitialializePlayerCountButtons();
            InitializeShipSelectionView();
            InitializeGameDetailsView();

            // Populate configuration using loadout or default
            var loadout = LoadoutSystem.LoadGameLoadout(SelectedGame.Mode).Loadout;
            SetIntensity(loadout.Intensity == 0 ? SelectedGame.MinIntensity : loadout.Intensity);
            SetPlayerCount(loadout.PlayerCount == 0 ? SelectedGame.MinPlayers : loadout.PlayerCount);

            var index = loadout.ShipType == ShipTypes.Random ? 0 : ShipSelectionView.Models.IndexOf(ShipSelectionView.Models.Where(x => (x as SO_Ship).Class == loadout.ShipType).FirstOrDefault());
            if (index == -1) index = 0;

            ShipSelectionView.Select(index);
        }

        void InitialializeIntensityButtons()
        {
            for (var i = 0; i < 4; i++)
            {
                IntensityButtons[i].SetIntensityLevel(i + 1);
                IntensityButtons[i].SetActive(SelectedGame.MinIntensity <= i+1 && SelectedGame.MaxIntensity > i);
                IntensityButtons[i].SetSelected(false);
            }
        }
        void InitialializePlayerCountButtons()
        {
            for (var i = 0; i < 3; i++)
            {
                PlayerCountButtons[i].SetPlayerCount(i + 1);
                PlayerCountButtons[i].SetActive(i < SelectedGame.MaxPlayers && i >= SelectedGame.MinPlayers - 1);
                PlayerCountButtons[i].SetSelected(false);
            }
        }

        void InitializeShipSelectionView()
        {
            if (RespectInventoryForShipSelection)
            {
                List<SO_Captain> filteredCaptains = SelectedGame.Captains.Where(x => CaptainManager.Instance.UnlockedShips.Contains(x.Ship)).ToList();
                ShipSelectionView.AssignModels(filteredCaptains.ConvertAll(x => (ScriptableObject)x.Ship));
            }
            else
            {
                ShipSelectionView.AssignModels(SelectedGame.Captains.ConvertAll(x => (ScriptableObject)x.Ship));
            }
        }

        void InitializeGameDetailsView()
        {
            Debug.Log($"Populating Game Details: {SelectedGame.DisplayName}");
            Debug.Log($"Populating Game Details: {SelectedGame.Description}");
            Debug.Log($"Populating Game Details: {SelectedGame.Icon}");
            Debug.Log($"Populating Game Details: {SelectedGame.PreviewClip}");

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

        public void SelectShip(SO_Ship ship)
        {
            ArcadeExploreView.SelectShip(ship);
        }

        public void SetPlayerCount(int playerCount)
        {
            foreach (var button in PlayerCountButtons)
                button.SetSelected(button.Count == playerCount);;

            ArcadeExploreView.SetPlayerCount(playerCount);
        }

        public void SetIntensity(int intensity)
        {
            foreach (var button in IntensityButtons)
                button.SetSelected(button.Intensity == intensity);

            ArcadeExploreView.SetIntensity(intensity);
        }

        public void ToggleFavorite()
        {
            ArcadeExploreView.ToggleFavorite();

            SelectedGameFavoriteIcon.Favorited = FavoriteSystem.IsFavorited(SelectedGame.Mode);
        }
    }
}
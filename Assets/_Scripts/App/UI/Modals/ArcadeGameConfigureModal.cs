using CosmicShore.App.UI.Views;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.App.UI.Modals
{
    public class ArcadeGameConfigureModal : ModalWindowManager
    {
        [SerializeField] ArcadeExploreView ArcadeExploreView;
        [SerializeField] List<PlayerCountButton> PlayerCountButtons = new(4);
        [SerializeField] List<IntensitySelectButton> IntensityButtons = new(4);
        SO_ArcadeGame SelectedGame;

        void OnEnable()
        {
            foreach (var intensityButton in IntensityButtons)
                intensityButton.OnSelect += SetIntensity;

            foreach (var playerCountButton in PlayerCountButtons)
                playerCountButton.OnSelect += SetPlayerCount;
        }

        void OnDisable()
        {
            foreach (var intensityButton in IntensityButtons)
                intensityButton.OnSelect -= SetIntensity;

            foreach (var playerCountButton in PlayerCountButtons)
                playerCountButton.OnSelect -= SetPlayerCount;
        }

        public void SetSelectedGame(SO_ArcadeGame selectedGame)
        {
            SelectedGame = selectedGame;
            InitialializeIntensityButtons();
            InitialializePlayerCountButtons();
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

        public void SetPlayerCount(int playerCount)
        {
            foreach (var button in PlayerCountButtons)
                button.SetSelected(button.Count == playerCount);;

            ArcadeExploreView.SetPlayerCount(playerCount);
        }

        public void SetIntensity(int intensity)
        {
            Debug.LogError("Set Intensity Invoked");
            foreach (var button in IntensityButtons)
                button.SetSelected(button.Intensity == intensity);

            ArcadeExploreView.SetIntensity(intensity);
        }
    }
}
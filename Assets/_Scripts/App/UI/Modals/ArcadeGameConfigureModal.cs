using CosmicShore.App.UI.Views;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.App.UI.Modals
{
    public class ArcadeGameConfigureModal : ModalWindowManager
    {
        [SerializeField] ArcadeExploreView ArcadeExploreView;
        [SerializeField] List<IntensitySelectButton> IntensityButtons = new(4);
        SO_ArcadeGame SelectedGame;

        public void SetSelectedGame(SO_ArcadeGame selectedGame)
        {
            SelectedGame = selectedGame;
            InitialializeIntensityButtons();
        }

        void InitialializeIntensityButtons()
        {
            for (var i = 0; i < 4; i++)
            {
                IntensityButtons[i].SetActive(SelectedGame.MinIntensity <= i+1 && SelectedGame.MaxIntensity > i);
                IntensityButtons[i].SetSelected(false);
                IntensityButtons[i].SetIntensityLevel(i + 1);
            }
        }

        public void SetIntensity(int intensity)
        {
            foreach (var button in IntensityButtons)
                button.SetSelected(false);

            IntensityButtons[intensity - 1].SetSelected(true);

            ArcadeExploreView.SetIntensity(intensity);
        }
    }
}
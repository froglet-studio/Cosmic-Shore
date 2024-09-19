using CosmicShore.App.Systems;
using CosmicShore.App.UI;
using CosmicShore.App.UI.Modals;
using CosmicShore.Core;
using CosmicShore.Models.Enums;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public class HangarTrainingModal : ModalWindowManager
    {
        [SerializeField] Transform GameSelectionContainer;
        [SerializeField] Image ShipModelImage;
        [SerializeField] TMP_Text SelectedGameName;
        [SerializeField] TMP_Text SelectedGameDescription;
        [SerializeField] GameObject SelectedGamePreviewWindow;
        [SerializeField] HangarTrainingGameButton TrainingGameButton1;
        [SerializeField] HangarTrainingGameButton TrainingGameButton2;
        [SerializeField] GameplayRewardButton RewardButton;
        [SerializeField] List<IntensitySelectButton> IntensityButtons = new(4);

        SO_TrainingGame SelectedGame;
        int Intensity;

        void OnEnable()
        {
            foreach (var intensityButton in IntensityButtons)
                intensityButton.OnSelect += SetIntensity;
        }

        void OnDisable()
        {
            foreach (var intensityButton in IntensityButtons)
                intensityButton.OnSelect -= SetIntensity;
        }

        void InitialializeIntensityButtons()
        {
            for (var i = 0; i < 4; i++)
                IntensityButtons[i].SetIntensityLevel(i + 1);
        }

        public void SetTrainingGames(List<SO_TrainingGame> trainingGames)
        {
            InitialializeIntensityButtons();
            TrainingGameButton1.AssignGame(trainingGames[0]);
            TrainingGameButton2.AssignGame(trainingGames[1]);
            SelectGame(trainingGames[0]);
        }

        public void SelectGame(SO_TrainingGame selectedGame)
        {
            SelectedGame = selectedGame;

            Debug.Log($"SelectTainingGame: {SelectedGame.Game.DisplayName}");
            TrainingGameButton1.SetActive(TrainingGameButton1.TrainingGame == selectedGame);
            TrainingGameButton2.SetActive(TrainingGameButton2.TrainingGame == selectedGame);

            // Disable Intensity Buttons based on progress
            var trainingProgress = TrainingGameProgressSystem.GetGameProgress(selectedGame.Game.Mode);

            Debug.LogWarning($"trainingProgress.CurrentIntensity: {trainingProgress.CurrentIntensity}, Game.Mode:{selectedGame.Game.Mode}");

            for (var i = 0; i < 4; i++)
            {
                IntensityButtons[i].SetActive(i < trainingProgress.CurrentIntensity);

                if (trainingProgress.IsTierSatisfied(i + 1) && !trainingProgress.IsTierClaimed(i + 1))
                {
                    Debug.Log($"Claimable tier alert {i + 1}!!");
                    IntensityButtons[i].GetComponent<Image>().color = Color.green;

                }
            }

            PopulateTrainingGameDetails(trainingProgress.CurrentIntensity);
        }

        void PopulateTrainingGameDetails(int currentIntensity)
        {
            var game = SelectedGame.Game;
            Debug.Log($"Populating Training Details List: {game.DisplayName}");
            Debug.Log($"Populating Training  Details List: {game.Description}");
            Debug.Log($"Populating Training  Details List: {game.Icon}");
            Debug.Log($"Populating Training  Details List: {game.PreviewClip}");

            // Game details
            if (ShipModelImage != null) ShipModelImage.sprite = game.Icon;
            SelectedGameName.text = game.DisplayName;
            SelectedGameDescription.text = game.Description;

            // Intensity Selection
            SetIntensity(currentIntensity);

            // Rewards
            

            // Preview
            if (SelectedGamePreviewWindow != null)
            {
                for (var i = 2; i < SelectedGamePreviewWindow.transform.childCount; i++)
                    Destroy(SelectedGamePreviewWindow.transform.GetChild(i).gameObject);

                var preview = Instantiate(game.PreviewClip);
                preview.transform.SetParent(SelectedGamePreviewWindow.transform, false);
                SelectedGamePreviewWindow.SetActive(true);
                Canvas.ForceUpdateCanvases();
            }
        }

        public void SetIntensity(int intensity)
        {
            Intensity = intensity;

            switch (intensity)
            {
                case 1:
                    //rewardValue = SelectedGame.IntensityOneReward.Value.ToString();
                    RewardButton.gameObject.SetActive(false);
                    RewardButton.SetReward(SelectedGame.IntensityOneReward);
                    break;
                case 2:
                    //rewardValue = SelectedGame.IntensityTwoReward.Value.ToString();
                    RewardButton.gameObject.SetActive(false);
                    RewardButton.SetReward(SelectedGame.IntensityTwoReward);
                    break;
                case 3:
                    //rewardValue = SelectedGame.IntensityThreeReward.Value.ToString();
                    RewardButton.gameObject.SetActive(false);
                    RewardButton.SetReward(SelectedGame.IntensityThreeReward);
                    break;
                case 4:
                    //rewardValue = SelectedGame.IntensityFourReward.Value.ToString();
                    RewardButton.gameObject.SetActive(true);
                    RewardButton.SetReward(SelectedGame.IntensityFourReward);

                    break;
            }

            var trainingProgress = TrainingGameProgressSystem.GetGameProgress(SelectedGame.Game.Mode);
            if (trainingProgress.IsTierClaimed(intensity))
                RewardButton.MarkClaimed();
            else if (trainingProgress.IsTierSatisfied(intensity))
                RewardButton.MakeRewardAvailable();
            else
                RewardButton.MakeRewardUnavailable();

            RewardButton.SetTier(intensity);

            foreach (var button in IntensityButtons)
                button.SetSelected(false);

            IntensityButtons[intensity - 1].SetSelected(true);
        }

        public void LaunchSelectedGame()
        {
            var shipResources = new ResourceCollection();
            if (SelectedGame.ElementOne.Element == Element.Charge || SelectedGame.ElementTwo.Element == Element.Charge)
                shipResources.Charge = 1;
            
            if (SelectedGame.ElementOne.Element == Element.Mass || SelectedGame.ElementTwo.Element == Element.Mass)
                shipResources.Mass = 1;

            if (SelectedGame.ElementOne.Element == Element.Space || SelectedGame.ElementTwo.Element == Element.Space)
                shipResources.Space = 1;
            
            if (SelectedGame.ElementOne.Element == Element.Time || SelectedGame.ElementTwo.Element == Element.Time )
                shipResources.Time = 1;
            
            Arcade.Instance.LaunchTrainingGame(SelectedGame.Game.Mode, SelectedGame.ShipClass.Class, shipResources, Intensity, 1, false);
        }
    }
}

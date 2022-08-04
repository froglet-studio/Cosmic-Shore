using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using static StarWriter.Core.GameSetting;

/// <summary>
/// Controls the final score and high score display panel
/// </summary>
namespace StarWriter.Core
{
    public class FinalScoresBoard : MonoBehaviour
    {
        public int currentScore = 000;
        public int highScore = 000;

        // List of 0-9 sprites
        public List<Sprite> NumIcons = new List<Sprite>();

        public Image scoreHundredsPlace;
        public Image scoreTensPlace;
        public Image scoreOnesPlace;

        public Image highScoreHundredsPlace;
        public Image highScoreTensPlace;
        public Image hignScoreOnesPlace;

        public Image BedazzledHighScoreImage;

        private void OnEnable()
        {
            GameManager.onGameOver += OnGameOver;
            AdvertisementMenu.onDeclineAd += OnDeclineAd;
        }

        private void OnDisable()
        {
            GameManager.onGameOver -= OnGameOver;
            AdvertisementMenu.onDeclineAd -= OnDeclineAd;
        }

        private void OnGameOver()
        {
            currentScore = PlayerPrefs.GetInt(PlayerPrefKeys.score.ToString());
            highScore = PlayerPrefs.GetInt(PlayerPrefKeys.highScore.ToString());
            BedazzledHighScoreImage.gameObject.SetActive(ScoringManager.IsScoreBedazzleWorthy);
            DisplayCurrentScoreWithSprites();
            DisplayHighScoreWithSprites();
        }

        private void OnDeclineAd()
        {            
            currentScore = PlayerPrefs.GetInt(PlayerPrefKeys.score.ToString());
            highScore = PlayerPrefs.GetInt(PlayerPrefKeys.highScore.ToString());

            BedazzledHighScoreImage.gameObject.SetActive(ScoringManager.IsScoreBedazzleWorthy);
            DisplayCurrentScoreWithSprites();
            DisplayHighScoreWithSprites();
        }

        public void DisplayCurrentScoreWithSprites()
        {
            int hundreds = currentScore / 100;
            int tens = (currentScore % 100) / 10;
            int ones = (currentScore % 10);

            scoreHundredsPlace.sprite = NumIcons[hundreds];
            scoreTensPlace.sprite = NumIcons[tens];
            scoreOnesPlace.sprite = NumIcons[ones];       
        }

        public void DisplayHighScoreWithSprites()
        {
            int hundreds = highScore / 100;
            int tens = (highScore % 100) / 10;
            int ones = (highScore % 10);

            highScoreHundredsPlace.sprite = NumIcons[hundreds];
            highScoreTensPlace.sprite = NumIcons[tens];
            hignScoreOnesPlace.sprite = NumIcons[ones];
        }
    }
}
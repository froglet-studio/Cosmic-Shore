using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using UnityEngine.UI;

/// <summary>
/// Controls the final and high currentScore display panel
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

        public GameObject replayButton;

        GameManager gameManager;
        
        void Start()
        {
            gameManager = GameManager.Instance;

            currentScore = PlayerPrefs.GetInt("Score");
            highScore = PlayerPrefs.GetInt("High Score");

            DisplayCurrentScoreWithSprites();
            DisplayHighScoreWithSprites();

            replayButton.SetActive(true);
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

        public void OnClickReplayGameButtonPressed()
        {
            gameManager.RestartGame();
        }
    }
}



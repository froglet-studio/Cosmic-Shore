using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls the final and high score display panel
/// </summary>
namespace StarWriter.Core
{
    public class FinalScoresBoard : MonoBehaviour
    {
        public TextMeshProUGUI scoreText;
        public TextMeshProUGUI highScoreText;
        public GameObject replayButton;

        GameManager gameManager;
        
        void Start()
        {
            gameManager = GameManager.Instance;
            DisplayScores();
        }

        public void DisplayScores()
        {
            // check that all score text and buttons are enabled
            scoreText.gameObject.SetActive(true);
            highScoreText.gameObject.SetActive(true);
            replayButton.SetActive(true);

            // Set final and high score
            scoreText.text = "Score: " + PlayerPrefs.GetFloat("Score").ToString();
            highScoreText.text = "High Score: " + PlayerPrefs.GetFloat("High Score").ToString();
        }

        public void OnClickReplayGameButtonPressed()
        {
            gameManager.RestartGame();
        }
    }
}



using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

namespace StarWriter.Core
{
    public class FinalScoresBoard : MonoBehaviour
    {
        public TextMeshProUGUI scoreText;
        public TextMeshProUGUI highScoreText;
        public GameObject replayButton;
        public GameObject intensityMeter;

        private GameManager gameManager;



        void Start()
        {
            gameManager = GameManager.Instance;
            scoreText.text = "Score: " + PlayerPrefs.GetFloat("Score").ToString();
            highScoreText.text = "High Score: " + PlayerPrefs.GetFloat("High Score").ToString();
        }


        public void OnEndCameraPositionReached()
        {
            scoreText.gameObject.SetActive(true);
            highScoreText.gameObject.SetActive(true);
            replayButton.SetActive(true);
            intensityMeter.SetActive(false);

        }

        public void OnReplayGameButtonPressed()
        {
            SceneManager.LoadScene(2);
        }
    }
}



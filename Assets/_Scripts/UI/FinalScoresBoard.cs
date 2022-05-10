using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace StarWriter.Core
{
    public class FinalScoresBoard : MonoBehaviour
    {
        public TextMeshProUGUI scoreText;
        public TextMeshProUGUI highScoreText;

        private GameManager gameManager;



        void Start()
        {
            gameManager = GameManager.Instance;
            scoreText.text = PlayerPrefs.GetFloat("Score").ToString();
            highScoreText.text = PlayerPrefs.GetFloat("High Score").ToString();
        }

        public void OnQuitButtonPressed()
        {
            gameManager.OnQuitButtonPressed();
        }

        public void OnReplayGameButtonPressed()
        {
            gameManager.OnReplayButtonPressed();
        }
    }
}



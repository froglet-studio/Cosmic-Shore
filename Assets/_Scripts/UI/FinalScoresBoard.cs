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


        // Start is called before the first frame update
        void Start()
        {
            gameManager = GameManager.Instance;
            scoreText.text = PlayerPrefs.GetFloat("Score").ToString();
            highScoreText.text = PlayerPrefs.GetFloat("High Score").ToString();
        }

        // Update is called once per frame
        void Update()
        {

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



using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using static StarWriter.Core.GameSetting;

/// <summary>
/// Controls the final SinglePlayerScore and high SinglePlayerScore display panel
/// </summary>
namespace StarWriter.Core
{
    public class FinalScoresBoard : MonoBehaviour
    {
        public int currentScore = 000;
        public int highScore = 000;

        // List of 0-9 sprites
        public List<Sprite> NumIcons = new List<Sprite>();

        [SerializeField] Image[] ScoreDisplayImages;
        [SerializeField] Image[] HighScoreDisplayImages;

        private void OnEnable()
        {
            GameManager.onGameOver += OnGameOver;
        }

        private void OnDisable()
        {
            GameManager.onGameOver -= OnGameOver;
        }

        private void OnGameOver()
        {
            currentScore = PlayerPrefs.GetInt(PlayerPrefKeys.Score.ToString());
            highScore = PlayerPrefs.GetInt(PlayerPrefKeys.HighScore.ToString());
            DisplayCurrentScoreWithSprites();
            DisplayHighScoreWithSprites();
        }

        int Magnitude(int value)
        {
            int magnitude = 0;
            while (value > 0)
            {
                magnitude++;
                value = value / 10;
            };

            return magnitude;
        }

        public void DisplaySpritedNumber(int value, Image[] display)
        {
            var magnitude = Magnitude(value);

            if (magnitude >= display.Length)
            {
                Debug.LogError($"DisplaySpritedNumber - Not enough images in display array to show number - value's magnitude:{magnitude}, display array length:{display.Length} ");
                return;
            }

            // Below loop is implementing this algo, then assigning the associated sprite
            // int hundreds = (currentScore % 1000) / 100;
            // int tens     = (currentScore % 100) / 10;
            // int ones     = (currentScore % 10) / 1;

            for (var i=0; i <= magnitude; i++)
            {
                int powOfTenModulo = (int)Mathf.Pow(10, i+1);
                int powOfTenDivision = (int)Mathf.Pow(10, i);
                int valueAtMagnitude = (value % powOfTenModulo) / powOfTenDivision;

                if (display[i] == null)
                {
                    Debug.LogError($"DisplaySpritedNumber - Image element of display array was null, exiting - image index: {i}");
                    return;
                }

                display[i].sprite = NumIcons[valueAtMagnitude];
            }
        }

        public void DisplayCurrentScoreWithSprites()
        {
            DisplaySpritedNumber(currentScore, ScoreDisplayImages);
        }

        public void DisplayHighScoreWithSprites()
        {
            DisplaySpritedNumber(highScore, HighScoreDisplayImages);
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;

public class ScoringManager : MonoBehaviour
{
    [SerializeField]
    int score = 0;

    public TextMeshProUGUI scoreText;

    private void OnEnable()
    {
        IntensitySystem.onPlayerIntensityOverflow += AddExcessIntensityToScore;
        IntensitySystem.zeroIntensity += GameOver;
        MutonPopUp.AddToScore += AddMutonBous;
    }

    private void OnDisable()
    {
        IntensitySystem.onPlayerIntensityOverflow -= AddExcessIntensityToScore;
        IntensitySystem.zeroIntensity -= GameOver;
        MutonPopUp.AddToScore -= AddMutonBous;
    }

    public void AddExcessIntensityToScore(string uuid, int amount) // TODO Needs to be private... put events on mutons and trails to handle
    {
        if (uuid == "admin") { score += amount; }

        UpdateScoreBoard(score);
    }
    public void UpdateScoreBoard(int value)
    {

        scoreText.text = value.ToString("D3"); // score text located on the intensity bar
    }

    private void AddMutonBous(string uuid, int amount)
    {
        if (uuid == "admin") { score += amount; }

        UpdateScoreBoard(score);
    }

    private void GameOver()
    {
        PlayerPrefs.SetInt("Score", score);
        //Compares Score to High Score and saves the highest value
        if (PlayerPrefs.GetInt("High Score") < score)
        {
            PlayerPrefs.SetInt("High Score", score);
        }
        PlayerPrefs.Save();   
    }
}

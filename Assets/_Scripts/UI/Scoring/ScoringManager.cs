using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using StarWriter.Core;

public class ScoringManager : MonoBehaviour
{
    [SerializeField]
    int score = 0;

    public TextMeshProUGUI scoreText;

    private void OnEnable()
    {
        FuelSystem.onPlayerFuelOverflow += AddExcessFuelToScore;
        FuelSystem.zeroFuel += GameOver;
        MutonPopUp.AddToScore += AddMutonBous;
        GameManager.onExtendPlayGame += KeepOldScore;
    }

    

    private void OnDisable()
    {
        FuelSystem.onPlayerFuelOverflow += AddExcessFuelToScore;
        FuelSystem.zeroFuel += GameOver;
        MutonPopUp.AddToScore -= AddMutonBous;
        GameManager.onExtendPlayGame -= KeepOldScore;
    }

    private void AddExcessFuelToScore(string uuid, int amount)
    {
        if (uuid == "admin") { score += amount; }

        UpdateScoreBoard(score);
    }
    public void UpdateScoreBoard(int value)
    {
        scoreText.text = value.ToString("D3"); // score text located on the fuel bar
    }

    private void AddMutonBous(string uuid, int amount)
    {
        if (uuid == "admin") { score += amount; }

        UpdateScoreBoard(score);
    }

    private void KeepOldScore()
    {
        score = PlayerPrefs.GetInt("Score");
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

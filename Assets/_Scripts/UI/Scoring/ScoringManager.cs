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
    [SerializeField]
    private int extendedLifeScore;
    [SerializeField]
    private int extendedLifeHighScore;
    [SerializeField]
    private float extralifeModifier = 0.8f;

    public TextMeshProUGUI scoreText;

    public delegate void OnAdQualifyEvent(bool hotness);
    public static event OnAdQualifyEvent onAdQualify;

    public delegate void OnAdDisqualifyEvent();
    public static event OnAdDisqualifyEvent onAdDisqualify;

    private void OnEnable()
    {
        FuelSystem.onPlayerFuelOverflow += AddExcessFuelToScore;
        FuelSystem.zeroFuel += GameOver;
        MutonPopUp.AddToScore += AddMutonBous;
        GameManager.onExtendPlayGame += ExtendGamePlay;
    }

    

    private void OnDisable()
    {
        FuelSystem.onPlayerFuelOverflow += AddExcessFuelToScore;
        FuelSystem.zeroFuel += GameOver;
        MutonPopUp.AddToScore -= AddMutonBous;
        GameManager.onExtendPlayGame -= ExtendGamePlay;
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

    private void ExtendGamePlay()
    {
        score = PlayerPrefs.GetInt("Score");
        PlayerPrefs.SetInt(GameSetting.PlayerPrefKeys.getsExtraLife.ToString(), 0);  // set false 
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
        QualifyForExtendedLifeAd();
    }

    public void QualifyForExtendedLifeAd()
    {
        bool hotness;
        if ((extralifeModifier * (float)PlayerPrefs.GetInt("Extended High Score") <= (float)extendedLifeScore) && 
                (PlayerPrefs.GetInt(GameSetting.PlayerPrefKeys.getsExtraLife.ToString()) == 1))
        {
            hotness = true;
            onAdQualify?.Invoke(hotness);
            //TODO do ad button with hotness and muted decline button
        }
        else if (PlayerPrefs.GetInt(GameSetting.PlayerPrefKeys.getsExtraLife.ToString()) == 1)
        {
            hotness = false;
            onAdQualify?.Invoke(hotness);
            //TODO do regular ad button and regular decline button
        }
        else
        {
            onAdDisqualify?.Invoke();
        }

    }

    private void ExtendedLifeGameOver()
    {
        extendedLifeScore = score;
        if (PlayerPrefs.GetInt("Extended High Score") < extendedLifeScore)
        {
            PlayerPrefs.SetInt("Extended High Score", extendedLifeScore);
        }
    }

   
}

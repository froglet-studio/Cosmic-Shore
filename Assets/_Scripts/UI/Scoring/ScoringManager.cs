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
    bool bedazzled; //wether or not the watchAdButton is amped up

    public TextMeshProUGUI scoreText;

    public delegate void OnGameOverEvent(bool bedazzled, bool advertisement);
    public static event OnGameOverEvent onGameOver;

    private void OnEnable()
    {
        GameManager.onDeath += OnDeath;
        MutonPopUp.AddToScore += AddMutonBous;
        GameManager.onExtendPlayGame += ExtendGamePlay;
    }

    private void OnDisable()
    {
        GameManager.onDeath -= OnDeath;
        MutonPopUp.AddToScore -= AddMutonBous;
        GameManager.onExtendPlayGame -= ExtendGamePlay;
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

    private void OnDeath()
    {

        if (PlayerPrefs.GetInt(GameSetting.PlayerPrefKeys.adsEnabled.ToString()) == 1)
        {
            bedazzled = ((PlayerPrefs.GetInt("Single Life High Score") * extralifeModifier) <= score);
            
        }
        if (PlayerPrefs.GetInt("Single Life High Score") < score)
        {
            PlayerPrefs.SetInt("Single LifeHigh Score", score);
        }

        UpdatePlayerPrefScores();
        QualifyForExtendedLifeAd();
    }

    public void QualifyForExtendedLifeAd()
    {
        bool advertisements = (PlayerPrefs.GetInt(GameSetting.PlayerPrefKeys.adsEnabled.ToString()) == 1);
        if ((extralifeModifier * (float)PlayerPrefs.GetInt("Single Life High Score") <= (float)extendedLifeScore) && 
                (PlayerPrefs.GetInt(GameSetting.PlayerPrefKeys.adsEnabled.ToString()) == 1))
        {
            bedazzled = true;
            advertisements = true;
            onGameOver?.Invoke(bedazzled, advertisements);
        }
        else if (PlayerPrefs.GetInt(GameSetting.PlayerPrefKeys.adsEnabled.ToString()) == 1)
        {
            bedazzled = false;
            advertisements = true;
            onGameOver?.Invoke(bedazzled, advertisements);
        }
        else
        {
            bedazzled = false;
            advertisements = false;
            onGameOver?.Invoke(bedazzled, advertisements);
        }

    }

    public void UpdatePlayerPrefScores()
    {
        PlayerPrefs.SetInt("Score", score);

        //Compares Score to High Score and saves the highest value
        if (PlayerPrefs.GetInt("High Score") < score)
        {
            PlayerPrefs.SetInt("High Score", score);
        }
        PlayerPrefs.Save();
    }

    private void ExtendGamePlay()
    {
        score = PlayerPrefs.GetInt("Score");
        PlayerPrefs.SetInt(GameSetting.PlayerPrefKeys.adsEnabled.ToString(), 0);  // set false 
    }

    //private void ExtendedLifeGameOver()
    //{
    //    extendedLifeScore = score;
    //    if (PlayerPrefs.GetInt("Extended High Score") < extendedLifeScore)
    //    {
    //        PlayerPrefs.SetInt("Extended High Score", extendedLifeScore);
    //    }

//    PlayerPrefs.SetInt("Score", score);
       
//        //Compares Score to High Score and saves the highest value
//        if (PlayerPrefs.GetInt("High Score") < score)
//        {
//            PlayerPrefs.SetInt("High Score", score);
//            if (PlayerPrefs.GetInt("Single Life High Score") < score)
//            {
//                PlayerPrefs.SetInt("Single LifeHigh Score", score);
//            }
//        }
//        PlayerPrefs.Save();


//QualifyForExtendedLifeAd();
    //} 
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using StarWriter.Core;
using System;

/// <summary>
/// Keeps track of Intesity to determine a win or lose condition
/// </summary>

public class ScoreBoard : MonoBehaviour
{

    //Player
    [SerializeField]
    float maxIntensity = 100f;
    [SerializeField]
    float currentIntesity;
    [SerializeField]
    float countDownRate;
    [SerializeField]
    float score = 0f;
    [SerializeField]
    float currentIntensity;

    

    //AI   
    [SerializeField]
    float aiScore = 0f;
    [SerializeField]
    float currentAiIntensity;

    public TextMeshProUGUI scoreText;

    private void OnEnable()
    {
        Trail.OnTrailCollision += ChangeIntensity;
        MutonPopUp.OnMutonPopUpCollision += ChangeIntensity;
    }

    private void OnDisable()
    {
        Trail.OnTrailCollision -= ChangeIntensity;
        MutonPopUp.OnMutonPopUpCollision -= ChangeIntensity;
    }


    // Start is called before the first frame update
    void Start()
    {
        currentIntensity = maxIntensity;
        currentAiIntensity = maxIntensity;
        StartCoroutine(CountDownCoroutine());
    }

    IEnumerator CountDownCoroutine()
    {
        while (currentIntensity != 0)
        {
            yield return new WaitForSeconds(1);
            ChangeIntensity(countDownRate, "admin");
            ChangeIntensity(countDownRate, "ai");
        }
        
    }
   
    private void ChangeIntensity(float amount, string uuid)
    {
        
        if (uuid == "admin")
        {
            if (currentIntensity != 0) { currentIntensity += amount; }
            if (currentIntensity > 100)
            {
                float excessIntesity = currentIntensity - 100f;
                AddExcessIntensityToScore(excessIntesity, "admin");
                currentIntensity = 100;
            }
            if (currentIntensity <= 0)
            {
                scoreText.text = "you dead" + System.Environment.NewLine +
                                 "Final Score: " + score.ToString() + System.Environment.NewLine +
                                 "Bob's Score: " + aiScore.ToString();
                currentIntensity = 0;
            }
            if (currentIntensity != 0) { UpdateCurrentIntensity(currentIntensity, uuid); }

        }
        if (uuid == "ai" && currentIntensity != 0)
        {
            currentAiIntensity += amount;
            if (currentAiIntensity >= 100)
            {
                currentAiIntensity += amount;
                float excessIntesity = currentAiIntensity - 100f;
                AddExcessIntensityToScore(excessIntesity, "ai");
                currentAiIntensity = 100;
            }
            if (currentAiIntensity <= 0)
            {
                currentAiIntensity = 0;
            }
            UpdateCurrentIntensity(currentAiIntensity, uuid);
        } 
        
        
    }

    private void UpdateCurrentIntensity(float amount, string uuid)
    {
        if (uuid == "admin") { currentIntensity = amount; }
        if (uuid == "ai") { currentAiIntensity = amount; }    
        scoreText.text = "Intensity: " + currentIntensity.ToString() + System.Environment.NewLine
                      + "Your Score: " + score.ToString() + System.Environment.NewLine
                      + "Bob's Int: " + currentAiIntensity.ToString() + System.Environment.NewLine
                      + "Bob's Score: " + aiScore.ToString();
    }

    private void AddExcessIntensityToScore(float amount, string uuid)
    {
        if (uuid == "admin") { score += amount; }
        if (uuid == "ai") { aiScore += amount; }
        
    }

    private void OnDestroy()
    {
        PlayerPrefs.SetFloat("Score", score);
        if(PlayerPrefs.GetFloat("High Score") <= 0)
        {
            PlayerPrefs.SetFloat("High Score", score);
        }
        
        if (PlayerPrefs.GetFloat("High Score") >= 0)
        {
            float highScore = PlayerPrefs.GetFloat("High Score");
            if(highScore >= score) { return; }

            PlayerPrefs.SetFloat("High Score", highScore);
        }

            
    }

}

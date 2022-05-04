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

    [SerializeField]
    float maxIntesity = 100f;
    [SerializeField]
    float countDownRate;

    [SerializeField]
    float score = 0f;
    [SerializeField]
    float currentIntesity;

    
    [SerializeField]
    float aiScore = 0f;
    [SerializeField]
    float currentAiIntesity;

    public TextMeshProUGUI scoreText;

    private void OnEnable()
    {
        Trail.OnTrailCollision += ChangeIntesity;
        MutonPopUp.OnMutonPopUpCollision += ChangeIntesity;
    }

    private void OnDisable()
    {
        Trail.OnTrailCollision -= ChangeIntesity;
        MutonPopUp.OnMutonPopUpCollision -= ChangeIntesity;
    }


    // Start is called before the first frame update
    void Start()
    {
        currentIntesity = maxIntesity;
        currentAiIntesity = maxIntesity;
        StartCoroutine(CountDownCoroutine());
    }

    IEnumerator CountDownCoroutine()
    {
        while (currentIntesity != 0)
        {
            yield return new WaitForSeconds(1);
            ChangeIntesity(countDownRate, "admin");
            ChangeIntesity(countDownRate, "ai");
        }
        
    }
   
    private void ChangeIntesity(float amount, string uuid)
    {
        
        if (uuid == "admin")
        {
            if (currentIntesity != 0) { currentIntesity += amount; }
            if (currentIntesity > 100)
            {
                float excessIntesity = currentIntesity - 100f;
                AddExcessIntesityToScore(excessIntesity, "admin");
                currentIntesity = 100;
            }
            if (currentIntesity <= 0)
            {
                scoreText.text = "you dead" + System.Environment.NewLine +
                                 "Final Score: " + score.ToString() + System.Environment.NewLine +
                                 "Bob's Score: " + aiScore.ToString();
                currentIntesity = 0;
            }
            if (currentIntesity != 0) { UpdateCurrentIntesity(currentIntesity, uuid); }

        }
        if (uuid == "ai" && currentIntesity != 0)
        {
            currentAiIntesity += amount;
            if (currentAiIntesity >= 100)
            {
                currentAiIntesity += amount;
                float excessIntesity = currentAiIntesity - 100f;
                AddExcessIntesityToScore(excessIntesity, "ai");
                currentAiIntesity = 100;
            }
            if (currentAiIntesity <= 0)
            {
                currentAiIntesity = 0;
            }
            UpdateCurrentIntesity(currentAiIntesity, uuid);
        } 
        
        
    }

    private void UpdateCurrentIntesity(float amount, string uuid)
    {
        if (uuid == "admin") { currentIntesity = amount; }
        if (uuid == "ai") { currentAiIntesity = amount; }    
        scoreText.text = "Intensity: " + currentIntesity.ToString() + System.Environment.NewLine
                      + "Your Score: " + score.ToString() + System.Environment.NewLine
                      + "Bob's Int: " + currentAiIntesity.ToString() + System.Environment.NewLine
                      + "Bob's Score: " + aiScore.ToString();
    }

    private void AddExcessIntesityToScore(float amount, string uuid)
    {
        if (uuid == "admin") { score += amount; }
        if (uuid == "ai") { aiScore += amount; }
        
    }

}

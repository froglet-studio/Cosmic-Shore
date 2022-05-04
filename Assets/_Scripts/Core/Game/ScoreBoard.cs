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
    private float score = 0f;
    [SerializeField]
    private float maxIntesity = 100f;
    [SerializeField]
    private float currentIntesity;

    public TextMeshProUGUI scoreText;

    private void OnEnable()
    {
        Trail.OnTrailCollision += GainIntesity;
        MutonPopUp.OnMutonPopUpCollision += GainIntesity;
    }

    private void OnDisable()
    {
        Trail.OnTrailCollision -= LoseIntesity;
        MutonPopUp.OnMutonPopUpCollision -= LoseIntesity;
    }


    // Start is called before the first frame update
    void Start()
    {
        currentIntesity = maxIntesity;
    }

    private void LoseIntesity(float amount, string uuid)
    {
        currentIntesity -= amount;
        if (currentIntesity <= 0)
        {
            currentIntesity = 0;
        }
        UpdateCurrentIntesity(currentIntesity, uuid);
    }

    private void GainIntesity(float amount, string uuid)
    {
        currentIntesity += amount;
        if (currentIntesity >= 100)
        {
            float excessIntesity = currentIntesity - 100f;
            AddExcessIntesityToScore(excessIntesity);
            currentIntesity = 100;

        }
        UpdateCurrentIntesity(currentIntesity, uuid);
    }

    private void UpdateCurrentIntesity(float amount, string uuid)
    {
        currentIntesity = amount;
        
    }

    private void AddExcessIntesityToScore(float amount)
    {
        score += amount;
        scoreText.text = score.ToString();
    }

    
}

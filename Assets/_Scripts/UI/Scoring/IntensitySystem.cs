using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;

public class IntensitySystem : MonoBehaviour
{
    #region Events
    public delegate void OnInensityOverflow(string uuid, int amount);
    public static event OnInensityOverflow onPlayerIntensityOverflow;

    public delegate void OnIntensityChangeEvent(string uuid, float intensity);
    public static event OnIntensityChangeEvent onIntensityChange;

    public delegate void OnGameOverEvent();
    public static event OnGameOverEvent gameOver;
    
    #endregion
    #region Floats
    [Tooltip("Initial and Max intensity level from 0-1")]
    [SerializeField]
    [Range(0, 1)]
    float maxIntensity = 1f;
    [Tooltip("Current intensity level from 0-1")]
    [SerializeField]
    [Range(0, 1)]
    float currentIntensity;
    
    [SerializeField]
    float rateOfIntesityChange = -0.02f;
    
    #endregion

    [SerializeField]
    string uuidOfPlayer = "";

    public float CurrentIntensity { get => currentIntensity; }

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

    void Start()
    {
        currentIntensity = maxIntensity;
        StartCoroutine(CountDownCoroutine());
    }

    IEnumerator CountDownCoroutine() // intensity
    {
        while (currentIntensity != 0)
        {
            yield return new WaitForSeconds(1);
            ChangeIntensity("admin", rateOfIntesityChange); //Only effects current player

        }
    }
    

    private void ChangeIntensity(string uuid, float amount)
    {
        uuidOfPlayer = uuid;  //Recieves uuid of from Collision Events
        if (currentIntensity != 0) { currentIntensity += amount; }
        if (currentIntensity > 1f)
        {
            int excessIntesity = (int)(currentIntensity - 1f);
            AddExcessIntensityToScore(uuidOfPlayer, excessIntesity);  //Sending excess to Score Manager
            currentIntensity = 1;
        }
        if (currentIntensity <= 0)
        {
            currentIntensity = 0;
            GameOver();
        }
        if (currentIntensity != 0) 
        { 
            UpdateCurrentIntensity(uuidOfPlayer, currentIntensity);
            UpdateIntensityBar(uuid, currentIntensity);
        }
        
    }

    private void AddExcessIntensityToScore(string uuidOfPlayer, int excessIntesity)
    {
        if(onPlayerIntensityOverflow != null) { onPlayerIntensityOverflow(uuidOfPlayer, excessIntesity); }
    }

    private void UpdateIntensityBar(string uuidOfPlayer, float currentIntensity)
    {
        if(onIntensityChange != null) { onIntensityChange(uuidOfPlayer, currentIntensity); }
    }

    private void UpdateCurrentIntensity(string uuid, float amount)
    {
        if (uuid == "admin") { currentIntensity = amount; }
       
    }

    private void GameOver()
    {
        gameOver?.Invoke();
    }

    private void OnDestroy()
    {
        //intensityMeter.SetActive(false);
    }

    
}

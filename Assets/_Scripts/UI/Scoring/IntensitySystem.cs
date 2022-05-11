using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;

public class IntensitySystem : MonoBehaviour
{

    public delegate void OnInensityOverflow(string uuid, float amount);
    public static event OnInensityOverflow onPlayerIntensityOverflow;

    public delegate void OnIntensityChangeEvent(string uuid, float amout);
    public static event OnIntensityChangeEvent onIntensityChange;
   

    //Player
    [SerializeField]
    float maxIntensity = 100f;
    [SerializeField]
    float currentIntensity;
    [SerializeField]
    string uuidOfPlayer = "admin";


    [SerializeField]
    float rateOfIntesityChange = -1f;

    //public TextMeshProUGUI intensityText;

    //public GameObject intensityMeter;

   

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

    IEnumerator CountDownCoroutine()
    {
        while (currentIntensity != 0)
        {
            yield return new WaitForSeconds(1);
            ChangeIntensity(uuidOfPlayer, rateOfIntesityChange);

        }
    }
    

    private void ChangeIntensity(string uuid, float amount)
    { 
        if (currentIntensity != 0) { currentIntensity += amount; }
        if (currentIntensity > 100)
        {
            float excessIntesity = currentIntensity - 100f;
            AddExcessIntensityToScore(uuidOfPlayer, excessIntesity);  //Sending excess to Score Manager
            currentIntensity = 100;
        }
        if (currentIntensity <= 0)
        {
            currentIntensity = 0;
        }
        if (currentIntensity != 0) 
        { 
            UpdateCurrentIntensity(uuidOfPlayer, currentIntensity);
            UpdateIntensityBar(uuid, currentIntensity);
        }
        
    }

    private void AddExcessIntensityToScore(string uuidOfPlayer, float excessIntesity)
    {
        if(onPlayerIntensityOverflow != null) { onPlayerIntensityOverflow(uuidOfPlayer, excessIntesity); }
    }

    private void UpdateIntensityBar(string uuidOfPlayer, float amount)
    {
        if(onIntensityChange != null) { onIntensityChange(uuidOfPlayer, currentIntensity); }
    }

    private void UpdateCurrentIntensity(string uuid, float amount)
    {
        if (uuid == "admin") { currentIntensity = amount; }
       
    }

    private void OnDestroy()
    {
        //intensityMeter.SetActive(false);
    }

    
}

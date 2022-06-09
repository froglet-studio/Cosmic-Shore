 using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New_Tutorial_Stage", menuName = "Create SO/Tutorial Stage")]
public class TutorialStage : ScriptableObject
{
    [SerializeField]
    private string stageName;
    [SerializeField]
    private NarrationLine lineOne;
    [SerializeField]
    private float lineOneDisplayTime;
    [SerializeField]
    private bool isStarted;
    [SerializeField]
    private bool hasAnotherAttempt;
    [SerializeField]
    private bool hasMuton;
    [SerializeField]
    private bool hasActiveMuton;
    [SerializeField]
    private bool hasFuelBar;
    [SerializeField]
    private bool hasCompleted;
    [SerializeField]
    private GameObject uiPanel;

    public string StageName { get => stageName; set => stageName = value; }
    public NarrationLine LineOne { get => lineOne; set => lineOne = value; }
    public float LineOneDisplayTime { get => lineOneDisplayTime; set => lineOneDisplayTime = value; }
    public bool IsStarted { get => isStarted; set => isStarted = value; }
    public bool HasAnotherAttempt { get => hasAnotherAttempt; set => hasAnotherAttempt = value; }
    public bool HasMuton { get => hasMuton; set => hasMuton = value; }
    public bool HasActiveMuton { get => hasActiveMuton; set => hasActiveMuton = value; }
    public bool HasFuelBar { get => hasFuelBar; set => hasFuelBar = value; }
    public bool HasCompleted { get => hasCompleted; set => hasCompleted = value; }
    public GameObject UiPanel { get => uiPanel; set => uiPanel = value; }
   

    public void Begin()
    {
        isStarted = true;
        if(uiPanel != null) { uiPanel.SetActive(true); }
        
    }

    public void RetryOnce()
    {
        hasAnotherAttempt = false; // only allows 1 retry
    }

    public void End()
    {
        hasCompleted = true;
        if (uiPanel != null) { uiPanel.SetActive(false); }
    }
}

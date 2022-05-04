using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TutorialStage : ScriptableObject
{
    string stageName;

    bool isStarted;
    bool hasCompleted;

    GameObject uiPanel;

    public string StageName { get => stageName; set => stageName = value; }
    public bool IsStarted { get => isStarted; set => isStarted = value; }
    public bool HasCompleted { get => hasCompleted; set => hasCompleted = value; }
    public GameObject UiPanel { get => uiPanel; set => uiPanel = value; }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New_Tutorial_Stage", menuName = "Create SO/Tutorial Stage")]
public class TutorialStage : ScriptableObject
{
    [SerializeField]
    private string stageName;
    [SerializeField]
    private bool isStarted;
    [SerializeField]
    private bool hasCompleted;
    [SerializeField]
    private GameObject uiPanel;

    public string StageName { get => stageName; set => stageName = value; }
    public bool IsStarted { get => isStarted; set => isStarted = value; }
    public bool HasCompleted { get => hasCompleted; set => hasCompleted = value; }
    public GameObject UiPanel { get => uiPanel; set => uiPanel = value; }

    public void Begin()
    {
        isStarted = true;
        uiPanel.SetActive(true);
    }

    public void End()
    {
        hasCompleted = true;
        uiPanel.SetActive(false);
    }
}

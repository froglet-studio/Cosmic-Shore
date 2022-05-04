using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core;
using System;

public class TutorialManager : MonoBehaviour
{
    [SerializeField]
    public GameObject LearnPitchPanel;
    [SerializeField]
    public GameObject LearnYawPanel;
    [SerializeField]
    public GameObject LearnRollPanel;
    [SerializeField]
    public GameObject LearnThrottlePanel;
    [SerializeField]
    public GameObject LearnGyroPanel;

    private bool hasLearnedPitch = false;
    private bool hasLearnedYaw = false;
    private bool hasLearnedRoll = false;
    private bool hasLearnedThrottle = false;
    private bool hasLearnedGyro = false;
    private bool hasCompletedTutorial = false;

    private GameManager gameManager;

    // Start is called before the first frame update
    void Start()
    {
        gameManager = GameManager.Instance;
        hasCompletedTutorial = gameManager.HasCompletedTutorial;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (!hasCompletedTutorial)
        {
            hasCompletedTutorial = AskIfWantToDoTutorial();
        }
        if (!hasLearnedPitch)
        {
            LearnPitch();
        }
        if(hasLearnedPitch && !hasLearnedYaw)
        {
            LearnYaw();
        }
        if(hasLearnedYaw && !hasLearnedRoll)
        {
            LearnRoll();
        }
        if(hasLearnedRoll && !hasLearnedThrottle)
        {
            LearnThrottle();
        }
        if(hasLearnedThrottle && !hasLearnedGyro)
        {
            LearnGryo();
        }
        if (hasLearnedGyro)
        {
            CompleteTutorial();
        }
    }

    private bool AskIfWantToDoTutorial()
    {
        //TODO Create Pop up yes no on game manager
        return true;
    }

    // Learn pitch control - both finger contacts up or down together
    public void LearnPitch()
    {
        ActivatePanel(LearnPitchPanel.name);
        //TODO Measure contacts have moved up or down together
        hasLearnedPitch = true;
    }
    //Learn yaw control - both finger contacts left or right together
    public void LearnYaw()
    {
        ActivatePanel(LearnYawPanel.name);
        hasLearnedYaw = true;
    }
    //Learn roll control - measures difference in Y axix screenspace between left and right finger contacts
    public void LearnRoll()
    {
        ActivatePanel(LearnRollPanel.name);
        hasLearnedRoll = true;
    }
    //Learn throttle control - measures difference in X axix screenspace between left and right finger contacts
    public void LearnThrottle()
    {
        ActivatePanel(LearnThrottlePanel.name);
        hasLearnedThrottle = true;
    }
    //Learn yaw control - both finger contacts left or right together
    public void LearnGryo()
    {
        ActivatePanel(LearnGyroPanel.name);
        hasLearnedGyro = true;
    }
    //Tells GameSettings and GameManager that Tutorial has been completed
    public void CompleteTutorial()
    {
        hasCompletedTutorial = true;
        gameManager.HasCompletedTutorial = hasCompletedTutorial;
        Destroy(this);
    }

    public void ActivatePanel(string panelToBeActivated)
    {
        LearnPitchPanel.SetActive(panelToBeActivated.Equals(LearnPitchPanel.name));
        LearnYawPanel.SetActive(panelToBeActivated.Equals(LearnYawPanel.name));
        LearnRollPanel.SetActive(panelToBeActivated.Equals(LearnRollPanel.name));
        LearnThrottlePanel.SetActive(panelToBeActivated.Equals(LearnThrottlePanel.name));
        LearnGyroPanel.SetActive(panelToBeActivated.Equals(LearnGyroPanel.name));
    }
}

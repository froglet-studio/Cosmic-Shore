using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core;

public class Tutorial : MonoBehaviour
{
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
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (gameManager.HasCompletedTutorial)
        {
            
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
    // Learn pitch control - both finger contacts up or down together
    public void LearnPitch()
    {
        //TODO Measure contacts have moved up or down together
        hasLearnedPitch = true;
    }
    //Learn yaw control - both finger contacts left or right together
    public void LearnYaw()
    {
        hasLearnedYaw = true;
    }
    //Learn yaw control - both finger contacts left or right together
    public void LearnRoll()
    {
        hasLearnedRoll = true;
    }
    //Learn yaw control - both finger contacts left or right together
    public void LearnThrottle()
    {

    }
    //Learn yaw control - both finger contacts left or right together
    public void LearnGryo()
    {

    }
    //Tells GameSettings and GameManager that Tutorial has been completed
    public void CompleteTutorial()
    {

    }
}

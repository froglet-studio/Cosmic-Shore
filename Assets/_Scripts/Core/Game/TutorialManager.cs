using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core;
using UnityEngine.SceneManagement;

public class TutorialManager : MonoBehaviour
{
    public List<GameObject> tutorialPanels;

    public List<TutorialStage> tutorialStages; //SO Assests

    public List<bool> tutorialTestsGrading;

    public bool hasCompletedTutorial = false;

    private int index = 0;

    // Start is called before the first frame update
    void Start()
    {
        //Ref panels in scene to the SO Assest and clear bools
        foreach (TutorialStage stage in tutorialStages){
            tutorialStages[index].UiPanel = tutorialPanels[index];
            tutorialStages[index].IsStarted = tutorialStages[index].HasCompleted = false;
            index++;
        }

        index = 0;
        tutorialStages[0].Begin();
    }
    private void Update()
    {
        if (tutorialStages[index].IsStarted)
        {
            CheckActionPerformed();
            //Check if Action Performed
            if (true)
            {
                
                tutorialStages[index].HasCompleted = true;
                index++;
            }
        }
    }

    public void CheckActionPerformed()
    {
        if(index == 0)
        LearnPitchUp();
        if(index == 1)
        LearnPitchUp();
    }
    
    // Learn pitch control - both finger contacts up or down together
    public void LearnPitchUp()
    {

            if (Input.GetKeyDown(KeyCode.Space))//TODO Measure contacts have moved up or down together
            {
                tutorialStages[index].HasCompleted = true;
            } 

                 
    }
    public void LearnPitchDown()
    {

        if (Input.GetKeyDown(KeyCode.Space))//TODO Measure contacts have moved up or down together
        {
            tutorialStages[index].HasCompleted = true;
        }


    }
    //Learn yaw control - both finger contacts left or right together
    public void LearnYaw()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            tutorialStages[index].HasCompleted = true;
        }
    }
    //Learn roll control - measures difference in Y axix screenspace between left and right finger contacts
    public void LearnRoll()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            tutorialStages[index].HasCompleted = true;
        }
    }
    //Learn throttle control - measures difference in X axix screenspace between left and right finger contacts
    public void LearnThrottle()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            tutorialStages[index].HasCompleted = true;
        }
    }
    //Learn yaw control - both finger contacts left or right together
    public void LearnGryo()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {

            tutorialStages[index].HasCompleted = true;
            CompleteTutorial();
        }
    }
    //Tells GameSettings and GameManager that Tutorial has been completed
    public void CompleteTutorial()
    {
        hasCompletedTutorial = true;
        SceneManager.LoadScene(0);
    }
}

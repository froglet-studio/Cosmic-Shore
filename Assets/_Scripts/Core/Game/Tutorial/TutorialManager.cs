using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core;
using UnityEngine.SceneManagement;
using System;

public class TutorialManager : MonoBehaviour
{
    public List<GameObject> tutorialPanels;

    public List<TutorialStage> tutorialStages; //SO Assests

    public Dictionary<string, bool> TutorialTests = new Dictionary<string, bool>();

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
        InitializeTutorialTests();
        index = 0;
        tutorialStages[0].Begin();
    }

    private void InitializeTutorialTests()
    {
        TutorialTests.Add(tutorialStages[0].StageName, false);
        TutorialTests.Add(tutorialStages[1].StageName, false);
    }

    private void Update()
    {
        if(index >= tutorialStages.Count || PlayerPrefs.GetInt("Skip Tutorial") == 1)
        {
            if (TutorialTests.ContainsValue(!false))
            {
                CompleteTutorial();
            }
        }
        if (tutorialStages[index].IsStarted)
        {
            CheckCurrentTestPassed();
        }
    }

    public void CheckCurrentTestPassed()
    {
        TutorialTests.TryGetValue(tutorialStages[index].StageName, out bool value);
        if (value == true)
        {
            tutorialStages[index].End();
            Debug.Log("Passed tutorial test " + tutorialStages[index].StageName);
            index++;
            tutorialStages[index].Begin();           
        } 
    }
    
    
    //Tells GameSettings and GameManager that Tutorial has been completed
    public void CompleteTutorial()
    {
        hasCompletedTutorial = true;
        SceneManager.LoadScene(0);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core;
using UnityEngine.SceneManagement;
using System;

public class TutorialManager : MonoBehaviour
{
    [SerializeField]
    TutorialPlayerController playerController;

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

    private void InitializeTutorialTests() //Adding Test Names and setting bools false
    {
        TutorialTests.Add(tutorialStages[0].StageName, false);
        TutorialTests.Add(tutorialStages[1].StageName, false);
        TutorialTests.Add(tutorialStages[2].StageName, false);
        TutorialTests.Add(tutorialStages[3].StageName, false);
        TutorialTests.Add(tutorialStages[4].StageName, false);
        TutorialTests.Add(tutorialStages[5].StageName, false);
        TutorialTests.Add(tutorialStages[6].StageName, false);
        TutorialTests.Add(tutorialStages[7].StageName, false);
        TutorialTests.Add(tutorialStages[8].StageName, false);

    }

    private void Update()
    {
        if(index >= tutorialStages.Count -1 || PlayerPrefs.GetInt("Skip Tutorial") == 1)
        {
            if (TutorialTests.ContainsValue(true))
            {
                CompleteTutorial();
            }
        }
        if (tutorialStages[index].IsStarted)
        {
            playerController.controlLevels[tutorialStages[0].StageName] = true;        
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

    public void StartGyroTest()
    {
        //TODO figure out how to prove gyro has been used

    }
    
    
    //Tells GameSettings and GameManager that Tutorial has been completed
    public void CompleteTutorial()
    {
        hasCompletedTutorial = true;
        GameSetting setting = GameSetting.Instance;
        SceneManager.LoadScene(0);
    }
}

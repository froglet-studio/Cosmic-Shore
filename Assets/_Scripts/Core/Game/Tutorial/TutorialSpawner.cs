using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TutorialSpawner : MonoBehaviour
{
    
    // objects
    [SerializeField]
    private GameObject muton;
    [SerializeField]
    private GameObject jailBlockWall;
    [SerializeField]
    private GameObject fuelBar;
    //[SerializeField]
    //private GameObject trail;


    // tutorial refs
    [SerializeField]
    private TutorialManager tutorialManager;

    [SerializeField]
    private int index;

    private void Start()
    {
        TutorialManager.Instance.OnTutorialIndexChange += OnIndexChange;
        jailBlockWall.SetActive(false);
        muton.SetActive(false);
    }

    private void OnDisable()
    {
        TutorialManager.Instance.OnTutorialIndexChange += OnIndexChange;
    }

    private void OnIndexChange(int idx)
    {
        index = idx;
        SetUpScene();
    }

    // Update is called once per frame
    void Update()
    {
        if (muton.activeInHierarchy) { return; } // TODO cage or other stuff

        if (tutorialManager.tutorialStages[index].HasAnotherAttempt || !tutorialManager.tutorialStages[index].HasActiveMuton)
        {
            muton.SetActive(true);
            tutorialManager.tutorialStages[index].HasActiveMuton = true;
        }       
    }

    private void SetUpScene()
    {
        if (tutorialManager.tutorialStages[index].HasMuton)
        {
            muton.SetActive(true);
            tutorialManager.tutorialStages[index].HasActiveMuton = true;
        }
        else
        {
            muton.SetActive(false);
        }
        if (index == 3 || index == 5)
        {
            jailBlockWall.SetActive(true);
        }
        else
        {
            jailBlockWall.SetActive(false);
        }
        if(index >= 6)
        {
            //turn on player trails
        }
        if(tutorialManager.tutorialStages[index].HasFuelBar)
        {
            fuelBar.SetActive(false);
        }
        else
        {
            fuelBar.SetActive(true);
        }
    }
}

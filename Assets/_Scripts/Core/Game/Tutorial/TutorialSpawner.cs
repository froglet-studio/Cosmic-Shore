using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TutorialSpawner : MonoBehaviour
{
    [SerializeField]
    private TutorialManager tutorialManager;
    [SerializeField]
    private GameObject muton;
    //[SerializeField]
    //private GameObject block;
    [SerializeField]
    private int index;

    private bool hasActiveMuton = false;

    private void Start()
    {
        TutorialManager.Instance.OnTutorialIndexChange += OnIndexChange;
    }

    private void OnDisable()
    {
        TutorialManager.Instance.OnTutorialIndexChange += OnIndexChange;
    }

    private void OnIndexChange(int idx)
    {
        index = idx;
    }

    // Update is called once per frame
    void Update()
    {
        if (muton.activeInHierarchy) { return; } // TODO cage or other stuff

        if (tutorialManager.tutorialStages[index].HasMuton && !tutorialManager.tutorialStages[index].HasActiveMuton) 
        {
            muton.SetActive(true);
            tutorialManager.tutorialStages[index].HasActiveMuton = true;
        }
        if (tutorialManager.tutorialStages[index].HasAnotherAttempt || !tutorialManager.tutorialStages[index].HasActiveMuton)
        {
            muton.SetActive(true);
            tutorialManager.tutorialStages[index].HasActiveMuton = true;
        }       
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TutorialMuton : MonoBehaviour
{
    TutorialManager tutorialManager;

    public string stageName;

    public List<Vector3> spawnPoints;

    int index = 0;

    // Start is called before the first frame update
    void Start()
    {
        transform.position = spawnPoints[index];
    }


    private void OnCollisionEnter(Collision collision)
    {
        stageName = tutorialManager.tutorialStages[index].StageName;
        tutorialManager.TutorialTests.Remove(stageName);
        tutorialManager.TutorialTests.Add(stageName, true);
        index++;
        transform.position = spawnPoints[index];
    }
}

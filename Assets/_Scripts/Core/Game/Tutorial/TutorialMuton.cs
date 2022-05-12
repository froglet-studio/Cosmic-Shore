using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TutorialMuton : MonoBehaviour
{
    [SerializeField]
    TutorialManager tutorialManager;

    [SerializeField]
    private GameObject player;

    public string stageName;

    public List<Vector3> spawnPointsOffset;

    public int index = 0;

    private int gyroIndex;

    List<Collision> collisions;

    // Start is called before the first frame update
    void Start()
    {
        transform.position = player.transform.forward + spawnPointsOffset[index];
        stageName = tutorialManager.tutorialStages[index].StageName;
        gyroIndex = spawnPointsOffset.Count - 1; // final stage testing gyro does not involve hitting a test Muton
        collisions = new List<Collision>();
    }

   

    private void OnCollisionEnter(Collision collision)
    {
        collisions.Add(collision);
        
    }

    private void Update()
    {
        if (collisions.Count > 0)
        {
            Collide(collisions[0].collider);
            collisions.Clear();
        }
    }

    void Collide(Collider other)
    {
        stageName = tutorialManager.tutorialStages[index].StageName;
        tutorialManager.TutorialTests[stageName] = true;
        if (index < spawnPointsOffset.Count - 1)
        {
            
            if (stageName == tutorialManager.tutorialStages[gyroIndex].StageName)
            {
                tutorialManager.StartGyroTest();
                gameObject.SetActive(false);
            }
            else
            {
                index++;
                transform.position = player.transform.forward + spawnPointsOffset[index];
            }           
        }       
    }
}

using System;
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

    public List<Vector3> spawnPointsOffset;

    private int tutorialStageIndex = 0;

    private float distance = 10f;

    private string stageName;

    List<Collision> collisions;

    private void OnEnable()
    {
        TutorialManager.Instance.OnTutorialIndexChange += OnIndexChange;
    }

    private void OnDisable()
    {
        TutorialManager.Instance.OnTutorialIndexChange += OnIndexChange;
    }

    private void OnIndexChange(int idx)
    {
        tutorialStageIndex = idx;
    }

    // Start is called before the first frame update
    void Start()
    {
        //tutorialManager = TutorialManager.Instance;
        MoveMuton();
        tutorialStageIndex = tutorialManager.Index;
        collisions = new List<Collision>();
        tutorialManager.tutorialStages[tutorialStageIndex].HasActiveMuton = true;
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

        if (!tutorialManager.tutorialStages[tutorialStageIndex].HasAnotherAttempt)
        {
            if(distance >= (Vector3.Distance(player.transform.position, transform.position)))
            {
                MoveMuton();
            }
        }
    }
    void Collide(Collider other)
    {     
        stageName = tutorialManager.tutorialStages[tutorialStageIndex].StageName;
        tutorialManager.TutorialTests[stageName] = true;
        tutorialManager.tutorialStages[tutorialStageIndex].HasActiveMuton = false;

        gameObject.SetActive(false);
            
    }
    void MoveMuton()
    {

        transform.position = player.transform.position +
                             player.transform.right * spawnPointsOffset[tutorialStageIndex].x +
                             player.transform.up * spawnPointsOffset[tutorialStageIndex].y +
                             player.transform.forward * spawnPointsOffset[tutorialStageIndex].z;
    }
}

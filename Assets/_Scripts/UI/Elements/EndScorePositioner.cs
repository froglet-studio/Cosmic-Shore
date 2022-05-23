using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndScorePositioner : MonoBehaviour
{
    [SerializeField]
    GameObject LookAtTarget;

    [SerializeField]
    GameObject FollowTarget;

    [SerializeField]
    float offset = 10;
    // Start is called before the first frame update
    void Start()
    {
         
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = FollowTarget.transform.position + offset * (LookAtTarget.transform.position - FollowTarget.transform.position).normalized; 
    }
}

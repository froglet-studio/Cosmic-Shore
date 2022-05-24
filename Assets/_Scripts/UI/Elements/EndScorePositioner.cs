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
        transform.rotation = FollowTarget.transform.rotation;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        transform.position = FollowTarget.transform.position + offset * ( LookAtTarget.transform.position - FollowTarget.transform.position).normalized;
        transform.localRotation = Quaternion.Euler(0, FollowTarget.transform.rotation.eulerAngles.y - transform.localRotation.eulerAngles.y, 0) * transform.localRotation;

    }
}

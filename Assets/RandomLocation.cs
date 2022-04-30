using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomLocation : MonoBehaviour
{
    float sphereRadius = 100;

    [SerializeField]
    GameObject brokenSphere;

    [SerializeField]
    Transform shipTransform;

    // Start is called before the first frame update
    void Start()
    {
        transform.position = Random.insideUnitSphere * sphereRadius;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        brokenSphere.transform.position = transform.position;
        brokenSphere.transform.up = shipTransform.up;
        Instantiate<GameObject>(brokenSphere);
        transform.position = Random.insideUnitSphere * sphereRadius;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RandomLocation : MonoBehaviour
{
    float sphereRadius = 100;

    [SerializeField]
    GameObject brokenSphere;

    [SerializeField]
    Transform shipTransform;

    [SerializeField]
    TextMeshProUGUI outputText;

    int score = 0;

    // Start is called before the first frame update
    void Start()
    {
        transform.position = Random.insideUnitSphere * sphereRadius;
        outputText.text = "Score: " + score.ToString();
    }

    // Update is called once per frame
    void Update()
    {   
        
    }
    
    private void OnTriggerEnter(Collider other)
    {
        brokenSphere.transform.position = transform.position;
        brokenSphere.transform.localEulerAngles = transform.localEulerAngles;
        //brokenSphere.transform.forward = transform.forward;
        //brokenSphere.transform.right = transform.right;
        //brokenSphere.transform.up = transform.up;
        Instantiate<GameObject>(brokenSphere);
        transform.position = Random.insideUnitSphere * sphereRadius;
        score++;
        outputText.text = "Score: " + score.ToString();
    }
}

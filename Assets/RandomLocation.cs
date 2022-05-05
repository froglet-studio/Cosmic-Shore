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
    TextMeshProUGUI outputText;

    [SerializeField]
    IntensityBar IntensityBar;

    [SerializeField]
    float MutonIntensityBoost = .1f;

    int score = 0;

    void Start()
    {
        transform.position = Random.insideUnitSphere * sphereRadius;
        //outputText.text = "Score: " + score.ToString();
    }
    
    private void OnTriggerEnter(Collider other)
    {
        brokenSphere.transform.position = transform.position;
        brokenSphere.transform.localEulerAngles = transform.localEulerAngles;
        Instantiate<GameObject>(brokenSphere);
        transform.position = Random.insideUnitSphere * sphereRadius;
        IntensityBar.IncreaseIntensity(MutonIntensityBoost);
        score++;
        outputText.text = "Score: " + score.ToString();
    }
}

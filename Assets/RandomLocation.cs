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
    }
    
    private void OnTriggerEnter(Collider other)
    {
        var spherePop = Instantiate<GameObject>(brokenSphere);
        spherePop.transform.position = transform.position;
        spherePop.transform.localEulerAngles = transform.localEulerAngles;
        transform.position = Random.insideUnitSphere * sphereRadius;
        IntensityBar.IncreaseIntensity(MutonIntensityBoost); // TODO: use events instead
        score++;
        outputText.text = score.ToString("D3");
    }
}

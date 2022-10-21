using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core;
using TMPro;

public class Timer : MonoBehaviour
{
    public float timeRemaining;
    public TMP_Text textMeshPro;

    void Update()
    {
        timeRemaining -= Time.deltaTime;
        textMeshPro.text = timeRemaining.ToString();
        if (timeRemaining <= 0)
        {
            GameManager.EndGame();
            
        }
    }
}

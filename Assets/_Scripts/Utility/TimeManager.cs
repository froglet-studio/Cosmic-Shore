using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public float slowDownLenght = 2f;
    public float timeScaleModifier = 0.05f;

    // Update is called once per frame
    void Update()
    {
        Time.timeScale += (1f / slowDownLenght * Time.unscaledDeltaTime);
        Time.timeScale = Mathf.Clamp(Time.timeScale , 0, 1);
    }

    public void ChangeMotion(float _timeScaleModifier)
    {
        timeScaleModifier = _timeScaleModifier;
        Time.timeScale = timeScaleModifier;
        Time.fixedDeltaTime = Time.timeScale * 0.2f;
    }
}

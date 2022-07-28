using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    [SerializeField]
    private float slowDownLenght = 2f;
    [SerializeField]
    private float timeScaleModifier = 0.05f;

    // Update is called once per frame
    void Update()
    {
        Time.timeScale += (1f / slowDownLenght * Time.unscaledDeltaTime);
        Time.timeScale = Mathf.Clamp(Time.timeScale , 0, 1);
    }

    public void ChangeTimeScale(float _timeScaleModifier)
    {
        timeScaleModifier = _timeScaleModifier;
        Time.timeScale = timeScaleModifier;
        Time.fixedDeltaTime = Time.timeScale * 0.2f;
    }

    public void ChangeTimeScale(float _timeScaleModifier, float time)
    {
        slowDownLenght = time;
        timeScaleModifier = _timeScaleModifier;
        Time.timeScale = timeScaleModifier;
        Time.fixedDeltaTime = Time.timeScale * 0.2f;
    }

    public void ResetTimeManagerToDefaultValues()
    {
        slowDownLenght = 2f;
        timeScaleModifier = 0.05f;
    }
}

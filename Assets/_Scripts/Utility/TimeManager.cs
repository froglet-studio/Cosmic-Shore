using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core;
using System;

public class TimeManager : MonoBehaviour
{
    [SerializeField] float timeScaleModifier = 0.05f;

    private void OnEnable()
    {
        FuelSystem.OnFuelEmpty += SetTimeScaleToOneX;
        GameManager.onGameOver += SetTimeScaleToOneX;
    }

    private void OnDisable()
    {
        FuelSystem.OnFuelEmpty -= SetTimeScaleToOneX;
        GameManager.onGameOver -= SetTimeScaleToOneX;
    }

    private void Start()
    {
        ChangeTimeScale(timeScaleModifier);
    }

    private void SetTimeScaleToOneX()
    {
        ChangeTimeScale(1);
    }

    public void ChangeTimeScale(float _timeScaleModifier)
    {
        timeScaleModifier = _timeScaleModifier;
        Time.timeScale = timeScaleModifier;
    }
}

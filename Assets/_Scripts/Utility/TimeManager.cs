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
        FuelSystem.OnFuelEmpty += OnZeroFuel;
        GameManager.onGameOver += OnGameOver;
        
    }

    private void OnDisable()
    {
        FuelSystem.OnFuelEmpty -= OnZeroFuel;
        GameManager.onGameOver -= OnGameOver;
    }

    private void Start()
    {
        ChangeTimeScale(1);
    }

    private void OnZeroFuel()
    {
        ChangeTimeScale(1);
    }

    private void OnGameOver()
    {
        ChangeTimeScale(1f);
    }

    public void ChangeTimeScale(float _timeScaleModifier)
    {
        timeScaleModifier = _timeScaleModifier;
        Time.timeScale = timeScaleModifier;
    }
}

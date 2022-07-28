using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core;
using System;

public class TimeManager : MonoBehaviour
{
    //[SerializeField]
    //private float slowDownLength = 2f;
    [SerializeField]
    private float timeScaleModifier = 0.05f;

    private void OnEnable()
    {
        FuelSystem.zeroFuel += OnZeroFuel;
        GameManager.onGameOver += OnGameOver;
        
    }

    private void OnDisable()
    {
        FuelSystem.zeroFuel -= OnZeroFuel;
        GameManager.onGameOver -= OnGameOver;
    }

    private void Start()
    {
        ChangeTimeScale(1);
    }

    private void OnZeroFuel()
    {
        ChangeTimeScale(1f);
    }

    private void OnGameOver()
    {
        ChangeTimeScale(1f);
    }

    // Update is called once per frame
    //void Update()
    //{
    //    Time.timeScale += (1f / slowDownLength * Time.unscaledDeltaTime);
    //    Time.timeScale = Mathf.Clamp(Time.timeScale , 0, 1);
    //}

    public void ChangeTimeScale(float _timeScaleModifier)
    {
        timeScaleModifier = _timeScaleModifier;
        Time.timeScale = timeScaleModifier;
        //Time.fixedDeltaTime = Time.timeScale * Time.fixedDeltaTime; 
    }

    //public void ChangeTimeScale(float _timeScaleModifier, float time)
    //{
    //    slowDownLength = time;
    //    timeScaleModifier = _timeScaleModifier;
    //    Time.timeScale = timeScaleModifier;
    //    Time.fixedDeltaTime = Time.timeScale * 0.2f;
    //}

    //public void ResetTimeManagerToDefaultValues()
    //{
    //    slowDownLength = 2f;
    //    timeScaleModifier = 0.05f;
    //}
}

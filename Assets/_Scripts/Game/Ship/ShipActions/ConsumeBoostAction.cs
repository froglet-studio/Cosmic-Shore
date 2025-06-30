using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game;
using System;


public class ConsumeBoostAction : ShipAction
{
    [SerializeField] ElementalFloat boostMultiplier = new(4f);
    [SerializeField] float boostDuration = 4f;
    [SerializeField] int resourceIndex = 1;
    [SerializeField] float resourceCost = .25f;
    public int ResourceIndex => resourceIndex;
    public float BoostDuration => boostDuration;

    public event Action<float, float> OnBoostStarted; // (duration, currentResourceAmount)
    public event Action OnBoostEnded;

    public override void Initialize(IShip ship)
    {
        base.Initialize(ship);
        Ship.ShipStatus.BoostMultiplier = 0;
    }

    public override void StartAction()
    {
        if (Ship.ShipStatus.ResourceSystem.Resources[resourceIndex].CurrentAmount >= resourceCost)
        {
            OnBoostStarted?.Invoke(boostDuration, Ship.ShipStatus.ResourceSystem.Resources[resourceIndex].CurrentAmount);
            StartCoroutine(ConsumeBoostCoroutine());
        }
    }

    public override void StopAction()
    {
        
    }

    IEnumerator ConsumeBoostCoroutine()
    {
        var multiplier = boostMultiplier.Value;
        Ship.ShipStatus.ResourceSystem.ChangeResourceAmount(resourceIndex,-resourceCost);
        ShipStatus.Boosting = true;
        Ship.ShipStatus.BoostMultiplier += multiplier;
        yield return new WaitForSeconds(boostDuration);
        Ship.ShipStatus.BoostMultiplier -= multiplier;
        if (Ship.ShipStatus.BoostMultiplier <= 1)
        {
            Ship.ShipStatus.BoostMultiplier = 1;
            ShipStatus.Boosting = false;
            OnBoostEnded?.Invoke();
        }
    }
}

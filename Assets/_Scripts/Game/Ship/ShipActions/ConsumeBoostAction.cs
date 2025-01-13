using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;


public class ConsumeBoostAction : ShipAction
{
    ShipStatus shipData;
    [SerializeField] ElementalFloat boostMultiplier = new(4f);
    [SerializeField] float boostDuration = 4f;
    [SerializeField] int resourceIndex = 1;
    [SerializeField] float resourceCost = .25f;

    protected override void Start()
    {
        BindElementalFloats(Ship);
        shipData = Ship.ShipStatus;
        Ship.BoostMultiplier = 0;
    }
    public override void StartAction()
    {
        if (Ship.ResourceSystem.Resources[resourceIndex].CurrentAmount >= resourceCost) StartCoroutine(ConsumeBoostCoroutine());
    }

    public override void StopAction()
    {
        
    }

    IEnumerator ConsumeBoostCoroutine()
    {
        var multiplier = boostMultiplier.Value;
        Ship.ResourceSystem.ChangeResourceAmount(resourceIndex,-resourceCost);
        shipData.Boosting = true;
        Ship.BoostMultiplier += multiplier;
        yield return new WaitForSeconds(boostDuration);
        Ship.BoostMultiplier -= multiplier;
        if (Ship.BoostMultiplier <= 1)
        {
            Ship.BoostMultiplier = 1;
            shipData.Boosting = false;
        }
    }
}

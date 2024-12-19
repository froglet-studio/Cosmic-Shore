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
        BindElementalFloats(ship);
        shipData = ship.GetComponent<ShipStatus>();
        ship.boostMultiplier = 0;
    }
    public override void StartAction()
    {
        if (ship.ResourceSystem.Resources[resourceIndex].CurrentAmount >= resourceCost) StartCoroutine(ConsumeBoostCoroutine());
    }

    public override void StopAction()
    {
        
    }

    IEnumerator ConsumeBoostCoroutine()
    {
        var multiplier = boostMultiplier.Value;
        ship.ResourceSystem.ChangeResourceAmount(resourceIndex,-resourceCost);
        shipData.Boosting = true;
        ship.boostMultiplier += multiplier;
        yield return new WaitForSeconds(boostDuration);
        ship.boostMultiplier -= multiplier;
        if (ship.boostMultiplier <= 1)
        {
            ship.boostMultiplier = 1;
            shipData.Boosting = false;
        }
    }
}

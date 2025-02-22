using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;


public class ConsumeBoostAction : ShipAction
{
    [SerializeField] ElementalFloat boostMultiplier = new(4f);
    [SerializeField] float boostDuration = 4f;
    [SerializeField] int resourceIndex = 1;
    [SerializeField] float resourceCost = .25f;

    protected override void InitializeShipAttributes()
    {
        base.InitializeShipAttributes();
        Ship.ShipStatus.BoostMultiplier = 0;
    }

    public override void StartAction()
    {
        if (Ship.ShipStatus.ResourceSystem.Resources[resourceIndex].CurrentAmount >= resourceCost) StartCoroutine(ConsumeBoostCoroutine());
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
        }
    }
}

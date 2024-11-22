using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;


public class ConsumeBoostAction : ShipAction
{
    ShipStatus shipData;
    [SerializeField] float boostMultiplier = 4f;
    [SerializeField] float boostDuration = 4f;
    [SerializeField] int resourceIndex = 1;
    [SerializeField] float resourceCost = .25f;

    protected override void Start()
    {
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
        ship.ResourceSystem.ChangeResourceAmount(resourceIndex,-resourceCost);
        shipData.Boosting = true;
        ship.boostMultiplier += boostMultiplier;
        yield return new WaitForSeconds(boostDuration);
        ship.boostMultiplier -= boostMultiplier;
        if (ship.boostMultiplier <= 0)
        {
            shipData.Boosting = false;
        }
    }
}

using CosmicShore.Core;
using System.Collections;
using UnityEngine;

public class ChargeBoostAction : ShipAction
{
    bool BoostCharging;
    [SerializeField] float MaxBoostMultiplier = 2;
    [SerializeField] float BoostChargeRate = .33f;
    [SerializeField] float BoostDischargeRate = .25f;
    [SerializeField] int boostResourceIndex = 0;

    public override void StartAction()
    {
        StopAllCoroutines();
        if (ResourceSystem) ResourceSystem.Resources[boostResourceIndex].CurrentAmount = 0;
        BoostCharging = true;
        StartCoroutine(BoostChargeCoroutine());
    }

    public override void StopAction()
    {
        StopAllCoroutines();
        BoostCharging = false;
        StartCoroutine(DischargeBoostCoroutine());
    }

    IEnumerator BoostChargeCoroutine()
    {
        while (BoostCharging)
        {
            Ship.ShipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex, BoostChargeRate);
            ShipStatus.ChargedBoostCharge = 1 + ResourceSystem.Resources[boostResourceIndex].CurrentAmount;
            yield return new WaitForSeconds(.1f);
        }
    }

    IEnumerator DischargeBoostCoroutine()
    {
        // TODO: figure out how to get ship data component here so that it is not null
        
        ShipStatus.ChargedBoostDischarging = true;
        while (ShipStatus.ChargedBoostCharge > 1)
        {
            Ship.ShipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex , - BoostDischargeRate);
            ShipStatus.ChargedBoostCharge = 1 + (MaxBoostMultiplier * ResourceSystem.Resources[boostResourceIndex].CurrentAmount);
            yield return new WaitForSeconds(.1f);
        }
        ShipStatus.ChargedBoostCharge = 1;
        ShipStatus.ChargedBoostDischarging = false;

        Ship.ShipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex, -ResourceSystem.Resources[boostResourceIndex].CurrentAmount);

    }
}
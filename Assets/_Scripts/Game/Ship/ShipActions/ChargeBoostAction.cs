using CosmicShore.Core;
using System.Collections;
using UnityEngine;

public class ChargeBoostAction : ShipAction
{
    bool boostCharging;
    ShipStatus shipStatus;
    [SerializeField] float MaxBoostMultiplier = 2;
    [SerializeField] float BoostChargeRate = .33f;
    [SerializeField] float BoostDischargeRate = .25f;
    [SerializeField] int boostResourceIndex = 0;

    public override void StartAction()
    {
        StopAllCoroutines();
        if (ShipStatus.ResourceSystem) ShipStatus.ResourceSystem.Resources[boostResourceIndex].CurrentAmount = 0;
        boostCharging = true;
        StartCoroutine(BoostChargeCoroutine());
    }

    public override void StopAction()
    {
        StopAllCoroutines();
        boostCharging = false;
        StartCoroutine(DischargeBoostCoroutine());
    }

    IEnumerator BoostChargeCoroutine()
    {
        while (boostCharging)
        {
            ShipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex, BoostChargeRate);
            shipStatus.ChargedBoostCharge = 1 + ShipStatus.ResourceSystem.Resources[boostResourceIndex].CurrentAmount;
            yield return new WaitForSeconds(.1f);
        }
    }

    IEnumerator DischargeBoostCoroutine()
    {
        // TODO: figure out how to get ship data component here so that it is not null
        
        ShipStatus.ChargedBoostDischarging = true;
        while (ShipStatus.ChargedBoostCharge > 1)
        {
            ShipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex , - BoostDischargeRate);
            shipStatus.ChargedBoostCharge = 1 + (MaxBoostMultiplier * ShipStatus.ResourceSystem.Resources[boostResourceIndex].CurrentAmount);
            yield return new WaitForSeconds(.1f);
        }
        ShipStatus.ChargedBoostCharge = 1;
        ShipStatus.ChargedBoostDischarging = false;

        ShipStatus.ResourceSystem.ChangeResourceAmount(boostResourceIndex, -ShipStatus.ResourceSystem.Resources[boostResourceIndex].CurrentAmount);

    }
}
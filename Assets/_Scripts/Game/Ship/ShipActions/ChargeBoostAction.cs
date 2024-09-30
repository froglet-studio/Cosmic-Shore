using CosmicShore.Core;
using System.Collections;
using UnityEngine;

public class ChargeBoostAction : ShipAction
{
    bool BoostCharging;
    [SerializeField] float BoostChargeRate = .33f;
    [SerializeField] float BoostDischargeRate = .25f;
    ShipStatus shipStatus;
    [SerializeField] int boostResourceIndex = 0;

    protected override void InitializeShipAttributes()
    {
        base.InitializeShipAttributes();
        GetShipStatus();
    }

    void GetShipStatus()
    {
        if (!TryGetComponent(out shipStatus))
            shipStatus = ship.GetComponent<ShipStatus>();
    }

    public override void StartAction()
    {
        StopAllCoroutines();
        if (resourceSystem) resourceSystem.Resources[boostResourceIndex].CurrentAmount = 0;
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
            ship.ResourceSystem.ChangeResourceAmount(boostResourceIndex, BoostChargeRate);
            shipStatus.ChargedBoostCharge = 1 + resourceSystem.Resources[boostResourceIndex].CurrentAmount;
            yield return new WaitForSeconds(.1f);
        }
    }

    IEnumerator DischargeBoostCoroutine()
    {
        // TODO: figure out how to get ship data component here so that it is not null
        
        shipStatus.ChargedBoostDischarging = true;
        while (shipStatus.ChargedBoostCharge > 1)
        {
            ship.ResourceSystem.ChangeResourceAmount(boostResourceIndex , - BoostDischargeRate);
            shipStatus.ChargedBoostCharge = 1 + resourceSystem.Resources[boostResourceIndex].CurrentAmount;
            yield return new WaitForSeconds(.1f);
        }
        shipStatus.ChargedBoostCharge = 1;
        shipStatus.ChargedBoostDischarging = false;

        ship.ResourceSystem.ChangeResourceAmount(boostResourceIndex, -resourceSystem.Resources[boostResourceIndex].CurrentAmount);

    }
}
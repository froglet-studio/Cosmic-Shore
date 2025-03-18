using CosmicShore.Core;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class ChargeBoostAction : ShipAction
{
    bool boostCharging;
    ShipStatus shipStatus;
    [SerializeField] float MaxBoostMultiplier = 2;
    [SerializeField] float BoostChargeRate = .33f;
    [SerializeField] float BoostDischargeRate = .25f;
    [FormerlySerializedAs("boostResourceIndex")]
    [SerializeField] int BoostResourceIndex = 0;

    protected override void InitializeShipAttributes()
    {
        base.InitializeShipAttributes();
        GetShipStatus();
    }

    void GetShipStatus()
    {
        if (!TryGetComponent(out shipStatus))
            shipStatus = Ship.ShipStatus;
    }

    public override void StartAction()
    {
        StopAllCoroutines();
        if (resourceSystem) resourceSystem.Resources[BoostResourceIndex].CurrentAmount = 0;
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
            Ship.ResourceSystem.ChangeResourceAmount(BoostResourceIndex, BoostChargeRate);
            shipStatus.ChargedBoostCharge = 1 + resourceSystem.Resources[BoostResourceIndex].CurrentAmount;
            yield return new WaitForSeconds(.1f);
        }
    }

    IEnumerator DischargeBoostCoroutine()
    {
        // TODO: figure out how to get ship data component here so that it is not null
        
        shipStatus.ChargedBoostDischarging = true;
        while (shipStatus.ChargedBoostCharge > 1)
        {
            Ship.ResourceSystem.ChangeResourceAmount(BoostResourceIndex , - BoostDischargeRate);
            shipStatus.ChargedBoostCharge = 1 + (MaxBoostMultiplier * resourceSystem.Resources[BoostResourceIndex].CurrentAmount);
            yield return new WaitForSeconds(.1f);
        }
        shipStatus.ChargedBoostCharge = 1;
        shipStatus.ChargedBoostDischarging = false;

        Ship.ResourceSystem.ChangeResourceAmount(BoostResourceIndex, -resourceSystem.Resources[BoostResourceIndex].CurrentAmount);
    }
}
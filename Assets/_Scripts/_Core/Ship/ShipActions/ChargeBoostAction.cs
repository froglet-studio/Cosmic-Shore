using StarWriter.Core;
using System.Collections;
using UnityEngine;

public class ChargeBoostAction : ShipActionAbstractBase
{
    bool BoostCharging;
    float BoostChargeRate = .33f;
    float MaxBoostCharge = 10;
    ShipData shipData;

    void Start()
    {
        shipData = ship.GetComponent<ShipData>();
    }

    void Update()
    {
        if (BoostCharging)
        {
            shipData.ChargedBoostCharge += BoostChargeRate * Time.deltaTime;
            ship.ResourceSystem.ChangeBoostAmount(BoostChargeRate);
        }
    }

    public override void StartAction()
    {
        BoostCharging = true;
    }

    public override void StopAction()
    {
        BoostCharging = false;
        StartChargedBoost();
    }

    public void StartChargedBoost()
    {
        StartCoroutine(DischargeBoostCoroutine());
    }

    IEnumerator DischargeBoostCoroutine()
    {
        shipData.ChargedBoostDischarging = true;
        while (shipData.ChargedBoostCharge > 1)
        {
            shipData.ChargedBoostCharge = Mathf.Clamp(shipData.ChargedBoostCharge - Time.deltaTime, 1, MaxBoostCharge);
            ship.ResourceSystem.ChangeBoostAmount(-Time.deltaTime);
            yield return null;
        }
        shipData.ChargedBoostDischarging = false;
        ship.ResourceSystem.ChangeBoostAmount(-ship.ResourceSystem.CurrentBoost);
    }
}
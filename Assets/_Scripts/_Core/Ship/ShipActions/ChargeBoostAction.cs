using StarWriter.Core;
using System.Collections;
using UnityEngine;

public class ChargeBoostAction : ShipActionAbstractBase
{
    bool BoostCharging;
    float BoostChargeRate = .33f;
    float MaxBoostCharge = 10;
    ShipStatus shipData;

    void Start()
    {
        shipData = ship.GetComponent<ShipStatus>();
    }

    void Update()
    {
        if (BoostCharging)
        {
            Debug.Log("charging boost");
            shipData.ChargedBoostCharge += BoostChargeRate * Time.deltaTime;
            ship.ResourceSystem.ChangeBoostAmount(BoostChargeRate * Time.deltaTime);
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
        shipData.ChargedBoostCharge = 1;
        shipData.ChargedBoostDischarging = false;
        ship.ResourceSystem.ChangeBoostAmount(-ship.ResourceSystem.CurrentBoost);
    }
}
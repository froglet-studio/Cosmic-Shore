using CosmicShore.Core;
using System.Collections;
using UnityEngine;

public class ChargeBoostAction : ShipAction
{
    bool BoostCharging;
    [SerializeField] float BoostChargeRate = .33f;
    [SerializeField] float BoostDischargeRate = .25f;
    [SerializeField] float MaxBoostCharge = 10;
    ShipStatus shipStatus;

    protected override void Start()
    {
        GetShipStatus();
    }

    void GetShipStatus()
    {
        if (!TryGetComponent(out shipStatus))
            shipStatus = ship.GetComponent<ShipStatus>();
    }

    void Update()
    {
        BoostCharge();
    }

    void BoostCharge()
    {
        if (BoostCharging)
        {
            if(shipStatus != null)  shipStatus.ChargedBoostCharge += BoostChargeRate * Time.deltaTime;

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

    void StartChargedBoost()
    {
        if (shipStatus) StartCoroutine(DischargeBoostCoroutine());
    }

    IEnumerator DischargeBoostCoroutine()
    {
        // TODO: figure out how to get ship data component here so that it is not null
        
        shipStatus.ChargedBoostDischarging = true;
        while (shipStatus.ChargedBoostCharge > 1)
        {
            shipStatus.ChargedBoostCharge = Mathf.Clamp(shipStatus.ChargedBoostCharge - Time.deltaTime * BoostDischargeRate, 1, MaxBoostCharge);
            ship.ResourceSystem.ChangeBoostAmount(-Time.deltaTime * BoostDischargeRate);
            yield return null;
        }
        shipStatus.ChargedBoostCharge = 1;
        shipStatus.ChargedBoostDischarging = false;
        
        ship.ResourceSystem.ChangeBoostAmount(-ship.ResourceSystem.CurrentBoost);
    }
}
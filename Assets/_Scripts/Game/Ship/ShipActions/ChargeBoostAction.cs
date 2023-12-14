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

    private void GetShipStatus()
    {
        if(!TryGetComponent(out shipStatus))
        {
            Debug.LogWarningFormat("{0} - {1} - {2}", nameof(ChargeBoostAction), nameof(GetShipStatus), "ship status is null, but still trying to get it.");
            shipStatus = ship.GetComponent<ShipStatus>();
        }
    }

    void Update()
    {
        BoostCharge();
    }

    private void BoostCharge()
    {
        if (BoostCharging)
        {
            //Debug.LogFormat("{0} - {1} - charging boost", nameof(ChargeBoostAction), nameof(BoostCharge));
            
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

    private void StartChargedBoost()
    {
        // if (DischargeBoostCoroutine() == null) return;
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
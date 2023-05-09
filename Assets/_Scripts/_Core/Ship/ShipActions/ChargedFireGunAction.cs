using StarWriter.Core;
using System;
using System.Collections;
using UnityEngine;

public class ChargedFireGunAction : ShipActionAbstractBase
{
    // TODO: WIP gun firing needs to be reworked
    [SerializeField] Gun gun;

    ResourceSystem resourceSystem;
    ShipData shipData;
    GameObject projectileContainer;
    float ammoCost;

    public float ProjectileScale = 1f;
    public Vector3 BlockScale = new(4f, 4f, 1f); // TODO: Get rid of the need for this.
    
    float charge = 0;
    [SerializeField] float chargePerSecond = 1;
    Coroutine gainCharge;

    void Start()
    {
        projectileContainer = new GameObject($"{ship.Player.PlayerName}_Projectiles");
        shipData = ship.GetComponent<ShipData>();
        resourceSystem = ship.ResourceSystem;
    }
    public override void StartAction()
    {
        if (shipData.LiveProjectiles) gun.Detonate = true;
        else gainCharge = StartCoroutine(GainChargeCoroutine());
    }

    IEnumerator GainChargeCoroutine()
    {
        var chargePeriod = .1f;
        while (charge<1)
        {
            yield return new WaitForSeconds(chargePeriod);
            charge += chargePerSecond * chargePeriod;
            resourceSystem.ChangeChargeAmount(chargePerSecond * chargePeriod);
        }
    }

    Coroutine checkProjectiles;

    IEnumerator CheckProjectiles()
    {
        while (projectileContainer.GetComponentsInChildren<Projectile>().Length > 0)
        {
            shipData.LiveProjectiles = true;
            yield return null;
        }
        shipData.LiveProjectiles = false;
    }

    private void StartCheckProjectiles()
    {
        StopCheckProjectiles();
        checkProjectiles = StartCoroutine(CheckProjectiles());
    }

    private void StopCheckProjectiles()
    {
        if (checkProjectiles != null)
        {
            StopCoroutine(checkProjectiles);
            checkProjectiles = null;
        }
    }

    public override void StopAction()
    {
        if (gun.Detonate) { gun.Detonate = false; }
        else
        {
            charge = Mathf.Clamp(charge, 0, 1);
            ammoCost = charge;
            if (gainCharge != null) StopCoroutine(gainCharge);
            if (resourceSystem.CurrentAmmo > ammoCost)
            {
                resourceSystem.ChangeAmmoAmount(-ammoCost);

                Vector3 inheritedVelocity;
                if (shipData.Attached) inheritedVelocity = transform.forward;
                else inheritedVelocity = shipData.Course;

                // TODO: WIP magic numbers
                gun.FireGun(projectileContainer.transform, 90, inheritedVelocity * shipData.Speed, ProjectileScale*charge, BlockScale * 2, true, float.MaxValue, charge);
                StartCheckProjectiles();
            }
            charge = 0;
            resourceSystem.ResetCharge();
        }
    }
}
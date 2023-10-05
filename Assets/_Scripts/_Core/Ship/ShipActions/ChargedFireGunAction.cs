using StarWriter.Core;
using System.Collections;
using UnityEngine;

public class ChargedFireGunAction : ShipAction
{
    // TODO: WIP gun firing needs to be reworked
    [SerializeField] Gun gun;
    [SerializeField] float chargePerSecond = 1;

    ResourceSystem resourceSystem;
    ShipStatus shipStatus;
    GameObject projectileContainer;
    float ammoCost;

    public float ProjectileScale = 1f;
    public Vector3 BlockScale = new(4f, 4f, 1f); // TODO: Get rid of the need for this.
    
    float charge = 0;
    Coroutine gainCharge;

    void Start()
    {
        projectileContainer = new GameObject($"{ship.Player.PlayerName}_Projectiles");
        shipStatus = ship.GetComponent<ShipStatus>();
        resourceSystem = ship.ResourceSystem;
    }
    public override void StartAction()
    {
        if (shipStatus.LiveProjectiles) gun.Detonate();
        else gainCharge = StartCoroutine(GainChargeCoroutine());
    }

    IEnumerator GainChargeCoroutine()
    {
        var chargePeriod = .1f;
        while (charge<1)
        {
            yield return new WaitForSeconds(chargePeriod);
            charge += chargePerSecond * chargePeriod;
            resourceSystem.ChangeEnergyAmount(chargePerSecond * chargePeriod);
        }
    }

    Coroutine checkProjectiles;

    IEnumerator CheckProjectiles()
    {
        while (projectileContainer.GetComponentsInChildren<Projectile>().Length > 0)
        {
            shipStatus.LiveProjectiles = true;
            yield return null;
        }
        shipStatus.LiveProjectiles = false;
    }

    void StartCheckProjectiles()
    {
        if (checkProjectiles != null)
            StopCoroutine(checkProjectiles);

        checkProjectiles = StartCoroutine(CheckProjectiles());
    }

    public override void StopAction()
    {
        if (charge != 0)
        {
            StopCoroutine(gainCharge);
            charge = Mathf.Clamp(charge, 0, 1);
            ammoCost = charge;

            if (resourceSystem.CurrentAmmo > ammoCost)
            {
                resourceSystem.ChangeAmmoAmount(-ammoCost);

                Vector3 inheritedDirection;
                if (shipStatus.Attached || shipStatus.Stationary) inheritedDirection = transform.forward;
                else inheritedDirection = shipStatus.Course;

                // TODO: WIP magic numbers
                gun.FireGun(projectileContainer.transform, 90, inheritedDirection * shipStatus.Speed, ProjectileScale * charge, true, float.MaxValue, charge);
                StartCheckProjectiles();
            }

            charge = 0;
            //resourceSystem.ResetEnergy();
        }
    }
}
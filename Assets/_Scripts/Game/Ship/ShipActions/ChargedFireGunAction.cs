using StarWriter.Core;
using System.Collections;
using _Scripts._Core.Ship.Projectiles;
using UnityEngine;

public class ChargedFireGunAction : ShipAction
{
    // TODO: WIP gun firing needs to be reworked
    [SerializeField] Gun gun;
    [SerializeField] float chargePerSecond = 1;

    ShipStatus shipStatus;
    [SerializeField] GameObject projectileContainer;


    public float ProjectileScale = 1f;

    Coroutine gainEnergy;

    protected override void Start()
    {
        base.Start();
        //projectileContainer = new GameObject($"{ship.Player.PlayerName}_Projectiles");
        shipStatus = ship.GetComponent<ShipStatus>();
    }
    public override void StartAction()
    {
        if (shipStatus.LiveProjectiles) gun.StopProjectile();
        else gainEnergy = StartCoroutine(GainEnergyCoroutine());
    }

    IEnumerator GainEnergyCoroutine()
    {
        var chargePeriod = .1f;
        while (resourceSystem.CurrentEnergy < resourceSystem.MaxEnergy)
        {
            yield return new WaitForSeconds(chargePeriod);
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
        if (shipStatus.LiveProjectiles) gun.DetonateProjectile();
        else 
        {
            StopCoroutine(gainEnergy);

            if (resourceSystem.CurrentAmmo > resourceSystem.CurrentEnergy)
            {
                resourceSystem.ChangeAmmoAmount(-resourceSystem.CurrentEnergy);

                Vector3 inheritedDirection;
                if (shipStatus.Attached || shipStatus.Stationary) inheritedDirection = transform.forward;
                else inheritedDirection = shipStatus.Course;

                // TODO: WIP magic numbers
                gun.FireGun(projectileContainer.transform, 90, inheritedDirection * shipStatus.Speed, ProjectileScale * resourceSystem.CurrentEnergy, true, float.MaxValue, resourceSystem.CurrentEnergy);
                StartCheckProjectiles();
            }

            resourceSystem.ResetEnergy();
        }
        
    }
}
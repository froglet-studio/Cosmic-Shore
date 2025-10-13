using CosmicShore.Core;
using System.Collections;
using CosmicShore.Game.Projectiles;
using UnityEngine;
using CosmicShore.Game;

public class ChargedFireGunAction : ShipAction
{
    // TODO: WIP gun firing needs to be reworked
    [SerializeField] Gun gun;
    [SerializeField] float chargePerSecond = 1;

    [SerializeField] GameObject projectileContainer;

    [SerializeField] int EnergyResourceIndex = 0;
    [SerializeField] int AmmoResourceIndex = 1;

    public float ProjectileScale = 1f;

    Coroutine gainEnergy;

    public override void Initialize(IVessel vessel)
    {
        base.Initialize(vessel);
        //projectileContainer = new GameObject($"{vessel.Player.PlayerName}_Projectiles");
    }
    public override void StartAction()
    {
        if (VesselStatus.LiveProjectiles) gun.StopProjectile();
        else gainEnergy = StartCoroutine(GainEnergyCoroutine());
    }

    IEnumerator GainEnergyCoroutine()
    {
        var chargePeriod = .1f;
        while (ResourceSystem.Resources[EnergyResourceIndex].CurrentAmount < ResourceSystem.Resources[EnergyResourceIndex].MaxAmount)
        {
            yield return new WaitForSeconds(chargePeriod);
            ResourceSystem.ChangeResourceAmount(EnergyResourceIndex, chargePerSecond * chargePeriod);
        }
    }

    Coroutine checkProjectiles;

    IEnumerator CheckProjectiles()
    {
        while (projectileContainer.GetComponentsInChildren<Projectile>().Length > 0)
        {
            VesselStatus.LiveProjectiles = true;
            yield return null;
        }
        VesselStatus.LiveProjectiles = false;
    }

    void StartCheckProjectiles()
    {
        if (checkProjectiles != null)
            StopCoroutine(checkProjectiles);

        checkProjectiles = StartCoroutine(CheckProjectiles());
    }

    public override void StopAction()
    {
        if (VesselStatus.LiveProjectiles) gun.DetonateProjectile();
        else 
        {
            StopCoroutine(gainEnergy);

            if (ResourceSystem.Resources[AmmoResourceIndex].CurrentAmount > ResourceSystem.Resources[EnergyResourceIndex].CurrentAmount)
            {
                ResourceSystem.ChangeResourceAmount(AmmoResourceIndex, -ResourceSystem.Resources[EnergyResourceIndex].CurrentAmount);

                Vector3 inheritedDirection;
                if (VesselStatus.Attached || VesselStatus.IsTranslationRestricted) inheritedDirection = transform.forward;
                else inheritedDirection = VesselStatus.Course;

                // TODO: WIP magic numbers
                gun.FireGun(projectileContainer.transform, 90, inheritedDirection * VesselStatus.Speed, ProjectileScale * ResourceSystem.Resources[EnergyResourceIndex].CurrentAmount, true, float.MaxValue, ResourceSystem.Resources[EnergyResourceIndex].CurrentAmount);
                StartCheckProjectiles();
            }

            ResourceSystem.ResetResource(EnergyResourceIndex);
        }
        
    }
}
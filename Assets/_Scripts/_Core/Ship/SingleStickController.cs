using UnityEngine;
using StarWriter.Core;
using System.Collections.Generic;

public class SingleStickController : ShipController
{
    [SerializeField] Gun topGun;
    [SerializeField] GameObject projectileContainer;
    [SerializeField] float chargeDepletionRate = -.05f;

    public float ProjectileScale = 1f;
    public Vector3 BlockScale = new(4f, 4f, 1f);

    List<Gun> guns;
    bool attached = false;

    protected override void Start()
    {
        base.Start();
        ship.InputController.Portrait = true;
        shipData.Portrait = true;

        projectileContainer = new GameObject($"{ship.Player.PlayerName}_Projectiles");
        guns = new List<Gun>() { topGun};

        foreach (var gun in guns)
        {
            gun.Team = ship.Team;
            gun.Ship = ship;
        }
    }

    protected override void Update()
    {
        base.Update();

        if (resourceSystem.CurrentAmmo > 0 && shipData.GunsActive) Fire();
    }

    protected override void MoveShip()
    {
        
            base.MoveShip();
    }

    public void BigFire() // TODO: move to Gun.cs
    {
        
        if (resourceSystem.CurrentAmmo > resourceSystem.MaxAmmo / 10f) // TODO: WIP magic numbers
        {
            resourceSystem.ChangeAmmoAmount(-resourceSystem.MaxAmmo / 10f); // TODO: WIP magic numbers

            Vector3 inheritedVelocity;
            if (attached) inheritedVelocity = transform.forward;
            else inheritedVelocity = shipData.Course;

            // TODO: WIP magic numbers
            topGun.FireGun(projectileContainer.transform, 90, inheritedVelocity * shipData.Speed, ProjectileScale * 15, BlockScale * 2, true, 3f);
        }
    }

    void Fire() // TODO: move to Gun.cs
    {
        resourceSystem.ChangeAmmoAmount(chargeDepletionRate * Time.deltaTime); // TODO: this should probably be an amount not a rate. let the gun cooldown handle delta time, but then there is asymmetry with the recharge rate . . . 

        var inheritedVelocity = attached ? transform.forward * shipData.Speed : shipData.Course * shipData.Speed;

        foreach (var gun in guns) // TODO: magic number for projectile speed (30)
            gun.FireGun(projectileContainer.transform, 90, inheritedVelocity, ProjectileScale, BlockScale);
    }
}
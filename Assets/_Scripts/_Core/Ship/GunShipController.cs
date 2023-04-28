using UnityEngine;
using StarWriter.Core;
using System.Collections.Generic;

public class GunShipController : ShipController
{
    [SerializeField] Gun topGun;
    [SerializeField] Gun leftGun;
    [SerializeField] Gun rightGun;
    GameObject projectileContainer;
    [SerializeField] TrailFollower trailFollower;
    [SerializeField] float chargeDepletionRate = -.05f;
    [SerializeField] float rechargeRate = .1f;

    public float ProjectileScale = 1f;
    public Vector3 BlockScale = new(4f, 4f, 1f);

    List<Gun> guns;
    bool moveForward = true;
    bool attached = false;

    protected override void Start()
    {
        base.Start();

        projectileContainer = new GameObject($"{ship.Player.PlayerName}_Projectiles");
        guns = new List<Gun>() { topGun, leftGun, rightGun };

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
        if (shipData.Attached && !attached)
            trailFollower.Attach(shipData.AttachedTrailBlock);
        else if (!shipData.Attached && attached)
            trailFollower.Detach();

        attached = shipData.Attached;

        if (attached)
            Slide();
        else
            base.MoveShip();
    }

    public void BigFire()
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

    void Fire()
    {
        resourceSystem.ChangeAmmoAmount(chargeDepletionRate * Time.deltaTime); // TODO: this should probably be an amount not a rate. let the gun cooldown handle delta time, but then there is asymmetry with the recharge rate . . . 

        var inheritedVelocity = attached ? transform.forward * shipData.Speed : shipData.Course * shipData.Speed;

        foreach (var gun in guns) // TODO: magic number for projectile speed (30)
            gun.FireGun(projectileContainer.transform, 90, inheritedVelocity, ProjectileScale, BlockScale);
    }

    void Slide()
    {
        // TODO: magic numbers
        float lookThreshold = -.6f;
        float zeroPosition = .2f;

        var throttle = (inputController.XDiff - zeroPosition)/(1 - zeroPosition);

        if (Vector3.Dot(transform.forward, shipData.Course) < lookThreshold && throttle > 0)
             moveForward = !moveForward;

        if ((moveForward && throttle > 0) || (!moveForward && throttle < 0))
            trailFollower.SetDirection(TrailFollowerDirection.Forward);
        else
            trailFollower.SetDirection(TrailFollowerDirection.Backward);

        trailFollower.Throttle = Mathf.Abs(throttle);
        trailFollower.Move();

        SlideActions();
    }
    void SlideActions()
    {
        // TODO: should this be pulled out as an action type?          
        resourceSystem.ChangeAmmoAmount(rechargeRate * Time.deltaTime);
    }

    public void FinalBlockSlideEffects()
    {
        shipData.AttachedTrailBlock = trailFollower.AttachedTrailBlock;

        if (shipData.AttachedTrailBlock.destroyed)
            shipData.AttachedTrailBlock.Restore();

        if (shipData.AttachedTrailBlock.Team == ship.Team)
        {
            //shipData.AttachedTrailBlock.Grow(4);
            shipData.AttachedTrailBlock.ActivateShield();
        }
        else shipData.AttachedTrailBlock.Steal(ship.Player.PlayerName, ship.Team);
    }

}
using System;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Game.Projectiles;
using UnityEngine;

public class FireGunAction : ShipAction
{
    public event Action OnGunFired;
    // TODO: WIP gun firing needs to be reworked
    [SerializeField] Gun gun;

    [SerializeField] GameObject projectileContainer;
    [SerializeField] float ammoCost = .03f;

    public float ProjectileScale = 1f;
    public int Energy = 0;
    public float Speed = 90;
    public ElementalFloat ProjectileTime = new ElementalFloat(3f);

    [SerializeField] int ammoIndex = 0;
    
    public float Ammo01
    {
        get
        {
            var r = ResourceSystem.Resources[ammoIndex];
            if (r == null || r.MaxAmount <= 0f) return 0f;
            return Mathf.Clamp01(r.CurrentAmount / r.MaxAmount);
        }
    }

    public override void Initialize(IShip ship)
    {
        base.Initialize(ship);
        gun.Initialize(ship.ShipStatus);
        projectileContainer.transform.parent = Ship.ShipStatus.Player.Transform;
    }
    public override void StartAction()
    {
        if (ResourceSystem.Resources[ammoIndex].CurrentAmount >= ammoCost) 
        {
            ResourceSystem.ChangeResourceAmount(ammoIndex, - ammoCost);

            Vector3 inheritedVelocity;
            if (ShipStatus.Attached) inheritedVelocity = gun.transform.forward;
            else inheritedVelocity = ShipStatus.Course;
            OnGunFired?.Invoke(); 
            gun.FireGun(projectileContainer.transform, Speed, inheritedVelocity * ShipStatus.Speed, ProjectileScale, true, ProjectileTime.Value, 0, FiringPatterns.Default, Energy);
        }
    }

    public override void StopAction()
    {

    }
}
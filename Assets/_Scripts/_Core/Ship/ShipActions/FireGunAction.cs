using StarWriter.Core;
using UnityEngine;

public class FireGunAction : ShipAction
{
    // TODO: WIP gun firing needs to be reworked
    [SerializeField] Gun gun;

    ResourceSystem resourceSystem;
    ShipStatus shipData;
    GameObject projectileContainer;
    [SerializeField] float ammoCost = .03f;

    public float ProjectileScale = 1f;
    public int Energy = 0;
    public float Speed = 90;
    public float ProjectileTime = 3f;

    void Start()
    {
        projectileContainer = new GameObject($"{ship.Player.PlayerName}_Projectiles");
        projectileContainer.transform.parent = ship.Player.transform;
        shipData = ship.GetComponent<ShipStatus>();
        resourceSystem = ship.ResourceSystem;
    }
    public override void StartAction()
    {
        if (resourceSystem.CurrentAmmo > ammoCost) 
        {
            resourceSystem.ChangeAmmoAmount(-ammoCost);

            Vector3 inheritedVelocity;
            if (shipData.Attached) inheritedVelocity = gun.transform.forward;
            else inheritedVelocity = shipData.Course;

            // TODO: WIP magic numbers
            gun.FireGun(projectileContainer.transform, Speed, inheritedVelocity * shipData.Speed, ProjectileScale, true, ProjectileTime, 0, FiringPatterns.Default, Energy);
        }
    }

    public override void StopAction()
    {

    }
}
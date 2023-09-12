using StarWriter.Core;
using UnityEngine;

public class FireGunAction : ShipActionAbstractBase
{
    // TODO: WIP gun firing needs to be reworked
    [SerializeField] Gun gun;

    ResourceSystem resourceSystem;
    ShipStatus shipData;
    GameObject projectileContainer;
    [SerializeField] float ammoCost = .03f;

    public float ProjectileScale = 1f;
    public Vector3 BlockScale = new(4f, 4f, 1f);

    void Start()
    {
        projectileContainer = new GameObject($"{ship.Player.PlayerName}_Projectiles");
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
            gun.FireGun(projectileContainer.transform, 90, inheritedVelocity * shipData.Speed, ProjectileScale, true, 3f);
        }
    }

    public override void StopAction()
    {

    }


}
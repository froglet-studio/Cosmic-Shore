using StarWriter.Core;
using UnityEngine;

public class DetonateProjectilesAction : ShipActionAbstractBase
{
    // TODO: WIP gun firing needs to be reworked
    [SerializeField] Gun topGun;

    ResourceSystem resourceSystem;
    ShipData shipData;
    GameObject projectileContainer;

    public float ProjectileScale = 1f;
    public Vector3 BlockScale = new(4f, 4f, 1f);

    void Start()
    {
        projectileContainer = new GameObject($"{ship.Player.PlayerName}_Projectiles");
        shipData = ship.GetComponent<ShipData>();
        resourceSystem = ship.ResourceSystem;
    }
    public override void StartAction()
    {
        if (resourceSystem.CurrentAmmo > resourceSystem.MaxAmmo / 10f) // TODO: WIP magic numbers
        {
            resourceSystem.ChangeAmmoAmount(-resourceSystem.MaxAmmo / 10f); // TODO: WIP magic numbers

            Vector3 inheritedVelocity;
            if (shipData.Attached) inheritedVelocity = transform.forward;
            else inheritedVelocity = shipData.Course;

            // TODO: WIP magic numbers
            topGun.FireGun(projectileContainer.transform, 90, inheritedVelocity * shipData.Speed, ProjectileScale * 15, BlockScale * 2, true, 3f);
        }
    }

    public override void StopAction()
    {

    }


}
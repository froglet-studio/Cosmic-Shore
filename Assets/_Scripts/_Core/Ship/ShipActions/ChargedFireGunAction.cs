using StarWriter.Core;
using System.Collections;
using UnityEngine;

public class ChargedFireGunAction : ShipActionAbstractBase
{
    // TODO: WIP gun firing needs to be reworked
    [SerializeField] Gun topGun;

    ResourceSystem resourceSystem;
    ShipData shipData;
    GameObject projectileContainer;
    float ammoCost;

    public float ProjectileScale = 1f;
    public Vector3 BlockScale = new(4f, 4f, 1f);
    
    float charge = 0;
    [SerializeField] float chargePerSecond = 1;
    Coroutine gainCharge;

    void Start()
    {
        projectileContainer = new GameObject($"{ship.Player.PlayerName}_Projectiles");
        shipData = ship.GetComponent<ShipData>();
        resourceSystem = ship.ResourceSystem;
        ammoCost = resourceSystem.MaxAmmo / 10f; // TODO: WIP magic numbers
    }
    public override void StartAction()
    {
        gainCharge = StartCoroutine(GainChargeCoroutine());
    }

    IEnumerator GainChargeCoroutine()
    {
        yield return new WaitForSeconds(.5f);
        charge += chargePerSecond * .5f;
    }

    public override void StopAction()
    {
        StopCoroutine(gainCharge);
        if (resourceSystem.CurrentAmmo > ammoCost)
        {
            resourceSystem.ChangeAmmoAmount(-ammoCost);

            Vector3 inheritedVelocity;
            if (shipData.Attached) inheritedVelocity = transform.forward;
            else inheritedVelocity = shipData.Course;

            // TODO: WIP magic numbers
            topGun.FireGun(projectileContainer.transform, 90, inheritedVelocity * shipData.Speed, ProjectileScale * 15, BlockScale * 2, true, 3f, charge);
        }
        charge = 0;
    }


}
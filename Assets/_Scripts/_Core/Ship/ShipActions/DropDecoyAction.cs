using _Scripts._Core.Ship.Projectiles;
using StarWriter.Core;
using UnityEngine;

public class DropDecoyAction : ShipAction
{
    [SerializeField] float decoysPerFullAmmo = 3;
    [SerializeField] float dropForwardDistance = 40;
    [SerializeField] float dropRadiusMinRange = 40;
    [SerializeField] float dropRadiusMaxRange = 60;
    [SerializeField] FakeCrystal decoy;


    public override void StartAction()
    {
        if (resourceSystem.CurrentAmmo > resourceSystem.MaxAmmo / decoysPerFullAmmo)
        {
            resourceSystem.ChangeAmmoAmount(-resourceSystem.MaxAmmo / decoysPerFullAmmo);

            var fake = Instantiate(decoy).GetComponent<FakeCrystal>();
            if (ship.Player.tag == "Player") fake.isplayer = true;
            fake.Team = ship.Team;
            fake.ItemType = ItemType.Debuff;
            fake.transform.position = ship.transform.position;
            fake.transform.position += Quaternion.Euler(0, 0, Random.Range(0, 360)) * ship.transform.up * Random.Range(dropRadiusMinRange, dropRadiusMaxRange);
            fake.transform.position += ship.transform.forward * dropForwardDistance;
        }
    }

    public override void StopAction()
    {

    }
}
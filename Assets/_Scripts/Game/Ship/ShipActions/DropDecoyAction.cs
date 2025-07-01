using CosmicShore.Game;
using CosmicShore.Game.Projectiles;
using UnityEngine;

public class DropDecoyAction : ShipAction
{
    [SerializeField] float decoysPerFullAmmo = 3;
    [SerializeField] float dropForwardDistance = 40;
    [SerializeField] float dropRadiusMinRange = 40;
    [SerializeField] float dropRadiusMaxRange = 60;
    [SerializeField] FakeCrystal decoy;

    [SerializeField] int resourceIndex = 0;
    float resourceCost;

    public override void Initialize(IShip ship)
    {
        base.Initialize(ship);
        resourceCost = ResourceSystem.Resources[resourceIndex].MaxAmount / decoysPerFullAmmo;
    }

    public override void StartAction()
    {
        if (ResourceSystem.Resources[resourceIndex].CurrentAmount > resourceCost)
        {
            ResourceSystem.ChangeResourceAmount(resourceIndex, -resourceCost);

            var fake = Instantiate(decoy).GetComponent<FakeCrystal>();
            if (Player.ActivePlayer && Player.ActivePlayer.Ship == Ship) fake.isplayer = true;
            fake.OwnTeam = Ship.ShipStatus.Team;
            fake.ItemType = ItemType.Debuff;
            fake.transform.position = Ship.Transform.position;
            fake.transform.position += Quaternion.Euler(0, 0, Random.Range(0, 360)) * Ship.Transform.up * Random.Range(dropRadiusMinRange, dropRadiusMaxRange);
            fake.transform.position += Ship.Transform.forward * dropForwardDistance;
        }
    }

    public override void StopAction()
    {

    }
}
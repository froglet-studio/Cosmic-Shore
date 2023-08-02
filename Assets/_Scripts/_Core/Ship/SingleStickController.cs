using UnityEngine;
using StarWriter.Core;
using System.Collections.Generic;

public class SingleStickController : ShipTransformer
{
    [SerializeField] Gun topGun;

    public float ProjectileScale = 1f;
    public Vector3 BlockScale = new(4f, 4f, 1f);

    List<Gun> guns;

    protected override void Start()
    {
        //ship.InputController.SingleStick = true;

        base.Start();

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
        
    }

    protected override void MoveShip()
    {
        
        base.MoveShip();
    }

}
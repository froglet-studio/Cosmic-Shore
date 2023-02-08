using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core;

public class GunShipController : ShipController
{
    [SerializeField] Gun gun;
    Player player;

    new void Start()
    {
        base.Start();
        player = GameObject.FindWithTag("Player").GetComponent<Player>();
        gun.Team = player.Team;
        gun.Ship = ship;
        
    }

    new private void Update()
    {
        base.Update();
        Fire();
    }

    override protected void Yaw()
    {
        
    }

    override protected void MoveShip()
    {
        var velocity = (minimumSpeed - (Mathf.Abs(inputController.XSum) * ThrottleScaler)) * transform.forward + (inputController.XSum * ThrottleScaler * transform.right);
        shipData.VelocityDirection = velocity.normalized;
        shipData.InputSpeed = velocity.magnitude;
        transform.position += shipData.Speed * shipData.VelocityDirection * Time.deltaTime;

    }

    void Fire()
    {
        if (inputController.XDiff > .5f)
        {
            gun.FireGun(player.transform, shipData.VelocityDirection*shipData.Speed);
        }
    }

}

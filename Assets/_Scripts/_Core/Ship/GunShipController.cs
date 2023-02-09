using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core;

public class GunShipController : ShipController
{
    [SerializeField] Gun topGun;
    [SerializeField] Gun leftGun;
    [SerializeField] Gun rightGun;

    int nextBlockIndex = 1;
    int previousBlockIndex;
    float trailLerpAmount;
    bool moveForward = true;

    float trailSpeed = 1f;

    Player player;

    protected override void Start()
    {
        base.Start();
        player = GameObject.FindWithTag("Player").GetComponent<Player>();

        topGun.Team = player.Team;
        topGun.Ship = ship;

        leftGun.Team = player.Team;
        leftGun.Ship = ship;
        
        rightGun.Team = player.Team;
        rightGun.Ship = ship;
        
    }

    protected override void Update()
    {
        base.Update();
        Fire();
    }

    //override protected void Yaw()
    //{

    //}

    protected override void MoveShip()
    {
        if (shipData.Attached) 
        {
            if (shipData.AttachedTrail.TrailSpawner.trailList[(int)nextBlockIndex].destroyed)
                trailLerpAmount += trailSpeed / 4f * Time.deltaTime;
            else
                trailLerpAmount += trailSpeed * Time.deltaTime;

            if (trailLerpAmount > 1)
            {
                previousBlockIndex = nextBlockIndex;
                if (moveForward) nextBlockIndex += 2;
                else nextBlockIndex -= 2;
                trailLerpAmount -= 1f;
            }

            transform.position = Vector3.Lerp(shipData.AttachedTrail.TrailSpawner.trailList[previousBlockIndex].transform.position,
                                              shipData.AttachedTrail.TrailSpawner.trailList[nextBlockIndex].transform.position,
                                              trailLerpAmount);

            //transform.rotation = Quaternion.Lerp(shipData.AttachedTrail.TrailSpawner.trailList[previousBlockIndex].transform.rotation,
            //                                     shipData.AttachedTrail.TrailSpawner.trailList[nextBlockIndex].transform.rotation,
            //                                     trailLerpAmount);

            if (shipData.AttachedTrail.TrailSpawner.trailList[(int)previousBlockIndex].destroyed)
                shipData.AttachedTrail.TrailSpawner.trailList[(int)previousBlockIndex].Restore();
        }
        else
        {
            base.MoveShip();
        }
 



    //    var velocity = (minimumSpeed - (Mathf.Abs(inputController.XSum) * ThrottleScaler)) * transform.forward + (inputController.XSum * ThrottleScaler * transform.right);
    //    shipData.VelocityDirection = velocity.normalized;
    //    shipData.InputSpeed = velocity.magnitude;
    //    transform.position += shipData.Speed * shipData.VelocityDirection * Time.deltaTime;

    }

    void Fire()
    {
        topGun.FireGun(player.transform, shipData.VelocityDirection * shipData.Speed);
        leftGun.FireGun(player.transform, shipData.VelocityDirection * shipData.Speed);
        rightGun.FireGun(player.transform, shipData.VelocityDirection * shipData.Speed);
    }

}

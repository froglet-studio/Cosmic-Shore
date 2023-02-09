using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core;
using static UnityEditor.ShaderGraph.Internal.KeywordDependentCollection;

public class GunShipController : ShipController
{
    [SerializeField] Gun topGun;
    [SerializeField] Gun leftGun;
    [SerializeField] Gun rightGun;

    public int nextBlockIndex = 1;
    public int previousBlockIndex;
    float trailLerpAmount;
    bool moveForward = true;

    [SerializeField] float maxTrailSpeed = 1f;
    [SerializeField] float reducedTrailSpeed = 1f;

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
            Slide();
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
    void Slide()
    {
        

        var gapStep = 2;
        var trailSpawner = shipData.AttachedTrail.TrailSpawner;
        if (moveForward && ship.TrailSpawner.gap > 0) nextBlockIndex = shipData.AttachedTrail.Index + gapStep;
        else if (ship.TrailSpawner.gap > 0) nextBlockIndex = shipData.AttachedTrail.Index - gapStep;
        else if (moveForward) nextBlockIndex = shipData.AttachedTrail.Index + 1;
        else nextBlockIndex = shipData.AttachedTrail.Index - 1;

        var distance = trailSpawner.trailList[nextBlockIndex].transform.position - shipData.AttachedTrail.transform.position;
        var timeSpaceCorrectedInput = inputController.XDiff * Time.deltaTime / distance.magnitude;

        if (trailSpawner.trailList[(int)nextBlockIndex].destroyed)
            trailLerpAmount += reducedTrailSpeed * timeSpaceCorrectedInput;
        else
            trailLerpAmount += maxTrailSpeed * timeSpaceCorrectedInput;

        transform.position = Vector3.Lerp(shipData.AttachedTrail.transform.position,
                                          trailSpawner.trailList[nextBlockIndex].transform.position,
                                          trailLerpAmount);

        if (trailLerpAmount > 1)
        {
            shipData.AttachedTrail = trailSpawner.trailList[nextBlockIndex];
            trailLerpAmount -= 1f;
        }

        if (moveForward)
        {
            if (Vector3.Dot(transform.forward, distance) > 0) 
            {
                Debug.Log("moving forward pointing forward");
            } 
            else
            {
                Debug.Log("moving forward pointing backward");
                moveForward = false;
                shipData.AttachedTrail = trailSpawner.trailList[nextBlockIndex];
                trailLerpAmount = 1 - trailLerpAmount;
            }
        }
        else
        {
            if (Vector3.Dot(transform.forward, distance) > 0) 
            {
                Debug.Log("moving backward pointing backward");
            } 
            else
            {
                Debug.Log("moving backward pointing forward");
                moveForward = true;
                shipData.AttachedTrail = trailSpawner.trailList[nextBlockIndex];
                trailLerpAmount = 1 - trailLerpAmount;
            }
        }


        //transform.rotation = Quaternion.Lerp(shipData.AttachedTrail.TrailSpawner.trailList[previousBlockIndex].transform.rotation,
        //                                     shipData.AttachedTrail.TrailSpawner.trailList[nextBlockIndex].transform.rotation,
        //                                     trailLerpAmount);

        if (shipData.AttachedTrail.destroyed)
            shipData.AttachedTrail.Restore();
    }
}

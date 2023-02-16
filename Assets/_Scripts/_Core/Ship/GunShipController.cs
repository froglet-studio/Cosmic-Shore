using UnityEngine;
using StarWriter.Core;
using System.Collections.Generic;

public class GunShipController : ShipController
{
    [SerializeField] List <Gun> gunList;

    public int nextBlockIndex = 1;
    public int previousBlockIndex;
    float trailLerpAmount;
    bool moveForward = true;

    float chargeDepletionRate = -.05f;
    float rechargeRate = .1f;

    [SerializeField] float maxTrailSpeed = 1f;
    [SerializeField] float reducedTrailSpeed = 1f;

    public float ProjectileScale = 1f;
    public Vector3 BlockScale = new Vector3(1.5f, 1.5f, 3f);


    int padding = 3; 

    Player player;

    protected override void Start()
    {
        base.Start();
        player = GameObject.FindWithTag("Player").GetComponent<Player>();

        foreach (Gun gun in gunList)
        {
            gun.Team = player.Team;
            gun.Ship = ship;
        }

    }

    protected override void Update()
    {
        base.Update();
        if (resourceSystem.CurrentAmmo > 0 && shipData.GunsActive) Fire();
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
        resourceSystem.ChangeAmmoAmount(uuid, chargeDepletionRate * Time.deltaTime);
        foreach (Gun gun in gunList)
        {
            gun.FireGun(player.transform, shipData.VelocityDirection * shipData.Speed, ProjectileScale, BlockScale);
        }
    }

    void Slide()
    {

        resourceSystem.ChangeAmmoAmount(uuid, rechargeRate * Time.deltaTime);
        var gapStep = 2;
        var trailSpawner = shipData.AttachedTrailBlock.TrailSpawner;
        if (moveForward && ship.TrailSpawner.gap > 0) nextBlockIndex = shipData.AttachedTrailBlock.Index + gapStep;
        else if (ship.TrailSpawner.gap > 0) nextBlockIndex = shipData.AttachedTrailBlock.Index - gapStep;
        else if (moveForward) nextBlockIndex = shipData.AttachedTrailBlock.Index + 1;
        else nextBlockIndex = shipData.AttachedTrailBlock.Index - 1;

        var distance = trailSpawner.trailList[nextBlockIndex].transform.position - shipData.AttachedTrailBlock.transform.position;
        var timeSpaceCorrectedInput = inputController.XDiff * Time.deltaTime / distance.magnitude;

        if (trailSpawner.trailList[(int)nextBlockIndex].destroyed)
            trailLerpAmount += reducedTrailSpeed * timeSpaceCorrectedInput;
        else
            trailLerpAmount += maxTrailSpeed * timeSpaceCorrectedInput;

        transform.position = Vector3.Lerp(shipData.AttachedTrailBlock.transform.position,
                                          trailSpawner.trailList[nextBlockIndex].transform.position,
                                          trailLerpAmount);

        if (trailLerpAmount > 1)
        {
            shipData.AttachedTrailBlock = trailSpawner.trailList[nextBlockIndex];
            trailLerpAmount -= 1f;
        }

        if (nextBlockIndex < padding || nextBlockIndex > trailSpawner.trailList.Count - padding)
        {
            transform.rotation *= Quaternion.Euler(0, 180, 0);
        }



        if (Vector3.Dot(transform.forward, distance) < 0 || nextBlockIndex < padding || nextBlockIndex > trailSpawner.trailList.Count - padding)
        {
            if (moveForward) moveForward = false;
            else moveForward = true;

            shipData.AttachedTrailBlock = trailSpawner.trailList[nextBlockIndex];
            trailLerpAmount = 1 - trailLerpAmount;
        }


        //transform.rotation = Quaternion.Lerp(shipData.AttachedTrail.TrailSpawner.trailList[previousBlockIndex].transform.rotation,
        //                                     shipData.AttachedTrail.TrailSpawner.trailList[nextBlockIndex].transform.rotation,
        //                                     trailLerpAmount);

        if (shipData.AttachedTrailBlock.destroyed)
        {
            shipData.AttachedTrailBlock.Restore();
            shipData.AttachedTrailBlock.Steal(player.name, player.Team);
        }
            
    }
}

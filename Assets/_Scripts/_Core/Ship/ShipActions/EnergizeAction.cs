using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core;

public class EnergizeAction : ShipAction
{
    [SerializeField] List<FireGunAction> fireActions;

    [SerializeField] float Speed = 70;
    float defaultSpeed;

    [SerializeField] float ProjectileTime = 6;
    float defaultProjectileTime;

    [SerializeField] int Energy = 1;
    int defaultEnergy;

    private void Start()
    {
        var firstGun = fireActions[0];

        defaultSpeed = firstGun.Speed;
        defaultProjectileTime = firstGun.ProjectileTime;
        defaultEnergy = firstGun.Energy;

    }

    public override void StartAction()
    {
            foreach (FireGunAction fireaction in fireActions)
            {
                if (fireaction.Energy < Energy) fireaction.Energy = Energy;
                if (fireaction.Speed < Speed) fireaction.Speed = Speed;
                if (fireaction.ProjectileTime < ProjectileTime) fireaction.ProjectileTime = ProjectileTime;
            }
    }

    public override void StopAction()
    {
        foreach (FireGunAction fireaction in fireActions)
        {
            fireaction.Energy = defaultEnergy;
            fireaction.Speed = defaultSpeed;
            fireaction.ProjectileTime = defaultProjectileTime;
        }
    }
}
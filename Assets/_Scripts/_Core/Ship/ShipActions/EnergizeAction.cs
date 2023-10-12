using System.Collections.Generic;
using UnityEngine;

public class EnergizeAction : ShipAction
{
    [SerializeField] List<FireGunAction> fireActions;

    [SerializeField] float Speed = 70;
    float defaultSpeed;

    [SerializeField] float ProjectileTime = 6;
    float defaultProjectileTime;

    [SerializeField] int Energy = 1;
    int defaultEnergy;

    protected override void Start()
    {
        var firstGun = fireActions[0];

        defaultSpeed = firstGun.Speed;
        defaultProjectileTime = firstGun.ProjectileTime.Value;
        defaultEnergy = firstGun.Energy;

    }

    public override void StartAction()
    {
            foreach (FireGunAction fireaction in fireActions)
            {
                if (fireaction.Energy < Energy) fireaction.Energy = Energy;
                if (fireaction.Speed < Speed) fireaction.Speed = Speed;
                if (fireaction.ProjectileTime.Value < ProjectileTime) fireaction.ProjectileTime.Value = ProjectileTime;
            }
    }

    public override void StopAction()
    {
        foreach (FireGunAction fireaction in fireActions)
        {
            fireaction.Energy = defaultEnergy;
            fireaction.Speed = defaultSpeed;
            fireaction.ProjectileTime.Value = defaultProjectileTime;
        }
    }
}
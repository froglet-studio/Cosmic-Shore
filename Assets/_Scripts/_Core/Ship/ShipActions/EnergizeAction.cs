using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core;

public class EnergizeAction : ShipAction
{
    [SerializeField] float duration;
    [SerializeField] float cooldown;
    [SerializeField] List<FireBarrageAction> fireActions;

    float fastSpeed = 70;
    float slowSpeed = 7;

    float longDuration = 6;
    float shortDuration = 3;
    
    bool onCooldown = false;

    public override void StartAction()
    {
        if (!onCooldown) StartCoroutine(EnergizeCoroutine());
    }

    public override void StopAction()
    {
       
    }

    IEnumerator EnergizeCoroutine()
    {
        onCooldown = true;

        // upgrade
        foreach (FireBarrageAction fireaction in fireActions)
        {
            fireaction.Energy = 2;
            fireaction.speed = fastSpeed;
            fireaction.projectileTime = longDuration;
        }

        // wait for player to fire
        while (Ship.ResourceSystem.CurrentAmmo == Ship.ResourceSystem.MaxAmmo)
        {
            yield return null;
        }

        // last a duration
        yield return new WaitForSeconds(duration);

        // return to default values TODO: retrieve defaults
        foreach (FireBarrageAction fireaction in fireActions)
        {
            fireaction.Energy = 0;
            fireaction.speed = slowSpeed;
            fireaction.projectileTime = shortDuration;
        }

        // start Cooldown timer
        yield return new WaitForSeconds(cooldown);

        // go off cooldown
        onCooldown = false;
    }
}
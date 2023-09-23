using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core;

public class EnergizeAction : ShipActionAbstractBase
{
    [SerializeField] float duration;
    [SerializeField] float cooldown;
    [SerializeField] List<FireBarrageAction> fireActions;
    float fastSpeed = 90;
    float slowSpeed = 7;
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
        foreach (FireBarrageAction fireaction in fireActions)
        {
            fireaction.FiringPattern = FiringPatterns.HexRing;
            fireaction.speed = fastSpeed;
        }

        yield return new WaitForSeconds(duration);

        foreach (FireBarrageAction fireaction in fireActions)
        {
            fireaction.FiringPattern = FiringPatterns.single;
            fireaction.speed = slowSpeed;
        }

        yield return new WaitForSeconds(cooldown);
        onCooldown = false;
    }
}
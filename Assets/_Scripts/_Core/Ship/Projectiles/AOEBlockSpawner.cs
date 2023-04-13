using System.Collections;
using System.Collections.Generic;
using StarWriter.Core;
using UnityEngine;

public class AOEBlockSpawner : AOEBlockCreation
{
    [SerializeField] SpawnableAbstractBase spawnable;

    protected override IEnumerator ExplodeCoroutine()
    {
        yield return new WaitForSeconds(ExplosionDelay);

        spawnable.Spawn().transform.SetPositionAndRotation(Ship.transform.position,Ship.transform.rotation);
        yield return new WaitForEndOfFrame();
    }
}
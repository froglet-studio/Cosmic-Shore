using UnityEngine;
using CosmicShore.Core;
using System.Collections;

public class PoolManager : PoolManagerBase
{
    [SerializeField] Ship ship;

    protected override void Awake()
    {
        base.Awake();
        StartCoroutine(WaitForShipInitialization());
    }

    IEnumerator WaitForShipInitialization()
    {
        // Wait until we have a valid ship reference and its player is initialized
        while (ship == null || ship.Player == null)
        {
            yield return new WaitForEndOfFrame();
        }
        
        transform.parent = ship.Player.Transform;
    }
}

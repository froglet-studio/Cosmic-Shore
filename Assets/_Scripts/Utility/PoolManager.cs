using UnityEngine;
using CosmicShore.Core;
using System.Collections;
using CosmicShore.Game;
using System.Collections.Generic;
using CosmicShore.Game.Projectiles;

public class PoolManager : PoolManagerBase
{
    [SerializeField, RequireInterface(typeof(IShip))] MonoBehaviour shipInstance;
    IShip Ship;

    protected override void Awake()
    {
        Ship = shipInstance as IShip;
        base.Awake();
        StartCoroutine(WaitForShipInitialization());
    }

    IEnumerator WaitForShipInitialization()
    {
        // Wait until we have a valid ship reference and its player is initialized
        while (Ship == null || Ship.ShipStatus.Player == null)
        {
            // yield return new WaitForEndOfFrame();
            yield return null;
        }
        
        transform.parent = Ship.ShipStatus.Player.Transform;
    }
}

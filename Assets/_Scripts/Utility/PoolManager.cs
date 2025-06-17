using UnityEngine;
using CosmicShore.Core;
using System.Collections;
using CosmicShore.Game;

public class PoolManager : PoolManagerBase
{
    [SerializeField, RequireInterface(typeof(IShip))] MonoBehaviour shipInstance;
    public IShip ship { get; private set; }

    protected override void Awake()
    {
        ship = shipInstance as IShip;
        base.Awake();
        StartCoroutine(WaitForShipInitialization());
    }

    IEnumerator WaitForShipInitialization()
    {
        // Wait until we have a valid ship reference and its player is initialized
        while (ship == null || ship.ShipStatus.Player == null)
        {
            yield return new WaitForEndOfFrame();
        }
        
        transform.parent = ship.ShipStatus.Player.Transform;
    }
}

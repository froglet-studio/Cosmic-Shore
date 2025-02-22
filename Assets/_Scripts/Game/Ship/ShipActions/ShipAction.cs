using CosmicShore.Core;
using CosmicShore.Game;
using System.Collections;
using UnityEngine;

public abstract class ShipAction : ElementalShipComponent
{
    public IShip Ship { get; set; }
    protected IShipStatus ShipStatus => Ship.ShipStatus;
    protected ResourceSystem ResourceSystem => Ship.ShipStatus.ResourceSystem;

    IEnumerator Start()
    {
        // Give time for components to initialize to make sure the ship object has been assigned
        yield return new WaitForSecondsRealtime(.1f);
        InitializeShipAttributes();
    }

    public abstract void StartAction();
    public abstract void StopAction();

    protected virtual void InitializeShipAttributes()
    {
        if (Ship != null)
        {
            BindElementalFloats(Ship);
        }
        else
        {
            Debug.LogErrorFormat("{0} - {1} - {2}", nameof(ShipAction), nameof(InitializeShipAttributes), "ship instance is null.");
        }
    }
}
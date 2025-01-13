using CosmicShore.Core;
using CosmicShore.Game;
using System.Collections;
using UnityEngine;

public abstract class ShipAction : ElementalShipComponent
{
    protected ResourceSystem resourceSystem;
    public IShip Ship { get; set; }
    public abstract void StartAction();
    public abstract void StopAction();

    protected virtual void Start()
    {
        StartCoroutine(InitializeShipAttributesCoroutine());
    }

    // Give time for components to initialize to make sure the ship object has been assigned
    IEnumerator InitializeShipAttributesCoroutine()
    {
        yield return new WaitForSecondsRealtime(.1f);
        InitializeShipAttributes();
    }

    protected virtual void InitializeShipAttributes()
    {
        if (Ship != null)
        {
            BindElementalFloats(Ship);
            resourceSystem = Ship.ResourceSystem;
        }
        else
        {
            Debug.LogErrorFormat("{0} - {1} - {2}", nameof(ShipAction), nameof(InitializeShipAttributes), "ship instance is null.");
        }
    }
}
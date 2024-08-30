using CosmicShore.Core;
using System.Collections;
using UnityEngine;

public abstract class ShipAction : ElementalShipComponent
{
    protected ResourceSystem resourceSystem;

    protected Ship ship;
    public Ship Ship { get => ship; set => ship = value; }
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

    void InitializeShipAttributes()
    {
        if (ship != null)
        {
            BindElementalFloats(ship);
            resourceSystem = ship.ResourceSystem;
        }
        else
        {
            Debug.LogErrorFormat("{0} - {1} - {2}", nameof(ShipAction), nameof(InitializeShipAttributes), "ship instance is null.");
        }
    }
}
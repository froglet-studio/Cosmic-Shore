using CosmicShore.Core;
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
        InitializeShipAttributes();
    }

    private void InitializeShipAttributes()
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
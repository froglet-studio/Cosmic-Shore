using StarWriter.Core;
using UnityEngine;

public abstract class ShipAction : ElementalShipComponent
{
    protected ResourceSystem resourceSystem;

    protected Ship ship;
    public Ship Ship { get => ship; set => ship = value; }
    public abstract void StartAction();
    public abstract void StopAction();

    void Start()
    {
        if (ship != null)
        {
            BindElementalFloats(ship);
            resourceSystem = ship.GetComponent<ResourceSystem>();
        }
    }
}
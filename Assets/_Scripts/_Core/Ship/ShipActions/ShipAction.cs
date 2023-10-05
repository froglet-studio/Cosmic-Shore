using StarWriter.Core;
using UnityEngine;

public abstract class ShipAction : ElementalShipComponent
{
    protected Ship ship;
    public Ship Ship { get => ship; set => ship = value; }
    public abstract void StartAction();
    public abstract void StopAction();
}
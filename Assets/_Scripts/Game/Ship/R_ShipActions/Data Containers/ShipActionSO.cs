// ShipActionSO.cs
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game;

public abstract class ShipActionSO : ScriptableObject
{
    public IShip Ship { get; private set; }
    protected IShipStatus ShipStatus => Ship?.ShipStatus;
    protected ResourceSystem ResourceSystem => ShipStatus?.ResourceSystem;
    
    public virtual void Initialize(IShip ship)
    {
        Ship = ship;
        ElementalFloatBinder.BindAndClone(this, ship, GetType().Name);
        ResetRuntime();
    }

    public virtual void ResetRuntime() { }

    public abstract void StartAction(ActionExecutorRegistry execs);
    public abstract void StopAction(ActionExecutorRegistry execs);
}

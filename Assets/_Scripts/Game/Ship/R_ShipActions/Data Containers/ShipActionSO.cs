// ShipActionSO.cs
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game;

public abstract class ShipActionSO : ScriptableObject
{
    public IVessel Ship { get; private set; }
    protected IVesselStatus ShipStatus => Ship?.VesselStatus;
    protected ResourceSystem ResourceSystem => ShipStatus?.ResourceSystem;
    
    public virtual void Initialize(IVessel ship)
    {
        Ship = ship;
        ElementalFloatBinder.BindAndClone(this, ship, GetType().Name);
        ResetRuntime();
    }

    public virtual void ResetRuntime() { }
    public virtual bool IsEdgeTriggered => false;
    public abstract void StartAction(ActionExecutorRegistry execs);
    public abstract void StopAction(ActionExecutorRegistry execs);
}

using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

public abstract class ShipActionSO : ScriptableObject
{
    public virtual void Initialize(IVessel ship)
    {
        // ElementalFloatBinder.BindAndClone(this, ship, GetType().Name);
    }

    public virtual void ResetRuntime() { }
    public virtual bool IsEdgeTriggered => false;

    /// <summary>
    /// Stateless: vessel context is passed in each call.
    /// </summary>
    public abstract void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus);
    public abstract void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus);
}
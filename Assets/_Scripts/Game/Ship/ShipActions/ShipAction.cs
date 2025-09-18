using CosmicShore.Core;
using CosmicShore.Game;

public abstract class ShipAction : ElementalShipComponent
{
    protected IVessel Vessel { get; private set; }
    protected IVesselStatus VesselStatus => Vessel.VesselStatus;
    protected ResourceSystem ResourceSystem => Vessel.VesselStatus.ResourceSystem;
    public bool IsInitialized { get; private set; }

    public virtual void Initialize(IVessel vessel)
    {
        Vessel = vessel;
        BindElementalFloats(Vessel);
        IsInitialized = true;
    }

    public abstract void StartAction();
    public abstract void StopAction();
}
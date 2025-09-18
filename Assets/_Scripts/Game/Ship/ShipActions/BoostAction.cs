using CosmicShore.Core;
using CosmicShore.Game;

public class BoostAction : ShipAction
{
    public override void Initialize(IVessel vessel)
    {
        base.Initialize(vessel);
    }
    public override void StartAction()
    {
        if (VesselStatus != null)
        {
            VesselStatus.Boosting = true;
            VesselStatus.IsStationary = false;
        }
    }

    public override void StopAction()
    {
        VesselStatus.Boosting = false;
    }
}
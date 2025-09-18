using CosmicShore.Core;

public class DetachAction : ShipAction
{
    public override void StartAction()
    {
        if (VesselStatus.Attached)
        {
            VesselStatus.Attached = false;
            VesselStatus.AttachedTrailBlock = null;
        }
    }

    public override void StopAction()
    {
        // Implementing Abstract Method
    }
}
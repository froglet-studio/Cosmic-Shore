using CosmicShore.Core;

public class DetachAction : ShipAction
{
    public override void StartAction()
    {
        if (VesselStatus.IsAttached)
        {
            VesselStatus.IsAttached = false;
            VesselStatus.AttachedPrism = null;
        }
    }

    public override void StopAction()
    {
        // Implementing Abstract Method
    }
}
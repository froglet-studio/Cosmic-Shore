using CosmicShore.Core;

public class ToggleAlignAction : ShipAction
{
    public override void StartAction()
    {
        VesselStatus.AlignmentEnabled = false;
    }

    public override void StopAction()
    {
        VesselStatus.AlignmentEnabled = true;
    }
}
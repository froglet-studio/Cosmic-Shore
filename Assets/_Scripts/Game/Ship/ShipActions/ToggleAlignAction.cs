using CosmicShore.Core;

public class ToggleAlignAction : ShipAction
{
    public override void StartAction()
    {
        ShipStatus.AlignmentEnabled = false;
    }

    public override void StopAction()
    {
        ShipStatus.AlignmentEnabled = true;
    }
}
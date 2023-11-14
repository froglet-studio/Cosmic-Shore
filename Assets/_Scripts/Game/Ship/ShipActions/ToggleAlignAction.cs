using CosmicShore.Core;

public class ToggleAlignAction : ShipAction
{
    ShipStatus shipData;

    protected override void Start()
    {
        base.Start();
        shipData = ship.ShipStatus;
    }
    public override void StartAction()
    {
        shipData.AlignmentEnabled = true;
    }

    public override void StopAction()
    {
        shipData.AlignmentEnabled = false;
    }
}
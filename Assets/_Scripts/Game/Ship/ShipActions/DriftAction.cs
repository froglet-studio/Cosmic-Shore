using CosmicShore.Core;

public class DriftAction : ShipAction
{
    ShipStatus shipData;

    protected override void Start()
    {
        base.Start();
        shipData = ship.GetComponent<ShipStatus>();
    }
    public override void StartAction()
    {
        shipData.Drifting = true;
    }

    public override void StopAction()
    {
        shipData.Drifting = false;
    }
}
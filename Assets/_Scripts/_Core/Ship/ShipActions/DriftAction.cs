using StarWriter.Core;

public class DriftAction : ShipActionAbstractBase
{
    ShipStatus shipData;

    void Start()
    {
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
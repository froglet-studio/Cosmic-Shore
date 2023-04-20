using StarWriter.Core;

public class DriftAction : ShipActionAbstractBase
{
    ShipData shipData;

    void Start()
    {
        shipData = ship.GetComponent<ShipData>();
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
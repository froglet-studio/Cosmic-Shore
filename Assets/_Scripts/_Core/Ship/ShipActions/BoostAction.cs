using StarWriter.Core;

public class BoostAction : ShipActionAbstractBase
{
    ShipData shipData;

    void Start()
    {
        shipData = ship.GetComponent<ShipData>();
    }
    public override void StartAction()
    {
        shipData.Boosting = true;
    }

    public override void StopAction()
    {
        shipData.Boosting = false;
    }
}
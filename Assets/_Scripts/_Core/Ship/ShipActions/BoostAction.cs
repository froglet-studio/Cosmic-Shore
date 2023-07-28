using StarWriter.Core;

public class BoostAction : ShipActionAbstractBase
{
    ShipStatus shipData;

    void Start()
    {
        shipData = ship.GetComponent<ShipStatus>();
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
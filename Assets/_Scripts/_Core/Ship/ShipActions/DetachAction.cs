using StarWriter.Core;

public class DetachAction : ShipActionAbstractBase
{
    ShipData shipData;

    void Start()
    {
        shipData = ship.GetComponent<ShipData>();
    }
    public override void StartAction()
    {
        if (shipData.Attached)
        {
            shipData.Attached = false;
            shipData.AttachedTrailBlock = null;
        }
    }

    public override void StopAction()
    {
    }
}
using CosmicShore.Core;

public class BoostAction : ShipAction
{
    ShipStatus shipStatus;

    protected override void InitializeShipAttributes()
    {
        base.InitializeShipAttributes();
        shipStatus = Ship.ShipStatus;
    }
    public override void StartAction()
    {
        if (shipStatus) shipStatus.Boosting = true;
    }

    public override void StopAction()
    {
        shipStatus.Boosting = false;
    }
}

using CosmicShore.Core;

public class BoostAction : ShipAction
{
    protected override void InitializeShipAttributes()
    {
        base.InitializeShipAttributes();
    }
    public override void StartAction()
    {
        if (ShipStatus != null) ShipStatus.Boosting = true;
    }

    public override void StopAction()
    {
        ShipStatus.Boosting = false;
    }
}

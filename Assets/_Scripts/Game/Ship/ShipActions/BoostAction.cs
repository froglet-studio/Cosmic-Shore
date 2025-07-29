using CosmicShore.Core;
using CosmicShore.Game;

public class BoostAction : ShipAction
{
    public override void Initialize(IShip ship)
    {
        base.Initialize(ship);
    }
    public override void StartAction()
    {
        if (ShipStatus != null)
        {
            ShipStatus.Boosting = true;
            ShipStatus.IsStationary = false;
        }
    }

    public override void StopAction()
    {
        ShipStatus.Boosting = false;
    }
}

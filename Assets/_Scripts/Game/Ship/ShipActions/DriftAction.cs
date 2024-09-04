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
        ship.ShipTransformer.PitchScaler *= 1.5f;
        ship.ShipTransformer.YawScaler *= 1.5f;
        ship.ShipTransformer.RollScaler *= 1.5f;
        shipData.Drifting = true;
    }

    public override void StopAction()
    {
        ship.ShipTransformer.PitchScaler /= 1.5f;
        ship.ShipTransformer.YawScaler /= 1.5f;
        ship.ShipTransformer.RollScaler /= 1.5f;
        shipData.Drifting = false;
    }
}
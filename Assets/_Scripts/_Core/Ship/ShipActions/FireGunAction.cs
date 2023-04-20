public class FireGunAction : ShipActionAbstractBase
{
    // TODO: WIP gun firing needs to be reworked
    public override void StartAction()
    {
        ship.GetComponent<GunShipController>().BigFire();
    }

    public override void StopAction()
    {
    }
}
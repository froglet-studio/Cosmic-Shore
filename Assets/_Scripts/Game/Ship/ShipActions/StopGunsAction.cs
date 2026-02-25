using CosmicShore.Game.Ship;

namespace CosmicShore.Game.Ship.ShipActions
{
    public class StopGunsAction : ShipAction
    {
        public override void StartAction()
        {
            VesselStatus.GunsActive = false;
        }

        public override void StopAction()
        {
        }
    }
}

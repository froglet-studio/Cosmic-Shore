using CosmicShore.Game.Ship;

namespace CosmicShore.Game.Ship.ShipActions
{
    public class ToggleAlignAction : ShipAction
    {
        public override void StartAction()
        {
            VesselStatus.AlignmentEnabled = false;
        }

        public override void StopAction()
        {
            VesselStatus.AlignmentEnabled = true;
        }
    }
}

using CosmicShore.Gameplay;

namespace CosmicShore.Gameplay
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

using CosmicShore.Game.Ship;


namespace CosmicShore.Game.Ship
{
    public class BoostAction : ShipAction
    {
        public override void Initialize(IVessel vessel)
        {
            base.Initialize(vessel);
        }
        public override void StartAction()
        {
            if (VesselStatus != null)
            {
                VesselStatus.IsBoosting = true;
                VesselStatus.IsStationary = false;
            }
        }

        public override void StopAction()
        {
            VesselStatus.IsBoosting = false;
        }
    }
}

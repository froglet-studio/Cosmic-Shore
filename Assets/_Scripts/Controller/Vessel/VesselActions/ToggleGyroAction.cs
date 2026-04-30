using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    public class ToggleGyroAction : ShipAction
    {
        InputController inputController;

        public override void Initialize(IVessel vessel)
        {
            base.Initialize(vessel);
            if (Vessel != null) inputController = Vessel.VesselStatus.InputController;
        }

        public override void StartAction()
        {
            inputController.OnToggleGyro(true);
        }

        public override void StopAction()
        {
            inputController.OnToggleGyro(false);
        }
    }
}

using UnityEngine;
using CosmicShore.Core;

namespace CosmicShore
{
    public class ShipCameraCustomizer : ElementalShipComponent
    {
        public IShip Ship { get; private set; }

        [SerializeField] ShipCameraSettings cameraSettings;
        [SerializeField] bool isOrthographic = false;

        public void Initialize(IShip ship)
        {
            Ship = ship;
            var controller = CustomCameraController.Instance;
            controller.Orthographic(isOrthographic);

            BindElementalFloats(Ship);

            if (Ship.ShipStatus.AIPilot.AutoPilotEnabled)
                return;

            controller.ApplyShipCameraSettings(cameraSettings);
            var target = cameraSettings.followTarget ? cameraSettings.followTarget : ship.Transform;
            controller.SetupGamePlayCameras(target);
        }
    }
}


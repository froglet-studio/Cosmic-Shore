using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Game;

namespace CosmicShore
{
    public class ShipCameraCustomizer : ElementalShipComponent
    {
        public IShip Ship { get; private set; }

        [SerializeField] public List<ShipCameraOverrides> ControlOverrides;
        [SerializeField] float closeCamDistance;
        [SerializeField] ElementalFloat farCamDistance;
        CameraManager cameraManager;
        public Transform FollowTarget;
        public Vector3 FollowTargetPosition;

        [SerializeField] bool isOrthographic = false;

        public void Initialize(IShip ship)
        {
            Ship = ship;
            cameraManager = CameraManager.Instance;
            cameraManager.isOrthographic = isOrthographic;

            Debug.Log($"Camera is being initialized {ship.ShipStatus.Name}");
            
            BindElementalFloats(Ship);
            ApplyShipControlOverrides(ControlOverrides);
        }

        void ApplyShipControlOverrides(List<ShipCameraOverrides> controlOverrides)
        {

            // Camera controls are only relevant for human pilots
            if (Ship.ShipStatus.AIPilot.AutoPilotEnabled)
                return;

            foreach (ShipCameraOverrides effect in controlOverrides)
            {
                Debug.Log($"<color=blue>The current effect {effect}");
                switch (effect)
                {
                    case ShipCameraOverrides.CloseCam:
                        cameraManager.CloseCamDistance = closeCamDistance;
                        cameraManager.SetOffsetPosition(new Vector3(
                            cameraManager.CurrentOffset.x,
                            cameraManager.CurrentOffset.y,
                            closeCamDistance));
                        break;
                    case ShipCameraOverrides.FarCam:
                        cameraManager.FarCamDistance = farCamDistance.Value;
                        break;
                    case ShipCameraOverrides.SetFixedFollowOffset:
                        cameraManager.SetCloseCameraActive();
                        cameraManager.SetFixedFollowOffset(FollowTargetPosition);
                        break;
                    case ShipCameraOverrides.SetFollowTarget:
                        FollowTarget.parent = null;
                        FollowTarget.position = FollowTargetPosition;
                        goto case ShipCameraOverrides.ChangeFollowTarget;
                    case ShipCameraOverrides.ChangeFollowTarget:
                        cameraManager.FollowOverride = true;
                        cameraManager.SetupGamePlayCameras();
                        break;
                    
                }
            }
        }
    }
}

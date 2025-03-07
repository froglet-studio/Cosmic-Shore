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
            cameraManager = Ship.CameraManager;
            cameraManager.FollowOverride = false;
            cameraManager.isOrthographic = isOrthographic;
            

            BindElementalFloats(Ship);
            ApplyShipControlOverrides(ControlOverrides);
        }

        void ApplyShipControlOverrides(List<ShipCameraOverrides> controlOverrides)
        {

            // Camera controls are only relevant for human pilots
            if (Ship.AIPilot.AutoPilotEnabled)
                return;

            foreach (ShipCameraOverrides effect in controlOverrides)
            {
                switch (effect)
                {
                    case ShipCameraOverrides.CloseCam:
                        cameraManager.CloseCamDistance = closeCamDistance;
                        break;
                    case ShipCameraOverrides.FarCam:
                        cameraManager.FarCamDistance = farCamDistance.Value;
                        break;
                    case ShipCameraOverrides.SetFollowTarget:
                        FollowTarget.parent = null;
                        FollowTarget.position = FollowTargetPosition;
                        goto case ShipCameraOverrides.ChangeFollowTarget;
                    case ShipCameraOverrides.ChangeFollowTarget:
                        cameraManager.FollowOverride = true;
                        cameraManager.OverrideOffset = FollowTargetPosition;
                        cameraManager.SetupGamePlayCameras();
                        break;
                    
                }
            }
        }
    }
}

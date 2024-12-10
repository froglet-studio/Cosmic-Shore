using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;

namespace CosmicShore
{
    public class ShipCameraCustomizer : ElementalShipComponent
    {
        Ship ship;

        [SerializeField] public List<ShipCameraOverrides> ControlOverrides;
        [SerializeField] float closeCamDistance;
        [SerializeField] ElementalFloat farCamDistance;
        CameraManager cameraManager;
        public Transform FollowTarget;
        public Vector3 FollowTargetPosition;

        [SerializeField] bool isOrthographic = false;

        void Start()
        {
            ship = GetComponent<Ship>();
            cameraManager = ship.cameraManager;
            cameraManager.isOrthographic = isOrthographic;

            BindElementalFloats(ship);

            ApplyShipControlOverrides(ControlOverrides);
        }

        void ApplyShipControlOverrides(List<ShipCameraOverrides> controlOverrides)
        {

            // Camera controls are only relevant for human pilots
            if (ship.AutoPilot.AutoPilotEnabled)
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
                        cameraManager.SetupGamePlayCameras();
                        break;
                    
                }
            }
        }
    }
}

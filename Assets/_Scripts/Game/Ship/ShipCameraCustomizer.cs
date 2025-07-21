using UnityEngine;
using CosmicShore.Game;
using CosmicShore.Game.CameraSystem;

namespace CosmicShore
{
    public class ShipCameraCustomizer : ElementalShipComponent, ICameraConfigurator
    {
        public IShip Ship { get; private set; }

        [SerializeField] CameraSettingsSO settings;
        CameraManager cameraManager;
        public Transform FollowTarget;
        public Vector3 FollowTargetPosition;
        [SerializeField] bool isOrthographic = false;

        public void Initialize(IShip ship)
        {
            Ship = ship;
            cameraManager = CameraManager.Instance;
            cameraManager.isOrthographic = settings != null && settings.isOrthographic;

            Debug.Log($"Camera is being initialized {ship.ShipStatus.Name}");

            BindElementalFloats(Ship);
            var cam = cameraManager.GetActiveController();
            Configure(cam);
        }

        public void Configure(ICameraController ctrl)
        {
            if (ctrl == null) return;
            ctrl.ApplySettings(settings);
        }
    }
}

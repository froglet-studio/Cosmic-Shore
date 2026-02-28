using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    public class CameraManager : Singleton<CameraManager>
    {
        [SerializeField] ThemeManagerDataContainerSO _themeManagerData;
        [SerializeField] private ScriptableEventTransform _onInitializePlayerCamera;

        // TODO - Need to have a game over event, to activate the end camera
        // += SetEndCameraActive
        // [SerializeField] private ScriptableEventNoParam _onGameOver;

        private ICameraController _playerCamera;
        private ICameraController _deathCamera;
        private ICameraController _activeController;

        [SerializeField] private CustomCameraController endCamera;
        [SerializeField] private Transform endCameraFollowTarget;
        [SerializeField] private Transform endCameraLookAtTarget;

        private Transform _playerFollowTarget;

        public Transform PlayerFollowTarget
        {
            get => _playerFollowTarget;
            set => _playerFollowTarget = value;
        }

        public override void Awake()
        {
            base.Awake();
            _playerCamera = GetOrFindCameraController("CM PlayerCam");
            _deathCamera = GetOrFindCameraController("CM DeathCam");
            endCamera = GetOrFindCameraController("CM EndCam") as CustomCameraController;
        }

        private void OnEnable()
        {
            _onInitializePlayerCamera.OnRaised += SetupGamePlayCameras;
        }

        void OnDisable()
        {
            _onInitializePlayerCamera.OnRaised -= SetupGamePlayCameras;
        }

        private ICameraController GetOrFindCameraController(string name)
        {
            Transform t = transform.Find(name);
            if (t)
            {
                var ctrl = t.GetComponent<ICameraController>();
                if (ctrl == null)
                {
                    ctrl = t.gameObject.AddComponent<CustomCameraController>();
                }
                return ctrl;
            }
            CSDebug.LogWarning($"[CameraManager] Could not find camera controller: {name}");
            return null;
        }

        public Transform GetCloseCamera() => (_playerCamera as CustomCameraController)?.transform;

        public void SetupGamePlayCameras(Transform followTarget)
        {
            if(!gameObject.activeInHierarchy) gameObject.SetActive(true);

            _playerFollowTarget = followTarget;
            _playerCamera?.SetFollowTarget(_playerFollowTarget);
            _deathCamera?.SetFollowTarget(_playerFollowTarget);
            _themeManagerData.SetBackgroundColor(Camera.main);

            SetCloseCameraActive();

            var shipGO = _playerFollowTarget.gameObject;
            var shipCustomizer = shipGO.GetComponent<VesselCameraCustomizer>();
            shipCustomizer.Configure(_playerCamera);

            // Snap camera to correct initial position to prevent retaining
            // stale end-game or transition state from the previous session.
            if (_playerCamera is CustomCameraController pcc)
                pcc.SnapToTarget();
        }

        /// <summary>
        /// Configures the end camera to follow the given target with the vessel's
        /// camera settings applied. Used by Menu_Main to follow the autopilot vessel.
        /// </summary>
        public void SetupEndCameraFollow(Transform followTarget)
        {
            if (!gameObject.activeInHierarchy) gameObject.SetActive(true);

            endCamera.SetFollowTarget(followTarget);

            var customizer = followTarget.GetComponent<VesselCameraCustomizer>();
            customizer.Configure(endCamera);

            endCamera.SnapToTarget();
            SetEndCameraActive();
            _themeManagerData.SetBackgroundColor(Camera.main);
        }

        public void SetCloseCameraActive() => SetActiveCamera(_playerCamera);

        public void SetDeathCameraActive() => SetActiveCamera(_deathCamera);

        public void SetEndCameraActive() => SetActiveCamera(endCamera);

        void SetActiveCamera(ICameraController controller)
        {
                if (_playerCamera != null) _playerCamera.Deactivate();
                if (_deathCamera != null) _deathCamera.Deactivate();
                if (endCamera != null) endCamera.Deactivate();

            controller?.Activate();
            _activeController = controller;
        }

        public ICameraController GetActiveController() => _activeController;

        /// <summary>
        /// Deactivates all managed cameras (player, death, end) without activating a replacement.
        /// Used by the menu to hand control to the Cinemachine-driven main menu camera.
        /// </summary>
        public void DeactivateAllCameras()
        {
            if (_playerCamera != null) _playerCamera.Deactivate();
            if (_deathCamera != null) _deathCamera.Deactivate();
            if (endCamera != null) endCamera.Deactivate();
            _activeController = null;
        }

        /// <summary>
        /// Snaps the player camera to its follow target's current position.
        /// Call after vessel teleport or end-game cinematic to reset the camera.
        /// </summary>
        public void SnapPlayerCameraToTarget()
        {
            if (_playerCamera is CustomCameraController pcc)
                pcc.SnapToTarget();
        }

        /// <summary>
        /// Blends the player camera from the given pose to its follow target
        /// over <paramref name="duration"/> seconds. Call after
        /// <see cref="SetupGamePlayCameras"/> to replace the snap with a smooth transition.
        /// </summary>
        public void BlendPlayerCameraFrom(Vector3 fromPos, Quaternion fromRot, float duration)
        {
            if (_playerCamera is CustomCameraController pcc)
                pcc.BlendFromPosition(fromPos, fromRot, duration);
        }

        public void SetNormalizedCloseCameraDistance(float normalizedDistance)
        {
            if (_playerCamera == null) return;
            // float close = CloseCamDistance > 0 ? CloseCamDistance : 10f;
            // float far = FarCamDistance > 0 ? FarCamDistance : 40f;
            // float distance = Mathf.Lerp(close, far, normalizedDistance);
            // _playerCamera.SetCameraDistance(distance);
        }
    }
}

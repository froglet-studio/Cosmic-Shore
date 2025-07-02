using System.Collections;
using UnityEngine;
using CosmicShore.Utilities;

namespace CosmicShore.Core
{
    public class CustomCameraController : SingletonPersistent<CustomCameraController>
    {
        public enum CameraState { MainMenu, Gameplay, Death, EndGame }

        [SerializeField] Camera mainCamera;
        [SerializeField] CameraConfig mainMenuConfig;
        [SerializeField] CameraConfig gameplayConfig;
        [SerializeField] CameraConfig deathConfig;
        [SerializeField] CameraConfig endGameConfig;

        CameraState currentState;
        Transform followTarget;
        Vector3 fixedFollowOffset;
        bool fixedFollow;
        bool isOrthographic;

        public Transform CameraTransform => mainCamera ? mainCamera.transform : transform;

        public void ApplyShipCameraSettings(ShipCameraSettings settings)
        {
            if (settings == null) return;

            gameplayConfig.followOffset.z = settings.closeCamDistance;
            gameplayConfig.maxZoomDistance = Mathf.Abs(settings.farCamDistance);

            fixedFollow = settings.fixedFollow;
            fixedFollowOffset = settings.followOffset;
            if (settings.followTarget != null)
                followTarget = settings.followTarget;
        }

        void OnEnable()
        {
            GameManager.OnPlayGame += () => SetCameraState(CameraState.Gameplay);
            GameManager.OnGameOver += () => SetCameraState(CameraState.EndGame);
        }

        void OnDisable()
        {
            GameManager.OnPlayGame -= () => SetCameraState(CameraState.Gameplay);
            GameManager.OnGameOver -= () => SetCameraState(CameraState.EndGame);
        }

        void Start()
        {
            SetCameraState(CameraState.MainMenu);
        }

        void LateUpdate()
        {
            if (followTarget != null && !fixedFollow)
            {
                mainCamera.transform.position = followTarget.position + fixedFollowOffset;
                mainCamera.transform.rotation = followTarget.rotation;
            }
        }

        void ApplyConfig(CameraConfig config)
        {
            mainCamera.fieldOfView = config.fieldOfView;
            mainCamera.nearClipPlane = config.nearClip;
            mainCamera.farClipPlane = config.farClip;
            isOrthographic = config.orthographic;
            mainCamera.orthographic = config.orthographic;
            mainCamera.orthographicSize = config.orthoSize;
            fixedFollowOffset = config.followOffset;
        }

        void SetCameraState(CameraState state)
        {
            currentState = state;
            switch (state)
            {
                case CameraState.MainMenu:
                    ApplyConfig(mainMenuConfig);
                    break;
                case CameraState.Gameplay:
                    ApplyConfig(gameplayConfig);
                    break;
                case CameraState.Death:
                    ApplyConfig(deathConfig);
                    break;
                case CameraState.EndGame:
                    ApplyConfig(endGameConfig);
                    break;
            }
        }

        // Public API matching old CameraManager
        public void SetupGamePlayCameras(Transform target)
        {
            followTarget = target;
            SetCameraState(CameraState.Gameplay);
        }

        public void SetMainMenuCameraActive() => SetCameraState(CameraState.MainMenu);
        public void SetDeathCameraActive() => SetCameraState(CameraState.Death);
        public void SetEndCameraActive() => SetCameraState(CameraState.EndGame);

        public void OnMainMenu()
        {
            SetMainMenuCameraActive();
        }

        public void ZoomCloseCameraOut(float rate)
        {
            if (fixedFollow) return;
            StartCoroutine(ZoomCoroutine(rate));
        }

        IEnumerator ZoomCoroutine(float rate)
        {
            while (fixedFollowOffset.z > -gameplayConfig.maxZoomDistance)
            {
                fixedFollowOffset.z -= Time.deltaTime * Mathf.Abs(rate);
                yield return null;
            }
        }

        public void ResetCloseCameraToNeutral(float rate)
        {
            StartCoroutine(ResetZoomCoroutine(rate));
        }

        IEnumerator ResetZoomCoroutine(float rate)
        {
            while (fixedFollowOffset.z < gameplayConfig.followOffset.z)
            {
                fixedFollowOffset.z += Time.deltaTime * Mathf.Abs(rate);
                yield return null;
            }
            fixedFollowOffset = gameplayConfig.followOffset;
        }

        public void SetFixedFollowOffset(Vector3 offset)
        {
            fixedFollow = true;
            fixedFollowOffset = offset;
        }

        public void Orthographic(bool value)
        {
            isOrthographic = value;
            mainCamera.orthographic = value;
        }

        public void SetNormalizedCloseCameraDistance(float normalized)
        {
            fixedFollowOffset.z = Mathf.Lerp(gameplayConfig.followOffset.z, -gameplayConfig.maxZoomDistance, Mathf.Clamp01(normalized));
        }

        public void SetOffsetPosition(Vector3 offset)
        {
            fixedFollowOffset = offset;
        }
    }
}

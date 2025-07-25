using UnityEngine;
using CosmicShore.Game.CameraSystem;

namespace CosmicShore.Game.CameraSystem
{
    public interface ICameraManager
    {
        Transform GetCloseCamera();
        Vector3 CurrentOffset { get; }
        void OnMainMenu();
        void SetupGamePlayCameras();
        void SetupGamePlayCameras(Transform _transform);
        void SetMainMenuCameraActive();
        void SetCloseCameraActive();
        void SetDeathCameraActive();
        void SetEndCameraActive();
        void SetFixedFollowOffset(Vector3 offset);
        ICameraController GetActiveController();
        void SetNormalizedCloseCameraDistance(float normalizedDistance);
        void SetOffsetPosition(Vector3 position);
        void ZoomCloseCameraOut(float growthRate);
        void ResetCloseCameraToNeutral(float shrinkRate);
    }
}

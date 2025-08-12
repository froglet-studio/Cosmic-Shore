using UnityEngine;

namespace CosmicShore.Game.CameraSystem
{
    public interface ICameraController
    {
        void ApplySettings(CameraSettingsSO settings);
        void SetFollowTarget(Transform target);
        void Activate();
        void Deactivate();

        void SetCameraDistance(float distance);
        float GetCameraDistance();
        float NeutralOffsetZ { get; }
        float ZoomSmoothTime { get; }
        bool AdaptiveZoomEnabled { get; }
    
        // void LerpCameraDistance(float start, float end, float duration);
    }
}
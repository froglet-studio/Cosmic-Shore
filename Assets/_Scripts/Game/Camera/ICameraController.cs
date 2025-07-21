using UnityEngine;

namespace CosmicShore.Game.CameraSystem
{
    public interface ICameraController
    {
        void ApplySettings(CameraSettingsSO settings);
        void SetFollowTarget(Transform target);
        void Activate();
        void Deactivate();
    }
}

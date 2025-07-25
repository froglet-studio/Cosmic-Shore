using UnityEngine;

namespace CosmicShore.Game.CameraSystem
{
    public interface ICameraOffsetHandler
    {
        Vector3 CurrentOffset { get; }
        bool FixedFollow { get; set; }
        void Initialize();
        void Apply();
        void Restore();
        void SetOffset(Vector3 offset);
        void SetNormalizedDistance(MonoBehaviour owner, float normalizedDistance, float closeCamDistance, float farCamDistance);
        void SetOffsetPosition(MonoBehaviour owner, Vector3 position);
        void ZoomOut(MonoBehaviour owner, float growthRate, float farCamDistance);
        void ResetToNeutral(MonoBehaviour owner, float shrinkRate, float closeCamDistance);
    }
}

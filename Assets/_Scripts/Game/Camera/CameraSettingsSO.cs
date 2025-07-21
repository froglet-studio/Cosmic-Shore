using UnityEngine;

namespace CosmicShore.Game.CameraSystem
{
    [CreateAssetMenu(fileName = "CameraSettings", menuName = "CosmicShore/Camera/CameraSettingsSO", order = 30)]
    public class CameraSettingsSO : ScriptableObject
    {
        #region Camera Parameters
        public float closeCamDistance = 10f;
        public float farCamDistance = 40f;
        public Vector3 followOffset = new Vector3(0f, 10f, -20f);
        public float followSmoothTime = 0.2f;
        public float rotationSmoothTime = 5f;
        public bool disableRotationLerp = false;
        public bool useFixedUpdate = false;
        public float fieldOfView = 60f;
        public float nearClipPlane = 0.3f;
        public float farClipPlane = 1000f;
        public bool isOrthographic = false;
        public float orthographicSize = 5f;
        #endregion
    }
}

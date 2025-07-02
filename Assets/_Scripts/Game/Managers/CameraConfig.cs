using UnityEngine;

namespace CosmicShore.Core
{
    [System.Serializable]
    public class CameraConfig
    {
        public float fieldOfView = 60f;
        public float nearClip = 0.1f;
        public float farClip = 1000f;
        public bool orthographic = false;
        public float orthoSize = 10f;
        public Vector3 followOffset = Vector3.zero;
        public float maxZoomDistance = 10f;
    }
}

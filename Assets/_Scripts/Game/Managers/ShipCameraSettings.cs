using UnityEngine;

namespace CosmicShore.Core
{
    [System.Serializable]
    public class ShipCameraSettings
    {
        public float closeCamDistance = -8f;
        public float farCamDistance = -20f;
        public bool fixedFollow = false;
        public Vector3 followOffset = Vector3.zero;
        public Transform followTarget;
    }
}

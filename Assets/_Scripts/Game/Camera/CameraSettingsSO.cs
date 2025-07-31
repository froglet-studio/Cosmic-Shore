using UnityEngine;

namespace CosmicShore.Game.CameraSystem
{
    public enum CameraMode
    {
        FixedCamera,     // “Static” offset (X,Y,Z)
        DynamicCamera,   // Zoomable between min/max distances

        Orthographic     // Orthographic projection
    }

    [CreateAssetMenu(
        fileName = "CameraSettings",
        menuName = "CosmicShore/Camera/CameraSettingsSO",
        order = 30)]
    public class CameraSettingsSO : ScriptableObject
    {
        [Tooltip("Set the type of camera. Use Fixed Camera for no smoothening or dampening features, use dynamic if you want them!")]
        public CameraMode mode = CameraMode.FixedCamera;
        
        [Tooltip("Follow Offset Values")]
        public Vector3 followOffset = new Vector3(0f, 10f, -20f);
        
        [Tooltip("This is a new name for the close cam distance value.")]
        public float dynamicMinDistance = 10f;
        [Tooltip("This is a new name for the far cam distance value.")]
        public float dynamicMaxDistance = 40f;

        [Tooltip("Used only in dynamic mode, controls the smoothening effect time.")]
        public float followSmoothTime = 0.2f;
        [Tooltip("Used only in dynamic mode, controls the smoothening effect time for vertical movement.")]
        public float rotationSmoothTime = 5f;
        public bool  disableSmoothing = false;
        
        public float nearClipPlane = 0.3f;
        public float farClipPlane  = 1000f;


        public float   orthographicSize      = 5f;            // Used only in Orthographic mode
    }
}
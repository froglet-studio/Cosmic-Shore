using UnityEngine;

namespace CosmicShore.Gameplay
{
    public enum CameraMode
    {
        FixedCamera, 
        DynamicCamera,   
        Orthographic    
    }

    [CreateAssetMenu(fileName = "CameraSettings", menuName = "ScriptableObjects/Camera/CameraSettingsSO", order = 30)]
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

        /// <summary>
        /// The whole fleet ships 12000, and this initializer is the value a NEW vessel gets: it
        /// was 1000, so a vessel whose camera asset is authored without naming this field came out
        /// with a twelfth of the fleet's draw distance. The Butterfly shipped that way — 1000 does
        /// not even cross a standard 1200-radius cell (2400 across), so the far wall of the arena
        /// was clipped away and it read as the draw distance collapsing on that one hull.
        /// 12000 clears the largest arena the game ships (Cleave's 3600-radius membrane, ~7200
        /// across, plus the Serpent's 250-unit camera setback) with room to spare.
        /// Held by `Tools/Build/check_vessel_camera_farclip.py`.
        /// </summary>
        public float farClipPlane  = 12000f;

        [Tooltip("Enable smooth zoom-out on button hold")]
        public bool enableAdaptiveZoom;

        [Tooltip("Maximum extra distance (behind target) when Adaptive Zoom is enabled")]
        public float adaptiveMaxDistance;
        
        public float   orthographicSize      = 5f;            // Used only in Orthographic mode
    }
}
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public enum CameraMode
    {
        FixedCamera = 0,
        DynamicCamera = 1,
        Orthographic = 2
    }

    [CreateAssetMenu(fileName = "CameraSettings", menuName = "ScriptableObjects/Camera/CameraSettingsSO", order = 30)]
    public class CameraSettingsSO : ScriptableObject
    {
        [Tooltip("Set the type of camera. Use Fixed Camera for no smoothening or dampening features, use dynamic if you want them!")]
        public CameraMode mode = CameraMode.FixedCamera;
        
        [Tooltip("Follow Offset Values")]
        public Vector3 followOffset = new Vector3(0f, 10f, -20f);

        [Tooltip("How far ahead of the vessel's nose the camera looks, world units. 0 (every vessel that does not " +
                 "set it) looks at the hull itself. A chase camera that looks ahead keeps the hull low in the frame " +
                 "and shows where it is going: the Stoat's is 40, its studio's chase camera.")]
        public float lookAheadDistance = 0f;

        [Tooltip("How far above the vessel (along its up) the camera's look point sits, world units. 0 = the hull's " +
                 "own height. The Stoat's is 3, its studio's chase camera.")]
        public float lookAheadLift = 0f;

        [Tooltip("Eased chase, per second: the camera's position and look point each close on their target at this " +
                 "rate (k = 1 - e^(-rate*dt)), up tied to the hull's, so the hull swings in the frame through a turn. " +
                 "0 (every vessel that does not set it) = hard-attached, as before. The Stoat's is 7, its studio's chase camera.")]
        public float chaseEaseRate = 0f;

        [Tooltip("The vertical field of view the offset and look-ahead above were framed at, degrees. The camera keeps " +
                 "the player's own field of view (a graphics setting) and moves nearer or further so the hull reads the " +
                 "same size and sits in the same place on screen as at this one. 0 (every vessel that does not set it) = " +
                 "use the offsets as written. The Stoat's is 68, its studio's camera.")]
        [Range(0f, 120f)]
        public float framingFieldOfView = 0f;
        
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
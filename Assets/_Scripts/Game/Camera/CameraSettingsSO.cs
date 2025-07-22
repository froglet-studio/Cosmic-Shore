// Assets/Scripts/CameraSystem/CameraSettingsSO.cs
using UnityEngine;
using System;

namespace CosmicShore.Game.CameraSystem
{
    /// <summary>
    /// Flags for which special override modes to apply at runtime.
    /// </summary>
    [Flags]
    public enum ControlOverrideFlags
    {
        None           = 0,
        CloseCam       = 1 << 0,
        FarCam         = 1 << 1,
        FollowTarget   = 1 << 2,
        FixedOffset    = 1 << 3,
        Orthographic   = 1 << 4
    }

    [CreateAssetMenu(
        fileName = "CameraSettings",
        menuName = "CosmicShore/Camera/CameraSettingsSO",
        order = 30)]
    public class CameraSettingsSO : ScriptableObject
    {
  
        [Tooltip("Offset from the target transform")]
        public Vector3 followOffset      = new Vector3(0f, 10f, -20f);
        [Tooltip("Damping time for SmoothDamp positioning")]
        public float   followSmoothTime  = 0.2f;
        [Tooltip("Smoothing time for rotation lerp")]
        public float   rotationSmoothTime = 5f;
        [Tooltip("If true, camera snaps instantly instead of smoothing rotation")]
        public bool    disableRotationLerp = false;
        [Tooltip("Run UpdateCamera() in FixedUpdate instead of LateUpdate")]
        public bool    useFixedUpdate     = false;

   
        [Tooltip("Geometry closer than this distance is not rendered")]
        public float nearClipPlane  = 0.3f;
        [Tooltip("Geometry farther than this distance is not rendered")]
        public float farClipPlane   = 1000f;
        
    
        [Tooltip("Select which override modes to apply when this SO is used")]
        public ControlOverrideFlags controlOverrides = ControlOverrideFlags.None;

  
        [Tooltip("Distance behind the ship in CloseCam mode")]
        public float closeCamDistance = 10f;
        [Tooltip("Distance behind the ship in FarCam mode")]
        public float farCamDistance   = 40f;

 
        [Tooltip("If using FollowTarget override, reposition target here before following")]
        public Vector3 followTargetPosition = Vector3.zero;


        [Tooltip("If using FixedOffset override, lock camera here (world-space offset)")]
        public Vector3 fixedOffsetPosition = Vector3.zero;

 
        [Tooltip("Orthographic size when Orthographic override is used")]
        public float orthographicSize = 5f;
    }
}

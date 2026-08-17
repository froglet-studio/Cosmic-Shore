using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The camera behaviour a <see cref="MenuCameraConfigSO"/> describes while the
    /// menu is in autopilot (lava-lamp) state. Every kind frames the LOCAL VESSEL -
    /// there is deliberately no way to point a menu camera at anything else.
    /// </summary>
    public enum MenuCameraRigKind
    {
        /// <summary>Slow horizontal orbit around the vessel - the showcase framing.</summary>
        OrbitVessel = 0,

        /// <summary>Pulled-back cinematic trail behind the vessel with soft damping.</summary>
        CinematicTrail = 1,

        /// <summary>Close, responsive chase - near the gameplay framing, minimal lag.</summary>
        ChaseTight = 2,

        /// <summary>High vantage looking down at the vessel - a slow map-like pan.</summary>
        TopDownPan = 3,
    }

    /// <summary>
    /// One menu camera configuration for Menu_Main's autopilot state - framing, smoothing,
    /// lens, and how long the blend to/from the gameplay camera lasts when entering or
    /// leaving freestyle from this framing.
    ///
    /// The target is NOT part of the configuration: every config frames the local vessel,
    /// structurally - <see cref="MainMenuCameraController"/> resolves the vessel each frame
    /// and a config has no field with which to retarget. Driven without Cinemachine.
    ///
    /// Contains only serialized data plus pure, stateless pose math (same pattern as
    /// <see cref="CameraSettingsSO"/> / ElementalBarsConfigSO helpers).
    /// </summary>
    [CreateAssetMenu(fileName = "MenuCameraConfig", menuName = "ScriptableObjects/Camera/MenuCameraConfigSO", order = 31)]
    public class MenuCameraConfigSO : ScriptableObject
    {
        [Header("Rig")]
        [Tooltip("Which camera behaviour this configuration uses. All kinds frame the local vessel.")]
        public MenuCameraRigKind rigKind = MenuCameraRigKind.OrbitVessel;

        [Header("Trail / Chase Framing")]
        [Tooltip("Offset from the vessel for CinematicTrail / ChaseTight. Expressed in the vessel's " +
                 "frame (yaw-only by default, see below).")]
        public Vector3 followOffset = new(0f, 50f, -80f);

        [Tooltip("When on, the offset follows only the vessel's heading (yaw) and ignores pitch/roll. " +
                 "An AI pilot banks and loops constantly - yaw-only framing keeps the horizon stable " +
                 "instead of whipping the camera around every barrel roll.")]
        public bool yawOnlyOffset = true;

        [Header("Orbit Framing (OrbitVessel)")]
        [Tooltip("Horizontal distance from the vessel while orbiting.")]
        public float orbitRadius = 80f;

        [Tooltip("Height above the vessel while orbiting.")]
        public float orbitHeight = 28f;

        [Tooltip("Orbit speed in degrees per second.")]
        public float orbitDegreesPerSecond = 6f;

        [Header("Top-Down Framing (TopDownPan)")]
        [Tooltip("Height above the vessel for the top-down vantage.")]
        public float topDownHeight = 70f;

        [Tooltip("World-space back offset so the camera is not exactly vertical - a slight tilt " +
                 "keeps the look rotation well-conditioned and lets vessel facing read at a glance.")]
        public float topDownBackOffset = -12f;

        [Header("Smoothing")]
        [Tooltip("SmoothDamp time (seconds) for the camera position chasing its desired framing. " +
                 "Higher = dreamier lag; 0 = rigid attach.")]
        public float positionSmoothTime = 1f;

        [Tooltip("Exponential sharpness of the look-at rotation (same semantic as the gameplay " +
                 "camera's rotationSmoothTime: higher = snappier). 0 = instant.")]
        public float rotationSharpness = 4f;

        [Header("Lens")]
        [Tooltip("Field of view while this configuration is active. 0 = match the gameplay camera's " +
                 "FOV (recommended - guarantees no lens jump when entering freestyle).")]
        public float fieldOfView = 0f;

        [Header("Freestyle Transition")]
        [Tooltip("How long the blend between this framing and the gameplay camera lasts, both " +
                 "entering and leaving freestyle. Close framings read best short (~0.7s), distant " +
                 "framings read best longer (~1.5-2s).")]
        public float blendDuration = 1.2f;

        /// <summary>
        /// The undamped position this configuration wants, given the vessel's frame.
        /// <paramref name="offsetAnchor"/> is the rotation to apply to <see cref="followOffset"/>
        /// for trail/chase kinds (the caller supplies either the vessel's full rotation or its
        /// yaw-only anchor, per <see cref="yawOnlyOffset"/>).
        /// </summary>
        public Vector3 ComputeDesiredPosition(Vector3 targetPosition, Quaternion offsetAnchor, float orbitPhaseDegrees)
        {
            switch (rigKind)
            {
                case MenuCameraRigKind.OrbitVessel:
                    Vector3 radial = Quaternion.AngleAxis(orbitPhaseDegrees, Vector3.up) * Vector3.back * orbitRadius;
                    return targetPosition + radial + Vector3.up * orbitHeight;

                case MenuCameraRigKind.TopDownPan:
                    return targetPosition + new Vector3(0f, topDownHeight, topDownBackOffset);

                default: // CinematicTrail, ChaseTight
                    return targetPosition + offsetAnchor * followOffset;
            }
        }

        /// <summary>
        /// Up hint for the look-at rotation. Top-down looks nearly straight down, so world-up
        /// would be degenerate there - the vessel's flattened heading is used instead (which
        /// also makes the top-down view pan with the vessel's facing).
        /// </summary>
        public Vector3 ComputeLookUpHint(Quaternion targetRotation)
        {
            if (rigKind != MenuCameraRigKind.TopDownPan) return Vector3.up;

            Vector3 flatForward = Vector3.ProjectOnPlane(targetRotation * Vector3.forward, Vector3.up);
            return flatForward.sqrMagnitude > 1e-6f ? flatForward.normalized : Vector3.forward;
        }

        /// <summary>
        /// Yaw-only rotation of the vessel (heading with a level horizon). Returns false when the
        /// vessel points straight up/down, in which case the caller should keep its last good anchor.
        /// </summary>
        public static bool TryGetYawAnchor(Quaternion targetRotation, out Quaternion yawAnchor)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(targetRotation * Vector3.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 1e-6f)
            {
                yawAnchor = Quaternion.identity;
                return false;
            }

            yawAnchor = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
            return true;
        }
    }
}

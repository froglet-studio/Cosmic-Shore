using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The camera behaviour a <see cref="MenuCameraConfigSO"/> describes while the
    /// menu is in autopilot (lava-lamp) state.
    ///
    /// A config still has NO target field - the rig KIND declares what it frames, so a
    /// menu camera can never be authored to point at an arbitrary object. Every kind but
    /// <see cref="LavaLamp"/> frames the LOCAL VESSEL; the lava lamp frames the CELL
    /// (orbiting its centre, aiming at its crystal), which is the one other thing the
    /// menu has ever framed.
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

        /// <summary>
        /// The original "lava lamp": a distant, very slow orbit of the CELL CENTRE, aimed at
        /// the cell's crystal under heavy damping. The vessel is not framed at all - it is one
        /// of the things drifting through the shot. This is the only rig kind that needs no
        /// vessel, so it runs from the moment the scene loads.
        /// </summary>
        LavaLamp = 4,
    }

    /// <summary>
    /// One menu camera configuration for Menu_Main's autopilot state - framing, smoothing,
    /// lens, and how long the blend to/from the gameplay camera lasts when entering or
    /// leaving freestyle from this framing.
    ///
    /// The target is NOT part of the configuration: what a config frames is decided by its
    /// <see cref="MenuCameraRigKind"/> and resolved every frame by
    /// <see cref="MainMenuCameraController"/> (the local vessel, or - for
    /// <see cref="MenuCameraRigKind.LavaLamp"/> - the cell and its crystal). There is no field
    /// with which to retarget. Driven without Cinemachine.
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

        [Header("Lava Lamp Framing (LavaLamp)")]
        [Tooltip("Distance from the cell centre the camera orbits at. The historical rig used 350, " +
                 "which sits outside the nucleus (radius ~200) and well inside the membrane " +
                 "(radius ~1200) - at a 60 degree FOV the nucleus almost exactly fills the frame " +
                 "vertically, which is what put 'the entire cell' on display.")]
        public float lavaLampOrbitRadius = 350f;

        [Tooltip("Axis the camera orbits the cell centre about (normalized at use). The historical " +
                 "rig tilted the orbit plane 45 degrees with (0, 1, -1), so the camera rises over " +
                 "the cell and back under it rather than tracking a flat equator.")]
        public Vector3 lavaLampOrbitAxis = new(0f, 1f, -1f);

        [Tooltip("Orbit speed in degrees per second. The historical rig ran ~2.83 deg/s - a full " +
                 "lap every ~2 minutes. This is deliberately slow enough that the camera reads as " +
                 "still and ALL the motion on screen belongs to the vessels, trails and crystals.")]
        public float lavaLampDegreesPerSecond = 2.83f;

        [Tooltip("Direction from the cell centre to the camera at orbit phase zero (normalized at " +
                 "use). Only its component perpendicular to the orbit axis sweeps; the parallel " +
                 "component fixes how far off the axis the orbit sits.")]
        public Vector3 lavaLampStartDirection = new(0f, 0f, -1f);

        [Tooltip("World-space vertical lift applied on top of the orbit position, so the framing " +
                 "sits slightly above the cell's midline. The historical rig used 30.")]
        public float lavaLampHeightOffset = 30f;

        [Tooltip("Aim at the cell's crystal (the original behaviour - the slowly-respawning crystal " +
                 "gives the camera something alive to drift toward). Off aims at the cell centre, " +
                 "for a perfectly still frame. Falls back to the centre whenever no crystal exists.")]
        public bool lavaLampAimAtCrystal = true;

        [Range(0.5f, 0.999f)]
        [Tooltip("CAMERA ROLL DIAL. World-up as the look hint gives EXACTLY a level horizon, so the " +
                 "camera only ever rolls once the view gets vertical enough that this blend engages " +
                 "and slides the hint toward the orbit axis. Value is |dot(viewDir, up)| - 1.0 is " +
                 "straight down.\n\n" +
                 "Higher = less roll. This is NOT a numerical-safety limit: LookRotation stays " +
                 "well-conditioned to ~0.9999, so the only cost of raising it is that when the view " +
                 "DOES pass vertical the roll is compressed into a narrower, faster band.\n\n" +
                 "0.99 suits an INCLINED orbit (an axis perpendicular to the start direction, e.g. " +
                 "(1,1,0)), which never goes over the pole - only a crystal near the camera's own " +
                 "latitude can push the view that steep, so the blend effectively never fires. " +
                 "Lower it (~0.85) for an orbit that DOES cross the pole - the legacy (0,1,-1) cone " +
                 "- where the roll is unavoidable and wants to be spread out gently instead.")]
        public float lavaLampPoleBlendStart = 0.99f;

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
        /// True when this configuration cannot produce a pose without a vessel. The lava lamp
        /// frames the cell, so it runs before (and without) any vessel; every other kind holds
        /// its last framing until one exists.
        /// </summary>
        public bool RequiresVessel => rigKind != MenuCameraRigKind.LavaLamp;

        /// <summary>Orbit rate for whichever kind this configuration orbits with.</summary>
        public float OrbitDegreesPerSecond => rigKind switch
        {
            MenuCameraRigKind.OrbitVessel => orbitDegreesPerSecond,
            MenuCameraRigKind.LavaLamp => lavaLampDegreesPerSecond,
            _ => 0f,
        };

        /// <summary>The orbit axis this configuration sweeps about.</summary>
        public Vector3 OrbitAxis => rigKind == MenuCameraRigKind.LavaLamp
            ? Normalized(lavaLampOrbitAxis, Vector3.up)
            : Vector3.up;

        /// <summary>The radial direction from the framing centre at orbit phase zero.</summary>
        public Vector3 OrbitStartDirection => rigKind == MenuCameraRigKind.LavaLamp
            ? Normalized(lavaLampStartDirection, Vector3.back)
            : Vector3.back;

        /// <summary>
        /// The undamped position this configuration wants.
        /// <paramref name="framingCenter"/> is what the rig is anchored to - the vessel for every
        /// vessel kind, the CELL CENTRE for <see cref="MenuCameraRigKind.LavaLamp"/>.
        /// <paramref name="offsetAnchor"/> is the rotation to apply to <see cref="followOffset"/>
        /// for trail/chase kinds (the caller supplies either the vessel's full rotation or its
        /// yaw-only anchor, per <see cref="yawOnlyOffset"/>).
        /// </summary>
        public Vector3 ComputeDesiredPosition(Vector3 framingCenter, Quaternion offsetAnchor, float orbitPhaseDegrees)
        {
            switch (rigKind)
            {
                case MenuCameraRigKind.OrbitVessel:
                    Vector3 radial = Quaternion.AngleAxis(orbitPhaseDegrees, Vector3.up) * Vector3.back * orbitRadius;
                    return framingCenter + radial + Vector3.up * orbitHeight;

                case MenuCameraRigKind.LavaLamp:
                    // A rotation about a tilted axis keeps the camera on the sphere of
                    // lavaLampOrbitRadius: the start direction's perpendicular component sweeps a
                    // circle while its parallel component stays put. This is exactly what the
                    // legacy rig's per-frame position rotation traced, expressed as an absolute
                    // function of phase so it cannot drift over a long session.
                    Vector3 orbit = Quaternion.AngleAxis(orbitPhaseDegrees, OrbitAxis)
                                    * OrbitStartDirection * lavaLampOrbitRadius;
                    return framingCenter + orbit + Vector3.up * lavaLampHeightOffset;

                case MenuCameraRigKind.TopDownPan:
                    return framingCenter + new Vector3(0f, topDownHeight, topDownBackOffset);

                default: // CinematicTrail, ChaseTight
                    return framingCenter + offsetAnchor * followOffset;
            }
        }

        /// <summary>
        /// The orbit phase that puts the camera at its current bearing around
        /// <paramref name="centerToCamera"/>'s centre. Used to seed the phase on takeover and on
        /// config switches so an orbit picks up where the camera already is instead of dragging
        /// it around. Reduces to the flattened XZ bearing for <see cref="MenuCameraRigKind.OrbitVessel"/>.
        /// </summary>
        public float ComputeOrbitPhaseDegrees(Vector3 centerToCamera)
        {
            Vector3 axis = OrbitAxis;
            Vector3 sweep = Vector3.ProjectOnPlane(centerToCamera, axis);
            Vector3 startSweep = Vector3.ProjectOnPlane(OrbitStartDirection, axis);
            if (sweep.sqrMagnitude < 1e-4f || startSweep.sqrMagnitude < 1e-6f) return 0f;

            return Vector3.SignedAngle(startSweep, sweep, axis);
        }

        /// <summary>
        /// Up hint for the look-at rotation, given the direction the camera is looking.
        ///
        /// Top-down looks nearly straight down, so world-up would be degenerate there - the
        /// vessel's flattened heading is used instead (which also makes the top-down view pan with
        /// the vessel's facing). The lava lamp's tilted orbit carries it over the cell's pole,
        /// where world-up is degenerate too and a from-scratch LookRotation would roll-flip; there
        /// the hint slides toward the orbit axis, which the view direction holds a constant angle
        /// to by construction and therefore never parallels.
        /// </summary>
        public Vector3 ComputeLookUpHint(Quaternion targetRotation, Vector3 lookDirection)
        {
            switch (rigKind)
            {
                case MenuCameraRigKind.TopDownPan:
                    Vector3 flatForward = Vector3.ProjectOnPlane(targetRotation * Vector3.forward, Vector3.up);
                    return flatForward.sqrMagnitude > 1e-6f ? flatForward.normalized : Vector3.forward;

                case MenuCameraRigKind.LavaLamp:
                    return PoleSafeUp(lookDirection, OrbitAxis, lavaLampPoleBlendStart);

                default:
                    return Vector3.up;
            }
        }

        /// <summary>
        /// World-up everywhere except near the vertical singularity, where it eases into
        /// <paramref name="fallbackUp"/> so the horizon never snaps. The final guard covers a
        /// configuration whose fallback is itself vertical.
        ///
        /// <paramref name="blendStart"/> is the ROLL dial, not a safety limit — below it the
        /// horizon is exactly level, so every degree of camera roll the lava lamp ever shows comes
        /// from this blend. See <see cref="lavaLampPoleBlendStart"/> for how to pick it.
        /// </summary>
        static Vector3 PoleSafeUp(Vector3 lookDirection, Vector3 fallbackUp, float blendStart)
        {
            float start = Mathf.Clamp(blendStart, 0.5f, 0.999f);

            Vector3 direction = Normalized(lookDirection, Vector3.forward);
            float verticality = Mathf.Abs(Vector3.Dot(direction, Vector3.up));

            Vector3 up = Vector3.up;
            if (verticality > start)
                up = Vector3.Slerp(Vector3.up, fallbackUp, Mathf.InverseLerp(start, 1f, verticality));

            if (Mathf.Abs(Vector3.Dot(Normalized(up, Vector3.up), direction)) < 0.999f)
                return up;

            Vector3 perpendicular = Vector3.Cross(direction, Vector3.right);
            return perpendicular.sqrMagnitude > 1e-4f ? perpendicular : Vector3.Cross(direction, Vector3.forward);
        }

        static Vector3 Normalized(Vector3 value, Vector3 fallback) =>
            value.sqrMagnitude > 1e-6f ? value.normalized : fallback;

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

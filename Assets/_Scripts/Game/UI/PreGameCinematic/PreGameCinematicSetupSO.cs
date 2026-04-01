using UnityEngine;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// The type of pre-game cinematic camera behavior.
    /// Each mode gets a distinct visual introduction.
    /// </summary>
    public enum PreGameCinematicType
    {
        /// <summary>Default circular orbit around scene center, then transition to player.</summary>
        Orbit,
        /// <summary>Fly along the track path above segments, then settle behind the player.</summary>
        TrackFlyover,
        /// <summary>Orbit center then sweep through each player vessel before settling.</summary>
        PlayerShowcase,
        /// <summary>Wide dramatic orbit at greater distance with sweeping height changes.</summary>
        WideOrbit,
        /// <summary>Spiral inward from a distance, revealing the arena.</summary>
        SpiralReveal,
    }

    /// <summary>
    /// ScriptableObject configuration for a pre-game cinematic camera setup.
    /// Assign one per game mode via <see cref="PreGameCinematicLibrarySO"/>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PreGameCinematicSetup",
        menuName = "ScriptableObjects/Cinematics/Pre-Game Cinematic Setup")]
    public class PreGameCinematicSetupSO : ScriptableObject
    {
        [Header("Cinematic Type")]
        [Tooltip("Which camera behavior to use for this game mode's pre-game intro.")]
        public PreGameCinematicType cinematicType = PreGameCinematicType.Orbit;

        [Header("Shared Settings")]
        [Tooltip("Time to transition camera to behind the player at the end.")]
        [Range(0.3f, 4f)]
        public float transitionToPlayerTime = 1.5f;

        [Tooltip("Pause time between waypoints or key frames.")]
        [Range(0f, 2f)]
        public float pauseBetweenKeyframes = 0.3f;

        [Header("Orbit Settings (Orbit / WideOrbit)")]
        [Tooltip("Radius of the circular orbit path.")]
        public float orbitRadius = 150f;

        [Tooltip("Height above the center point.")]
        public float orbitHeight = 60f;

        [Tooltip("Total duration of the orbit phase.")]
        public float orbitDuration = 6f;

        [Tooltip("Number of segments in the orbit (more = smoother).")]
        [Range(2, 16)]
        public int orbitSegments = 4;

        [Header("Wide Orbit Extras")]
        [Tooltip("Height variation amplitude for dramatic sweeping motion.")]
        public float heightVariation = 40f;

        [Tooltip("Number of full sweep cycles during the orbit.")]
        [Range(1, 4)]
        public int sweepCycles = 2;

        [Header("Track Flyover Settings (HexRace)")]
        [Tooltip("Height above the track surface during flyover.")]
        public float flyoverHeight = 40f;

        [Tooltip("How far ahead of the track start to begin the flyover.")]
        public float flyoverLeadDistance = 50f;

        [Tooltip("Speed multiplier for traversing the track.")]
        public float flyoverSpeed = 1f;

        [Tooltip("Duration of the flyover phase.")]
        public float flyoverDuration = 5f;

        [Tooltip("Downward look angle in degrees during flyover (0 = forward, 90 = straight down).")]
        [Range(0f, 90f)]
        public float flyoverLookDownAngle = 30f;

        [Header("Player Showcase Settings (Crystal Capture)")]
        [Tooltip("Time spent focusing on each player vessel.")]
        public float perPlayerFocusTime = 1.5f;

        [Tooltip("Distance from each player vessel during focus.")]
        public float playerFocusDistance = 25f;

        [Tooltip("Height offset when focusing on a player.")]
        public float playerFocusHeight = 10f;

        [Tooltip("Do an initial orbit before showcasing players.")]
        public bool doInitialOrbitBeforeShowcase = true;

        [Tooltip("Duration of the initial orbit before showcasing (if enabled).")]
        public float initialOrbitDuration = 3f;

        [Header("Spiral Reveal Settings (Freestyle)")]
        [Tooltip("Starting distance for the spiral.")]
        public float spiralStartDistance = 250f;

        [Tooltip("Ending distance for the spiral (closer to center).")]
        public float spiralEndDistance = 80f;

        [Tooltip("Number of spiral rotations.")]
        [Range(0.5f, 4f)]
        public float spiralRotations = 1.5f;

        [Tooltip("Starting height for the spiral.")]
        public float spiralStartHeight = 120f;

        [Tooltip("Ending height for the spiral.")]
        public float spiralEndHeight = 40f;

        [Tooltip("Total duration of the spiral reveal.")]
        public float spiralDuration = 5f;
    }
}

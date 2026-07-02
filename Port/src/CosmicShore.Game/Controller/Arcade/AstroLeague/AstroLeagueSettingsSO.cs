// Ported verbatim from Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueSettingsSO.cs
// (AstroLeague arc). Mechanical substitutions only (README table).
using CosmicShore.Engine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Single designer-facing config for Astro League: match rules, kickoff pacing,
    /// billiard ball physics, impact juice, AI striker tuning, and arena visuals.
    /// All gameplay feel lives here — the MonoBehaviours only execute it.
    /// </summary>
    [CreateAssetMenu(
        fileName = "AstroLeagueSettings",
        menuName = "ScriptableObjects/Arcade/AstroLeagueSettings")]
    public class AstroLeagueSettingsSO : ScriptableObject
    {
        [Header("Match Rules")]
        [Tooltip("Regulation match length in seconds")]
        public float matchDurationSeconds = 180f;

        [Tooltip("Domain goal sum that ends the match early (mercy rule). Published to GameDataSO.GoalTargetCount.")]
        public int goalLimit = 5;

        [Tooltip("If true and the score is tied at full time, play sudden-death golden goal")]
        public bool goldenGoalOvertime = true;

        [Header("Intensity Scale (arena + ball + team spawns)")]
        [Tooltip("Arena, ball, goals and team-spawn distances scale from 1x at intensity 1 up to this " +
                 "factor at the max intensity (4). 4x is the playable ceiling (the old 10x was too big); " +
                 "intensities 1-4 step evenly 1x / 2x / 3x / 4x. Vessels stay their normal size.")]
        public float intensityScaleAtMax = 4f;

        [Tooltip("Highest intensity level used for the scale ramp (the arcade card's MaxIntensity).")]
        public int maxIntensityLevel = 4;

        [Header("Arena — Spherical Boundary (the cell nucleus)")]
        [Tooltip("Radius (at intensity 1) of the spherical play boundary. The arena scales the cell " +
                 "NUCLEUS to this radius so the nucleus sphere IS the wall, and the ball bounces " +
                 "elastically off its inner surface (a radial reflect, no collider). ~190 circumscribes " +
                 "the legacy 300x200x100 box so the goals/spawns sit inside. Scales with match intensity.")]
        public float boundaryRadius = 190f;

        [Header("Vessel Recoil (anti-clip)")]
        [Tooltip("Backward velocity (units/sec) applied to a vessel when it strikes the ball, so it " +
                 "bounces away and can't clip into the ball. Scaled by hit strength.")]
        public float vesselRecoilSpeed = 30f;

        [Tooltip("Seconds the vessel recoil impulse lasts (cosine-windowed by VesselTransformer).")]
        public float vesselRecoilDuration = 0.2f;

        [Header("Kickoff Pacing")]
        [Tooltip("Seconds of GOAL! celebration (real time) before the ball resets")]
        public float celebrationSeconds = 2.2f;

        [Tooltip("Time.timeScale during the goal celebration slow-mo. Solo sessions only — " +
                 "never applied with a second connected client (local timescale desyncs peers).")]
        [Range(0.05f, 1f)] public float celebrationTimeScale = 0.35f;

        [Tooltip("Seconds the ball stays frozen at center during a kickoff count-in")]
        public float kickoffFreezeSeconds = 2.4f;

        [Tooltip("Seconds the winner banner holds before the shared scoreboard flow takes over")]
        public float winnerBannerSeconds = 2.5f;

        [Header("Kickoff Parking")]
        [Tooltip("Distance from arena center toward a team's own goal where its vessels park for kickoff")]
        public float kickoffLineDistance = 110f;

        [Tooltip("Lateral spacing between teammates parked on the same kickoff line")]
        public float kickoffLateralSpacing = 30f;

        [Header("Ball — Vessel Strike (elastic, momentum-conserving)")]
        [Tooltip("Arcade pop on a vessel strike. The strike is a momentum-conserving ELASTIC bounce " +
                 "off the moving hull (the ball gains up to ~2× the vessel's speed on a head-on hit); " +
                 "this adds an EXTRA launch of (multiplier − 1) × vessel speed along the aim direction. " +
                 "1 = pure elastic, no extra pop.")]
        public float hitBoostMultiplier = 2.5f;

        [Tooltip("Aim bias for the strike's extra pop: 0 = along the physical contact normal " +
                 "(pure billiard deflection), 1 = along the pilot's heading (full aim control).")]
        [Range(0f, 1f)] public float directionalBias = 0.45f;

        [Tooltip("Vessel speed below this threshold is ignored (prevents ghost taps)")]
        public float minimumHitSpeed = 5f;

        [Tooltip("Minimum seconds between strikes from the SAME vessel. Dedups the hull+trigger " +
                 "double-fire AND paces dribble taps while a vessel keeps pushing the ball. The " +
                 "anti-clip depenetration runs every contact frame regardless of this cooldown, so a " +
                 "vessel can never clip the ball even between strikes.")]
        public float vesselStrikeCooldown = 0.12f;

        [Tooltip("Anti-clip: every contact frame the ball is pushed so its center is at least " +
                 "(ball radius + this) from the vessel root — guarantees the vessel hull never clips " +
                 "through the ball, including the trigger-only ships (Serpent/Sparrow) that have no " +
                 "physical depenetration. Roughly the vessel's visual hull reach.")]
        public float vesselClearRadius = 12f;

        [Header("Ball — Physics (zero friction)")]
        public float maxSpeed = 220f;
        public float ballMass = 3f;
        [Tooltip("Restitution for the ball's ELASTIC bounces off walls and vessels (1 = perfectly " +
                 "elastic). The ball has ZERO passive friction/drag — it coasts at constant speed " +
                 "between collisions. It NEVER bounces off prisms (it passes through them); the only " +
                 "thing that slows it is plowing through opposing-color prism mass (see below).")]
        [Range(0f, 1f)] public float ballBounciness = 1f;

        [Tooltip("How hard opposing-color prism MASS slows the ball as it plows through (it keeps its " +
                 "direction, only its speed drops). Per eaten prism: speed ×= ballMass / (ballMass + " +
                 "this × prismVolume). 0 = no drag (ball never slows); higher = a thick enemy wall " +
                 "brakes the ball hard. Same-color and shielded prisms cost no speed.")]
        public float prismDragMassScale = 0.25f;

        [Header("Ball — Angular Dynamics (rotational inertia)")]
        [Tooltip("Angular damping on the ball rigidbody. A small amount so spin imparted by off-center " +
                 "vessel strikes gradually settles instead of tumbling forever, while still reading as " +
                 "a freely-spinning billiard payload.")]
        public float ballAngularDamping = 0.3f;

        [Tooltip("Cap on the ball's angular speed (rad/s). Unity's default rigidbody clamp (7 rad/s) " +
                 "is too low to read as a fast spin — raise it so off-center strikes produce a " +
                 "visible tumble on the faceted icosphere.")]
        public float maxAngularSpeed = 40f;

        [Header("Ball — Mesh")]
        [Tooltip("Icosphere subdivision count for the ball mesh (each level ×4 the faces: " +
                 "0=20, 1=80, 2=320, 3=1280 tris). Level 2 is medium-poly — faceted enough that " +
                 "rotation is clearly visible, dense enough to read as round.")]
        public int ballMeshSubdivisions = 2;

        [Header("Ball — Prism Scan")]
        [Tooltip("Radius (× the ball's world radius) of the per-tick spatial scan that resolves prism " +
                 "interactions. 1 = exactly the ball's cross-section (clears a ball-sized tunnel); " +
                 "slightly above 1 catches prisms just grazing the surface. The ball is a first-class " +
                 "entity — this scan runs every physics tick on every peer, independent of colliders.")]
        public float prismScanRadiusFactor = 1.1f;

        [Header("Ball — Client Replication")]
        [Tooltip("How aggressively non-server peers blend toward the dead-reckoned ball position (higher = snappier)")]
        public float clientSmoothingRate = 12f;

        [Tooltip("Position error beyond which non-server peers snap instead of smoothing")]
        public float clientSnapDistance = 30f;

        [Header("Juice — Hitstop (solo sessions only)")]
        public float hitstopDuration = 0.045f;
        [Range(0.01f, 1f)] public float hitstopTimeScale = 0.1f;
        [Tooltip("Ball speed required to trigger hitstop on a strike")]
        public float hitstopSpeedThreshold = 70f;

        [Header("Juice — Camera Shake")]
        public float strikeShakeIntensity = 1.0f;
        public float strikeShakeDuration = 0.18f;
        public float goalShakeIntensity = 2.5f;
        public float goalShakeDuration = 0.5f;

        [Tooltip("Camera shake fades with distance from the impact, reaching zero at this radius")]
        public float shakeFalloffRadius = 180f;

        [Header("Juice — Flash & Particles")]
        [Tooltip("Seconds the ball emission spikes after a strike")]
        public float impactFlashDuration = 0.12f;
        [Tooltip("Emission multiplier at peak flash")]
        public float impactFlashIntensity = 14f;
        public int impactParticleBurst = 28;
        public int goalParticleBurst = 120;

        [Header("Ball — Speed-Reactive Visuals")]
        public float minTrailWidth = 0.6f;
        public float maxTrailWidth = 5f;
        public float minEmissionIntensity = 2.5f;
        public float maxEmissionIntensity = 11f;
        public float minLightRange = 25f;
        public float maxLightRange = 90f;
        [Tooltip("Ball speed at which speed-reactive visuals are fully maxed")]
        public float speedForMaxVisuals = 160f;

        [Header("AI Striker")]
        [Tooltip("How far behind the ball (along the shot line) the AI aims its approach")]
        public float strikerApproachLead = 18f;

        [Tooltip("When recovering position, how far past the ball the AI swings wide")]
        public float strikerRecoverDistance = 60f;

        [Header("Arena — Goal Portal Colors")]
        [Tooltip("Only the GAMEPLAY goal-portal rings are colored here. The arena no longer owns any " +
                 "boundary or atmosphere visuals — the playfield boundary read is the Cell's MembranePrefab " +
                 "and the drifting hypersea motes are the Cell's CytoplasmPrefab (CLAUDE.md ▸ \"Universality — " +
                 "one HyperSea, one rule set\"). Do not re-add an arena-local edge cage or plankton system; " +
                 "tune those on the Astro League Cell Config / its prefabs instead.")]
        public Color jadeGoalColor = new(0.15f, 1f, 0.55f, 0.5f);
        public Color rubyGoalColor = new(1f, 0.22f, 0.35f, 0.5f);
    }
}

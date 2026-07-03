using UnityEngine;

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
                 "factor at the max intensity. Kept tight (1x..2x) so all four courts play at a similar " +
                 "size: intensities 1-4 step evenly 1x / 1.33x / 1.67x / 2x. Vessels stay normal size.")]
        public float intensityScaleAtMax = 2f;

        [Tooltip("Highest intensity level used for the scale ramp (the arcade card's MaxIntensity).")]
        public int maxIntensityLevel = 4;

        [Header("Arena — Court Boundary (the cell nucleus)")]
        [Tooltip("Court geometry the ball bounces off, ONE PER INTENSITY (index 0 = intensity 1). FLAT " +
                 "polytope walls BANK the ball (billiards/air-hockey/Rocket-League feel); Sphere focuses " +
                 "it toward center (the legacy baseline); NotchedRing adds a central ring choke point. " +
                 "The cell NUCLEUS is morphed to this shape so the wall you see is the wall the ball " +
                 "hits. Default 1-4: BeveledBox, Hex, Cylinder, Sphere (central goal) — re-map freely. Falls back " +
                 "to the last entry above maxIntensityLevel.")]
        public AstroLeagueBoundaryShape[] boundaryShapesByIntensity =
        {
            AstroLeagueBoundaryShape.BeveledBox,
            AstroLeagueBoundaryShape.HexagonalPrism,
            AstroLeagueBoundaryShape.Cylinder,
            AstroLeagueBoundaryShape.Sphere,
        };

        [Tooltip("Per-intensity 'central shared goal' toggle (index 0 = intensity 1). When ON, the two " +
                 "goal detectors move to the arena CENTER facing opposite ways — ONE shared goal where " +
                 "the pass DIRECTION decides which domain scores — and the ball spawns off-center. " +
                 "Default: only intensity 4 (the Sphere). Re-map freely alongside boundaryShapesByIntensity.")]
        public bool[] centralGoalByIntensity = { false, false, false, true };

        [Tooltip("For a central-goal court: how far off the arena center (world units at intensity 1, " +
                 "along X, in the goal's plane) the ball spawns, so it doesn't start sitting in the goal.")]
        public float centralBallSpawnOffset = 70f;

        [Tooltip("Radius (at intensity 1) of the boundary ONLY when the shape is Sphere. ~190 " +
                 "circumscribes the legacy 300x200x100 court. Scales with match intensity. Polytope " +
                 "shapes derive their walls from the arena's length/width/height instead.")]
        public float boundaryRadius = 190f;

        [Tooltip("0..1 chamfer depth for the OctagonalPrism (how far the 4 goal-axis edges are cut " +
                 "toward the corner). ~0.5 reads as a clean octagon; higher = more cut.")]
        [Range(0f, 1f)] public float octagonBevelFraction = 0.5f;

        [Tooltip("0..1 chamfer depth for the BeveledBox (every edge + corner cut). Higher = rounder, " +
                 "more Rocket-League corner-ramp redirect; lower = closer to a sharp box.")]
        [Range(0f, 1f)] public float beveledBoxBevelFraction = 0.45f;

        [Header("Arena — NotchedRing (central ring obstacle)")]
        [Tooltip("Outer court the central ring sits inside, for the NotchedRing shape (default Cylinder). " +
                 "Anything except NotchedRing itself.")]
        public AstroLeagueBoundaryShape notchedRingOuterShape = AstroLeagueBoundaryShape.Cylinder;

        [Tooltip("Ring radius as a fraction of the court cross-section radius (min(width,height)/2). The " +
                 "central hole = (major − tube) and must clear the ball, so keep major above tube.")]
        [Range(0f, 1f)] public float ringMajorRadiusFraction = 0.5f;

        [Tooltip("Ring thickness (tube radius) as a fraction of the court cross-section radius. The ball " +
                 "bounces off the OUTSIDE of this tube.")]
        [Range(0f, 1f)] public float ringTubeRadiusFraction = 0.18f;

        [Tooltip("Angle (degrees, atan2(y,x)) of the notch center — the gap cut in the ring, a shooting lane.")]
        public float notchCenterDegrees = 0f;

        [Tooltip("Half-width of the notch gap in degrees (0 = a solid ring, no gap). 30 = a 60° opening.")]
        [Range(0f, 90f)] public float notchHalfWidthDegrees = 30f;

        /// <summary>Court shape for an intensity level (1-based), clamped to the configured array.</summary>
        public AstroLeagueBoundaryShape ShapeForIntensity(int intensity)
        {
            if (boundaryShapesByIntensity == null || boundaryShapesByIntensity.Length == 0)
                return AstroLeagueBoundaryShape.Box;
            int idx = Mathf.Clamp(intensity - 1, 0, boundaryShapesByIntensity.Length - 1);
            return boundaryShapesByIntensity[idx];
        }

        /// <summary>Whether the intensity (1-based) uses the central shared-goal layout.</summary>
        public bool CentralGoalForIntensity(int intensity)
        {
            if (centralGoalByIntensity == null || centralGoalByIntensity.Length == 0) return false;
            int idx = Mathf.Clamp(intensity - 1, 0, centralGoalByIntensity.Length - 1);
            return centralGoalByIntensity[idx];
        }

        [Header("Vessel Recoil (juice)")]
        [Tooltip("Backward velocity (units/sec) added to a vessel when it strikes the ball, a subtle " +
                 "'bounce off' juice. DEFAULT 0 (OFF): anti-clip is already guaranteed by the ball's own " +
                 "depenetration (EjectBallFromVessel), so any recoil only fights player control — a " +
                 "frictionless ball that keeps bouncing back into a vessel re-fires it every cooldown, " +
                 "stacking toward VesselTransformer.velocityModifierMax (100) and throwing the vessel " +
                 "back 'like crazy'. Dial up only for a deliberate subtle bounce; scaled by hit strength.")]
        public float vesselRecoilSpeed = 0f;

        [Tooltip("Seconds the vessel recoil impulse lasts (cosine-windowed by VesselTransformer).")]
        public float vesselRecoilDuration = 0.12f;

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

        [Tooltip("Wall-bounce juice (camera shake / haptic / burst) only fires when the PERPENDICULAR " +
                 "into-wall speed is at least this fraction of maxSpeed. A frictionless ball skimming " +
                 "tangentially along a curved wall has ~0 perpendicular speed, so this stops it from " +
                 "continuously shaking the camera (the high-frequency jitter).")]
        [Range(0f, 1f)] public float wallJuiceMinIntensity = 0.12f;

        [Tooltip("Minimum seconds between wall-bounce juice events — rate-limits the camera shake/haptic " +
                 "so even repeated hard bounces can't spam it. Keep ≥ strikeShakeDuration so each shake " +
                 "fully decays before the next can fire (no overlap).")]
        public float wallJuiceCooldown = 0.2f;

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

using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// How a skim racer flies: the line it holds, how hard it steers, and — the difficulty — how it
    /// falls short of perfect.
    ///
    /// <para><b>Difficulty is imperfection, never a stat</b> (ARCHITECTURE.md R7). Every tier flies
    /// the same ship with the same turn rate and the same skim economy as a human. What changes is
    /// what a human varies: how much throttle they hold, how late they react to a crystal appearing,
    /// how tight a line they can keep. <see cref="ForIntensity"/> is where the Skim Race intensity
    /// becomes a tier (decision D1: "defaults from intensity").</para>
    ///
    /// Plain data, no Unity object, so the harness can build any profile it measures.
    /// </summary>
    public sealed class SkimRacerProfile
    {
        [Tooltip("Throttle (XDiff) the pilot holds on the straights. 1 = full stick.")]
        public float Throttle = 1f;

        [Tooltip("Height of the racing line above the ribbon's centre, along the plates' own up. " +
                 "The hull clears a waypoint marker's shell with any roll at 6.5; the skimmer " +
                 "still reaches the plates up to about 8.5.")]
        public float NominalHeight = 7.5f;

        [Tooltip("Clearance kept between the hull's reach and anything solid, on top of the exact " +
                 "envelope.")]
        public float ClearanceMargin = 3.25f;

        [Tooltip("Clearance the RACING LINE keeps between the hull's reach and the plates, on top of " +
                 "the exact envelope. The line sits at this bound wherever it cuts a bend toward the " +
                 "ribbon, so it has to cover the tracking error at speed, not just a rounding error.")]
        public float LaneMargin = 1f;

        [Tooltip("Clearance (u) the plan keeps between the hull and anything that is not the ribbon - " +
                 "a wake rail, a ring. A touch resets the boost to 1, which costs several seconds to " +
                 "rebuild, so this covers the tracking error rather than a rounding error.")]
        public float ObstacleMargin = 0.75f;

        [Tooltip("How close to a crystal's centre the line is planned to pass. The collection sphere " +
                 "is 24 u and is sampled at 25 Hz — 12 u apart at 300 u/s — so the hull is caught at " +
                 "any pass under ~23 u; 20 leaves 3 u of tracking error to spare.")]
        public float PassRadius = 20f;

        [Tooltip("What leaving the skimmer's reach costs the racing line, in (rad/s)² of stick " +
                 "demand per u² per grid point. Low lets the line hop out of reach to spread a sharp " +
                 "bend; high holds it in reach at the price of slowing for the bend.")]
        public float BandWeight = 0.5f;

        [Tooltip("How far (u) across the plates the racing line may cut a bend while still skimming. " +
                 "The plate's shell is ~31 u wide; the skimmer reaches it from anywhere over it.")]
        public float LaneWidth = 12f;

        [Tooltip("Fraction of the ship's pitch/yaw authority a PLANNED manoeuvre may use, measured " +
                 "on the whole line (the ribbon's own bends included). The rest is left for the " +
                 "feedback that keeps the ship on it.")]
        public float PlanAuthority = 0.85f;

        [Tooltip("Fraction of the ship's pitch/yaw authority the line ahead may need at the current " +
                 "speed before the pilot lifts off the throttle.")]
        public float SpeedAuthority = 1.0f;

        [Tooltip("Stick authority multiplier for a ship whose belly sits at RollOffsetDegrees to the " +
                 "plates: a bend across or out of the plates then lies on a DIAGONAL of the pitch and " +
                 "yaw axes, which has up to the square root of 2 of either alone. Applies to the " +
                 "planning budget and the throttle's speed cap alike.")]
        public float RollBonus = 1f;

        [Header("Optimised line")]
        [Tooltip("Shape the line with SkimPathOptimizer (a solved offset path) rather than the " +
                 "key-and-candidate planner.")]
        public bool UseOptimizer = true;
        [Tooltip("Weight per (rad/s) squared of stick demand above the planning budget.")]
        public float OptHingeWeight = 100f;
        [Tooltip("Weight per (rad/s) squared of stick demand anywhere: picks the smoothest of the " +
                 "lines the constraints leave open.")]
        public float OptDemandWeight = 1e-4f;
        [Tooltip("Weight per u squared outside the skimmer's reach while the boost is still low.")]
        public float OptBandWeightRamp = 10f;
        [Tooltip("Weight per u squared outside the skimmer's reach at full boost.")]
        public float OptBandWeightCruise = 0.1f;
        [Tooltip("ADMM penalty on the line's point copies (per u squared): how hard each iteration " +
                 "fits the line to the projected copies. Convergence speed only, not the answer.")]
        public float OptRho = 10f;
        [Tooltip("An obstacle joins the line solve once the line passes within its radius plus this (u).")]
        public float OptObstacleActivation = 12f;
        [Tooltip("How far (u) outside the skimmer's reach of the ribbon the ship has to be before it " +
                 "MERGES onto the nearest face first and goes for its crystal from the band. Off the " +
                 "ribbon (the start, spawned a hundred units below it) every second spent winding round " +
                 "to the crystal's face is a second the exponential boost ramp has not started.")]
        public float ApproachDistance = 100000f;
        [Tooltip("Line solve: a point may only change face round an edge, never across the plates.")]
        public bool OptFaceLock = false;
        /// <summary>Tooling experiment: obstacle copies' penalty as a multiple of the solver's rho.</summary>
        public float OptObstacleRhoScale = 1f;
        /// <summary>Tooling experiment: extra solver iterations while an active obstacle is violated.</summary>
        public int OptObstacleExtraIterations = 0;
        /// <summary>Tooling experiment: post-solve passes pushing control points out of violated obstacles.</summary>
        public int OptPolishPasses = 0;
        /// <summary>Tooling experiment: carry obstacle copies + duals across re-plans.</summary>
        public bool OptCarryObstacleDuals = false;
        /// <summary>Tooling experiment: samples per line segment kept out of the plates' clearance box.</summary>
        public int OptSegmentClearanceSamples = 0;
        /// <summary>Tooling experiment: read the line's turn per unit of its own length (not ribbon arc).</summary>
        public bool PathArcCurvature = false;
        /// <summary>Tooling experiment: the optimiser's turn model per unit of the line's own length.</summary>
        public bool OptArcAware = false;
        /// <summary>Tooling experiment: share of the clearance margin the segment samples keep.</summary>
        public float OptSegmentMarginScale = 1f;
        /// <summary>Tooling experiment: obstacle copies leave their rod clear of the plates too.</summary>
        public bool OptClearAwareObstacles = false;
        /// <summary>Tooling experiment: passes pushing the seed out of obstacles before a solve.</summary>
        public int OptSeedPolishPasses = 0;
        [Tooltip("Turn round for a crystal flown past without collecting it, when it is this close, rather than " +
                 "leave it for the next lap.")]
        public bool RecoverMissedCrystal = false;
        [Tooltip("Missed crystal: farthest (u) it may be behind to turn round for.")]
        public float RecoverMaxDistance = 400f;
        [Tooltip("Missed crystal: seconds after which the turn-round gives up and leaves it for the next lap.")]
        public float RecoverMaxSeconds = 12f;
        [Tooltip("Missed crystal: go straight for a crystal this many seconds ahead when the line passes wider of it than RescueMiss.")]
        public float RescueSeconds = 0.6f;
        [Tooltip("Missed crystal: the least distance (u) ahead the rescue looks.")]
        public float RescueMinAhead = 40f;
        [Tooltip("Missed crystal: how wide (u) of the crystal the line may pass before the pilot goes straight for it.")]
        public float RescueMiss = 24f;
        [Tooltip("Until the ship first reaches the skim band flying along the ribbon, plan and throttle with the " +
                 "line's own length (not ribbon arc) and keep the plates' clearance between knots too.")]
        public bool ApproachArcAware = false;
        [Tooltip("Below this speed (u/s), plan and throttle with the line's own length rather than ribbon arc. 0 = off.")]
        public float ArcAwareBelowSpeed = 0f;
        [Tooltip("Approach: heading (deg) off the ribbon's tangent under which, in the band, the approach ends.")]
        public float ApproachEndDegrees = 30f;
        [Tooltip("Approach: samples per line segment kept out of the plates' clearance box.")]
        public int ApproachSegmentSamples = 4;
        [Tooltip("Approach: hold the whole line on the side of the plates the ship comes up to.")]
        public bool ApproachLockFace = false;
        [Tooltip("Spawned further than this (u) outside the skim band, the racer flies its start straight at " +
                 "the first crystal (round the ribbon's nearer edge if need be) before planning a line. 0 = off.")]
        public float LaunchOffBand = 0f;
        [Tooltip("Launch: how hard (rad/s per radian of heading error) the nose is turned onto its aim.")]
        public float LaunchGain = 2f;
        [Tooltip("Launch: clearance (u) kept between the hull's reach and the plates on the way.")]
        public float LaunchMargin = 3f;
        [Tooltip("Launch onto the nearer broad face's skim band (and plan the crystal from there) rather than at the crystal.")]
        public bool LaunchMerge = false;
        [Tooltip("Merging launch: the band point aimed at is this many times the ship's distance out ahead of it.")]
        public float LaunchLead = 2f;
        [Tooltip("Merging launch: the least lead (u).")]
        public float LaunchMinLead = 60f;
        [Tooltip("Merging launch: hand over to the planner this far (u) outside the skim band.")]
        public float LaunchHandover = 4f;
        [Tooltip("A line solved from nothing (or an approach) is seeded as a merge onto the nearest face.")]
        public bool SeedMerge = true;
        [Tooltip("Passes of fitting the line's speed to what it can be flown at, and re-solving (0 = plan flat out).")]
        public int SpeedFitPasses = 2;
        [Tooltip("Speed fit reads each line segment's demand at its two ends, where it peaks, not only at its middle.")]
        public bool OptFitSegmentEnds = false;
        [Tooltip("Share of the stick a stretch of line may need at its fitted speed: above the share the line is shaped to (PlanAuthority), so the speed is lowered only where the shaping could not get the line under budget.")]
        public float SpeedFitAuthority = 0.95f;
        [Tooltip("u/s of planned speed shed per unit of line when braking for a bend (the throttle shut sheds 1.5).")]
        public float PlanBrakePerUnit = 1.0f;
        [Tooltip("Throttle follows the plan's speed profile, led by the throttle lag.")]
        public bool FollowPlanSpeed = true;
        [Tooltip("EXPERIMENT: aim the throttle at the speed cap instead of shutting it when over.")]
        public bool ProportionalBrake = false;
        [Tooltip("Over the speed cap, brake onto the cap's braking curve (the throttle that lands there at the next scan) instead of shutting the throttle.")]
        public bool DeadBeatBrake = false;
        [Tooltip("EXPERIMENT: weight of the speed-change term in the turn-rate feed-forward.")]
        public float SpeedChangeFeedForward = 1f;
        [Tooltip("Seconds of flight between the optimiser's points (12 to 36 u).")]
        public float OptGridSeconds = 0.12f;
        [Tooltip("ADMM iterations for a fresh line.")]
        public int OptColdIterations = 150;
        [Tooltip("ADMM iterations to refine the line being flown.")]
        public int OptWarmIterations = 50;

        [Header("Steering")]
        [Tooltip("Natural frequency (rad/s) of the cross-track loop.")]
        public float TrackFrequency = 4f;
        [Tooltip("Damping ratio of the cross-track loop.")]
        public float TrackDamping = 0.9f;
        [Tooltip("How fast (1/s) the commanded rotation is servoed onto the one that produces the " +
                 "desired turn.")]
        public float ServoGain = 10f;
        [Tooltip("Seconds ahead the line's curvature is read, to lead the servo.")]
        public float LeadSeconds = 0f;
        [Tooltip("Roll gain toward belly-to-ribbon (1/s).")]
        public float RollGain = 3f;
        [Tooltip("Fraction of the roll authority the belly-to-ribbon roll may use. Roll is for " +
                 "clearance and wake placement only — pitch and yaw turn equally fast, so the line " +
                 "never depends on it.")]
        public float RollAuthority = 0.6f;
        [Tooltip("Angle (degrees) between the ship's belly and the plates it skims. At 45 the two " +
                 "directions a ribbon bends in both lie on diagonals of the ship's pitch and yaw " +
                 "axes, which have √2 the authority of either alone, and the wake rails are laid " +
                 "above and below the line instead of across it. 0 = square on.")]
        public float RollOffsetDegrees = 45f;

        [Tooltip("An error this large (u) is not corrected toward the old plan — the plan is " +
                 "re-anchored on where the ship actually is.")]
        public float ReanchorDistance = 10f;

        [Header("Imperfection")]
        [Tooltip("Seconds between a crystal appearing and the pilot starting to plan for it.")]
        public float ReactionSeconds = 0f;
        [Tooltip("Amplitude (u) of a slow wander added to the racing line. Lateral wander is " +
                 "symmetric; vertical wander only ever lifts the line away from the plates.")]
        public float LineWander = 0f;
        [Tooltip("Seconds per wander cycle at full speed.")]
        public float LineWanderSeconds = 3f;

        /// <summary>The expert: holds the tightest safe line with no wander and no delay. Every
        /// easier tier is this pilot with something taken away.</summary>
        public static SkimRacerProfile Expert() => new SkimRacerProfile();

        /// <summary>
        /// The tier a Skim Race intensity implies. Calibrated in Tools/Build/squirrel_ai_harness
        /// against the intensity-2 target — an AI that beats a strong human's 1:26 with about a 1:10
        /// — and ordered so a higher intensity is never an easier pilot.
        /// </summary>
        public static SkimRacerProfile ForIntensity(int intensity)
        {
            var p = Expert();
            switch (Mathf.Clamp(intensity, 1, 4))
            {
                case 1:
                    p.Throttle = 0.8f;
                    p.ReactionSeconds = 0.5f;
                    p.LineWander = 1.5f;
                    break;
                case 2:
                    p.Throttle = 0.9f;
                    p.ReactionSeconds = 0.3f;
                    p.LineWander = 1f;
                    break;
                case 3:
                    p.Throttle = 0.96f;
                    p.ReactionSeconds = 0.15f;
                    p.LineWander = 0.5f;
                    break;
            }
            return p;
        }
    }
}

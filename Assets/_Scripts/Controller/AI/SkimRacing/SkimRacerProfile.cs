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
        [Tooltip("Penalty on a rail's copy in the line solve, as a multiple of OptRho. A rail's copy " +
                 "pulls on the four control points under it where a point's copy pulls on one, so at the " +
                 "points' own penalty the line comes out of a rail too slowly to clear it inside a solve: " +
                 "5 halved rail touches in a three-racer field.")]
        public float OptObstacleRhoScale = 5f;
        [Tooltip("Passes pushing the seed out of every rail it runs through before a solve, so each " +
                 "rail's copy starts on the side the seed was nearest and they all push one way.")]
        public int OptSeedPolishPasses = 3;
        [Tooltip("Passes of fitting the line's speed to what it can be flown at, and re-solving (0 = plan flat out).")]
        public int SpeedFitPasses = 2;
        [Tooltip("Share of the stick a stretch of line may need at its fitted speed: above the share the line is shaped to (PlanAuthority), so the speed is lowered only where the shaping could not get the line under budget.")]
        public float SpeedFitAuthority = 0.95f;
        [Tooltip("u/s of planned speed shed per unit of line when braking for a bend (the throttle shut sheds 1.5).")]
        public float PlanBrakePerUnit = 1.0f;
        [Tooltip("Throttle follows the plan's speed profile, led by the throttle lag.")]
        public bool FollowPlanSpeed = true;
        [Tooltip("Seconds of flight between the optimiser's points (12 to 36 u).")]
        public float OptGridSeconds = 0.12f;
        [Tooltip("ADMM iterations for a fresh line.")]
        public int OptColdIterations = 150;
        [Tooltip("ADMM iterations to refine the line being flown.")]
        public int OptWarmIterations = 50;

        [Header("Approach")]
        [Tooltip("Until the ship first reaches the skim band flying along the ribbon (it spawns a " +
                 "hundred units off it), plan and throttle with the line's own length rather than ribbon " +
                 "arc, and keep the plates' clearance between knots as well as at them.")]
        public bool ApproachArcAware = true;
        [Tooltip("Approach: heading (deg) off the ribbon's tangent under which, in the band, the approach ends.")]
        public float ApproachEndDegrees = 30f;
        [Tooltip("Approach: samples per line segment kept out of the plates' clearance box.")]
        public int ApproachSegmentSamples = 4;

        [Header("Missed crystal")]
        [Tooltip("Turn round for a crystal flown past without collecting it, or one the line is about to " +
                 "miss, rather than leave it for the next lap: a turn-round costs a few seconds, a lap " +
                 "twenty.")]
        public bool RecoverMissedCrystal = true;
        [Tooltip("Farthest (u) behind a missed crystal may be to turn round for.")]
        public float RecoverMaxDistance = 400f;
        [Tooltip("Seconds after which a turn-round gives up and leaves the crystal for the next lap.")]
        public float RecoverMaxSeconds = 12f;
        [Tooltip("Go straight for a crystal this many seconds ahead when the line passes wider of it than RescueMiss.")]
        public float RescueSeconds = 0.6f;
        [Tooltip("The least distance (u) ahead the rescue looks.")]
        public float RescueMinAhead = 40f;
        [Tooltip("How wide (u) of the crystal the line may pass before the pilot goes straight for it.")]
        public float RescueMiss = 24f;
        [Tooltip("How hard (rad/s per radian of heading error) the nose is turned onto a turn-round's aim.")]
        public float RecoverGain = 2f;
        [Tooltip("Clearance (u) a turn-round keeps between the hull's reach and the plates.")]
        public float RecoverMargin = 3f;
        [Tooltip("Back onto the ribbon after a turn-round: the band point aimed at is this many times " +
                 "the ship's distance out ahead of it.")]
        public float MergeLead = 2f;
        [Tooltip("Back onto the ribbon after a turn-round: the least lead (u).")]
        public float MergeMinLead = 60f;

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
        /// The tier a Skim Race intensity implies (decision D1: difficulty defaults from intensity),
        /// ordered so a higher intensity is never an easier pilot.
        ///
        /// <para>Intensities 2 to 4 fly the EXPERT. The intensity-2 brief is an AI that beats a strong
        /// human's 1:26 (set against two of the old AIs) by as much as it can — about 1:10 was the
        /// ask — and the expert is still short of that (Tools/Build/squirrel_ai_harness, three-racer
        /// field: mean 81 s, race winner 77 s), so nothing is taken away there. Intensity 1 is the
        /// expert with a human's imperfections added: a tenth less throttle, 0.3 s to react to a new
        /// crystal, and a 1 u wander — 11% slower in the same field (88 s against 79 s).</para>
        /// </summary>
        public static SkimRacerProfile ForIntensity(int intensity)
        {
            var p = Expert();
            if (Mathf.Clamp(intensity, 1, 4) == 1)
            {
                p.Throttle = 0.9f;
                p.ReactionSeconds = 0.3f;
                p.LineWander = 1f;
            }
            return p;
        }
    }
}

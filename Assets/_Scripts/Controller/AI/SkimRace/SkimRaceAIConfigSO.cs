using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Skim Race pilot's POLICY: every tunable the racing controller reads, in one asset.
    ///
    /// The shipped instance lives at <c>Resources/SkimRaceAIConfig</c> so a clean build loads it
    /// with no scene wiring (<see cref="LoadDefault"/>). The values are the output of the tuning
    /// loop recorded in <c>Docs/SKIM_RACE_AI.md</c>; change them there, re-run the benchmark, and
    /// bump <see cref="PolicyVersion"/> so every benchmark record names the policy that flew it.
    ///
    /// Nothing here grants the AI anything a human pilot does not have: every field shapes how
    /// the pilot USES the normal stick/throttle/drift inputs.
    /// </summary>
    [CreateAssetMenu(fileName = "SkimRaceAIConfig", menuName = "ScriptableObjects/AI/Skim Race AI Config")]
    public class SkimRaceAIConfigSO : ScriptableObject
    {
        public const string ResourcePath = "SkimRaceAIConfig";

        [Header("Identity")]
        [Tooltip("Stamped into every benchmark record. Bump whenever a value below changes.")]
        public string PolicyVersion = "skimrace-v1";

        [Header("Deployment")]
        [Tooltip("Install the Skim Race pilot on every AI seat of a normal (non-training) Skim Race.")]
        public bool DeployInNormalPlay = true;

        [Header("Decision rate")]
        [Tooltip("Decisions per second. The action is held between decisions. 0 = every frame.")]
        [Min(0f)] public float DecisionHz = 0f;

        [Header("Racing line")]
        [Tooltip("Track lookahead in SECONDS of travel at the current speed.")]
        [Min(0f)] public float LookaheadSeconds = 0.9f;
        [Min(0f)] public float LookaheadMin = 60f;
        [Min(0f)] public float LookaheadMax = 360f;
        [Tooltip("Height above the track ribbon (along the ribbon's normal) the line is flown at - " +
                 "inside skimmer reach, clear of the hull.")]
        public float SkimHeight = 5.5f;
        [Tooltip("Half-width (arc length along the track) of the smooth bump that bends the racing " +
                 "line through the next crystal. Wider = gentler swerve, started earlier.")]
        [Min(1f)] public float CrystalBumpHalfWidth = 300f;
        [Tooltip("At LOW skim boost the crystal approach (bump width and direct-flight distance) is " +
                 "shortened by this factor, so the line stays in skim range and rebuilds boost before " +
                 "it leaves the ribbon. 1 = no change.")]
        [Range(0.05f, 1f)] public float LowBoostApproachScale = 1f;
        [Tooltip("Skim boost at and above which the full approach is used.")]
        [Min(1.01f)] public float LowBoostFull = 3f;
        [Tooltip("Boost-gated DIRECT flight: at or above this skim boost the pilot leaves the ribbon and " +
                 "flies straight at the crystal, letting each pickup's ring (+0.8) hold the boost - above " +
                 "~3.2x on these tracks pickups come faster than the 0.3/s decay. It drops back to the " +
                 "ribbon below (this - DirectBoostHysteresis) to skim the boost back up. 0 = off.")]
        [Min(0f)] public float DirectBoost = 0f;
        [Min(0f)] public float DirectBoostHysteresis = 0.7f;
        [Tooltip("Direct flight: the straight line is re-routed where the hull centre would come " +
                 "within this distance of the ribbon's contact shell (covers the 2 u wingtips).")]
        [Min(0f)] public float DirectViaClearance = 4f;
        [Tooltip("Same-face re-route: height of the via-point above the blocking plate.")]
        [Min(0f)] public float DirectViaLift = 14f;
        [Tooltip("Opposite-face re-route: lateral distance of the via-point beside the ribbon's edge " +
                 "(the shell reaches 15 u from the centreline).")]
        [Min(0f)] public float DirectViaLateral = 26f;
        [Tooltip("Whether the laid-mass guard also keeps the hull off the TRACK's contact shells: " +
                 "0 = never, 1 = only during direct flight (a straight line between crystals cuts " +
                 "across the ribbon), 2 = always.")]
        [Range(0, 2)] public int TrackGuard = 1;
        [Tooltip("Predicted hull clearance (world units) the guard keeps from the track's contact shells.")]
        [Min(0f)] public float TrackGuardMargin = 1f;
        [Tooltip("How far INSIDE the crystal's capture radius the pass point sits (safety margin).")]
        [Min(0f)] public float PassMargin = 8f;
        [Tooltip("Capture radius assumed when the crystal's collider cannot be measured.")]
        [Min(1f)] public float DefaultCaptureRadius = 24f;
        [Tooltip("The ribbon's no-fly slab, half-height along its normal: a super-shielded track " +
                 "prism's shell reaches 1.5 x its 1-unit thickness, plus the hull's half-height.")]
        [Min(0f)] public float RibbonClearHeight = 3.4f;
        [Tooltip("The ribbon's no-fly slab, half-width: the shell reaches 1.5 x the 10-unit width.")]
        [Min(0f)] public float RibbonClearLateral = 18f;
        [Tooltip("When a pass crosses to the far side of the ribbon, the height change uses this " +
                 "fraction of the bump width, so the lateral swing round the edge comes first.")]
        [Range(0.1f, 1f)] public float CrossingHeightFraction = 0.33f;
        [Tooltip("Sequenced face change: the swing past the ribbon's edge is completed one pursuit " +
                 "look-ahead BEFORE the height starts to change, so the line the pursuit actually flies " +
                 "(the chord to its look-ahead point) never cuts through the plates. Off = the original " +
                 "simultaneous swing/ramp.")]
        public bool SequencedCrossing = false;
        [Tooltip("Face change: the whole swing-and-ramp profile is started this many SECONDS of travel " +
                 "earlier (scaled by the current speed, capped at half the approach), so the hull - which " +
                 "lags its stick by ~0.67 s - is already past the plate edge when the height changes. 0 = off.")]
        [Min(0f)] public float CrossingLeadSeconds = 0f;
        [Tooltip("While a face change is being approached, line pursuit's look-ahead is multiplied by this, " +
                 "so the chord the pursuit flies hugs the line round the plate edge. 1 = off.")]
        [Range(0.1f, 1f)] public float CrossingLookaheadScale = 1f;
        [Tooltip("Throttle ceiling while within CrossingSlowDistance before a face change (a strike costs " +
                 "the whole boost; a brief lift costs far less). 1 = off.")]
        [Range(0.1f, 1f)] public float CrossingThrottle = 1f;
        [Tooltip("Arc distance before the crystal over which CrossingThrottle applies.")]
        [Min(0f)] public float CrossingSlowDistance = 150f;
        [Tooltip("Look-ahead (seconds of travel) of the safety layer that keeps the hull out of " +
                 "the ribbon's slab. 0 disables it.")]
        [Min(0f)] public float SlabGuardSeconds = 0f;
        [Tooltip("Hull veto look-ahead, seconds. The predicted hull path is tested against the " +
                 "track prisms' contact shells; 0 disables the veto.")]
        [Min(0f)] public float HullGuardSeconds = 0f;
        [Tooltip("Predicted clearance (world units) below which the hull veto fires.")]
        [Min(0f)] public float HullMargin = 1.2f;
        [Tooltip("How hard the veto's escape aim lifts away from the threatening prism.")]
        [Min(0f)] public float HullEscapeLift = 40f;
        [Tooltip("Inside this straight-line distance, with the crystal in front, the pilot flies at " +
                 "the crystal itself (removes pure pursuit's corner cutting at the pickup).")]
        [Min(0f)] public float CrystalDirectDistance = 120f;
        [Tooltip("In the terminal approach, how far the aim moves from the pass point toward the " +
                 "crystal's centre (0..1) when the centre is clear of the ribbon's slab.")]
        [Range(0f, 1f)] public float TerminalCentreBias = 0.5f;
        [Tooltip("Fly the terminal straight line to the pass point only when that straight line is " +
                 "clear of the ribbon's contact shell; otherwise keep to the racing line (which flies a " +
                 "face change round the ribbon's edge).")]
        public bool TerminalNeedsClearChord = false;
        [Tooltip("Hull-centre clearance from a track shell below which the terminal chord counts as blocked.")]
        [Min(0f)] public float TerminalChordClearance = 1.5f;
        [Tooltip("Keep the racing line out of the slab band: within the ribbon's lateral reach it stays " +
                 "at least RibbonClearHeight off the plane.")]
        public bool LineBandClamp = false;
        [Tooltip("A crystal pass point beside the ribbon (|lateral| >= RibbonClearLateral) is flown from " +
                 "the current face, never as a face change - a face change there costs two crossings " +
                 "(there, and back for the next crystal) for a point that needs none.")]
        public bool BesidePassNoCrossing = false;
        [Tooltip("A crystal too deep to take from this face is taken from BESIDE the ribbon (out past " +
                 "the plate edge, at its own height) when the capture sphere reaches there, instead of " +
                 "a full face change.")]
        public bool SidePassOverCrossing = false;
        [Tooltip("Pursuit flies the straight chord to its look-ahead point; when that chord would bring " +
                 "the hull centre within this distance of a track shell the look-ahead is shortened " +
                 "until it does not. 0 = off.")]
        [Min(0f)] public float ChordClearance = 0f;

        [Tooltip("Extra skim height per AI lane: seat k skims at SkimHeight + k x this, so seats never " +
                 "share a height with each other's trail rails (rail half-thickness 0.42 + hull half-height 0.3).")]
        [Min(0f)] public float LaneHeightStep = 3f;

        [Tooltip("After a pickup, hold the stick neutral for this many world units of travel so the " +
                 "hull passes through the pickup ring's hollow centre (ring: 8 prisms, radius 8.2, " +
                 "centred 8 u ahead, 7.5 long). 0 = off.")]
        [Min(0f)] public float PickupClearDistance = 0f;

        [Header("Laid-mass guard")]
        [Tooltip("Look-ahead (seconds) of the guard against mass the race has LAID - the Squirrel's " +
                 "own trail rails and the pickup rings. A hull touching any prism resets the skim " +
                 "boost, so a predicted contact swaps the stick for the nearest one that clears. " +
                 "0 disables the guard.")]
        [Min(0f)] public float MassGuardSeconds = 0.5f;
        [Tooltip("Predicted hull clearance (world units) the guard keeps from laid mass.")]
        [Min(0f)] public float MassGuardMargin = 0.8f;
        [Tooltip("How long a candidate stick is held in the guard's rollout before the pursuit " +
                 "continuation takes over (seconds).")]
        [Min(0.02f)] public float MassGuardSegment = 0.25f;
        [Tooltip("Rollout step (seconds).")]
        [Min(0.005f)] public float MassGuardStep = 0.04f;
        [Tooltip("The guard's rollout continues by re-following the racing line from wherever it has " +
                 "got to (what the driver will actually do), instead of flying at a fixed aim point.")]
        public bool RolloutFollowsLine = false;
        [Tooltip("Hull half-width used by the guard's contact test (Squirrel hull box 4.12 x 0.59 x 3.11).")]
        [Min(0f)] public float HullHalfWidth = 2.06f;
        [Tooltip("Hull half-length used by the guard's contact test.")]
        [Min(0f)] public float HullHalfLength = 1.55f;

        [Header("Tracking MPC (model-predictive line following)")]
        [Tooltip("Choose the stick by rolling each candidate forward and keeping the one whose predicted " +
                 "path stays closest to the racing line (pure path following; the line carries the safety).")]
        public bool UseTrackMpc = false;
        [Min(1f)] public float TrackMpcHz = 20f;
        [Min(0.1f)] public float TrackMpcHorizon = 0.8f;
        [Min(0.02f)] public float TrackMpcSegment = 0.25f;
        [Min(0.01f)] public float TrackMpcStep = 0.05f;
        [Tooltip("Lead (world units) of the line point each predicted position is compared with.")]
        [Min(0f)] public float TrackMpcLead = 15f;
        [Tooltip("Reward (per second of horizon left) for a predicted pickup.")]
        [Min(0f)] public float TrackMpcCaptureReward = 200f;
        [Tooltip("Fractional preference for the pursuit controller's own stick.")]
        [Range(0f, 0.9f)] public float TrackMpcNominalBias = 0.1f;
        [Tooltip("Penalty for a rollout whose hull comes within MpcHullMargin of a track shell, added to the " +
                 "tracking cost (squared world units). 0 = strikes are not scored (the original behaviour).")]
        [Min(0f)] public float TrackMpcStrikeCost = 0f;

        [Header("Model-predictive control")]
        [Tooltip("Replace the guard with full MPC: each decision scores ~50 stick/throttle commands by " +
                 "rolling the Squirrel's own dynamics forward against the visible track shells and laid " +
                 "mass (skims, pickups and strikes included) and flies the cheapest.")]
        public bool UseMpc = false;
        [Min(1f)] public float MpcHz = 15f;
        [Min(0.2f)] public float MpcHorizon = 1.5f;
        [Min(0.02f)] public float MpcSegment = 0.3f;
        [Min(0.01f)] public float MpcStep = 0.05f;
        [Range(0f, 1f)] public float MpcSlowThrottle = 0.55f;
        [Tooltip("Predicted hull clearance (world units) below which a rollout counts as a strike.")]
        [Min(0f)] public float MpcHullMargin = 0.9f;
        [Min(0f)] public float MpcCaptureMargin = 2f;
        [Tooltip("Seconds-equivalent value of 1x of banked boost.")]
        [Min(0f)] public float MpcBoostValue = 1.5f;
        [Min(0f)] public float MpcStrikeCost = 20f;
        [Tooltip("A predicted strike also costs the boost it would reset (MpcBoostValue x (boost - 1)): what " +
                 "a strike actually takes from the race. Off = the flat MpcStrikeCost.")]
        public bool MpcStrikeUsesBoostLoss = false;
        [Tooltip("Preference for the pursuit controller's own command (seconds), so the plan only " +
                 "overrides it for a real gain.")]
        [Min(0f)] public float MpcNominalBias = 0.05f;

        [Header("Steering")]
        [Tooltip("How far past the desired heading the commanded heading is allowed to LEAD the " +
                 "visible hull (fraction of the remaining error). 1 = command exactly the desired " +
                 "heading (no overshoot, ~0.67s settle).")]
        [Min(0f)] public float LeadGain = 1.6f;
        [Tooltip("Cap on the commanded-vs-hull lead, degrees.")]
        [Range(0f, 90f)] public float MaxLeadDegrees = 75f;
        [Tooltip("Stick gain on the commanded-heading error (per degree).")]
        [Min(0f)] public float StickGainPerDegree = 0.12f;
        [Tooltip("Roll stick used to keep the hull's up near world up (cosmetic; does not change the path).")]
        [Range(0f, 1f)] public float LevelingRoll = 0.25f;

        [Header("Line tracker (lag-inverting path following)")]
        [Tooltip("Follow the racing line with a model-based tracker instead of pure pursuit: the hull's heading " +
                 "is a first-order lag of the commanded heading (tau = 1/FollowRate), so the controller commands " +
                 "the desired heading PLUS the turn the line will make during tau (curvature x speed x tau), with a " +
                 "cross-track correction. Pure pursuit with a capped lead cannot lead a tight curve at speed. Off = pursuit.")]
        public bool UseLineTracker = false;
        [Tooltip("Cross-track correction: the hull aims to rejoin the line within this many seconds of travel.")]
        [Min(0.05f)] public float TrackerConvergeSeconds = 0.6f;
        [Tooltip("Floor on the cross-track rejoin distance (world units).")]
        [Min(1f)] public float TrackerConvergeMin = 20f;
        [Tooltip("Scale on the lag feedforward (1 = exactly the measured follow-rate lag).")]
        [Min(0f)] public float TrackerFeedforwardGain = 1f;
        [Tooltip("Extra lead on the remaining hull heading error (0 = none), as in the pursuit steering law.")]
        [Min(0f)] public float TrackerHeadingGain = 0.6f;
        [Tooltip("Cap on the total commanded-vs-desired lead, degrees.")]
        [Range(0f, 120f)] public float TrackerMaxLeadDegrees = 90f;
        [Tooltip("Distance along the line (world units) over which its curvature is measured.")]
        [Min(2f)] public float TrackerCurvatureSpan = 12f;

        [Header("Throttle")]
        [Range(0f, 1f)] public float CruiseThrottle = 1f;
        [Tooltip("Throttle floor while slowing to tighten a turn onto a crystal.")]
        [Range(0f, 1f)] public float MinThrottle = 0.35f;
        [Tooltip("A crystal closer than (this x the current turn radius x sin(off-angle)) is " +
                 "treated as unreachable at this speed and the throttle is eased.")]
        [Min(0f)] public float ReachabilityMargin = 1.25f;

        [Tooltip("Near a crystal, roll the hull's own dynamics forward under this driver's pursuit at a few " +
                 "throttle levels and fly the HIGHEST throttle whose predicted path enters the capture sphere " +
                 "(a slower hull turns tighter). A missed pickup orbits and stalls into recovery, which - not " +
                 "strikes - dominates the slowest seat's time. Off = the Dubins test alone.")]
        public bool CaptureThrottleSearch = false;
        [Tooltip("Rollout horizon of the capture search (seconds).")]
        [Min(0.2f)] public float CaptureHorizon = 1.5f;
        [Tooltip("The search runs while the pass point is within this many seconds of travel.")]
        [Min(0.1f)] public float CaptureWindowSeconds = 2f;
        [Tooltip("Units inside the capture radius a rollout must reach to count as a pickup.")]
        [Min(0f)] public float CaptureMargin = 3f;

        [Header("Level approach")]
        [Tooltip("Near a crystal, choose the stick by rolling the hull's own dynamics forward over a grid of " +
                 "stick commands (held, then this driver's pursuit of the pass point until capture, then ~1 s of " +
                 "exit back toward the skim line) and fly the one that captures with the most clearance from the " +
                 "track's contact shells. Most I2 strikes are the hull diving into the plate's face on the approach " +
                 "and just after the pickup. Off = pursuit alone.")]
        public bool UseLevelApproach = false;
        [Tooltip("The level approach runs while the pass point is within this many seconds of travel.")]
        [Min(0.2f)] public float LevelApproachSeconds = 2f;
        [Tooltip("How long a candidate stick is held before the pursuit continuation takes over (seconds).")]
        [Min(0.02f)] public float LevelSegment = 0.3f;
        [Tooltip("Exit horizon after the predicted capture (seconds).")]
        [Min(0f)] public float LevelExitSeconds = 1f;
        [Tooltip("Decisions per second (the chosen stick is held between).")]
        [Min(1f)] public float LevelHz = 15f;
        [Tooltip("Predicted hull-centre/wingtip clearance from a track shell below which a rollout counts as a strike.")]
        [Min(0f)] public float LevelStrikeMargin = 1f;
        [Tooltip("Clearance (world units) above which more clearance earns nothing.")]
        [Min(0f)] public float LevelClearanceCap = 4f;
        [Tooltip("Seconds of time a unit of clearance (up to the cap) is worth.")]
        [Min(0f)] public float LevelClearanceWeight = 0.15f;
        [Tooltip("Cost (seconds) of a predicted strike.")]
        [Min(0f)] public float LevelStrikeCost = 6f;
        [Tooltip("Preference (seconds) for the pursuit controller's own stick.")]
        [Min(0f)] public float LevelNominalBias = 0.05f;

        [Header("Drift")]
        [Tooltip("Use the hull's normal drift input on sharp heading changes.")]
        public bool UseDrift = false;
        [Range(0f, 180f)] public float DriftEnterDegrees = 70f;
        [Range(0f, 180f)] public float DriftExitDegrees = 25f;

        [Header("Planner (model-predictive)")]
        [Tooltip("Choose each command by rolling the Squirrel's own dynamics forward for a grid of " +
                 "stick/throttle candidates (SkimRacePlanner). Off = pure pursuit only.")]
        public bool UsePlanner = false;
        [Tooltip("Planner decisions per second (the command is held between).")]
        [Min(1f)] public float PlannerHz = 20f;
        [Min(0.2f)] public float PlannerHorizon = 1.5f;
        [Tooltip("How long a candidate command is held before the pursuit continuation takes over.")]
        [Min(0.02f)] public float PlannerSegment = 0.3f;
        [Min(0.005f)] public float PlannerStep = 0.0333f;
        [Range(0f, 1f)] public float PlannerSlowThrottle = 0.5f;
        [Tooltip("Units inside the crystal's capture radius a rollout must reach to count as a pickup.")]
        [Min(0f)] public float PlannerCaptureMargin = 4f;
        [Tooltip("Skim reach used by rollouts (the skimmer sphere radius).")]
        [Min(0f)] public float PlannerSkimReach = 7.5f;
        [Tooltip("Seconds of race time one unit of boost multiplier is worth to the planner.")]
        [Min(0f)] public float PlannerBoostValue = 1.5f;
        [Tooltip("Cost (seconds) of leaving a pickup facing away from the track's direction.")]
        [Min(0f)] public float PlannerExitWeight = 0.6f;
        [Tooltip("Track clearance above HullMargin that starts to cost (soft keep-out).")]
        [Min(0f)] public float PlannerClearanceBuffer = 1.5f;
        [Min(0f)] public float PlannerClearancePenalty = 0.5f;
        [Min(0f)] public float PlannerEffortCost = 0.01f;

        [Header("Boost Ring")]
        [Tooltip("Use the hull's own Boost Ring ability (its normal RT / right-finger control) as a " +
                 "launch pad: lay it straight ahead and fly through its centre for a ring's worth of skims.")]
        public bool UseLaunchRing = false;
        [Tooltip("Only lay a ring while the skim boost is below this.")]
        [Min(1f)] public float RingBelowBoost = 3f;
        [Tooltip("Heading error (and command-vs-hull error) allowed when laying, degrees.")]
        [Min(0f)] public float RingAlignDegrees = 1.5f;
        [Tooltip("How far ahead of the nose the ability lays the ring (SquirrelTubeAction.forwardOffset).")]
        [Min(0f)] public float RingForwardDistance = 100f;
        [Tooltip("The ability's cooldown at rest (SquirrelTubeAction.cooldown). The executor refuses " +
                 "an early press anyway; this only stops the pilot holding a line for nothing.")]
        [Min(0f)] public float RingCooldownSeconds = 20.5f;

        [Header("Recovery")]
        [Tooltip("Seconds with no progress (no crystal and no approach) before recovery engages.")]
        [Min(0.5f)] public float StallSeconds = 4f;
        [Tooltip("Metres of closing distance on the target that count as progress.")]
        [Min(1f)] public float ProgressEpsilon = 25f;
        [Tooltip("Seconds of recovery (reduced throttle, direct pursuit) once engaged.")]
        [Min(0.1f)] public float RecoverySeconds = 2.5f;
        [Range(0f, 1f)] public float RecoveryThrottle = 0.45f;
        [Tooltip("Seconds of no target before the pilot falls back to pure track following.")]
        [Min(0f)] public float MissingTargetGrace = 0.25f;

        public static SkimRaceAIConfigSO LoadDefault()
        {
            var cfg = Resources.Load<SkimRaceAIConfigSO>(ResourcePath);
            return cfg != null ? cfg : CreateInstance<SkimRaceAIConfigSO>();
        }

        /// <summary>The policy tuned for one intensity's track (<c>SkimRaceAIConfig_I&lt;n&gt;</c>),
        /// falling back to the base policy. Each intensity is a different track, so each may carry
        /// its own tuning; the pilot code is the same.</summary>
        public static SkimRaceAIConfigSO LoadFor(int intensity)
        {
            var cfg = Resources.Load<SkimRaceAIConfigSO>($"{ResourcePath}_I{intensity}");
            return cfg != null ? cfg : LoadDefault();
        }
    }
}

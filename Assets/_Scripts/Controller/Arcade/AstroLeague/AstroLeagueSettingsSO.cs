using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Single designer-facing config for Astro League: match rules, kickoff pacing,
    /// billiard ball physics, impact juice, AI striker tuning, and arena visuals.
    /// All gameplay feel lives here - the MonoBehaviours only execute it.
    /// </summary>
    [CreateAssetMenu(
        fileName = "AstroLeagueSettings",
        menuName = "ScriptableObjects/Arcade/AstroLeagueSettings")]
    public class AstroLeagueSettingsSO : ScriptableObject
    {
        [Header("Match Rules")]
        [Tooltip("Regulation match length in seconds")]
        [SerializeField] float matchDurationSeconds = 180f;
        public float MatchDurationSeconds => matchDurationSeconds;

        [Tooltip("Domain goal sum that ends the match early (mercy rule). Published to GameDataSO.GoalTargetCount.")]
        [SerializeField] int goalLimit = 5;
        public int GoalLimit => goalLimit;

        [Tooltip("If true and the score is tied at full time, play sudden-death golden goal")]
        [SerializeField] bool goldenGoalOvertime = true;
        public bool GoldenGoalOvertime => goldenGoalOvertime;

        [Header("Arena - Base Dimensions (intensity 1, before the scale table)")]
        [Tooltip("Court length along the GOAL AXIS (Z). The goal lines sit at ±length/2 and the " +
                 "flat caps there backboard missed shots. Authored HERE rather than on the scene's " +
                 "AstroLeagueArena so the whole playfield resizes from one asset - the controller " +
                 "derives the goal and team-spawn positions from it, so nothing in the scene has to " +
                 "be dragged when this changes.")]
        [SerializeField] float arenaLength = 400f;
        public float ArenaLength => arenaLength;

        [Tooltip("Court width (X).")]
        [SerializeField] float arenaWidth = 320f;
        public float ArenaWidth => arenaWidth;

        [Tooltip("Court height (Y). Deliberately close to the width: this is a 3D flight game and a " +
                 "flat pizza-box court wastes the axis that makes it one.")]
        [SerializeField] float arenaHeight = 240f;
        public float ArenaHeight => arenaHeight;

        [Tooltip("Goal mouth + portal-ring radius (base / intensity 1). ONE number: the arena draws " +
                 "its portal rings at it and the controller hands the same value to every " +
                 "AstroLeagueGoal, so the ring you aim at IS the mouth that scores (they used to be " +
                 "two independently-authored fields that could silently disagree).")]
        [SerializeField] float goalMouthRadius = 62f;
        public float GoalMouthRadius => goalMouthRadius;

        [Tooltip("Distance from the arena centre, toward its own goal, where a team parks for " +
                 "kickoff (base / intensity 1, scaled with the arena). Keep it comfortably inside " +
                 "arenaLength/2 so a kickoff line never sits on top of a goal mouth.")]
        [SerializeField] float kickoffLineDistance = 150f;
        public float KickoffLineDistance => kickoffLineDistance;

        [Header("Intensity Scale (arena + ball + team spawns)")]
        [Tooltip("Arena, ball, goal and team-spawn scale PER INTENSITY (index 0 = intensity 1). " +
                 "Overrides the legacy even ramp whenever non-empty; falls back to the last entry " +
                 "above its length. Multiplies the base dimensions above. History worth keeping: the " +
                 "court shipped far too SMALL (a pitch you crossed in two seconds), was taken to " +
                 "3-4.5x, and that overshot - a frictionless ball in a huge box read as pong. These " +
                 "are that pass cut by 40%. Vessels stay normal size.")]
        [SerializeField] float[] arenaScaleByIntensity = { 1.8f, 2.1f, 2.4f, 2.7f };
        public float[] ArenaScaleByIntensity => arenaScaleByIntensity;

        [Tooltip("LEGACY fallback ramp (used only when arenaScaleByIntensity is empty): scale steps " +
                 "evenly from 1x at intensity 1 up to this factor at maxIntensityLevel.")]
        [SerializeField] float intensityScaleAtMax = 2f;
        public float IntensityScaleAtMax => intensityScaleAtMax;

        [Tooltip("Highest intensity level used for the legacy scale ramp (the arcade card's MaxIntensity).")]
        [SerializeField] int maxIntensityLevel = 4;
        public int MaxIntensityLevel => maxIntensityLevel;

        /// <summary>
        /// Arena/ball/layout scale for an intensity level (1-based). Per-intensity table first
        /// (clamped to the configured array), legacy even 1x→intensityScaleAtMax ramp when the
        /// table is empty.
        /// </summary>
        public float ScaleForIntensity(int intensity)
        {
            if (arenaScaleByIntensity != null && arenaScaleByIntensity.Length > 0)
            {
                int idx = Mathf.Clamp(intensity - 1, 0, arenaScaleByIntensity.Length - 1);
                return Mathf.Max(0.01f, arenaScaleByIntensity[idx]);
            }

            int maxLevel = Mathf.Max(2, maxIntensityLevel);
            float t = (Mathf.Clamp(intensity, 1, maxLevel) - 1f) / (maxLevel - 1f);
            return Mathf.Lerp(1f, Mathf.Max(1f, intensityScaleAtMax), t);
        }

        [Header("Arena - Court Boundary (the cell nucleus)")]
        [Tooltip("Court geometry the ball bounces off, ONE PER INTENSITY (index 0 = intensity 1). FLAT " +
                 "polytope walls BANK the ball (billiards/air-hockey/Rocket-League feel); Sphere focuses " +
                 "it toward center (the legacy baseline); NotchedRing adds a central ring choke point. " +
                 "The cell NUCLEUS is morphed to this shape so the wall you see is the wall the ball " +
                 "hits. Default 1-4: BeveledBox, Hex, Cylinder, Sphere (central goal) - re-map freely. Falls back " +
                 "to the last entry above maxIntensityLevel.")]
        public AstroLeagueBoundaryShape[] boundaryShapesByIntensity =
        {
            AstroLeagueBoundaryShape.BeveledBox,
            AstroLeagueBoundaryShape.HexagonalPrism,
            AstroLeagueBoundaryShape.Cylinder,
            AstroLeagueBoundaryShape.Sphere,
        };

        [Tooltip("Per-intensity 'central shared goal' toggle (index 0 = intensity 1). When ON, the two " +
                 "goal detectors move to the arena CENTER facing opposite ways - ONE shared goal where " +
                 "the pass DIRECTION decides which domain scores - and the ball spawns off-center. " +
                 "Default: only intensity 4 (the Sphere). Re-map freely alongside boundaryShapesByIntensity.")]
        [SerializeField] bool[] centralGoalByIntensity = { false, false, false, true };
        public bool[] CentralGoalByIntensity => centralGoalByIntensity;

        [Tooltip("For a central-goal court: how far off the arena center (world units at intensity 1, " +
                 "along X, in the goal's plane) the ball spawns, so it doesn't start sitting in the goal.")]
        [SerializeField] float centralBallSpawnOffset = 70f;
        public float CentralBallSpawnOffset => centralBallSpawnOffset;

        [Tooltip("Radius (at intensity 1) of the boundary ONLY when the shape is Sphere. Should " +
                 "circumscribe the box court above (sqrt of the summed half-extents squared) so the " +
                 "sphere reads as the same size arena. Scales with match intensity. Polytope shapes " +
                 "derive their walls from the arena's length/width/height instead.")]
        [SerializeField] float boundaryRadius = 285f;
        public float BoundaryRadius => boundaryRadius;

        [Tooltip("0..1 chamfer depth for the OctagonalPrism (how far the 4 goal-axis edges are cut " +
                 "toward the corner). ~0.5 reads as a clean octagon; higher = more cut.")]
        [SerializeField, Range(0f, 1f)] float octagonBevelFraction = 0.5f;
        public float OctagonBevelFraction => octagonBevelFraction;

        [Tooltip("0..1 chamfer depth for the BeveledBox (every edge + corner cut). Higher = rounder, " +
                 "more Rocket-League corner-ramp redirect; lower = closer to a sharp box.")]
        [SerializeField, Range(0f, 1f)] float beveledBoxBevelFraction = 0.45f;
        public float BeveledBoxBevelFraction => beveledBoxBevelFraction;

        [Header("Arena - NotchedRing (central ring obstacle)")]
        [Tooltip("Outer court the central ring sits inside, for the NotchedRing shape (default Cylinder). " +
                 "Anything except NotchedRing itself.")]
        [SerializeField] AstroLeagueBoundaryShape notchedRingOuterShape = AstroLeagueBoundaryShape.Cylinder;
        public AstroLeagueBoundaryShape NotchedRingOuterShape => notchedRingOuterShape;

        [Tooltip("Ring radius as a fraction of the court cross-section radius (min(width,height)/2). The " +
                 "central hole = (major − tube) and must clear the ball, so keep major above tube.")]
        [SerializeField, Range(0f, 1f)] float ringMajorRadiusFraction = 0.5f;
        public float RingMajorRadiusFraction => ringMajorRadiusFraction;

        [Tooltip("Ring thickness (tube radius) as a fraction of the court cross-section radius. The ball " +
                 "bounces off the OUTSIDE of this tube.")]
        [SerializeField, Range(0f, 1f)] float ringTubeRadiusFraction = 0.18f;
        public float RingTubeRadiusFraction => ringTubeRadiusFraction;

        [Tooltip("Angle (degrees, atan2(y,x)) of the notch center - the gap cut in the ring, a shooting lane.")]
        [SerializeField] float notchCenterDegrees = 0f;
        public float NotchCenterDegrees => notchCenterDegrees;

        [Tooltip("Half-width of the notch gap in degrees (0 = a solid ring, no gap). 30 = a 60° opening.")]
        [SerializeField, Range(0f, 90f)] float notchHalfWidthDegrees = 30f;
        public float NotchHalfWidthDegrees => notchHalfWidthDegrees;

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

        [Header("Arena - Edge Lining (super-shielded prisms)")]
        [Tooltip("Lay a lining of SUPER-SHIELDED (invulnerable) neutral prisms along the court's edges " +
                 "at every intensity - polytope hull edges, cylinder cap rims, sphere latitude rings. " +
                 "Laid per peer through the standard PrismFactory channel (prisms bloom in; removal is " +
                 "the animated Damage path - continuity law).")]
        [SerializeField] bool edgePrismsEnabled = true;
        public bool EdgePrismsEnabled => edgePrismsEnabled;

        [Tooltip("TOTAL prisms in the lining, distributed evenly over the court's summed edge length - " +
                 "FIXED across shapes and intensities so the lining's volume budget (count x prism " +
                 "volume) stays deterministic. The Astro League Cell Config's phase-volume thresholds " +
                 "are raised by exactly that budget (480 x 62.5 = 30000) - retune them together. " +
                 "Collider budget: the lining is FREE - a shield swaps the mesh and the mass, never " +
                 "the collider, so these prisms stay LOD-cullable like any other. What the count does " +
                 "buy is permanence: super-shielded mass is removable only by an energised blade with the Rhino's Space L5. " +
                 "480 is the doubling that keeps the rim reading as a line on the (much larger) " +
                 "current court instead of a dotted one.")]
        [SerializeField] int edgePrismCount = 480;
        public int EdgePrismCount => edgePrismCount;

        [Tooltip("Lining prism TargetScale (long Z axis laid ALONG the edge). Volume = x*y*z; total " +
                 "lining volume = edgePrismCount x that volume. Change either and retune the cell " +
                 "config's phase-volume thresholds to match.")]
        [SerializeField] Vector3 edgePrismScale = new(2.5f, 2.5f, 10f);
        public Vector3 EdgePrismScale => edgePrismScale;

        [Tooltip("Inward offset from the edge line toward the arena center (world units at intensity 1, " +
                 "scales with the arena) so the lining sits just inside the wall the ball bounces off.")]
        [SerializeField] float edgePrismInset = 6f;
        public float EdgePrismInset => edgePrismInset;

        [Header("Goal Reset (every non-final goal)")]
        [Tooltip("On every goal: every peer parks the vessels it owns back on their kickoff lines with " +
                 "speed ZEROED and clears the accumulated field prisms while the goal replay plays. " +
                 "Kickoff re-parks (idempotent) before GO.")]
        [SerializeField] bool goalResetsArena = true;
        public bool GoalResetsArena => goalResetsArena;

        [Tooltip("Seconds the staggered prism-clear sweep takes (center-out wave, canonical animated " +
                 "Damage path - never a raw Destroy). Keep within celebration + kickoff-freeze.")]
        [SerializeField] float goalPrismClearSeconds = 1.6f;
        public float GoalPrismClearSeconds => goalPrismClearSeconds;

        [Header("Goal Replay (the replay camera)")]
        [Tooltip("Replay the goal on the shared END camera (the replay camera) while the arena resets " +
                 "behind it: a visual-only ghost ball retraces the recorded flight into the goal, camera " +
                 "following. Purely local on every peer (the ball trajectory is already replicated); the " +
                 "gameplay camera is restored at kickoff GO or when playback ends.")]
        [SerializeField] bool goalReplayEnabled = true;
        public bool GoalReplayEnabled => goalReplayEnabled;

        [Tooltip("Seconds of ball flight recorded for the replay (ring buffer, cleared at every kickoff " +
                 "GO so a replay never crosses a reset).")]
        [SerializeField] float goalReplayRecordSeconds = 4f;
        public float GoalReplayRecordSeconds => goalReplayRecordSeconds;

        [Tooltip("Fraction of the celebration + kickoff-freeze window the ghost playback may fill; " +
                 "playback speed is derived to fit (slow-mo when the recording is short).")]
        [SerializeField, Range(0.3f, 1f)] float goalReplayWindowFraction = 0.85f;
        public float GoalReplayWindowFraction => goalReplayWindowFraction;

        [Tooltip("Playback-speed floor - a goal scored seconds after kickoff would otherwise stretch a " +
                 "tiny recording into extreme slow-mo.")]
        [SerializeField] float goalReplayMinPlaybackSpeed = 0.3f;
        public float GoalReplayMinPlaybackSpeed => goalReplayMinPlaybackSpeed;

        [Tooltip("Broadcast framing margin: the replay camera sits at a FIXED vantage beside the " +
                 "recorded flight, far enough back that the whole shot fits the field of view times " +
                 "this margin (1 = exact fit, higher = wider establishing shot), and PANS to track " +
                 "the ghost rather than chasing it.")]
        [SerializeField] float goalReplayFramingMargin = 1.35f;
        public float GoalReplayFramingMargin => goalReplayFramingMargin;

        [Tooltip("Elevation of the broadcast vantage above the flight's centroid, as a fraction of " +
                 "the vantage distance (0 = level with the play, higher = more of a stadium " +
                 "high-camera look).")]
        [SerializeField, Range(0f, 1f)] float goalReplayVantageElevation = 0.35f;
        public float GoalReplayVantageElevation => goalReplayVantageElevation;

        [Tooltip("How quickly the replay camera pans to keep the ghost in frame (higher = tighter " +
                 "tracking, lower = lazier broadcast pan that lets the ball lead the frame).")]
        [SerializeField] float goalReplayPanSpeed = 3.5f;
        public float GoalReplayPanSpeed => goalReplayPanSpeed;

        [Header("Vessel Recoil (juice)")]
        [Tooltip("Backward velocity (units/sec) added to a vessel when it strikes the ball, a subtle " +
                 "'bounce off' juice. DEFAULT 0 (OFF): anti-clip is already guaranteed by the ball's own " +
                 "depenetration (EjectBallFromPoint), so any recoil only fights player control - a " +
                 "frictionless ball that keeps bouncing back into a vessel re-fires it every cooldown, " +
                 "stacking toward VesselTransformer.velocityModifierMax (100) and throwing the vessel " +
                 "back 'like crazy'. Dial up only for a deliberate subtle bounce; scaled by hit strength.")]
        [SerializeField] float vesselRecoilSpeed = 0f;
        public float VesselRecoilSpeed => vesselRecoilSpeed;

        [Tooltip("Seconds the vessel recoil impulse lasts (cosine-windowed by VesselTransformer).")]
        [SerializeField] float vesselRecoilDuration = 0.12f;
        public float VesselRecoilDuration => vesselRecoilDuration;

        [Header("Kickoff Pacing")]
        [Tooltip("Seconds of GOAL! celebration (real time) before the ball resets")]
        [SerializeField] float celebrationSeconds = 2.2f;
        public float CelebrationSeconds => celebrationSeconds;

        [Tooltip("Time.timeScale during the goal celebration slow-mo. Solo sessions only - " +
                 "never applied with a second connected client (local timescale desyncs peers).")]
        [SerializeField, Range(0.05f, 1f)] float celebrationTimeScale = 0.35f;
        public float CelebrationTimeScale => celebrationTimeScale;

        [Tooltip("Seconds the ball stays frozen at center during a kickoff count-in")]
        [SerializeField] float kickoffFreezeSeconds = 2.4f;
        public float KickoffFreezeSeconds => kickoffFreezeSeconds;

        [Tooltip("Seconds the winner banner holds before the shared scoreboard flow takes over")]
        [SerializeField] float winnerBannerSeconds = 2.5f;
        public float WinnerBannerSeconds => winnerBannerSeconds;

        [Header("Kickoff Parking")]
        [Tooltip("Lateral spacing between teammates parked on the same kickoff line (base / " +
                 "intensity 1, scaled with the arena). The distance from centre is " +
                 "kickoffLineDistance, up with the other arena dimensions.")]
        [SerializeField] float kickoffLateralSpacing = 45f;
        public float KickoffLateralSpacing => kickoffLateralSpacing;

        [Header("Ball - Vessel Strike (elastic, momentum-conserving)")]
        [Tooltip("Arcade pop on a vessel strike. The strike is a momentum-conserving ELASTIC bounce " +
                 "off the moving hull (the ball gains up to ~2× the vessel's speed on a head-on hit); " +
                 "this adds an EXTRA launch of (multiplier − 1) × vessel speed along the aim direction. " +
                 "1 = pure elastic, no extra pop.")]
        [SerializeField] float hitBoostMultiplier = 2.5f;
        public float HitBoostMultiplier => hitBoostMultiplier;

        [Tooltip("Aim bias for the strike's extra pop: 0 = along the physical contact normal " +
                 "(pure billiard deflection), 1 = along the pilot's heading (full aim control).")]
        [SerializeField, Range(0f, 1f)] float directionalBias = 0.45f;
        public float DirectionalBias => directionalBias;

        [Tooltip("Vessel speed below this threshold is ignored (prevents ghost taps)")]
        [SerializeField] float minimumHitSpeed = 5f;
        public float MinimumHitSpeed => minimumHitSpeed;

        [Tooltip("Minimum seconds between strikes from the SAME vessel. Dedups the hull+trigger " +
                 "double-fire AND paces dribble taps while a vessel keeps pushing the ball. The " +
                 "anti-clip depenetration runs every contact frame regardless of this cooldown, so a " +
                 "vessel can never clip the ball even between strikes.")]
        [SerializeField] float vesselStrikeCooldown = 0.12f;
        public float VesselStrikeCooldown => vesselStrikeCooldown;

        [Tooltip("Anti-clip: every contact frame the ball is pushed so its center is at least " +
                 "(ball radius + this) from the vessel root - guarantees the vessel hull never clips " +
                 "through the ball, including the trigger-only ships (Serpent/Sparrow) that have no " +
                 "physical depenetration. Roughly the vessel's visual hull reach.")]
        [SerializeField] float vesselClearRadius = 12f;
        public float VesselClearRadius => vesselClearRadius;

        [Header("Ball - Explosions")]
        [Tooltip("Let AOE blasts shove the ball, the way they shove prisms. The blast hands over " +
                 "the SAME impact vector a prism receives (ExplosionImpulse.Along, no distance " +
                 "falloff - prisms do not get one either), so a weapon that throws mass hard " +
                 "throws the payload hard. Off restores the previous behaviour, where a blast " +
                 "passed through the ball with no effect at all.")]
        [SerializeField] bool explosionsAffectBall = true;
        public bool ExplosionsAffectBall => explosionsAffectBall;

        [Tooltip("Gain on the blast's impact vector before it is added to the ball's velocity. " +
                 "1 = the ball takes the blast's TRUE impulse, exactly as prism debris does — " +
                 "which is the default, because the blast's own Inertia is already the dial for " +
                 "how hard that blast throws. Below 1 the ball is a heavier payload than the mass " +
                 "around it; this shipped at 0.5 and, against a 300 u/s ball, made a blast worth " +
                 "~11% of top speed — a shove nobody could see.")]
        [SerializeField, Min(0f)] float explosionKickMultiplier = 1f;
        public float ExplosionKickMultiplier => explosionKickMultiplier;

        [Tooltip("Fraction of the blast kick that also arrives as SPIN, applied off-centre at the " +
                 "blast-facing surface. 0 = a pure shove.")]
        [SerializeField, Range(0f, 1f)] float explosionSpinFraction = 0.6f;
        public float ExplosionSpinFraction => explosionSpinFraction;

        [Tooltip("A blast RE-COLOURS the ball to the blasting domain, exactly as a vessel strike " +
                 "does - blowing the payload is a way to claim it. Off leaves ownership alone and " +
                 "makes the blast a pure shove.")]
        [SerializeField] bool explosionClaimsBall = true;
        public bool ExplosionClaimsBall => explosionClaimsBall;

        [Header("Ball - Vessel Strike (sword / blade contact)")]
        [Tooltip("Resolve a strike at the point on a SKIMMER BLADE that actually touched the ball " +
                 "(SkimmerSwingKinematics - the Rhino's sword) instead of at the vessel root. Two " +
                 "things change: the bounce normal comes off the blade, so a tip hit deflects the " +
                 "ball off the tip rather than off the fuselage 60 units away, and the strike speed " +
                 "is the blade point's TRUE velocity - a swung tip is many times faster than the " +
                 "hull, so a real swing sends the ball screaming. Vessels with no swinging skimmer " +
                 "are unaffected (the model reports not-ready and the hull path runs).")]
        [SerializeField] bool bladeAwareStrikes = true;
        public bool BladeAwareStrikes => bladeAwareStrikes;

        [Tooltip("Clearance around a BLADE's centreline, doing two jobs. Anti-clip: the ball is " +
                 "pushed off the blade segment to (ball radius + this) on every contact frame (the " +
                 "hull's vesselClearRadius, measured from the vessel root, cannot protect a 30-120 " +
                 "unit sword). And REACH: a swinging skimmer also carries a large sphere trigger - " +
                 "its skim field - so a contact further than (ball radius + this) from the blade " +
                 "centreline is the skim field, not the sword, and does not strike the ball. Raise " +
                 "it for a more forgiving swing, lower it to demand a cleaner hit.")]
        [SerializeField] float bladeClearRadius = 7f;
        public float BladeClearRadius => bladeClearRadius;

        [Tooltip("Extra launch multiplier at the very TIP of a blade, lerped from 1 at the hilt. " +
                 "The swing model already makes the tip faster on its own; this is the arcade " +
                 "reward on top for actually timing a tip strike. 1 = no tip bonus.")]
        [SerializeField, Min(1f)] float bladeTipStrikeBonus = 1.35f;
        public float BladeTipStrikeBonus => bladeTipStrikeBonus;

        [Header("Juice - Vessel Strike Feedback")]
        [Tooltip("Broadcast the full strike feedback (ball flash, contact burst, camera shake, " +
                 "audio cue) to every peer when a vessel connects. Before this existed a hit " +
                 "produced NOTHING except the ball changing direction - the single biggest reason " +
                 "the mode read as unresponsive.")]
        [SerializeField] bool strikeFeedbackEnabled = true;
        public bool StrikeFeedbackEnabled => strikeFeedbackEnabled;

        [Tooltip("Camera-shake multiplier applied to the STRIKING pilot on top of the shared " +
                 "distance falloff, so connecting feels different from watching somebody else " +
                 "connect. 1 = no extra emphasis.")]
        [SerializeField, Min(1f)] float strikerShakeEmphasis = 1.8f;
        public float StrikerShakeEmphasis => strikerShakeEmphasis;

        [Tooltip("Ball speed (as a fraction of maxSpeed) a strike must produce to read as a BIG " +
                 "hit - above it the flash, burst and shake all run at full strength and the " +
                 "heavier audio cue plays.")]
        [SerializeField, Range(0f, 1f)] float bigHitSpeedFraction = 0.45f;
        public float BigHitSpeedFraction => bigHitSpeedFraction;

        [Tooltip("Seconds the ball's impact POP lasts - a fast scale pulse on the ball's visual " +
                 "child that reads as impact even at the far end of a big arena, where a particle " +
                 "burst is a few pixels. Visual only: it rides a child transform, so the collider " +
                 "and BallWorldRadius (goal threshold, prism scan, depenetration) never move. " +
                 "0 disables it.")]
        [SerializeField] float strikePopSeconds = 0.2f;
        public float StrikePopSeconds => strikePopSeconds;

        [Tooltip("Peak pop amount (0.22 = the ball swells 22% at the moment of impact and eases " +
                 "back). Kept uniform rather than a directional squash so the faceted icosphere's " +
                 "spin never appears to jump.")]
        [SerializeField, Range(0f, 0.6f)] float strikePopAmount = 0.22f;
        public float StrikePopAmount => strikePopAmount;

        [Tooltip("Seconds a ball's visual BLOOMS IN over when it comes into existence — the " +
                 "continuity-of-existence law applied to the payload itself (a Scarab-forged " +
                 "ball must grow out of its crystal, never pop in). Visual child only, same " +
                 "rule as the strike pop: the physical radius is live from frame one. " +
                 "0 disables (instant, law-breaking — leave it on).")]
        [SerializeField, Min(0f)] float spawnBloomSeconds = 0.55f;
        public float SpawnBloomSeconds => spawnBloomSeconds;

        [Tooltip("Seconds after a ball is knocked out of the nucleus surface during which its " +
                 "court/cytoplasm boundary is NOT applied (SCARAB.md §4.6). An embedded ball sits " +
                 "part-sunk in the nucleus shell, which is on the wrong side of BOTH boundaries — " +
                 "so the frame it is released, containment corrects it and the ball JUMPS radially " +
                 "in or out. This grace lets its own velocity carry it across that band, after " +
                 "which containment engages on a ball that is already where it belongs. 0 = the " +
                 "old snap.")]
        [SerializeField, Min(0f)] float nucleusReleaseGraceSeconds = 1f;
        public float NucleusReleaseGraceSeconds => nucleusReleaseGraceSeconds;

        [Header("Ball - Detonation")]
        [Tooltip("Domain explosion spawned where a ball detonates (the nucleus overload / ball-cap " +
                 "overflow). Coloured by the BALL's domain, and the standard blast rules then " +
                 "apply: own-domain prisms are drawn LIT in the blast's domain colour (no " +
                 "perceived clipping - Docs/LIT.md), other " +
                 "domains are destroyed. Leave EMPTY for a burst with no blast — an unwired slot " +
                 "is a visible TODO, never a borrowed prefab.")]
        [SerializeField] AOEExplosion[] detonationExplosionPrefabs;
        public AOEExplosion[] DetonationExplosionPrefabs => detonationExplosionPrefabs;

        [Header("Ball - Physics (zero friction)")]
        [SerializeField] float maxSpeed = 380f;
        public float MaxSpeed => maxSpeed;
        [SerializeField] float ballMass = 3f;
        public float BallMass => ballMass;
        [Tooltip("Restitution for a VESSEL strike (1 = perfectly elastic). Keep this high - it is what " +
                 "lets a swung sword fire the payload. Wall caroms use wallRestitution instead.")]
        [SerializeField, Range(0f, 1f)] float ballBounciness = 1f;
        public float BallBounciness => ballBounciness;

        [Tooltip("Restitution for a WALL carom. Below 1 every bounce takes energy out, which is what " +
                 "stops the ball ricocheting around the court forever (the original 1.0 + zero drag " +
                 "made the mode read as pong). Lower = the ball dies against the boards faster; " +
                 "1 restores the old perfectly-elastic pinball.")]
        [SerializeField, Range(0f, 1f)] float wallRestitution = 0.72f;
        public float WallRestitution => wallRestitution;

        [Tooltip("Restitution for a carom off a SHIELDED PRISM. 1 (the default) is a pure " +
                 "REDIRECT: the into-prism component mirrors exactly, so the ball's SPEED is " +
                 "unchanged and only its heading turns. That is the whole point of armour here — " +
                 "a shielded prism costs the shield and the ball's line, never its momentum. " +
                 "Deliberately separate from wallRestitution: a wall is meant to bleed energy " +
                 "(0.72), and a prism is not.")]
        [SerializeField, Range(0f, 1f)] float prismCaromRestitution = 1f;
        public float PrismCaromRestitution => prismCaromRestitution;

        [Tooltip("Exponential speed decay per second on the ball's coast (0 = the original " +
                 "frictionless coast). This is what makes an untouched ball SETTLE, so it becomes a " +
                 "thing players go and contest rather than something ricocheting past them. Composes " +
                 "with the opposing-prism-mass drag below rather than replacing it.")]
        [SerializeField, Min(0f)] float ballDrag = 0.35f;
        public float BallDrag => ballDrag;

        [Tooltip("Multiplier on ballDrag once the ball is fully OUTSIDE the cell's nucleus — the " +
                 "hypersea getting thicker off the pitch. 1 disables the ramp. This is a soft " +
                 "boundary, not a wall: a ball that gets out is not teleported, culled or " +
                 "reflected, it simply bleeds speed fast enough to stop being gone. In a mode " +
                 "whose court IS the nucleus (Astro League) the ball is reflected at that radius " +
                 "anyway, so the ramp never engages and the mode is unaffected.")]
        [SerializeField, Min(1f)] float outsideNucleusDragMultiplier = 6f;
        public float OutsideNucleusDragMultiplier => outsideNucleusDragMultiplier;

        [Tooltip("How far past the nucleus surface the drag ramp takes to reach full strength, in " +
                 "world units. The ramp is linear across this band so leaving the pitch is a " +
                 "gradient rather than a cliff.")]
        [SerializeField, Min(1f)] float outsideNucleusDragFalloff = 250f;
        public float OutsideNucleusDragFalloff => outsideNucleusDragFalloff;

        [Tooltip("A ball OUTSIDE the nucleus is held inside this fraction of the cell's MEMBRANE " +
                 "radius, bouncing off the nucleus from the outside — the cytoplasm half of the " +
                 "ball's own nucleus containment (SCARAB.md §4.6). 1 = the cell's own membrane; " +
                 "just under it so a ball never rides the literal skin. This lives here rather " +
                 "than on ScarabNucleusFieldConfig because containment is a property of the BALL, " +
                 "applied in every cell it can reach — not of the seeding ability that made one.")]
        [SerializeField, Range(0.1f, 1f)] float cytoplasmOuterFraction = 0.95f;
        public float CytoplasmOuterFraction => cytoplasmOuterFraction;

        [Tooltip("Speed below which the remaining coast is snapped to zero. Exponential decay is " +
                 "asymptotic, so without this the ball creeps forever at an invisible speed and never " +
                 "actually comes to rest.")]
        [SerializeField, Min(0f)] float ballRestSpeed = 6f;
        public float BallRestSpeed => ballRestSpeed;

        [Tooltip("How hard opposing-color prism MASS slows the ball as it plows through (it keeps its " +
                 "direction, only its speed drops). Per eaten prism: speed ×= ballMass / (ballMass + " +
                 "this × prismVolume). 0 = no drag (ball never slows); higher = a thick enemy wall " +
                 "brakes the ball hard. Same-color and shielded prisms cost no speed.\n\n" +
                 "The slow depends on VOLUME and nothing else, so a DANGER prism and a plain one of " +
                 "the same size cost the ball exactly the same — see the tier note in " +
                 "AstroLeagueBall.ProcessPrismInteractions. Do not add a per-tier multiplier here.")]
        [SerializeField] float prismDragMassScale = 0.0167f;
        public float PrismDragMassScale => prismDragMassScale;

        [Header("Ball - Angular Dynamics (rotational inertia)")]
        [Tooltip("Angular damping on the ball rigidbody. A small amount so spin imparted by off-center " +
                 "vessel strikes gradually settles instead of tumbling forever, while still reading as " +
                 "a freely-spinning billiard payload.")]
        [SerializeField] float ballAngularDamping = 0.3f;
        public float BallAngularDamping => ballAngularDamping;

        [Tooltip("Cap on the ball's angular speed (rad/s). Unity's default rigidbody clamp (7 rad/s) " +
                 "is too low to read as a fast spin - raise it so off-center strikes produce a " +
                 "visible tumble on the faceted icosphere.")]
        [SerializeField] float maxAngularSpeed = 40f;
        public float MaxAngularSpeed => maxAngularSpeed;

        [Header("Ball - Mesh")]
        [Tooltip("Icosphere subdivision count for the ball mesh (each level ×4 the faces: " +
                 "0=20, 1=80, 2=320, 3=1280 tris). Level 2 is medium-poly - faceted enough that " +
                 "rotation is clearly visible, dense enough to read as round.")]
        [SerializeField] int ballMeshSubdivisions = 2;
        public int BallMeshSubdivisions => ballMeshSubdivisions;

        [Header("Ball - Prism Scan")]
        [Tooltip("Radius (× the ball's world radius) of the per-tick spatial scan that resolves prism " +
                 "interactions. 1 = exactly the ball's cross-section (clears a ball-sized tunnel); " +
                 "slightly above 1 catches prisms just grazing the surface. The ball is a first-class " +
                 "entity - this scan runs every physics tick on every peer, independent of colliders.")]
        [SerializeField] float prismScanRadiusFactor = 1.1f;
        public float PrismScanRadiusFactor => prismScanRadiusFactor;

        [Header("Ball - Client Replication")]
        [Tooltip("How aggressively non-server peers blend toward the dead-reckoned ball position (higher = snappier)")]
        [SerializeField] float clientSmoothingRate = 12f;
        public float ClientSmoothingRate => clientSmoothingRate;

        [Tooltip("Position error beyond which non-server peers snap instead of smoothing")]
        [SerializeField] float clientSnapDistance = 30f;
        public float ClientSnapDistance => clientSnapDistance;

        [Header("Juice - Hitstop (solo sessions only)")]
        [SerializeField] float hitstopDuration = 0.045f;
        public float HitstopDuration => hitstopDuration;
        [SerializeField, Range(0.01f, 1f)] float hitstopTimeScale = 0.1f;
        public float HitstopTimeScale => hitstopTimeScale;
        [Tooltip("Ball speed required to trigger hitstop on a strike")]
        [SerializeField] float hitstopSpeedThreshold = 70f;
        public float HitstopSpeedThreshold => hitstopSpeedThreshold;

        [Header("Juice - Camera Shake")]
        [SerializeField] float strikeShakeIntensity = 1.0f;
        public float StrikeShakeIntensity => strikeShakeIntensity;
        [SerializeField] float strikeShakeDuration = 0.18f;
        public float StrikeShakeDuration => strikeShakeDuration;
        [SerializeField] float goalShakeIntensity = 2.5f;
        public float GoalShakeIntensity => goalShakeIntensity;
        [SerializeField] float goalShakeDuration = 0.5f;
        public float GoalShakeDuration => goalShakeDuration;

        [Tooltip("Camera shake fades with distance from the impact, reaching zero at this radius. " +
                 "Scale it with the court - on the current arena a 180-unit falloff meant almost " +
                 "every event was silent for almost everybody.")]
        [SerializeField] float shakeFalloffRadius = 700f;
        public float ShakeFalloffRadius => shakeFalloffRadius;

        [Tooltip("Wall-bounce juice (camera shake / haptic / burst) only fires when the PERPENDICULAR " +
                 "into-wall speed is at least this fraction of maxSpeed. A frictionless ball skimming " +
                 "tangentially along a curved wall has ~0 perpendicular speed, so this stops it from " +
                 "continuously shaking the camera (the high-frequency jitter).")]
        [SerializeField, Range(0f, 1f)] float wallJuiceMinIntensity = 0.12f;
        public float WallJuiceMinIntensity => wallJuiceMinIntensity;

        [Tooltip("Minimum seconds between wall-bounce juice events - rate-limits the camera shake/haptic " +
                 "so even repeated hard bounces can't spam it. Keep ≥ strikeShakeDuration so each shake " +
                 "fully decays before the next can fire (no overlap).")]
        [SerializeField] float wallJuiceCooldown = 0.2f;
        public float WallJuiceCooldown => wallJuiceCooldown;

        [Header("Juice - Flash & Particles")]
        [Tooltip("Seconds the ball emission spikes after a strike")]
        [SerializeField] float impactFlashDuration = 0.12f;
        public float ImpactFlashDuration => impactFlashDuration;
        [Tooltip("Emission multiplier at peak flash")]
        [SerializeField] float impactFlashIntensity = 14f;
        public float ImpactFlashIntensity => impactFlashIntensity;
        [SerializeField] int impactParticleBurst = 28;
        public int ImpactParticleBurst => impactParticleBurst;
        [SerializeField] int goalParticleBurst = 120;
        public int GoalParticleBurst => goalParticleBurst;

        [Header("Ball - Speed-Reactive Visuals")]
        [Tooltip("Comet-wake width at the HEAD, as a fraction of the ball's DIAMETER, at rest. Stated " +
                 "relative to the ball because a TrailRenderer's width is world-space and ignores " +
                 "transform scale - a forged or intensity-scaled ball would otherwise trail a thread.")]
        [SerializeField] float trailWidthAtRest = 0.7f;
        public float TrailWidthAtRest => trailWidthAtRest;
        [Tooltip("Comet-wake head width (fraction of ball diameter) at speedForMaxVisuals.")]
        [SerializeField] float trailWidthAtSpeed = 1.25f;
        public float TrailWidthAtSpeed => trailWidthAtSpeed;
        [Tooltip("Seconds of wake the ball carries at rest.")]
        [SerializeField] float trailTimeAtRest = 0.3f;
        public float TrailTimeAtRest => trailTimeAtRest;
        [Tooltip("Seconds of wake the ball carries at speedForMaxVisuals.")]
        [SerializeField] float trailTimeAtSpeed = 1.1f;
        public float TrailTimeAtSpeed => trailTimeAtSpeed;
        [Tooltip("Wake DUST: glowing motes shed into the world per world unit the ball travels at full " +
                 "speed (scaled by speed; nothing below ~15% speed). They stay where they were shed and " +
                 "fade, so the path lingers after the ribbon has gone. 0 disables.")]
        [SerializeField] float wakeDustPerUnit = 0.28f;
        public float WakeDustPerUnit => wakeDustPerUnit;
        [SerializeField] float minEmissionIntensity = 2.5f;
        public float MinEmissionIntensity => minEmissionIntensity;
        [SerializeField] float maxEmissionIntensity = 11f;
        public float MaxEmissionIntensity => maxEmissionIntensity;
        [SerializeField] float minLightRange = 25f;
        public float MinLightRange => minLightRange;
        [SerializeField] float maxLightRange = 90f;
        public float MaxLightRange => maxLightRange;
        [Tooltip("Ball speed at which speed-reactive visuals are fully maxed")]
        [SerializeField] float speedForMaxVisuals = 160f;
        public float SpeedForMaxVisuals => speedForMaxVisuals;

        [Header("AI Striker")]
        [Tooltip("How far behind the ball (along the shot line) the AI aims its approach - the " +
                 "run-up that makes contact drive the ball goalward instead of sideways. Base / " +
                 "intensity 1; scaled with the arena.")]
        [SerializeField] float strikerApproachLead = 40f;
        public float StrikerApproachLead => strikerApproachLead;

        [Tooltip("When recovering position, how far past the ball the AI swings wide (base / " +
                 "intensity 1, scaled with the arena).")]
        [SerializeField] float strikerRecoverDistance = 120f;
        public float StrikerRecoverDistance => strikerRecoverDistance;

        [Tooltip("INTERCEPT PREDICTION: the AI aims where the ball WILL be, not where it is. Lead " +
                 "time is (distance to ball / this closing speed estimate), capped below - so a " +
                 "fast-moving ball is met rather than chased. 0 disables prediction (the old " +
                 "always-a-step-behind behaviour).")]
        [SerializeField] float strikerClosingSpeedEstimate = 120f;
        public float StrikerClosingSpeedEstimate => strikerClosingSpeedEstimate;

        [Tooltip("Cap on the intercept lead time (seconds). Without it a distant AI extrapolates " +
                 "the ball far past the wall it is about to bounce off and runs at empty space.")]
        [SerializeField] float strikerMaxLeadSeconds = 1.6f;
        public float StrikerMaxLeadSeconds => strikerMaxLeadSeconds;

        [Tooltip("ROLE SPLIT: fraction of each domain's AI that plays DEFENCE (rounded down, and " +
                 "a domain with a single AI always attacks). Defenders hold the line between the " +
                 "ball and their own goal instead of everybody piling onto the ball - the single " +
                 "biggest reason the old AI conceded: nobody was ever home.")]
        [SerializeField, Range(0f, 1f)] float strikerDefenderFraction = 0.5f;
        public float StrikerDefenderFraction => strikerDefenderFraction;

        [Tooltip("How far a defender sits off its own goal, toward the ball, as a fraction of the " +
                 "distance from its goal to the ball. 0 = parked in the mouth, 1 = on the ball.")]
        [SerializeField, Range(0.05f, 0.9f)] float defenderStandoffFraction = 0.35f;
        public float DefenderStandoffFraction => defenderStandoffFraction;

        [Tooltip("A defender clears rather than holds once the ball comes within this distance of " +
                 "its own goal (base / intensity 1, scaled with the arena) - at that point holding " +
                 "position is just watching it go in.")]
        [SerializeField] float defenderClearDistance = 260f;
        public float DefenderClearDistance => defenderClearDistance;

        [Tooltip("Safety margin (degrees) around the line to its OWN goal that an AI refuses to " +
                 "strike from. Inside it, contact would drive the ball at its own net, so it peels " +
                 "off and re-approaches instead. This is what stops AI own-goals.")]
        [SerializeField, Range(0f, 90f)] float strikerOwnGoalGuardDegrees = 55f;
        public float StrikerOwnGoalGuardDegrees => strikerOwnGoalGuardDegrees;

        [Header("Fauna - the pitch cleanup crew")]
        [Tooltip("Hold the cell's fauna OUTSIDE the court while the pitch is still clear, and let " +
                 "them in once it silts up. Implemented as Cell.FaunaExclusionRadius (a spatial " +
                 "diet + steering rule, never a wall - Docs/ECOSYSTEM.md §22.2b) driven off the " +
                 "cell's OWN volume phase ladder: Calm = closed, Restless or above = open. So " +
                 "'the arena is getting crowded' is read from the spine (LiveVolume), not from a " +
                 "signal invented for this mode.")]
        [SerializeField] bool faunaWaitOutsideCourt = true;
        public bool FaunaWaitOutsideCourt => faunaWaitOutsideCourt;

        [Tooltip("The closed exclusion radius, as a multiple of the court's own max extent. 1 = " +
                 "exactly the court (they patrol the outside of the wall you can see); above 1 " +
                 "holds them further out in the hypersea.")]
        [SerializeField, Min(0f)] float faunaExclusionCourtFraction = 1f;
        public float FaunaExclusionCourtFraction => faunaExclusionCourtFraction;

        [Tooltip("Seconds the exclusion wall takes to open and close. It sweeps rather than " +
                 "snapping so a swarm visibly pours in over the wall (continuity of existence " +
                 "applies to the pen's boundary too - a wall that teleports reads as a cheat).")]
        [SerializeField, Min(0f)] float faunaExclusionSweepSeconds = 3f;
        public float FaunaExclusionSweepSeconds => faunaExclusionSweepSeconds;

        [Header("Arena - Goal Portal Colors")]
        [Tooltip("Only the GAMEPLAY goal-portal rings are colored here. The arena no longer owns any " +
                 "boundary or atmosphere visuals - the playfield boundary read is the Cell's MembranePrefab " +
                 "and the drifting hypersea motes are the Cell's CytoplasmPrefab (CLAUDE.md ▸ \"Universality - " +
                 "one HyperSea, one rule set\"). Do not re-add an arena-local edge cage or plankton system; " +
                 "tune those on the Astro League Cell Config / its prefabs instead.")]
        [SerializeField] Color jadeGoalColor = new(0.15f, 1f, 0.55f, 0.5f);
        public Color JadeGoalColor => jadeGoalColor;
        [SerializeField] Color rubyGoalColor = new(1f, 0.22f, 0.35f, 0.5f);
        public Color RubyGoalColor => rubyGoalColor;
        [Tooltip("Gold's tint in the same per-domain family - no gold goal portal exists, but the ball " +
                 "tints to the LAST-HIT domain, which can be Gold.")]
        [SerializeField] Color goldGoalColor = new(1f, 0.82f, 0.2f, 0.5f);
        public Color GoldGoalColor => goldGoalColor;
    }
}

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
        public float matchDurationSeconds = 180f;

        [Tooltip("Domain goal sum that ends the match early (mercy rule). Published to GameDataSO.GoalTargetCount.")]
        public int goalLimit = 5;

        [Tooltip("If true and the score is tied at full time, play sudden-death golden goal")]
        public bool goldenGoalOvertime = true;

        [Header("Arena - Base Dimensions (intensity 1, before the scale table)")]
        [Tooltip("Court length along the GOAL AXIS (Z). The goal lines sit at ±length/2 and the " +
                 "flat caps there backboard missed shots. Authored HERE rather than on the scene's " +
                 "AstroLeagueArena so the whole playfield resizes from one asset - the controller " +
                 "derives the goal and team-spawn positions from it, so nothing in the scene has to " +
                 "be dragged when this changes.")]
        public float arenaLength = 400f;

        [Tooltip("Court width (X).")]
        public float arenaWidth = 320f;

        [Tooltip("Court height (Y). Deliberately close to the width: this is a 3D flight game and a " +
                 "flat pizza-box court wastes the axis that makes it one.")]
        public float arenaHeight = 240f;

        [Tooltip("Goal mouth + portal-ring radius (base / intensity 1). ONE number: the arena draws " +
                 "its portal rings at it and the controller hands the same value to every " +
                 "AstroLeagueGoal, so the ring you aim at IS the mouth that scores (they used to be " +
                 "two independently-authored fields that could silently disagree).")]
        public float goalMouthRadius = 62f;

        [Tooltip("Distance from the arena centre, toward its own goal, where a team parks for " +
                 "kickoff (base / intensity 1, scaled with the arena). Keep it comfortably inside " +
                 "arenaLength/2 so a kickoff line never sits on top of a goal mouth.")]
        public float kickoffLineDistance = 150f;

        [Header("Intensity Scale (arena + ball + team spawns)")]
        [Tooltip("Arena, ball, goal and team-spawn scale PER INTENSITY (index 0 = intensity 1). " +
                 "Overrides the legacy even ramp whenever non-empty; falls back to the last entry " +
                 "above its length. Multiplies the base dimensions above. History worth keeping: the " +
                 "court shipped far too SMALL (a pitch you crossed in two seconds), was taken to " +
                 "3-4.5x, and that overshot - a frictionless ball in a huge box read as pong. These " +
                 "are that pass cut by 40%. Vessels stay normal size.")]
        public float[] arenaScaleByIntensity = { 1.8f, 2.1f, 2.4f, 2.7f };

        [Tooltip("LEGACY fallback ramp (used only when arenaScaleByIntensity is empty): scale steps " +
                 "evenly from 1x at intensity 1 up to this factor at maxIntensityLevel.")]
        public float intensityScaleAtMax = 2f;

        [Tooltip("Highest intensity level used for the legacy scale ramp (the arcade card's MaxIntensity).")]
        public int maxIntensityLevel = 4;

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
        public bool[] centralGoalByIntensity = { false, false, false, true };

        [Tooltip("For a central-goal court: how far off the arena center (world units at intensity 1, " +
                 "along X, in the goal's plane) the ball spawns, so it doesn't start sitting in the goal.")]
        public float centralBallSpawnOffset = 70f;

        [Tooltip("Radius (at intensity 1) of the boundary ONLY when the shape is Sphere. Should " +
                 "circumscribe the box court above (sqrt of the summed half-extents squared) so the " +
                 "sphere reads as the same size arena. Scales with match intensity. Polytope shapes " +
                 "derive their walls from the arena's length/width/height instead.")]
        public float boundaryRadius = 285f;

        [Tooltip("0..1 chamfer depth for the OctagonalPrism (how far the 4 goal-axis edges are cut " +
                 "toward the corner). ~0.5 reads as a clean octagon; higher = more cut.")]
        [Range(0f, 1f)] public float octagonBevelFraction = 0.5f;

        [Tooltip("0..1 chamfer depth for the BeveledBox (every edge + corner cut). Higher = rounder, " +
                 "more Rocket-League corner-ramp redirect; lower = closer to a sharp box.")]
        [Range(0f, 1f)] public float beveledBoxBevelFraction = 0.45f;

        [Header("Arena - NotchedRing (central ring obstacle)")]
        [Tooltip("Outer court the central ring sits inside, for the NotchedRing shape (default Cylinder). " +
                 "Anything except NotchedRing itself.")]
        public AstroLeagueBoundaryShape notchedRingOuterShape = AstroLeagueBoundaryShape.Cylinder;

        [Tooltip("Ring radius as a fraction of the court cross-section radius (min(width,height)/2). The " +
                 "central hole = (major − tube) and must clear the ball, so keep major above tube.")]
        [Range(0f, 1f)] public float ringMajorRadiusFraction = 0.5f;

        [Tooltip("Ring thickness (tube radius) as a fraction of the court cross-section radius. The ball " +
                 "bounces off the OUTSIDE of this tube.")]
        [Range(0f, 1f)] public float ringTubeRadiusFraction = 0.18f;

        [Tooltip("Angle (degrees, atan2(y,x)) of the notch center - the gap cut in the ring, a shooting lane.")]
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

        [Header("Arena - Edge Lining (super-shielded prisms)")]
        [Tooltip("Lay a lining of SUPER-SHIELDED (invulnerable) neutral prisms along the court's edges " +
                 "at every intensity - polytope hull edges, cylinder cap rims, sphere latitude rings. " +
                 "Laid per peer through the standard PrismFactory channel (prisms bloom in; removal is " +
                 "the animated Damage path - continuity law).")]
        public bool edgePrismsEnabled = true;

        [Tooltip("TOTAL prisms in the lining, distributed evenly over the court's summed edge length - " +
                 "FIXED across shapes and intensities so the lining's volume budget (count x prism " +
                 "volume) stays deterministic. The Astro League Cell Config's phase-volume thresholds " +
                 "are raised by exactly that budget (480 x 62.5 = 30000) - retune them together. " +
                 "Collider budget: each lining prism holds an always-on convex MeshCollider (the " +
                 "engaged stellated shield) that collider-LOD cannot reclaim - keep this bounded. " +
                 "480 is the doubling that keeps the rim reading as a line on the (much larger) " +
                 "current court instead of a dotted one.")]
        public int edgePrismCount = 480;

        [Tooltip("Lining prism TargetScale (long Z axis laid ALONG the edge). Volume = x*y*z; total " +
                 "lining volume = edgePrismCount x that volume. Change either and retune the cell " +
                 "config's phase-volume thresholds to match.")]
        public Vector3 edgePrismScale = new(2.5f, 2.5f, 10f);

        [Tooltip("Inward offset from the edge line toward the arena center (world units at intensity 1, " +
                 "scales with the arena) so the lining sits just inside the wall the ball bounces off.")]
        public float edgePrismInset = 6f;

        [Header("Goal Reset (every non-final goal)")]
        [Tooltip("On every goal: every peer parks the vessels it owns back on their kickoff lines with " +
                 "speed ZEROED and clears the accumulated field prisms while the goal replay plays. " +
                 "Kickoff re-parks (idempotent) before GO.")]
        public bool goalResetsArena = true;

        [Tooltip("Seconds the staggered prism-clear sweep takes (center-out wave, canonical animated " +
                 "Damage path - never a raw Destroy). Keep within celebration + kickoff-freeze.")]
        public float goalPrismClearSeconds = 1.6f;

        [Header("Goal Replay (the replay camera)")]
        [Tooltip("Replay the goal on the shared END camera (the replay camera) while the arena resets " +
                 "behind it: a visual-only ghost ball retraces the recorded flight into the goal, camera " +
                 "following. Purely local on every peer (the ball trajectory is already replicated); the " +
                 "gameplay camera is restored at kickoff GO or when playback ends.")]
        public bool goalReplayEnabled = true;

        [Tooltip("Seconds of ball flight recorded for the replay (ring buffer, cleared at every kickoff " +
                 "GO so a replay never crosses a reset).")]
        public float goalReplayRecordSeconds = 4f;

        [Tooltip("Fraction of the celebration + kickoff-freeze window the ghost playback may fill; " +
                 "playback speed is derived to fit (slow-mo when the recording is short).")]
        [Range(0.3f, 1f)] public float goalReplayWindowFraction = 0.85f;

        [Tooltip("Playback-speed floor - a goal scored seconds after kickoff would otherwise stretch a " +
                 "tiny recording into extreme slow-mo.")]
        public float goalReplayMinPlaybackSpeed = 0.3f;

        [Tooltip("Broadcast framing margin: the replay camera sits at a FIXED vantage beside the " +
                 "recorded flight, far enough back that the whole shot fits the field of view times " +
                 "this margin (1 = exact fit, higher = wider establishing shot), and PANS to track " +
                 "the ghost rather than chasing it.")]
        public float goalReplayFramingMargin = 1.35f;

        [Tooltip("Elevation of the broadcast vantage above the flight's centroid, as a fraction of " +
                 "the vantage distance (0 = level with the play, higher = more of a stadium " +
                 "high-camera look).")]
        [Range(0f, 1f)] public float goalReplayVantageElevation = 0.35f;

        [Tooltip("How quickly the replay camera pans to keep the ghost in frame (higher = tighter " +
                 "tracking, lower = lazier broadcast pan that lets the ball lead the frame).")]
        public float goalReplayPanSpeed = 3.5f;

        [Header("Vessel Recoil (juice)")]
        [Tooltip("Backward velocity (units/sec) added to a vessel when it strikes the ball, a subtle " +
                 "'bounce off' juice. DEFAULT 0 (OFF): anti-clip is already guaranteed by the ball's own " +
                 "depenetration (EjectBallFromVessel), so any recoil only fights player control - a " +
                 "frictionless ball that keeps bouncing back into a vessel re-fires it every cooldown, " +
                 "stacking toward VesselTransformer.velocityModifierMax (100) and throwing the vessel " +
                 "back 'like crazy'. Dial up only for a deliberate subtle bounce; scaled by hit strength.")]
        public float vesselRecoilSpeed = 0f;

        [Tooltip("Seconds the vessel recoil impulse lasts (cosine-windowed by VesselTransformer).")]
        public float vesselRecoilDuration = 0.12f;

        [Header("Kickoff Pacing")]
        [Tooltip("Seconds of GOAL! celebration (real time) before the ball resets")]
        public float celebrationSeconds = 2.2f;

        [Tooltip("Time.timeScale during the goal celebration slow-mo. Solo sessions only - " +
                 "never applied with a second connected client (local timescale desyncs peers).")]
        [Range(0.05f, 1f)] public float celebrationTimeScale = 0.35f;

        [Tooltip("Seconds the ball stays frozen at center during a kickoff count-in")]
        public float kickoffFreezeSeconds = 2.4f;

        [Tooltip("Seconds the winner banner holds before the shared scoreboard flow takes over")]
        public float winnerBannerSeconds = 2.5f;

        [Header("Kickoff Parking")]
        [Tooltip("Lateral spacing between teammates parked on the same kickoff line (base / " +
                 "intensity 1, scaled with the arena). The distance from centre is " +
                 "kickoffLineDistance, up with the other arena dimensions.")]
        public float kickoffLateralSpacing = 45f;

        [Header("Ball - Vessel Strike (elastic, momentum-conserving)")]
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
                 "(ball radius + this) from the vessel root - guarantees the vessel hull never clips " +
                 "through the ball, including the trigger-only ships (Serpent/Sparrow) that have no " +
                 "physical depenetration. Roughly the vessel's visual hull reach.")]
        public float vesselClearRadius = 12f;

        [Header("Ball - Explosions")]
        [Tooltip("Let AOE blasts shove the ball, the way they shove prisms. The blast hands over " +
                 "the SAME impact vector a prism receives (ExplosionImpulse.Along, no distance " +
                 "falloff - prisms do not get one either), so a weapon that throws mass hard " +
                 "throws the payload hard. Off restores the previous behaviour, where a blast " +
                 "passed through the ball with no effect at all.")]
        public bool explosionsAffectBall = true;

        [Tooltip("Gain on the blast's impact vector before it is added to the ball's velocity. " +
                 "1 = the ball takes the blast's TRUE impulse, exactly as prism debris does — " +
                 "which is the default, because the blast's own Inertia is already the dial for " +
                 "how hard that blast throws. Below 1 the ball is a heavier payload than the mass " +
                 "around it; this shipped at 0.5 and, against a 300 u/s ball, made a blast worth " +
                 "~11% of top speed — a shove nobody could see.")]
        [Min(0f)] public float explosionKickMultiplier = 1f;

        [Tooltip("Fraction of the blast kick that also arrives as SPIN, applied off-centre at the " +
                 "blast-facing surface. 0 = a pure shove.")]
        [Range(0f, 1f)] public float explosionSpinFraction = 0.6f;

        [Tooltip("A blast RE-COLOURS the ball to the blasting domain, exactly as a vessel strike " +
                 "does - blowing the payload is a way to claim it. Off leaves ownership alone and " +
                 "makes the blast a pure shove.")]
        public bool explosionClaimsBall = true;

        [Header("Ball - Vessel Strike (sword / blade contact)")]
        [Tooltip("Resolve a strike at the point on a SKIMMER BLADE that actually touched the ball " +
                 "(SkimmerSwingKinematics - the Rhino's sword) instead of at the vessel root. Two " +
                 "things change: the bounce normal comes off the blade, so a tip hit deflects the " +
                 "ball off the tip rather than off the fuselage 60 units away, and the strike speed " +
                 "is the blade point's TRUE velocity - a swung tip is many times faster than the " +
                 "hull, so a real swing sends the ball screaming. Vessels with no swinging skimmer " +
                 "are unaffected (the model reports not-ready and the hull path runs).")]
        public bool bladeAwareStrikes = true;

        [Tooltip("Clearance around a BLADE's centreline, doing two jobs. Anti-clip: the ball is " +
                 "pushed off the blade segment to (ball radius + this) on every contact frame (the " +
                 "hull's vesselClearRadius, measured from the vessel root, cannot protect a 30-120 " +
                 "unit sword). And REACH: a swinging skimmer also carries a large sphere trigger - " +
                 "its skim field - so a contact further than (ball radius + this) from the blade " +
                 "centreline is the skim field, not the sword, and does not strike the ball. Raise " +
                 "it for a more forgiving swing, lower it to demand a cleaner hit.")]
        public float bladeClearRadius = 7f;

        [Tooltip("Extra launch multiplier at the very TIP of a blade, lerped from 1 at the hilt. " +
                 "The swing model already makes the tip faster on its own; this is the arcade " +
                 "reward on top for actually timing a tip strike. 1 = no tip bonus.")]
        [Min(1f)] public float bladeTipStrikeBonus = 1.35f;

        [Header("Juice - Vessel Strike Feedback")]
        [Tooltip("Broadcast the full strike feedback (ball flash, contact burst, camera shake, " +
                 "audio cue) to every peer when a vessel connects. Before this existed a hit " +
                 "produced NOTHING except the ball changing direction - the single biggest reason " +
                 "the mode read as unresponsive.")]
        public bool strikeFeedbackEnabled = true;

        [Tooltip("Camera-shake multiplier applied to the STRIKING pilot on top of the shared " +
                 "distance falloff, so connecting feels different from watching somebody else " +
                 "connect. 1 = no extra emphasis.")]
        [Min(1f)] public float strikerShakeEmphasis = 1.8f;

        [Tooltip("Ball speed (as a fraction of maxSpeed) a strike must produce to read as a BIG " +
                 "hit - above it the flash, burst and shake all run at full strength and the " +
                 "heavier audio cue plays.")]
        [Range(0f, 1f)] public float bigHitSpeedFraction = 0.45f;

        [Tooltip("Seconds the ball's impact POP lasts - a fast scale pulse on the ball's visual " +
                 "child that reads as impact even at the far end of a big arena, where a particle " +
                 "burst is a few pixels. Visual only: it rides a child transform, so the collider " +
                 "and BallWorldRadius (goal threshold, prism scan, depenetration) never move. " +
                 "0 disables it.")]
        public float strikePopSeconds = 0.2f;

        [Tooltip("Peak pop amount (0.22 = the ball swells 22% at the moment of impact and eases " +
                 "back). Kept uniform rather than a directional squash so the faceted icosphere's " +
                 "spin never appears to jump.")]
        [Range(0f, 0.6f)] public float strikePopAmount = 0.22f;

        [Tooltip("Seconds a ball's visual BLOOMS IN over when it comes into existence — the " +
                 "continuity-of-existence law applied to the payload itself (a Scarab-forged " +
                 "ball must grow out of its crystal, never pop in). Visual child only, same " +
                 "rule as the strike pop: the physical radius is live from frame one. " +
                 "0 disables (instant, law-breaking — leave it on).")]
        [Min(0f)] public float spawnBloomSeconds = 0.55f;

        [Tooltip("Seconds after a ball is knocked out of the nucleus surface during which its " +
                 "court/cytoplasm boundary is NOT applied (SCARAB.md §4.6). An embedded ball sits " +
                 "part-sunk in the nucleus shell, which is on the wrong side of BOTH boundaries — " +
                 "so the frame it is released, containment corrects it and the ball JUMPS radially " +
                 "in or out. This grace lets its own velocity carry it across that band, after " +
                 "which containment engages on a ball that is already where it belongs. 0 = the " +
                 "old snap.")]
        [Min(0f)] public float nucleusReleaseGraceSeconds = 1f;

        [Header("Ball - Detonation")]
        [Tooltip("Domain explosion spawned where a ball detonates (the nucleus overload / ball-cap " +
                 "overflow). Coloured by the BALL's domain, and the standard blast rules then " +
                 "apply: own-domain prisms take a temporary shield (no perceived clipping), other " +
                 "domains are destroyed. Leave EMPTY for a burst with no blast — an unwired slot " +
                 "is a visible TODO, never a borrowed prefab.")]
        public AOEExplosion[] detonationExplosionPrefabs;

        [Header("Ball - Physics (zero friction)")]
        public float maxSpeed = 380f;
        public float ballMass = 3f;
        [Tooltip("Restitution for a VESSEL strike (1 = perfectly elastic). Keep this high - it is what " +
                 "lets a swung sword fire the payload. Wall caroms use wallRestitution instead.")]
        [Range(0f, 1f)] public float ballBounciness = 1f;

        [Tooltip("Restitution for a WALL carom. Below 1 every bounce takes energy out, which is what " +
                 "stops the ball ricocheting around the court forever (the original 1.0 + zero drag " +
                 "made the mode read as pong). Lower = the ball dies against the boards faster; " +
                 "1 restores the old perfectly-elastic pinball.")]
        [Range(0f, 1f)] public float wallRestitution = 0.72f;

        [Tooltip("Restitution for a carom off a SHIELDED PRISM. 1 (the default) is a pure " +
                 "REDIRECT: the into-prism component mirrors exactly, so the ball's SPEED is " +
                 "unchanged and only its heading turns. That is the whole point of armour here — " +
                 "a shielded prism costs the shield and the ball's line, never its momentum. " +
                 "Deliberately separate from wallRestitution: a wall is meant to bleed energy " +
                 "(0.72), and a prism is not.")]
        [Range(0f, 1f)] public float prismCaromRestitution = 1f;

        [Tooltip("Exponential speed decay per second on the ball's coast (0 = the original " +
                 "frictionless coast). This is what makes an untouched ball SETTLE, so it becomes a " +
                 "thing players go and contest rather than something ricocheting past them. Composes " +
                 "with the opposing-prism-mass drag below rather than replacing it.")]
        [Min(0f)] public float ballDrag = 0.35f;

        [Tooltip("Multiplier on ballDrag once the ball is fully OUTSIDE the cell's nucleus — the " +
                 "hypersea getting thicker off the pitch. 1 disables the ramp. This is a soft " +
                 "boundary, not a wall: a ball that gets out is not teleported, culled or " +
                 "reflected, it simply bleeds speed fast enough to stop being gone. In a mode " +
                 "whose court IS the nucleus (Astro League) the ball is reflected at that radius " +
                 "anyway, so the ramp never engages and the mode is unaffected.")]
        [Min(1f)] public float outsideNucleusDragMultiplier = 6f;

        [Tooltip("How far past the nucleus surface the drag ramp takes to reach full strength, in " +
                 "world units. The ramp is linear across this band so leaving the pitch is a " +
                 "gradient rather than a cliff.")]
        [Min(1f)] public float outsideNucleusDragFalloff = 250f;

        [Tooltip("A ball OUTSIDE the nucleus is held inside this fraction of the cell's MEMBRANE " +
                 "radius, bouncing off the nucleus from the outside — the cytoplasm half of the " +
                 "ball's own nucleus containment (SCARAB.md §4.6). 1 = the cell's own membrane; " +
                 "just under it so a ball never rides the literal skin. This lives here rather " +
                 "than on ScarabNucleusFieldConfig because containment is a property of the BALL, " +
                 "applied in every cell it can reach — not of the seeding ability that made one.")]
        [Range(0.1f, 1f)] public float cytoplasmOuterFraction = 0.95f;

        [Tooltip("Speed below which the remaining coast is snapped to zero. Exponential decay is " +
                 "asymptotic, so without this the ball creeps forever at an invisible speed and never " +
                 "actually comes to rest.")]
        [Min(0f)] public float ballRestSpeed = 6f;

        [Tooltip("How hard opposing-color prism MASS slows the ball as it plows through (it keeps its " +
                 "direction, only its speed drops). Per eaten prism: speed ×= ballMass / (ballMass + " +
                 "this × prismVolume). 0 = no drag (ball never slows); higher = a thick enemy wall " +
                 "brakes the ball hard. Same-color and shielded prisms cost no speed.\n\n" +
                 "The slow depends on VOLUME and nothing else, so a DANGER prism and a plain one of " +
                 "the same size cost the ball exactly the same — see the tier note in " +
                 "AstroLeagueBall.ProcessPrismInteractions. Do not add a per-tier multiplier here.")]
        public float prismDragMassScale = 0.0167f;

        [Header("Ball - Angular Dynamics (rotational inertia)")]
        [Tooltip("Angular damping on the ball rigidbody. A small amount so spin imparted by off-center " +
                 "vessel strikes gradually settles instead of tumbling forever, while still reading as " +
                 "a freely-spinning billiard payload.")]
        public float ballAngularDamping = 0.3f;

        [Tooltip("Cap on the ball's angular speed (rad/s). Unity's default rigidbody clamp (7 rad/s) " +
                 "is too low to read as a fast spin - raise it so off-center strikes produce a " +
                 "visible tumble on the faceted icosphere.")]
        public float maxAngularSpeed = 40f;

        [Header("Ball - Mesh")]
        [Tooltip("Icosphere subdivision count for the ball mesh (each level ×4 the faces: " +
                 "0=20, 1=80, 2=320, 3=1280 tris). Level 2 is medium-poly - faceted enough that " +
                 "rotation is clearly visible, dense enough to read as round.")]
        public int ballMeshSubdivisions = 2;

        [Header("Ball - Prism Scan")]
        [Tooltip("Radius (× the ball's world radius) of the per-tick spatial scan that resolves prism " +
                 "interactions. 1 = exactly the ball's cross-section (clears a ball-sized tunnel); " +
                 "slightly above 1 catches prisms just grazing the surface. The ball is a first-class " +
                 "entity - this scan runs every physics tick on every peer, independent of colliders.")]
        public float prismScanRadiusFactor = 1.1f;

        [Header("Ball - Client Replication")]
        [Tooltip("How aggressively non-server peers blend toward the dead-reckoned ball position (higher = snappier)")]
        public float clientSmoothingRate = 12f;

        [Tooltip("Position error beyond which non-server peers snap instead of smoothing")]
        public float clientSnapDistance = 30f;

        [Header("Juice - Hitstop (solo sessions only)")]
        public float hitstopDuration = 0.045f;
        [Range(0.01f, 1f)] public float hitstopTimeScale = 0.1f;
        [Tooltip("Ball speed required to trigger hitstop on a strike")]
        public float hitstopSpeedThreshold = 70f;

        [Header("Juice - Camera Shake")]
        public float strikeShakeIntensity = 1.0f;
        public float strikeShakeDuration = 0.18f;
        public float goalShakeIntensity = 2.5f;
        public float goalShakeDuration = 0.5f;

        [Tooltip("Camera shake fades with distance from the impact, reaching zero at this radius. " +
                 "Scale it with the court - on the current arena a 180-unit falloff meant almost " +
                 "every event was silent for almost everybody.")]
        public float shakeFalloffRadius = 700f;

        [Tooltip("Wall-bounce juice (camera shake / haptic / burst) only fires when the PERPENDICULAR " +
                 "into-wall speed is at least this fraction of maxSpeed. A frictionless ball skimming " +
                 "tangentially along a curved wall has ~0 perpendicular speed, so this stops it from " +
                 "continuously shaking the camera (the high-frequency jitter).")]
        [Range(0f, 1f)] public float wallJuiceMinIntensity = 0.12f;

        [Tooltip("Minimum seconds between wall-bounce juice events - rate-limits the camera shake/haptic " +
                 "so even repeated hard bounces can't spam it. Keep ≥ strikeShakeDuration so each shake " +
                 "fully decays before the next can fire (no overlap).")]
        public float wallJuiceCooldown = 0.2f;

        [Header("Juice - Flash & Particles")]
        [Tooltip("Seconds the ball emission spikes after a strike")]
        public float impactFlashDuration = 0.12f;
        [Tooltip("Emission multiplier at peak flash")]
        public float impactFlashIntensity = 14f;
        public int impactParticleBurst = 28;
        public int goalParticleBurst = 120;

        [Header("Ball - Speed-Reactive Visuals")]
        public float minTrailWidth = 0.6f;
        public float maxTrailWidth = 5f;
        public float minEmissionIntensity = 2.5f;
        public float maxEmissionIntensity = 11f;
        public float minLightRange = 25f;
        public float maxLightRange = 90f;
        [Tooltip("Ball speed at which speed-reactive visuals are fully maxed")]
        public float speedForMaxVisuals = 160f;

        [Header("AI Striker")]
        [Tooltip("How far behind the ball (along the shot line) the AI aims its approach - the " +
                 "run-up that makes contact drive the ball goalward instead of sideways. Base / " +
                 "intensity 1; scaled with the arena.")]
        public float strikerApproachLead = 40f;

        [Tooltip("When recovering position, how far past the ball the AI swings wide (base / " +
                 "intensity 1, scaled with the arena).")]
        public float strikerRecoverDistance = 120f;

        [Tooltip("INTERCEPT PREDICTION: the AI aims where the ball WILL be, not where it is. Lead " +
                 "time is (distance to ball / this closing speed estimate), capped below - so a " +
                 "fast-moving ball is met rather than chased. 0 disables prediction (the old " +
                 "always-a-step-behind behaviour).")]
        public float strikerClosingSpeedEstimate = 120f;

        [Tooltip("Cap on the intercept lead time (seconds). Without it a distant AI extrapolates " +
                 "the ball far past the wall it is about to bounce off and runs at empty space.")]
        public float strikerMaxLeadSeconds = 1.6f;

        [Tooltip("ROLE SPLIT: fraction of each domain's AI that plays DEFENCE (rounded down, and " +
                 "a domain with a single AI always attacks). Defenders hold the line between the " +
                 "ball and their own goal instead of everybody piling onto the ball - the single " +
                 "biggest reason the old AI conceded: nobody was ever home.")]
        [Range(0f, 1f)] public float strikerDefenderFraction = 0.5f;

        [Tooltip("How far a defender sits off its own goal, toward the ball, as a fraction of the " +
                 "distance from its goal to the ball. 0 = parked in the mouth, 1 = on the ball.")]
        [Range(0.05f, 0.9f)] public float defenderStandoffFraction = 0.35f;

        [Tooltip("A defender clears rather than holds once the ball comes within this distance of " +
                 "its own goal (base / intensity 1, scaled with the arena) - at that point holding " +
                 "position is just watching it go in.")]
        public float defenderClearDistance = 260f;

        [Tooltip("Safety margin (degrees) around the line to its OWN goal that an AI refuses to " +
                 "strike from. Inside it, contact would drive the ball at its own net, so it peels " +
                 "off and re-approaches instead. This is what stops AI own-goals.")]
        [Range(0f, 90f)] public float strikerOwnGoalGuardDegrees = 55f;

        [Header("Fauna - the pitch cleanup crew")]
        [Tooltip("Hold the cell's fauna OUTSIDE the court while the pitch is still clear, and let " +
                 "them in once it silts up. Implemented as Cell.FaunaExclusionRadius (a spatial " +
                 "diet + steering rule, never a wall - Docs/ECOSYSTEM.md §22.2b) driven off the " +
                 "cell's OWN volume phase ladder: Calm = closed, Restless or above = open. So " +
                 "'the arena is getting crowded' is read from the spine (LiveVolume), not from a " +
                 "signal invented for this mode.")]
        public bool faunaWaitOutsideCourt = true;

        [Tooltip("The closed exclusion radius, as a multiple of the court's own max extent. 1 = " +
                 "exactly the court (they patrol the outside of the wall you can see); above 1 " +
                 "holds them further out in the hypersea.")]
        [Min(0f)] public float faunaExclusionCourtFraction = 1f;

        [Tooltip("Seconds the exclusion wall takes to open and close. It sweeps rather than " +
                 "snapping so a swarm visibly pours in over the wall (continuity of existence " +
                 "applies to the pen's boundary too - a wall that teleports reads as a cheat).")]
        [Min(0f)] public float faunaExclusionSweepSeconds = 3f;

        [Header("Arena - Goal Portal Colors")]
        [Tooltip("Only the GAMEPLAY goal-portal rings are colored here. The arena no longer owns any " +
                 "boundary or atmosphere visuals - the playfield boundary read is the Cell's MembranePrefab " +
                 "and the drifting hypersea motes are the Cell's CytoplasmPrefab (CLAUDE.md ▸ \"Universality - " +
                 "one HyperSea, one rule set\"). Do not re-add an arena-local edge cage or plankton system; " +
                 "tune those on the Astro League Cell Config / its prefabs instead.")]
        public Color jadeGoalColor = new(0.15f, 1f, 0.55f, 0.5f);
        public Color rubyGoalColor = new(1f, 0.22f, 0.35f, 0.5f);
        [Tooltip("Gold's tint in the same per-domain family - no gold goal portal exists, but the ball " +
                 "tints to the LAST-HIT domain, which can be Gold.")]
        public Color goldGoalColor = new(1f, 0.82f, 0.2f, 0.5f);
    }
}

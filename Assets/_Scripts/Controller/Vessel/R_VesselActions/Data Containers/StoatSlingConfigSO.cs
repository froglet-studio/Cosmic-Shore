using CosmicShore.Data;
using CosmicShore.Gameplay;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The Stoat's SLINGSHOT (<c>R_VesselActions/STOAT.md</c>): every number behind a trigger that lays
    /// an attractor–repulsor wormhole pair beside the hull. The pair itself is the black hole's
    /// (<c>Docs/BLACK_HOLE.md</c> §11) — this file only decides where, how big and how long.
    ///
    /// <para><b>Time mode is the SKIM SLING</b> since 2026-10-29 whenever <see cref="Skim"/> is set
    /// (<see cref="StoatSkimConfigSO"/>, <c>STOAT_DIPOLE.md</c> §1q); the catch below is its fallback.</para>
    ///
    /// <para><b>Time mode — the catch</b> (2026-10-14, the studio's dipole sling: <c>STOAT_DIPOLE.md</c> §1b). PRESS lays a
    /// LIGHT attractor <see cref="CatchAhead"/> ahead and <see cref="CatchSide"/> to the trigger's side, the repulsor
    /// mirrored on the other — in front of the hull, where the camera sees it. Two rings are drawn round it: the ORBIT
    /// radius (the squeeze sets it live: harder = tighter, <see cref="OrbitRadiusWide"/> → <see cref="OrbitRadiusTight"/>)
    /// and the DETECTION radius (<see cref="CatchReach"/> × the orbit). Fly past it and it CATCHES you onto the orbit;
    /// hold and you go round; RELEASE and you slingshot out along the tangent, the kick timed to a quarter-turn. It cannot
    /// suck you in: the orbit radius is many horizons wide and it catches you at it. The pair then falls together at
    /// <see cref="DriftSpeed"/> and annihilates.</para>
    ///
    /// <para><b>The crystal style</b> (<c>BlackHoleConfig.crystalPairs</c>, §13) still lays its pair on
    /// RELEASE, sized by the squeeze between <see cref="MinStrength"/> and <see cref="MaxStrength"/>, at
    /// <see cref="AheadHorizons"/> / <see cref="HalfGapHorizons"/>.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "StoatSlingConfig", menuName = "ScriptableObjects/Vessel Actions/Stoat Sling Config")]
    public sealed class StoatSlingConfigSO : ScriptableObject
    {
        [Header("Squeeze")]
        [Tooltip("Squeeze curve exponent: 1 = linear, >1 keeps a light squeeze wide and makes the last bit of travel count.")]
        [SerializeField, Range(0.5f, 4f)] float holdExponent = 1.5f;

        [Tooltip("Keyboard / mouse / touch have no analog trigger: seconds of hold that count as a full squeeze.")]
        [SerializeField, Range(0.1f, 5f)] float holdRampSeconds = 3f;

        [Tooltip("The autopilot writes no trigger; its every sling is this fraction of a full squeeze.")]
        [SerializeField, Range(0f, 1f)] float autopilotHold01 = 0.5f;

        [Header("Time mode - the SKIM SLING (2026-10-29: replaces the catch below while set)")]
        [Tooltip("The Skim Sling (STOAT_DIPOLE.md §1q): set, Time mode's triggers lay a Skimshell to ride and launch off; " +
                 "empty, they fly the catch below (kept as the fallback).")]
        [SerializeField] StoatSkimConfigSO skim;

        [Header("Time mode - the catch (press lays it ahead, fly past and it catches you, hold orbits, release slings)")]
        [Tooltip("How far AHEAD of the hull the pair is laid, world units (the studio's dpAhead) - in front, where you see it.")]
        [SerializeField, Range(20f, 400f)] float catchAhead = 150f;

        [Tooltip("How far to the pressed side the attractor is laid (the repulsor the same to the other side), world units " +
                 "(the studio's dpSide). Flying straight, about how close you pass it.")]
        [SerializeField, Range(0f, 200f)] float catchSide = 45f;

        [Tooltip("The DETECTION radius as a multiple of the orbit radius (the studio's dpCaptureX): pass inside it, with the " +
                 "attractor off your nose by at least catchDegrees, and it catches you. Inside the orbit radius it always does.")]
        [SerializeField, Range(1f, 3f)] float catchReach = 1.5f;

        [Tooltip("It catches you only once it is at least this far off the nose (the studio's dpCatchDeg): AS you pass it, " +
                 "not while it is still dead ahead.")]
        [SerializeField, Range(0f, 90f)] float catchDegrees = 50f;

        [Tooltip("Once caught, how fast the nose eases onto the orbit's tangent, per second (the studio's dpSettle).")]
        [SerializeField, Range(0.1f, 20f)] float settleRate = 3f;

        [Tooltip("The longest orbit, degrees round the attractor (the studio's dpMaxTurn): past it you are let go on your own.")]
        [SerializeField, Range(90f, 1080f)] float maxOrbitDegrees = 360f;

        [Tooltip("Orbit radius at the lightest squeeze, world units: a wide, gentle turn.")]
        [SerializeField, Min(1f)] float orbitRadiusWide = 150f;

        [Tooltip("Orbit radius fully squeezed, world units: the tightest turn.")]
        [SerializeField, Min(1f)] float orbitRadiusTight = 40f;

        [Tooltip("Space is reach: a multiplier on both orbit radii (and so the detection radius) - more Space, wider.")]
        [SerializeField] ElementalFloat orbitReach = ElementalFloat.Multiplier(1f, 1.4f, Element.Space, 1f);

        [Tooltip("How fast the orbit radius follows a changing squeeze, per second.")]
        [SerializeField, Range(0.1f, 20f)] float radiusFollowRate = 3f;

        [Tooltip("How hard a caught hull is held on the orbit's radius, per second.")]
        [SerializeField, Range(0.1f, 20f)] float radialCorrectionRate = 5f;

        [Header("Time mode - the catch's control (2026-10-27, STOAT_DIPOLE.md §1b; the studio's dp* rows)")]
        [Tooltip("On: the circle a caught hull is held on starts at the distance it was caught at and eases toward the " +
                 "squeeze's at orbitEaseRate - no sideways yank onto a preset circle at the catch. Off: snapped onto the " +
                 "squeeze's circle at radialCorrectionRate (round 6). Studio: dpHoldAtCatch.")]
        [SerializeField] bool holdAtCatch = true;

        [Tooltip("With holdAtCatch: how fast the held circle eases toward the squeeze's, per second. Studio: dpRadiusEase.")]
        [SerializeField, Range(0f, 5f)] float orbitEaseRate = 0.8f;

        [Tooltip("The orbit radius is at most this share of the distance between the poles (2 x catchSide): wider, the " +
                 "circle reaches round the repulsor and swings the hull past its side. 0 = no limit. Studio: dpWhiteClear.")]
        [SerializeField, Range(0f, 1.5f)] float whiteClear = 0.8f;

        [Header("The orbit (Yash, 2026-11-02 play test; Unity-first, not in the studio yet)")]
        [Tooltip("Once caught, the hull is drawn in toward the INNER orbit: this share of the squeeze's circle. 1 = held on " +
                 "the squeeze's circle as before. The outer, detection ring is unchanged, so the catch is as easy.")]
        [SerializeField, Range(0.2f, 1f)] float innerOrbitShare = 0.6f;

        [Tooltip("How fast a caught hull is drawn in to the inner orbit, per second. 0 = orbitEaseRate (the studio's ease).")]
        [SerializeField, Range(0f, 10f)] float innerPullRate = 1.5f;

        [Tooltip("While the orbit holds the hull, it flies this much faster than the catch's warp alone (0.15 = 15% more).")]
        [SerializeField, Range(0f, 1f)] float orbitSpeedBonus = 0.15f;

        [Header("The launch (Yash, 2026-11-02: flick to aim it, Mass is its power)")]
        [Tooltip("A release launches the hull toward where the stick points, at most this many degrees off the orbit's " +
                 "tangent (the full cone at a full stick). 0 = always along the tangent, the round-6 slingshot.")]
        [SerializeField, Range(0f, 90f)] float launchConeDegrees = 60f;

        [Tooltip("Below this much stick at the release, the launch goes along the tangent.")]
        [SerializeField, Range(0f, 1f)] float aimMinStick = 0.3f;

        [Tooltip("A FLICK launches out of the orbit: the stick swung from rest (under flickRestStick) to at least this " +
                 "within flickSeconds - a thumb swiped across a phone, or a stick snapped over. A slower push steers.")]
        [SerializeField, Range(0.3f, 1f)] float flickStick = 0.8f;

        [Tooltip("The stick counts as at rest under this.")]
        [SerializeField, Range(0f, 0.6f)] float flickRestStick = 0.3f;

        [Tooltip("...and a flick reaches flickStick within this many seconds of leaving rest.")]
        [SerializeField, Range(0.03f, 0.5f)] float flickSeconds = 0.15f;

        [Tooltip("MASS's one dial on the Stoat: how hard a launch kicks, × the timed kick. ×1 at rest → ×2.5 at level 10, " +
                 "read live on the simulating machine.")]
        [SerializeField] ElementalFloat launchPower = ElementalFloat.Multiplier(1f, 2.5f, Element.Mass, 1f);

        [Tooltip("MASS level 5, Armoured Wake: the prisms laid while a launch's kick lasts arrive SHIELDED. Gated on the " +
                 "replicated unlock bit; every peer runs it from the replicated release.")]
        [SerializeField] bool massUpgradeShieldsLaunchTrail = true;

        [Header("The aimed exit (Yash, 2026-11-03: \"slingshot at 30-45 degrees from angle of entry after a second of orbiting\")")]
        [Tooltip("On: the window is TIME in the orbit (exitWindowSeconds), and a release launches the hull on its ENTRY heading " +
                 "turned exitMinDegrees..exitMaxDegrees the way the orbit turns; the stick picks where (toward the turn the most, " +
                 "away the least, at rest the middle). Off: the window is degrees swept (sweetSpotDegrees) and the launch follows " +
                 "the orbit's tangent within launchConeDegrees.")]
        [SerializeField] bool aimedExit = true;

        [Tooltip("The least an aimed exit turns the entry heading, degrees (the stick held away from the turn).")]
        [SerializeField, Range(0f, 90f)] float exitMinDegrees = 30f;

        [Tooltip("The most an aimed exit turns the entry heading, degrees (the stick held toward the turn).")]
        [SerializeField, Range(0f, 90f)] float exitMaxDegrees = 45f;

        [Tooltip("Seconds of orbit at the centre of the aimed exit's window: the whole kick there.")]
        [SerializeField, Range(0.1f, 5f)] float exitWindowSeconds = 1.1f;

        [Tooltip("Seconds either side of exitWindowSeconds that still pay the WHOLE kick.")]
        [SerializeField, Range(0f, 2f)] float exitWindowHalfSeconds = 0.3f;

        [Tooltip("Seconds past the full window over which the kick eases (smoothstep) down to missKickShare.")]
        [SerializeField, Range(0.05f, 3f)] float exitWindowFalloffSeconds = 0.4f;

        [Header("Time mode - the TRAMPOLINE (Yash, 2026-11-04: replaces the orbit while on)")]
        [Tooltip("On: a press lays a lone WHITE HOLE ahead with a soft shell round it - no black hole, no orbit. Touch the shell " +
                 "and it gives like a trampoline and throws you off, the kick added. Where you touch it decides where you go: " +
                 "a graze turns you a little, deeper turns you more. RT bounces you right, LT left. Off: the orbit below.")]
        [SerializeField] bool trampoline = true;

        [Tooltip("How far ahead the white hole is laid, world units, along the line you are flying (your turn carried on).")]
        [SerializeField, Range(40f, 400f)] float trampolineAhead = 140f;

        [Tooltip("The soft shell's radius, world units, × Space's reach (orbitReach). 30 since 2026-11-07 (was 60: \"it moves " +
                 "the vessel too much\"): a smaller shell is relatively softer, so it turns you less.")]
        [SerializeField, Range(10f, 200f)] float trampolineRadius = 30f;

        [Tooltip("How far to the side, as a share of the radius, the shell is laid off your line (away from the bounce): flying " +
                 "on, you graze it there. On the 30 u shell 0.75 turns you about 17-30 degrees at 120-190 u/s; less turns you " +
                 "more (0 = head-on, straight back).")]
        [SerializeField, Range(0f, 0.95f)] float trampolineLine = 0.75f;

        [Tooltip("How soft the shell is: seconds a head-on bounce lasts (the spring is (pi / this)^2). More is softer and deeper.")]
        [SerializeField, Range(0.1f, 1.5f)] float trampolineSoftSeconds = 0.35f;

        [Tooltip("Seconds a trampoline nobody touches stays up.")]
        [SerializeField, Range(0.5f, 15f)] float trampolineSeconds = 4f;

        [Tooltip("The most of your current turn, degrees, carried on to where the white hole is laid (0 = straight ahead).")]
        [SerializeField, Range(0f, 180f)] float trampolinePredictDegrees = 90f;

        [Tooltip("The autopilot grazes as deep as the turn it wants: share off the centre = 1 - turn / this (stoat_orbit_sim.py " +
                 "--trampoline chose 100 for the 60 u shell, 55 for the 30 u one).")]
        [SerializeField, Range(40f, 300f)] float trampolineAiTurnScale = 55f;

        [Tooltip("Played when the hull touches the shell. Empty = silent.")]
        [SerializeField] EventReference trampolineTouchEvent;

        [Tooltip("Played when the shell throws the hull off (the bounce). Empty = silent.")]
        [SerializeField] EventReference trampolineBounceEvent;

        [Header("Space 5 - Accretion on the orbit (2026-11-02)")]
        [Tooltip("Each prism the held pair's attractor steals adds this share to the reach of the ripple that pair throws.")]
        [SerializeField, Range(0f, 0.2f)] float accretionPerPrism = 0.04f;
        [Tooltip("Accretion's ceiling on the ripple's reach bonus (0.4 = +40%).")]
        [SerializeField, Range(0f, 1f)] float accretionMax = 0.4f;

        [Header("Earned speed (Grok's review, 2026-11-02: holding a pair must not be the speed)")]
        [Tooltip("Before a catch, a held pair warps the flight to only this share of the catch's warp (catchWarpFromPress " +
                 "still gates it). The full warp is the orbit's, and after a good launch, its momentum.")]
        [SerializeField, Range(0f, 1f)] float pressWarpShare = 0.5f;

        [Tooltip("A release before the orbit has swept this many degrees pays NOTHING: no kick, no momentum.")]
        [SerializeField, Range(0f, 90f)] float minSweepDegrees = 30f;

        [Tooltip("A launch keeps the orbit's warp × its timing as MOMENTUM, fading to nothing over this many seconds. It " +
                 "never stacks with a held pair's warp: the flight runs at the larger of the two.")]
        [SerializeField, Range(0f, 10f)] float momentumSeconds = 6f;

        [Tooltip("The hard cap: a launch's kick never takes the hull past this × its speed at the release (the orbit's).")]
        [SerializeField, Range(1f, 3f)] float speedCapShare = 1.6f;

        [Tooltip("The kick runs on the warped flight clock: warp × kick is never past this × cruise (the engine's speed).")]
        [SerializeField, Range(1f, 6f)] float warpKickCapCruise = 3f;

        [Header("Time mode - the poles (LIGHTER than Mass mode's)")]
        [Tooltip("The attractor's (and repulsor's) horizon, world units. Mass mode's dipole poles are 2.1 u; Time mode's " +
                 "pull nothing it orbits - the catch flies the orbit - so they are light (the user, 2026-10-14).")]
        [SerializeField, Range(0.2f, 10f)] float poleHorizon = 1.6f;

        [Tooltip("The poles' gravitational parameter (what prisms feel). Mass mode's are 50 000; Time mode's are lighter.")]
        [SerializeField, Min(0f)] float poleGM = 20000f;

        [Header("Time mode - the rings (drawn for the pilot in place of the path)")]
        [Tooltip("Dots of each ring are laid this many world units apart.")]
        [SerializeField, Range(1f, 40f)] float ringDotSpacing = 5f;

        [Tooltip("Each ring dot's size, world units (never smaller on screen than the path dots' minimum).")]
        [SerializeField, Range(0.1f, 5f)] float ringDotSize = 0.9f;

        [Tooltip("The detection ring's opacity against the orbit ring's (faint: it is where it CAN catch you).")]
        [SerializeField, Range(0f, 1f)] float detectionRingAlpha = 0.35f;

        [Tooltip("The orbit ring's opacity before the catch (it brightens to full once you are on it).")]
        [SerializeField, Range(0f, 1f)] float orbitRingAlpha = 0.6f;

        [Header("Slingshot (on release - only once caught)")]
        [Tooltip("Boost on release at the lightest squeeze, as a fraction of the orbit speed.")]
        [SerializeField, Range(0f, 3f)] float slingBoostMin = 0.25f;

        [Tooltip("Boost on release at the deepest squeeze of the hold, as a fraction of the orbit speed.")]
        [SerializeField, Range(0f, 3f)] float slingBoostMax = 0.9f;

        [Tooltip("Seconds the boost takes to fade.")]
        [SerializeField, Range(0.1f, 5f)] float slingBoostSeconds = 1.5f;

        [Header("Timing (Time mode — the 90° slingshot)")]
        [Tooltip("The release a quarter-turn round the attractor pays the full kick: degrees of orbit swept at the centre of the " +
                 "window (the playtest's 90°, 2026-10-13 — out on a tangent square to the heading you came in on).")]
        [SerializeField, Range(10f, 360f)] float sweetSpotDegrees = 90f;

        [Tooltip("Degrees either side of the sweet spot that still pay the WHOLE kick.")]
        [SerializeField, Range(0f, 90f)] float sweetSpotHalfWidth = 15f;

        [Tooltip("Degrees past the full window over which the kick eases (smoothstep) down to the miss share.")]
        [SerializeField, Range(1f, 180f)] float sweetSpotFalloff = 30f;

        [Tooltip("Share of the kick a release OUTSIDE the window still gets (0 = the timing is everything).")]
        [SerializeField, Range(0f, 1f)] float missKickShare = 0.2f;

        [Tooltip("Played on every peer when a release lands inside the full window. Ships empty until a sound is chosen.")]
        [SerializeField] EventReference sweetSpotEvent;

        [Header("Pace (Time mode — the field's warp while the pair holds you)")]
        [Tooltip("The studio's dpWarp (2026-10-14: \"both mechanics similarly paced\"). While a Time-mode pair is held, the hull's " +
                 "flight clock warps exactly as the field trajectory's lime path does (StoatDipoleConfig boost, boostRise, " +
                 "boostFadeSeconds), to this share of that boost: 1 = the same warp, 0 = none. Measured in the studio's batch race " +
                 "at 1.15: catch-sling circuit 56 s best against the field's 60 s.")]
        [SerializeField, Range(0f, 1.5f)] float catchWarpShare = 1.15f;

        [Tooltip("The studio's dpWarpHeld: the warp starts as soon as the pair is laid and held (flying at the black hole is " +
                 "already fast, as any bend of the field's path is). Off = only once the attractor has caught the hull.")]
        [SerializeField] bool catchWarpFromPress = true;

        [Header("Life")]
        [Tooltip("How fast the two holes CLOSE on each other once let go (u/s); they annihilate where the horizons touch.")]
        [SerializeField, Range(0f, 200f)] float driftSpeed = 40f;

        [Tooltip("The longest a pair may be held, seconds; past it the pair lets go on its own and the hull slings.")]
        [SerializeField, Range(0.5f, 30f)] float lifetime = 12f;

        [Header("Crystal style (BlackHoleConfig.crystalPairs on — laid on release)")]
        [Tooltip("Strength of the slung crystal pair at the lightest press. Throat = strength × BlackHoleConfig.horizonPerStrength.")]
        [SerializeField] float minStrength = 2f;

        [Tooltip("Strength of the slung crystal pair at a fully buried trigger.")]
        [SerializeField] float maxStrength = 12f;

        [Tooltip("How far ahead of the hull the crystal pair's midpoint is laid, in throat radii.")]
        [SerializeField, Range(0f, 20f)] float aheadHorizons = 4f;

        [Tooltip("How far to either side each crystal pole sits, in throat radii. Space scales it. Floored so the two can never overlap.")]
        [SerializeField] ElementalFloat halfGapHorizons = ElementalFloat.Multiplier(6f, 9f, Element.Space, 1.5f);

        [Header("Autopilot sling")]
        [Tooltip("An autopilot slings when its target is at least this many degrees off the nose, to the side it wants to turn. 0 disables the AI sling.")]
        [SerializeField, Range(0f, 180f)] float aiSlingMinTurnDegrees = 30f;

        [Tooltip("Once caught, the autopilot lets go when its target is within this many degrees of the nose.")]
        [SerializeField, Range(1f, 90f)] float aiReleaseConeDegrees = 20f;

        [Tooltip("The autopilot lets go of a pair that has not caught it after this many seconds.")]
        [SerializeField, Range(0.5f, 10f)] float aiCatchGiveUpSeconds = 4f;

        [Tooltip("Seconds between an autopilot's slings — one pair at a time, so roughly its lifetime.")]
        [SerializeField, Range(0.5f, 20f)] float aiSlingIntervalSeconds = 3f;

        [Tooltip("An autopilot does not sling at a target closer than this (world units) — a turn that short is the stick's.")]
        [SerializeField, Min(0f)] float aiSlingMinDistance = 120f;

        [Tooltip("An autopilot also presses for the WARP alone, its target dead ahead, while its ring is at least this far " +
                 "away: on the orbit-only Stoat a held pair is the hull's only speed (Tools/Build/stoat_orbit_sim.py, " +
                 "2026-11-02). 0 = it presses only to turn (shipped since speed is earned by the orbit).")]
        [SerializeField, Min(0f)] float aiWarpMinDistance = 0f;

        [Tooltip("Once caught, the autopilot HOLDS for the sweet spot and launches there whenever its aim (launchConeDegrees) " +
                 "reaches its target, letting go anyway at this many degrees of orbit. Speed is earned by timed launches since " +
                 "2026-11-02 (Tools/Build/stoat_orbit_sim.py --grid-earned). 0 = the old rule: let go once the target is near the nose.")]
        [SerializeField, Range(0f, 360f)] float aiHoldForWindowDegrees = 120f;

        [Header("Audio (FMOD) — slots shipped EMPTY, wire in the inspector")]
        [Tooltip("Played once when a squeeze begins (the wind-up). Empty = silent.")]
        [SerializeField] EventReference holdStartEvent;

        [Tooltip("Played once when the pair is slung. Empty = silent.")]
        [SerializeField] EventReference slingEvent;

        public StoatSkimConfigSO Skim => skim;
        public float MinStrength => Mathf.Max(0.01f, Mathf.Min(minStrength, maxStrength));
        public float MaxStrength => Mathf.Max(MinStrength, maxStrength);
        public float HoldExponent => Mathf.Clamp(holdExponent, 0.5f, 4f);
        public float HoldRampSeconds => Mathf.Clamp(holdRampSeconds, 0.1f, 5f);
        public float AutopilotHold01 => Mathf.Clamp01(autopilotHold01);
        public float OrbitRadiusWide => Mathf.Max(1f, orbitRadiusWide);
        public float OrbitRadiusTight => Mathf.Clamp(orbitRadiusTight, 1f, OrbitRadiusWide);
        public ElementalFloat OrbitReach => orbitReach;
        public float CatchAhead => Mathf.Clamp(catchAhead, 20f, 400f);
        public float CatchSide => Mathf.Clamp(catchSide, 0f, 200f);
        public float CatchReach => Mathf.Clamp(catchReach, 1f, 3f);
        public float CatchDegrees => Mathf.Clamp(catchDegrees, 0f, 90f);
        public float SettleRate => Mathf.Clamp(settleRate, 0.1f, 20f);
        public float MaxOrbitDegrees => Mathf.Clamp(maxOrbitDegrees, 90f, 1080f);
        public float PoleHorizon => Mathf.Clamp(poleHorizon, 0.2f, 10f);
        public float PoleGM => Mathf.Max(0f, poleGM);
        public float RingDotSpacing => Mathf.Clamp(ringDotSpacing, 1f, 40f);
        public float RingDotSize => Mathf.Clamp(ringDotSize, 0.1f, 5f);
        public float DetectionRingAlpha => Mathf.Clamp01(detectionRingAlpha);
        public float OrbitRingAlpha => Mathf.Clamp01(orbitRingAlpha);
        public float AiReleaseConeDegrees => Mathf.Clamp(aiReleaseConeDegrees, 1f, 90f);
        public float AiCatchGiveUpSeconds => Mathf.Clamp(aiCatchGiveUpSeconds, 0.5f, 10f);
        public float RadiusFollowRate => Mathf.Clamp(radiusFollowRate, 0.1f, 20f);
        public float RadialCorrectionRate => Mathf.Clamp(radialCorrectionRate, 0.1f, 20f);
        public bool HoldAtCatch => holdAtCatch;
        public float OrbitEaseRate => Mathf.Clamp(orbitEaseRate, 0f, 5f);
        public float WhiteClear => Mathf.Clamp(whiteClear, 0f, 1.5f);
        public float SlingBoostMin => Mathf.Clamp(slingBoostMin, 0f, 3f);
        public float SlingBoostMax => Mathf.Clamp(slingBoostMax, 0f, 3f);
        public float SlingBoostSeconds => Mathf.Clamp(slingBoostSeconds, 0.1f, 5f);
        public float AheadHorizons => Mathf.Clamp(aheadHorizons, 0f, 20f);
        public ElementalFloat HalfGapHorizons => halfGapHorizons;
        public float DriftSpeed => Mathf.Clamp(driftSpeed, 0f, 200f);
        public float Lifetime => Mathf.Clamp(lifetime, 0.5f, 30f);
        public float AiSlingMinTurnDegrees => Mathf.Clamp(aiSlingMinTurnDegrees, 0f, 180f);
        public float AiSlingIntervalSeconds => Mathf.Clamp(aiSlingIntervalSeconds, 0.5f, 20f);
        public float AiSlingMinDistance => Mathf.Max(0f, aiSlingMinDistance);
        public float AiWarpMinDistance => Mathf.Max(0f, aiWarpMinDistance);
        public float AiHoldForWindowDegrees => Mathf.Clamp(aiHoldForWindowDegrees, 0f, 360f);
        public EventReference HoldStartEvent => holdStartEvent;
        public EventReference SlingEvent => slingEvent;
        public float SweetSpotDegrees => Mathf.Clamp(sweetSpotDegrees, 10f, 360f);
        public float SweetSpotHalfWidth => Mathf.Clamp(sweetSpotHalfWidth, 0f, 90f);
        public float SweetSpotFalloff => Mathf.Clamp(sweetSpotFalloff, 1f, 180f);
        public float MissKickShare => Mathf.Clamp01(missKickShare);
        public EventReference SweetSpotEvent => sweetSpotEvent;
        public float InnerOrbitShare => Mathf.Clamp(innerOrbitShare, 0.2f, 1f);
        public float InnerPullRate => Mathf.Clamp(innerPullRate, 0f, 10f);
        public float OrbitSpeedBonus => Mathf.Clamp01(orbitSpeedBonus);
        public float LaunchConeDegrees => Mathf.Clamp(launchConeDegrees, 0f, 90f);
        public bool AimedExit => aimedExit;
        public float ExitMinDegrees => Mathf.Clamp(Mathf.Min(exitMinDegrees, exitMaxDegrees), 0f, 90f);
        public float ExitMaxDegrees => Mathf.Clamp(Mathf.Max(exitMinDegrees, exitMaxDegrees), 0f, 90f);
        public float ExitWindowSeconds => Mathf.Clamp(exitWindowSeconds, 0.1f, 5f);
        public float ExitWindowHalfSeconds => Mathf.Clamp(exitWindowHalfSeconds, 0f, 2f);
        public float ExitWindowFalloffSeconds => Mathf.Clamp(exitWindowFalloffSeconds, 0.05f, 3f);
        /// <summary>The last second of the aimed exit's full window: the AI lets go by then.</summary>
        public float ExitWindowEndSeconds => ExitWindowSeconds + ExitWindowHalfSeconds;
        public bool Trampoline => trampoline;
        public float TrampolineAhead => Mathf.Clamp(trampolineAhead, 40f, 400f);
        public float TrampolineRadius => Mathf.Clamp(trampolineRadius, 10f, 200f);
        public float TrampolineLine => Mathf.Clamp(trampolineLine, 0f, 0.95f);
        public float TrampolineSoftSeconds => Mathf.Clamp(trampolineSoftSeconds, 0.1f, 1.5f);
        public float TrampolineSeconds => Mathf.Clamp(trampolineSeconds, 0.5f, 15f);
        public float TrampolinePredictDegrees => Mathf.Clamp(trampolinePredictDegrees, 0f, 180f);
        public float TrampolineAiTurnScale => Mathf.Clamp(trampolineAiTurnScale, 40f, 300f);
        public EventReference TrampolineTouchEvent => trampolineTouchEvent;
        public EventReference TrampolineBounceEvent => trampolineBounceEvent;
        public float AimMinStick => Mathf.Clamp01(aimMinStick);
        public float FlickStick => Mathf.Clamp(flickStick, 0.3f, 1f);
        public float FlickRestStick => Mathf.Clamp(flickRestStick, 0f, 0.6f);
        public float FlickSeconds => Mathf.Clamp(flickSeconds, 0.03f, 0.5f);
        public ElementalFloat LaunchPower => launchPower;
        public float PressWarpShare => Mathf.Clamp01(pressWarpShare);
        public float MinSweepDegrees => Mathf.Clamp(minSweepDegrees, 0f, 90f);
        public float MomentumSeconds => Mathf.Clamp(momentumSeconds, 0f, 10f);
        public float AccretionPerPrism => Mathf.Clamp(accretionPerPrism, 0f, 0.2f);
        public float AccretionMax => Mathf.Clamp01(accretionMax);
        public float WarpKickCapCruise => Mathf.Clamp(warpKickCapCruise, 1f, 6f);
        public float SpeedCapShare => Mathf.Clamp(speedCapShare, 1f, 3f);

        /// <summary>Whether a launch's trail is laid shielded right now (Mass 5, the replicated unlock bit).</summary>
        public bool ShieldsLaunchTrail(IVesselStatus status)
        {
            if (!massUpgradeShieldsLaunchTrail) return false;
            var abilities = status?.ElementalAbilityHandler;
            return abilities && abilities.IsUpgradeActive(Element.Mass);
        }
        public float CatchWarpShare => Mathf.Clamp(catchWarpShare, 0f, 1.5f);
        public bool CatchWarpFromPress => catchWarpFromPress;

        /// <summary>The catch's warp target for the field's <paramref name="fieldBoost"/>: 1 + (boost − 1) × the share.</summary>
        public float CatchWarpTarget(float fieldBoost) => 1f + (Mathf.Max(1f, fieldBoost) - 1f) * CatchWarpShare;
        /// <summary>How well a release after <paramref name="sweptDegrees"/> of orbit is timed, 0..1.</summary>
        public float SweetSpot(float sweptDegrees) =>
            StoatSlingMath.SweetSpot(sweptDegrees, SweetSpotDegrees, SweetSpotHalfWidth, SweetSpotFalloff);

        /// <summary>The aimed exit's timing after this many seconds of orbit, 0..1: the same curve as the sweet spot, in time.</summary>
        public float ExitTiming(float orbitSeconds) =>
            StoatSlingMath.SweetSpot(orbitSeconds, ExitWindowSeconds, ExitWindowHalfSeconds, ExitWindowFalloffSeconds);
    }
}

using CosmicShore.Data;
using CosmicShore.Gameplay;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The Stoat's SLINGSHOT, the SWING (<c>R_VesselActions/STOAT.md</c>, <c>STOAT_DIPOLE.md</c> §1b): every number behind a
    /// trigger that lays a lone, small black hole ahead on the pressed side, which swings the hull round it by the angle the
    /// stick asks and lets it go on that heading. The hole itself is the black hole's (<c>Docs/BLACK_HOLE.md</c>); this file
    /// only decides where, how big, how long and how it turns you.
    ///
    /// <para><b>Retired 2026-11-21</b> (Yash): the catch orbit and its aimed exit, the skim sling, the trampoline and the
    /// crystal style, and every row they read. The swing is the only Time-mode slingshot, for every pilot.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "StoatSlingConfig", menuName = "ScriptableObjects/Vessel Actions/Stoat Sling Config")]
    public sealed class StoatSlingConfigSO : ScriptableObject
    {
        [Header("Squeeze")]
        [Tooltip("Keyboard / mouse / touch have no analog trigger: seconds of hold that count as a full squeeze.")]
        [SerializeField, Range(0.1f, 5f)] float holdRampSeconds = 3f;

        [Tooltip("The autopilot writes no trigger; its every swing is this fraction of a full squeeze.")]
        [SerializeField, Range(0f, 1f)] float autopilotHold01 = 0.5f;

        [Header("The SWING (Yash, 2026-11-11: a lone black hole, the angle from the stick)")]
        [Tooltip("How far ahead of the hull the black hole is laid, world units.")]
        [SerializeField, Range(40f, 400f)] float swingAhead = 150f;

        [Tooltip("How far to the pressed side of your line the black hole is laid, world units: flying on, you pass it this close.")]
        [SerializeField, Range(5f, 150f)] float swingSide = 35f;

        [Tooltip("The black hole's horizon, world units: its size on screen.")]
        [SerializeField, Range(0.2f, 10f)] float swingHorizon = 0.8f;

        [Tooltip("The swing starts once you are this close to the hole, world units (× Space's reach)...")]
        [SerializeField, Range(10f, 300f)] float swingReach = 70f;

        [Tooltip("...and the hole is at least this far off your nose, degrees (you are passing beside it, not flying into it).")]
        [SerializeField, Range(0f, 90f)] float swingCatchDegrees = 40f;

        [Tooltip("The least a swing turns you, degrees: the stick held away from the hole.")]
        [SerializeField, Range(0f, 180f)] float swingMinDegrees = 15f;

        [Tooltip("The most a swing turns you, degrees: the stick held toward the hole. At rest, the middle.")]
        [SerializeField, Range(0f, 180f)] float swingMaxDegrees = 90f;

        [Tooltip("The fastest a swing turns you, degrees a second (it turns at your speed over your distance from the hole, up " +
                 "to this): slower is easier to aim with the stick.")]
        [SerializeField, Range(30f, 720f)] float swingDegreesPerSecond = 200f;

        [Tooltip("Seconds a black hole nobody swings round stays up.")]
        [SerializeField, Range(0.5f, 15f)] float swingSeconds = 4f;

        [Tooltip("The share of a second push paid when a swing lets you go (the hold's squeeze × Mass, under the caps). 0 " +
                 "(Yash, 2026-11-19: \"I want only one slingshot, the first one\"): the swing's own speed-up is the one slingshot, " +
                 "kept as momentum, and you leave on the dotted line's heading with no second push to overshoot your target.")]
        [SerializeField, Range(0f, 1f)] float swingKickShare = 0f;

        [Tooltip("The autopilot lets a swing throw it once it faces its target within this many degrees (after swingMinDegrees), " +
                 "or at swingMaxDegrees: its stick picks the angle toward its target.")]
        [SerializeField, Range(1f, 45f)] float aiSwingFaceDegrees = 8f;

        [Tooltip("Space is reach: a multiplier on swingReach - more Space, the swing starts from further out.")]
        [SerializeField] ElementalFloat orbitReach = ElementalFloat.Multiplier(1f, 1.4f, Element.Space, 0.5f);

        [Tooltip("The hole's gravitational parameter (what prisms feel).")]
        [SerializeField, Min(0f)] float poleGM = 20000f;

        [Tooltip("The Charge ripple a collapsing hole throws reads this separation, world units (StoatRippleConfig's reach).")]
        [SerializeField, Min(0f)] float rippleSeparation = 90f;

        [Tooltip("Played when a swing starts. Empty = silent.")]
        [SerializeField] EventReference swingStartEvent;

        [Tooltip("Played when a swing lets you go. Empty = silent.")]
        [SerializeField] EventReference swingFlingEvent;

        [Header("The (optional) second push - swingKickShare 0 ships; Mass is its power")]
        [Tooltip("MASS's dial on the Stoat: how hard a fling's second push kicks. ×1 at rest → ×2.5 at level 10, read live on " +
                 "the simulating machine.")]
        [SerializeField] ElementalFloat launchPower = ElementalFloat.Multiplier(1f, 2.5f, Element.Mass, 0.5f);

        [Tooltip("MASS level 5, Armoured Wake: the prisms laid for slingBoostSeconds after a swing arrive SHIELDED. Gated on the " +
                 "replicated unlock bit; every peer runs it from the replicated release.")]
        [SerializeField] bool massUpgradeShieldsLaunchTrail = true;

        [Tooltip("The push at the lightest squeeze, as a fraction of the engine's speed.")]
        [SerializeField, Range(0f, 3f)] float slingBoostMin = 0.25f;

        [Tooltip("The push at the deepest squeeze of the hold, as a fraction of the engine's speed.")]
        [SerializeField, Range(0f, 3f)] float slingBoostMax = 0.9f;

        [Tooltip("Seconds the push takes to fade - and how long Armoured Wake shields the trail after a swing.")]
        [SerializeField, Range(0.1f, 5f)] float slingBoostSeconds = 1.5f;

        [Tooltip("The hard cap: a push never takes the hull past this × its speed at the fling.")]
        [SerializeField, Range(1f, 3f)] float speedCapShare = 1.6f;

        [Tooltip("The push runs on the warped flight clock: warp × push is never past this × cruise (the engine's speed).")]
        [SerializeField, Range(1f, 6f)] float warpKickCapCruise = 3f;

        [Header("Space 5 - Accretion on the swing")]
        [Tooltip("Each prism the hole steals adds this share to the reach of the ripple it throws.")]
        [SerializeField, Range(0f, 0.2f)] float accretionPerPrism = 0.04f;
        [Tooltip("Accretion's ceiling on the ripple's reach bonus (0.4 = +40%).")]
        [SerializeField, Range(0f, 1f)] float accretionMax = 0.4f;

        [Header("Pace (the flight clock's warp while the hole is up; StoatPathfinderExecutor writes it)")]
        [Tooltip("While the hole is up the hull's flight clock warps toward this share of StoatDipoleConfig's boost: 1 = the same " +
                 "warp, 0 = none.")]
        [SerializeField, Range(0f, 1.5f)] float catchWarpShare = 1.15f;

        [Tooltip("The warp starts as soon as the hole is laid (at pressWarpShare of it). Off = only once it swings you.")]
        [SerializeField] bool catchWarpFromPress = true;

        [Tooltip("Before the swing starts, a laid hole warps the flight to only this share of the swing's warp.")]
        [SerializeField, Range(0f, 1f)] float pressWarpShare = 0.5f;

        [Tooltip("While the swing turns the hull, it flies this much faster than the swing's warp alone (0.15 = 15% more).")]
        [SerializeField, Range(0f, 1f)] float orbitSpeedBonus = 0.15f;

        [Tooltip("A swing keeps its warp as MOMENTUM, fading to nothing over this many seconds. It never stacks with a held " +
                 "hole's warp: the flight runs at the larger of the two.")]
        [SerializeField, Range(0f, 10f)] float momentumSeconds = 6f;

        [Header("The drawn reach ring and exit line (the local human pilot only)")]
        [Tooltip("Dots of each ring and line are laid this many world units apart.")]
        [SerializeField, Range(1f, 40f)] float ringDotSpacing = 5f;

        [Tooltip("Each dot's size, world units (never smaller on screen than the dipole config's minimum).")]
        [SerializeField, Range(0.1f, 5f)] float ringDotSize = 0.9f;

        [Tooltip("The reach ring's opacity before the swing starts (faint: it is where it CAN swing you).")]
        [SerializeField, Range(0f, 1f)] float detectionRingAlpha = 0.35f;

        [Tooltip("The reach ring's opacity while it swings you, and the exit line's before it does.")]
        [SerializeField, Range(0f, 1f)] float orbitRingAlpha = 0.6f;

        [Header("Autopilot swing")]
        [Tooltip("An autopilot presses when its target is at least this many degrees off the nose, to the side it wants to " +
                 "turn. 0 disables the AI swing.")]
        [SerializeField, Range(0f, 180f)] float aiSlingMinTurnDegrees = 30f;

        [Tooltip("Seconds between an autopilot's presses — one hole at a time.")]
        [SerializeField, Range(0.5f, 20f)] float aiSlingIntervalSeconds = 3f;

        [Tooltip("An autopilot does not press for a turn at a target closer than this (world units) — a turn that short is the stick's.")]
        [SerializeField, Min(0f)] float aiSlingMinDistance = 120f;

        [Tooltip("An autopilot also presses for the WARP alone, its target at any bearing, while it is at least this far away " +
                 "(0 = it presses only to turn).")]
        [SerializeField, Min(0f)] float aiWarpMinDistance = 0f;

        [Header("Audio (FMOD) — slots shipped EMPTY, wire in the inspector")]
        [Tooltip("Played once when a squeeze begins (the wind-up). Empty = silent.")]
        [SerializeField] EventReference holdStartEvent;

        public float HoldRampSeconds => Mathf.Clamp(holdRampSeconds, 0.1f, 5f);
        public float AutopilotHold01 => Mathf.Clamp01(autopilotHold01);
        public float SwingAhead => Mathf.Clamp(swingAhead, 40f, 400f);
        public float SwingSide => Mathf.Clamp(swingSide, 5f, 150f);
        public float SwingHorizon => Mathf.Clamp(swingHorizon, 0.2f, 10f);
        public float SwingReach => Mathf.Clamp(swingReach, 10f, 300f);
        public float SwingCatchDegrees => Mathf.Clamp(swingCatchDegrees, 0f, 90f);
        public float SwingMinDegrees => Mathf.Clamp(Mathf.Min(swingMinDegrees, swingMaxDegrees), 0f, 180f);
        public float SwingMaxDegrees => Mathf.Clamp(Mathf.Max(swingMinDegrees, swingMaxDegrees), 0f, 180f);
        public float SwingDegreesPerSecond => Mathf.Clamp(swingDegreesPerSecond, 30f, 720f);
        public float SwingSeconds => Mathf.Clamp(swingSeconds, 0.5f, 15f);
        public float SwingKickShare => Mathf.Clamp01(swingKickShare);
        public float AiSwingFaceDegrees => Mathf.Clamp(aiSwingFaceDegrees, 1f, 45f);
        public ElementalFloat OrbitReach => orbitReach;
        public float PoleGM => Mathf.Max(0f, poleGM);
        public float RippleSeparation => Mathf.Max(0f, rippleSeparation);
        public EventReference SwingStartEvent => swingStartEvent;
        public EventReference SwingFlingEvent => swingFlingEvent;
        public ElementalFloat LaunchPower => launchPower;
        public float SlingBoostMin => Mathf.Clamp(slingBoostMin, 0f, 3f);
        public float SlingBoostMax => Mathf.Clamp(slingBoostMax, 0f, 3f);
        public float SlingBoostSeconds => Mathf.Clamp(slingBoostSeconds, 0.1f, 5f);
        public float SpeedCapShare => Mathf.Clamp(speedCapShare, 1f, 3f);
        public float WarpKickCapCruise => Mathf.Clamp(warpKickCapCruise, 1f, 6f);
        public float AccretionPerPrism => Mathf.Clamp(accretionPerPrism, 0f, 0.2f);
        public float AccretionMax => Mathf.Clamp01(accretionMax);
        public float CatchWarpShare => Mathf.Clamp(catchWarpShare, 0f, 1.5f);
        public bool CatchWarpFromPress => catchWarpFromPress;
        public float PressWarpShare => Mathf.Clamp01(pressWarpShare);
        public float OrbitSpeedBonus => Mathf.Clamp01(orbitSpeedBonus);
        public float MomentumSeconds => Mathf.Clamp(momentumSeconds, 0f, 10f);
        public float RingDotSpacing => Mathf.Clamp(ringDotSpacing, 1f, 40f);
        public float RingDotSize => Mathf.Clamp(ringDotSize, 0.1f, 5f);
        public float DetectionRingAlpha => Mathf.Clamp01(detectionRingAlpha);
        public float OrbitRingAlpha => Mathf.Clamp01(orbitRingAlpha);
        public float AiSlingMinTurnDegrees => Mathf.Clamp(aiSlingMinTurnDegrees, 0f, 180f);
        public float AiSlingIntervalSeconds => Mathf.Clamp(aiSlingIntervalSeconds, 0.5f, 20f);
        public float AiSlingMinDistance => Mathf.Max(0f, aiSlingMinDistance);
        public float AiWarpMinDistance => Mathf.Max(0f, aiWarpMinDistance);
        public EventReference HoldStartEvent => holdStartEvent;

        /// <summary>Whether a swing's trail is laid shielded right now (Mass 5, the replicated unlock bit).</summary>
        public bool ShieldsLaunchTrail(IVesselStatus status)
        {
            if (!massUpgradeShieldsLaunchTrail) return false;
            var abilities = status?.ElementalAbilityHandler;
            return abilities && abilities.IsUpgradeActive(Element.Mass);
        }

        /// <summary>The swing's warp target for the field's <paramref name="fieldBoost"/>: 1 + (boost − 1) × the share.</summary>
        public float CatchWarpTarget(float fieldBoost) => 1f + (Mathf.Max(1f, fieldBoost) - 1f) * CatchWarpShare;
    }
}

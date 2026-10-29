using CosmicShore.Data;
using CosmicShore.Gameplay;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Every number of the Stoat's SKIM SLING (<c>R_VesselActions/STOAT_DIPOLE.md</c> §1q; the design plan's tuning table).
    /// It REPLACES Time mode's catch (Yash, 2026-10-29): <see cref="StoatSlingConfigSO.Skim"/> points here, and while it does
    /// the Time-mode triggers fly the skim - PRESS lays a pair <see cref="LayAhead"/> ahead and <see cref="LaySide"/> to the
    /// pressed side, its axis along the hull's up; fly past and the TIDEPOOL (<see cref="TidepoolScale"/> × R) captures you
    /// onto the SKIMSHELL (radius <see cref="SkimRadius"/>); the squeeze SPINS it (the trigger's side sets the way); RELEASE
    /// launches you along the surface, the gain timed by the angle ridden, and the spin HOOKS the launch. A steep entry
    /// PLUNKS (keeps only <see cref="PlunkCarry"/> of its speed).
    ///
    /// <para>Rows marked "(next)" are carried for the coming slices (graze skips, the perfect-skip window, rival riding)
    /// and read by nothing yet - they are here so the whole table tunes in one place.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "StoatSkimConfig", menuName = "ScriptableObjects/Vessel Actions/Stoat Skim Config")]
    public sealed class StoatSkimConfigSO : ScriptableObject
    {
        [Header("Placement (press)")]
        [Tooltip("How far AHEAD of the hull the pair's midpoint is laid, world units (100-250).")]
        [SerializeField, Range(20f, 400f)] float layAhead = 160f;

        [Tooltip("How far toward the pressed side the midpoint is laid, world units (0-80). Flying straight, about how close " +
                 "you pass its centre - under the Skimshell radius you pass through the Tidepool into it.")]
        [SerializeField, Range(0f, 200f)] float laySide = 40f;

        [Header("The Skimshell and the Tidepool")]
        [Tooltip("The Skimshell's radius R, world units (30-120): the sphere round the midpoint the hull rides.")]
        [SerializeField, Range(10f, 300f)] float skimRadius = 60f;

        [Tooltip("Space is reach: a multiplier on R (and so the Tidepool) - more Space, a bigger shell (1 -> 1.4).")]
        [SerializeField] ElementalFloat skimReach = ElementalFloat.Multiplier(1f, 1.4f, Element.Space, 1f);

        [Tooltip("The Tidepool's radius as a multiple of R (1.3-2.2): inside it, as you pass, you are captured.")]
        [SerializeField, Range(1f, 4f)] float tidepoolScale = 1.6f;

        [Tooltip("The Tidepool captures you only once the centre is at least this far off the nose: AS you pass, not while " +
                 "it is still dead ahead (inside the Skimshell it always does).")]
        [SerializeField, Range(0f, 90f)] float catchDegrees = 60f;

        [Tooltip("Each pole's distance from the midpoint along the spin axis, as a share of R (0.2-0.5).")]
        [SerializeField, Range(0.05f, 0.95f)] float poleHalfGap = 0.35f;

        [Tooltip("The poles' horizon, world units (light: the shell is flown, not fallen into).")]
        [SerializeField, Range(0.2f, 10f)] float poleHorizon = 1.6f;

        [Tooltip("The poles' gravitational parameter (what prisms feel; their spin drags prisms round).")]
        [SerializeField, Min(0f)] float poleGM = 20000f;

        [Header("Capture")]
        [Tooltip("How hard a captured hull is pinned to the Skimshell's radius, per second (3-10).")]
        [SerializeField, Range(0.5f, 30f)] float captureRate = 5f;

        [Tooltip("How fast the nose eases onto the surface once captured, per second (2-8).")]
        [SerializeField, Range(0.1f, 20f)] float settleRate = 3f;

        [Tooltip("The PLUNK: the share of speed a straight-in entry keeps (0.4-0.9). A flat entry keeps all of it: " +
                 "carry = lerp(plunkCarry, 1, cos^2 incidence).")]
        [SerializeField, Range(0f, 1f)] float plunkCarry = 0.6f;

        [Tooltip("An entry keeping less than this share is called a plunk (its sound plays).")]
        [SerializeField, Range(0f, 1f)] float plunkCallout = 0.85f;

        [Header("Spin (the squeeze is the rate, the trigger's side the way)")]
        [Tooltip("The Skimshell's surface speed at a full squeeze, u/s (20-80).")]
        [SerializeField, Range(0f, 200f)] float spinSpeedMax = 40f;

        [Tooltip("Squeeze curve: spin = squeeze^exponent (1-2).")]
        [SerializeField, Range(0.5f, 4f)] float spinExponent = 1.5f;

        [Tooltip("How fast the ride's speed eases toward the spin's, per second (0.3-2).")]
        [SerializeField, Range(0f, 10f)] float spinGrip = 0.8f;

        [Tooltip("How fast the ride's track is drawn onto the Skimline (the spin's flow), per second (0-1).")]
        [SerializeField, Range(0f, 5f)] float precess = 0.3f;

        [Header("Launch (release)")]
        [Tooltip("Launch gain G at the lightest squeeze: v_out = v_ride x (1 + G x timing).")]
        [SerializeField, Range(0f, 3f)] float launchGainMin = 0.25f;

        [Tooltip("Launch gain G at the deepest squeeze of the hold.")]
        [SerializeField, Range(0f, 3f)] float launchGainMax = 0.9f;

        [Tooltip("The release that pays the whole gain: degrees ridden at the window's centre.")]
        [SerializeField, Range(10f, 360f)] float sweetSpotDegrees = 90f;

        [Tooltip("Degrees either side of the sweet spot that still pay the whole gain.")]
        [SerializeField, Range(0f, 90f)] float sweetSpotHalfWidth = 15f;

        [Tooltip("Degrees past the window over which the gain eases to the miss share.")]
        [SerializeField, Range(1f, 180f)] float sweetSpotFalloff = 30f;

        [Tooltip("Share of the gain a release outside the window still gets.")]
        [SerializeField, Range(0f, 1f)] float missShare = 0.3f;

        [Tooltip("Seconds the launch kick takes to fade.")]
        [SerializeField, Range(0.1f, 5f)] float kickSeconds = 1.5f;

        [Tooltip("The launch speed never passes this many cruise speeds (2-4).")]
        [SerializeField, Range(1f, 6f)] float capCruises = 3f;

        [Tooltip("The HOOK (Magnus): the launch turns at curve x the spin's angular rate (surface speed / R), the way the ride " +
                 "was turning, fading over curveSeconds. 1 at a full squeeze on the defaults is ~15 degrees of hook.")]
        [SerializeField, Range(0f, 3f)] float curve = 1f;

        [Tooltip("Seconds the hook takes to fade (0.3-1.5).")]
        [SerializeField, Range(0.05f, 5f)] float curveSeconds = 0.8f;

        [Header("Limits")]
        [Tooltip("The longest ride, degrees round the shell: past it you are launched on your own.")]
        [SerializeField, Range(90f, 1080f)] float maxRideDegrees = 300f;

        [Tooltip("The longest a pair may be held, seconds: past it the pair lets go and the hull launches.")]
        [SerializeField, Range(0.5f, 30f)] float holdMax = 6f;

        [Tooltip("How fast the two poles close on each other once let go (u/s); they annihilate where they meet.")]
        [SerializeField, Range(0f, 200f)] float driftSpeed = 40f;

        [Header("Skips (chaining)")]
        [Tooltip("A press within this many seconds of the last launch CHAINS: the next pair is laid on the predicted launch " +
                 "line, and its capture is the next SKIP of the run. A press after it starts a fresh run.")]
        [SerializeField, Range(0f, 5f)] float skipWindow = 1.2f;

        [Tooltip("A chained press within this many seconds of the launch is a PERFECT skip (pays perfectBonus more gain).")]
        [SerializeField, Range(0f, 2f)] float perfectWindow = 0.4f;

        [Tooltip("A perfect skip's extra share of its gain (0.1 = +10%).")]
        [SerializeField, Range(0f, 1f)] float perfectBonus = 0.1f;

        [Tooltip("Each skip of a run pays decay^skip of the gain.")]
        [SerializeField, Range(0f, 1f)] float skipDecay = 0.8f;

        [Tooltip("The longest run, skips.")]
        [SerializeField, Range(0, 20)] int maxSkips = 5;

        [Tooltip("A chained skip entered this badly (carry below this) PLUNKS: the run breaks and the next press starts afresh.")]
        [SerializeField, Range(0f, 1f)] float chainBreakCarry = 0.7f;

        [Tooltip("How many steps the launch line is predicted in (the spin hook integrated along it).")]
        [SerializeField, Range(1, 64)] int predictSteps = 16;

        [Tooltip("With no trigger held, crossing your own live Skimshell this flat or flatter, degrees, GRAZES: a free skip " +
                 "bounce. Steeper passes through. Also the flatness a " +
                 "rival needs to opt in to riding (ELEMENTAL_ECONOMY §9b).")]
        [SerializeField, Range(0f, 90f)] float grazeAngle = 20f;

        [Tooltip("A graze's restitution: the share of the normal speed it reflects.")]
        [SerializeField, Range(0f, 1f)] float grazeRestitution = 0.9f;

        [Tooltip("The gain a graze adds, a share of the speed (still under the cap).")]
        [SerializeField, Range(0f, 1f)] float grazeGain = 0.08f;

        [Header("Rivals (ELEMENTAL_ECONOMY §9b - flagged for Garrett)")]
        [Tooltip("(next) Rivals may opt in to riding this Skimshell (flat entry + holding their own input). Off: no rival is " +
                 "ever touched. Never on: nothing moves a rival against its will.")]
        [SerializeField] bool rivalRiding = false;

        [Header("Skip juice (cheap, mobile-friendly)")]
        [Tooltip("A skip's world ripple ring lives this long, seconds.")]
        [SerializeField, Range(0.1f, 3f)] float skipRippleSeconds = 0.6f;

        [Tooltip("How fast the skip ripple ring grows, u/s.")]
        [SerializeField, Range(0f, 600f)] float skipRippleGrowth = 120f;

        [Tooltip("The SKIP xN counter shows this long after a skip, seconds.")]
        [SerializeField, Range(0.2f, 5f)] float skipCounterSeconds = 1.5f;

        [Tooltip("The skip haptic tick's strength (the shared Skim reward pulse), 0..1. Local human pilot only.")]
        [SerializeField, Range(0f, 1f)] float skipHaptic = 0.6f;

        [Header("World dots (no screen-space warp)")]
        [Tooltip("Dots of each ring are laid this many world units apart.")]
        [SerializeField, Range(1f, 40f)] float ringDotSpacing = 5f;

        [Tooltip("Each ring dot's size, world units.")]
        [SerializeField, Range(0.1f, 5f)] float ringDotSize = 0.9f;

        [Tooltip("The Tidepool ring's opacity (faint: where it CAN capture you).")]
        [SerializeField, Range(0f, 1f)] float tidepoolAlpha = 0.3f;

        [Tooltip("The Skimline's opacity before capture (full once you ride).")]
        [SerializeField, Range(0f, 1f)] float skimlineAlpha = 0.6f;

        [Header("Audio (FMOD) - shipped EMPTY")]
        [SerializeField] EventReference captureEvent;
        [SerializeField] EventReference plunkEvent;
        [SerializeField] EventReference launchEvent;
        [SerializeField] EventReference sweetSpotEvent;
        [SerializeField] EventReference skipEvent;
        [SerializeField] EventReference grazeEvent;
        [SerializeField] EventReference chainBreakEvent;

        public float LayAhead => Mathf.Clamp(layAhead, 20f, 400f);
        public float LaySide => Mathf.Clamp(laySide, 0f, 200f);
        public float SkimRadius => Mathf.Clamp(skimRadius, 10f, 300f);
        public ElementalFloat SkimReach => skimReach;
        public float TidepoolScale => Mathf.Clamp(tidepoolScale, 1f, 4f);
        public float CatchDegrees => Mathf.Clamp(catchDegrees, 0f, 90f);
        public float PoleHalfGap => Mathf.Clamp(poleHalfGap, 0.05f, 0.95f);
        public float PoleHorizon => Mathf.Clamp(poleHorizon, 0.2f, 10f);
        public float PoleGM => Mathf.Max(0f, poleGM);
        public float CaptureRate => Mathf.Clamp(captureRate, 0.5f, 30f);
        public float SettleRate => Mathf.Clamp(settleRate, 0.1f, 20f);
        public float PlunkCarry => Mathf.Clamp01(plunkCarry);
        public float PlunkCallout => Mathf.Clamp01(plunkCallout);
        public float SpinSpeedMax => Mathf.Clamp(spinSpeedMax, 0f, 200f);
        public float SpinExponent => Mathf.Clamp(spinExponent, 0.5f, 4f);
        public float SpinGrip => Mathf.Clamp(spinGrip, 0f, 10f);
        public float Precess => Mathf.Clamp(precess, 0f, 5f);
        public float LaunchGainMin => Mathf.Clamp(launchGainMin, 0f, 3f);
        public float LaunchGainMax => Mathf.Clamp(launchGainMax, 0f, 3f);
        public float SweetSpotDegrees => Mathf.Clamp(sweetSpotDegrees, 10f, 360f);
        public float SweetSpotHalfWidth => Mathf.Clamp(sweetSpotHalfWidth, 0f, 90f);
        public float SweetSpotFalloff => Mathf.Clamp(sweetSpotFalloff, 1f, 180f);
        public float MissShare => Mathf.Clamp01(missShare);
        public float KickSeconds => Mathf.Clamp(kickSeconds, 0.1f, 5f);
        public float CapCruises => Mathf.Clamp(capCruises, 1f, 6f);
        public float Curve => Mathf.Clamp(curve, 0f, 3f);
        public float CurveSeconds => Mathf.Clamp(curveSeconds, 0.05f, 5f);
        public float MaxRideDegrees => Mathf.Clamp(maxRideDegrees, 90f, 1080f);
        public float HoldMax => Mathf.Clamp(holdMax, 0.5f, 30f);
        public float DriftSpeed => Mathf.Clamp(driftSpeed, 0f, 200f);
        public float SkipWindow => Mathf.Clamp(skipWindow, 0f, 5f);
        public float PerfectWindow => Mathf.Clamp(perfectWindow, 0f, 2f);
        public float SkipDecay => Mathf.Clamp01(skipDecay);
        public int MaxSkips => Mathf.Clamp(maxSkips, 0, 20);
        public float PerfectBonus => Mathf.Clamp01(perfectBonus);
        public float ChainBreakCarry => Mathf.Clamp01(chainBreakCarry);
        public int PredictSteps => Mathf.Clamp(predictSteps, 1, 64);
        public float SkipRippleSeconds => Mathf.Clamp(skipRippleSeconds, 0.1f, 3f);
        public float SkipRippleGrowth => Mathf.Clamp(skipRippleGrowth, 0f, 600f);
        public float SkipCounterSeconds => Mathf.Clamp(skipCounterSeconds, 0.2f, 5f);
        public float SkipHaptic => Mathf.Clamp01(skipHaptic);
        public float GrazeAngle => Mathf.Clamp(grazeAngle, 0f, 90f);
        public float GrazeRestitution => Mathf.Clamp01(grazeRestitution);
        public float GrazeGain => Mathf.Clamp01(grazeGain);
        public bool RivalRiding => rivalRiding;
        public float RingDotSpacing => Mathf.Clamp(ringDotSpacing, 1f, 40f);
        public float RingDotSize => Mathf.Clamp(ringDotSize, 0.1f, 5f);
        public float TidepoolAlpha => Mathf.Clamp01(tidepoolAlpha);
        public float SkimlineAlpha => Mathf.Clamp01(skimlineAlpha);
        public EventReference CaptureEvent => captureEvent;
        public EventReference PlunkEvent => plunkEvent;
        public EventReference LaunchEvent => launchEvent;
        public EventReference SweetSpotEvent => sweetSpotEvent;
        public EventReference SkipEvent => skipEvent;
        public EventReference GrazeEvent => grazeEvent;
        public EventReference ChainBreakEvent => chainBreakEvent;

        /// <summary>How well a release after <paramref name="riddenDegrees"/> is timed, 0..1 (the Time catch's window).</summary>
        public float SweetSpot(float riddenDegrees) =>
            StoatSlingMath.SweetSpot(riddenDegrees, SweetSpotDegrees, SweetSpotHalfWidth, SweetSpotFalloff);
    }
}

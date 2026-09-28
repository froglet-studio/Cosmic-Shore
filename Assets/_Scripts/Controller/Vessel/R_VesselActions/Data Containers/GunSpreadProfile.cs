using System;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// How a full-auto gun's accuracy decays while the trigger is held, and how that decay is
    /// fed back to the pilot's hands.
    ///
    /// Authored ONCE, on the vessel's <see cref="FullAutoActionSO"/> — the same asset that
    /// already owns cadence, muzzle speed and flight time — so the Turret Stance adopts the
    /// identical cone through <c>bulletAction</c> rather than authoring a second copy of it.
    /// A turret shot IS a bullet, spread included: it goes where the bullet would have gone,
    /// and where the bullet would have died the prism stays.
    ///
    /// The shape of the mechanic: the gun is PERFECTLY accurate for the onset window, then opens
    /// to a SUSTAINABLE cap and holds there for a plateau, then BLOWS OUT to 5x that cap and holds
    /// there for a second plateau, then COLLAPSES to 5x the blow-out cap - a spread so wide the
    /// gun is virtually unusable.
    ///
    /// <para><b>The clock is HEAT, not hold time.</b> Releasing the trigger no longer resets the
    /// cone: heat builds at one second per second of fire and COOLS at
    /// <see cref="CoolingRateMultiplier"/> times that while the trigger is up, so a pilot who
    /// fired for ten seconds needs two seconds off to be pin-accurate again. The curve reads
    /// heat on its time axis exactly as it used to read hold time. See
    /// <c>R_VesselActions/SPARROW_SPRAY_ACCURACY.md</c>.</para>
    /// </summary>
    [Serializable]
    public class GunSpreadProfile
    {
        [Header("Cone")]
        [Tooltip("STAGE 1. Seconds of HEAT (a cold gun, one second of fire = one second of heat) " +
                 "during which the gun is PERFECTLY accurate - the accurate plateau. Heat does " +
                 "not reset on release, it cools at coolingRateMultiplier x the build rate, so " +
                 "a release buys the window back only in proportion to how long you let go.")]
        [SerializeField, Min(0f)] float onsetSeconds = 5f;

        [Tooltip("STAGE 2. Degrees of cone half-angle gained per second of continuous fire, once " +
                 "the onset window has elapsed. Together with the max half-angle this sets how " +
                 "long the first ramp takes: max / growth.")]
        [SerializeField, Min(0f)] float growthDegreesPerSecond = 0.3f;

        [Tooltip("The SUSTAINABLE cap - where the first ramp levels off and the plateau sits. " +
                 "Sized to sit JUST past the point where the spread starts costing you the " +
                 "target you actually wanted: wide enough that everything in the danger zone is " +
                 "taking rounds, narrow enough that a held burst still kills what it is pointed " +
                 "at. Note this is an ANGLE, so the miss distance grows with range - though the " +
                 "range itself shrinks as the cone opens (rangeAtFullSpread). Zero " +
                 "disables spread entirely (the sanctioned opt-out), blow-out included.")]
        [SerializeField, Min(0f)] float maxHalfAngleDegrees = 1.5f;

        [Header("Blow-out")]
        [Tooltip("STAGE 3. Seconds the cone HOLDS at the sustainable cap before it starts " +
                 "widening again. This is the band a pilot can actually fight in: the gun is " +
                 "as inaccurate as it is ever going to be while still being a gun. Zero welds " +
                 "the two ramps together into one kinked climb.")]
        [SerializeField, Min(0f)] float plateauSeconds = 5f;

        [Tooltip("STAGE 4. Degrees per second on the SECOND ramp, once the plateau expires. " +
                 "Authored FASTER than the first ramp on purpose - the failure accelerates, so " +
                 "the curve reads as a gun losing control rather than one degrading evenly. " +
                 "Zero is the sanctioned opt-out: the cone holds at the sustainable cap " +
                 "forever, which is the single-ramp curve this replaced.")]
        [SerializeField, Min(0f)] float blowoutGrowthDegreesPerSecond = 1.2f;

        [Tooltip("The FINAL cap, as a multiple of the sustainable cap - so the two can never " +
                 "drift apart when the base cap is retuned. At the shipped 5x the gun is area " +
                 "denial rather than a rifle, and it holds there for the second plateau before " +
                 "the collapse; 1 disables the blow-out (and with it the collapse).")]
        [SerializeField, Min(1f)] float blowoutMaxMultiplier = 5f;

        [Header("Collapse")]
        [Tooltip("STAGE 5. Seconds of heat the cone HOLDS at the blow-out cap before it " +
                 "collapses. Zero welds the second and third ramps together.")]
        [SerializeField, Min(0f)] float collapsePlateauSeconds = 5f;

        [Tooltip("STAGE 6. Degrees per second on the THIRD ramp, once the second plateau " +
                 "expires. Faster again than the blow-out ramp - the failure keeps accelerating. " +
                 "Zero is the sanctioned opt-out: the cone holds at the blow-out cap forever.")]
        [SerializeField, Min(0f)] float collapseGrowthDegreesPerSecond = 6f;

        [Tooltip("The COLLAPSE cap, as a multiple of the blow-out cap (so of the sustainable cap " +
                 "too). At the shipped 5x the spread is virtually unusable - the price of never " +
                 "letting the gun cool. 1 disables the collapse.")]
        [SerializeField, Min(1f)] float collapseMaxMultiplier = 5f;

        [Header("Heat")]
        [Tooltip("How much faster the gun COOLS than it heats. Heat builds at 1 per second of " +
                 "fire and drops at this many per second while the trigger is up, so at 5 a " +
                 "fully-hot gun (30 s of heat as shipped) is cold again after 6 s off. The trigger coming " +
                 "up does NOT reset accuracy.")]
        [SerializeField, Min(0.01f)] float coolingRateMultiplier = 5f;

        [Header("Range")]
        [Tooltip("Fraction of the authored range a round keeps at the FINAL cap (the collapse " +
                 "cap as shipped). Range falls linearly with the cone's half-angle - " +
                 "proportional to spread - from full range at a cold gun to this at full spread, " +
                 "so a hot gun is both wider and shorter. Applied as a muzzle-speed factor, so " +
                 "flight time and rounds-in-flight are unchanged. 1 disables the falloff.")]
        [SerializeField, Range(0.05f, 1f)] float rangeAtFullSpread = 0.25f;

        [Tooltip("Where inside the cone rounds land. 0.5 = uniform over the disc (the whole " +
                 "danger zone saturates evenly - the default). 1.0 = a dense core with a thin " +
                 "halo, so what you are aiming at still soaks most of the fire. Below 0.5 " +
                 "hollows the middle out toward the rim.")]
        [SerializeField, Range(0.05f, 2f)] float distributionBias = 0.5f;

        [Header("Haptics")]
        [Tooltip("Haptic strength the instant firing begins, before any accuracy has been lost. " +
                 "Above zero so the gun is FELT from the first round; the ramp to 1.0 as the " +
                 "cone opens is what tells the pilot their accuracy is going.")]
        [SerializeField, Range(0f, 1f)] float hapticFloor01 = 0.15f;

        [Tooltip("Seconds between haptic pulses while the gun is still accurate. The cadence " +
                 "tightens toward the max-spread interval as the cone opens, so the feel climbs " +
                 "in BOTH strength and rate - a gun winding up, not a constant hum.")]
        [SerializeField, Min(0.02f)] float hapticIntervalAtRest = 0.10f;

        [Tooltip("Seconds between haptic pulses at full spread. This is a REQUEST - it is issued " +
                 "on the next frame, so 0.045 is delivered as 0.050 at 60fps, exactly the spray " +
                 "clip's length. Do not go lower: duty is already saturated, and at one frame " +
                 "(~0.017 s) the envelope collapses to a flat hum - measured, see HAPTICS.md.")]
        [SerializeField, Min(0.02f)] float hapticIntervalAtMaxSpread = 0.045f;

        public float OnsetSeconds => onsetSeconds;
        public float GrowthDegreesPerSecond => growthDegreesPerSecond;
        public float MaxHalfAngleDegrees => maxHalfAngleDegrees;
        public float PlateauSeconds => plateauSeconds;
        public float BlowoutGrowthDegreesPerSecond => blowoutGrowthDegreesPerSecond;
        public float DistributionBias => distributionBias;

        /// <summary>Range kept at the final cap, 0..1 (see <see cref="GunSpreadMath.RangeFactor"/>).</summary>
        public float RangeAtFullSpread => Mathf.Clamp01(rangeAtFullSpread);

        /// <summary>
        /// The blow-out cap in DEGREES - derived from the sustainable cap, never authored
        /// beside it, so retuning the base cap carries the blow-out with it (one authored
        /// number per displayed quantity).
        /// </summary>
        public float BlowoutMaxHalfAngleDegrees => maxHalfAngleDegrees * Mathf.Max(1f, blowoutMaxMultiplier);

        /// <summary>
        /// The collapse cap in DEGREES - a multiple of the blow-out cap, derived for the same
        /// reason <see cref="BlowoutMaxHalfAngleDegrees"/> is.
        /// </summary>
        public float CollapseMaxHalfAngleDegrees => BlowoutMaxHalfAngleDegrees * Mathf.Max(1f, collapseMaxMultiplier);

        /// <summary>Heat lost per second while the trigger is up, as a multiple of the build rate.</summary>
        public float CoolingRateMultiplier => Mathf.Max(0.01f, coolingRateMultiplier);

        /// <summary>
        /// The authored curve, as the pure numbers <see cref="GunSpreadMath"/> consumes. Built
        /// per read - it is nine float copies and the profile is asked once per frame per firing
        /// vessel, so caching it would only add a staleness bug when someone retunes in play.
        /// </summary>
        public GunSpreadStages Stages => new GunSpreadStages(
            onsetSeconds, growthDegreesPerSecond, maxHalfAngleDegrees,
            plateauSeconds, blowoutGrowthDegreesPerSecond, BlowoutMaxHalfAngleDegrees,
            collapsePlateauSeconds, collapseGrowthDegreesPerSecond, CollapseMaxHalfAngleDegrees);

        /// <summary>Seconds of HEAT at which the cone reaches its final cap - also the heat
        /// ceiling, so a fully-hot gun needs this / <see cref="CoolingRateMultiplier"/> seconds
        /// off to be cold again.</summary>
        public float SecondsToFullSpread => Stages.SecondsToFullSpread;

        public float HapticFloor01 => hapticFloor01;
        public float HapticIntervalAtRest => hapticIntervalAtRest;

        /// <summary>Clamped so a mis-authored asset can never invert the ramp (which would make
        /// the pulses SLOW DOWN as the cone opens - the opposite of the intended read).</summary>
        public float HapticIntervalAtMaxSpread => Mathf.Min(hapticIntervalAtMaxSpread, hapticIntervalAtRest);

        /// <summary>True when this profile actually opens a cone. A zero max half-angle (or zero
        /// growth) is the sanctioned opt-out: the gun behaves exactly as it did before spread
        /// existed, and no haptic ramp is driven.</summary>
        public bool Enabled => maxHalfAngleDegrees > 0f && growthDegreesPerSecond > 0f;
    }
}

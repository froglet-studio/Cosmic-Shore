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
    /// The shape of the mechanic, in four parts: the gun is PERFECTLY accurate for the onset
    /// window, then opens to a SUSTAINABLE cap, then holds there for the plateau, then BLOWS OUT
    /// to a multiple of that cap for as long as the trigger stays down. Release and re-pull and
    /// it snaps back to pin-accurate, so short bursts stay surgical and only a pilot who never
    /// lets go ever meets the blow-out. See <c>R_VesselActions/SPARROW_SPRAY_ACCURACY.md</c>.
    /// </summary>
    [Serializable]
    public class GunSpreadProfile
    {
        [Header("Cone")]
        [Tooltip("STAGE 1. Grace period at the start of every trigger pull during which the gun " +
                 "is PERFECTLY accurate. This is what makes tapped and sustained-but-disciplined " +
                 "bursts surgical - size it to the engagement length that should stay free (at " +
                 "90 volleys/s, 2 s is ~180 volleys / 360 rounds). Accuracy resets the instant " +
                 "the trigger comes up, so a release and re-pull buys the whole window again.")]
        [SerializeField, Min(0f)] float onsetSeconds = 2f;

        [Tooltip("STAGE 2. Degrees of cone half-angle gained per second of continuous fire, once " +
                 "the onset window has elapsed. Together with the max half-angle this sets how " +
                 "long the first ramp takes: max / growth.")]
        [SerializeField, Min(0f)] float growthDegreesPerSecond = 0.75f;

        [Tooltip("The SUSTAINABLE cap - where the first ramp levels off and the plateau sits. " +
                 "Sized to sit JUST past the point where the spread starts costing you the " +
                 "target you actually wanted: wide enough that everything in the danger zone is " +
                 "taking rounds, narrow enough that a held burst still kills what it is pointed " +
                 "at. Note this is an ANGLE, so the miss distance grows with range - a Sparrow " +
                 "at high SPACE shoots much further and therefore groups much wider. Zero " +
                 "disables spread entirely (the sanctioned opt-out), blow-out included.")]
        [SerializeField, Min(0f)] float maxHalfAngleDegrees = 1.5f;

        [Header("Blow-out")]
        [Tooltip("STAGE 3. Seconds the cone HOLDS at the sustainable cap before it starts " +
                 "widening again. This is the band a pilot can actually fight in: the gun is " +
                 "as inaccurate as it is ever going to be while still being a gun. Zero welds " +
                 "the two ramps together into one kinked climb.")]
        [SerializeField, Min(0f)] float plateauSeconds = 2f;

        [Tooltip("STAGE 4. Degrees per second on the SECOND ramp, once the plateau expires. " +
                 "Authored FASTER than the first ramp on purpose - the failure accelerates, so " +
                 "the curve reads as a gun losing control rather than one degrading evenly. " +
                 "Zero is the sanctioned opt-out: the cone holds at the sustainable cap " +
                 "forever, which is the single-ramp curve this replaced.")]
        [SerializeField, Min(0f)] float blowoutGrowthDegreesPerSecond = 1.5f;

        [Tooltip("The FINAL cap, as a multiple of the sustainable cap - so the two can never " +
                 "drift apart when the base cap is retuned. At the shipped 5x the gun stops " +
                 "being aimable at all and becomes pure area denial; 1 disables the blow-out.")]
        [SerializeField, Min(1f)] float blowoutMaxMultiplier = 5f;

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

        [Tooltip("Seconds between haptic pulses at full spread. Keep it above ~0.04 s: " +
                 "NiceVibrations holds one clip at a time, so pulses closer than the clip " +
                 "length just cut each other off and the feel gets weaker, not stronger.")]
        [SerializeField, Min(0.02f)] float hapticIntervalAtMaxSpread = 0.045f;

        public float OnsetSeconds => onsetSeconds;
        public float GrowthDegreesPerSecond => growthDegreesPerSecond;
        public float MaxHalfAngleDegrees => maxHalfAngleDegrees;
        public float PlateauSeconds => plateauSeconds;
        public float BlowoutGrowthDegreesPerSecond => blowoutGrowthDegreesPerSecond;
        public float DistributionBias => distributionBias;

        /// <summary>
        /// The blow-out cap in DEGREES - derived from the sustainable cap, never authored
        /// beside it, so retuning the base cap carries the blow-out with it (one authored
        /// number per displayed quantity).
        /// </summary>
        public float BlowoutMaxHalfAngleDegrees => maxHalfAngleDegrees * Mathf.Max(1f, blowoutMaxMultiplier);

        /// <summary>
        /// The authored curve, as the pure numbers <see cref="GunSpreadMath"/> consumes. Built
        /// per read - it is six float copies and the profile is asked once per frame per firing
        /// vessel, so caching it would only add a staleness bug when someone retunes in play.
        /// </summary>
        public GunSpreadStages Stages => new GunSpreadStages(
            onsetSeconds, growthDegreesPerSecond, maxHalfAngleDegrees,
            plateauSeconds, blowoutGrowthDegreesPerSecond, BlowoutMaxHalfAngleDegrees);

        /// <summary>Seconds of unbroken fire before the cone reaches its final cap.</summary>
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

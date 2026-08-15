using System;
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
    /// The shape of the mechanic: hold the trigger and the cone opens, so the volume of fire
    /// covers a growing danger zone instead of a line; release and re-pull and it snaps back to
    /// pin-accurate, so short bursts stay surgical. See
    /// <c>R_VesselActions/SPARROW_SPRAY_ACCURACY.md</c>.
    /// </summary>
    [Serializable]
    public class GunSpreadProfile
    {
        [Header("Cone")]
        [Tooltip("Grace period at the start of every trigger pull during which the gun is " +
                 "PERFECTLY accurate. This is what makes tapped bursts surgical - size it to " +
                 "the burst length you want to stay free (at 60 volleys/s, 0.12 s is ~7 volleys " +
                 "/ 14 rounds). Accuracy resets the instant the trigger comes up, so a release " +
                 "and re-pull buys the whole window again.")]
        [SerializeField, Min(0f)] float onsetSeconds = 0.12f;

        [Tooltip("Degrees of cone half-angle gained per second of continuous fire, once the " +
                 "onset window has elapsed. Together with the max half-angle this sets how " +
                 "long a hold takes to open fully: max / growth + onset.")]
        [SerializeField, Min(0f)] float growthDegreesPerSecond = 3.2f;

        [Tooltip("The cap - the cone stops widening here no matter how long you hold. Sized to " +
                 "sit JUST past the point where the spread starts costing you the target you " +
                 "actually wanted: wide enough that everything in the danger zone is taking " +
                 "rounds, narrow enough that a held burst still kills what it is pointed at. " +
                 "Note this is an ANGLE, so the miss distance grows with range - a Sparrow at " +
                 "high SPACE shoots much further and therefore groups much wider.")]
        [SerializeField, Min(0f)] float maxHalfAngleDegrees = 4f;

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
        public float DistributionBias => distributionBias;
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

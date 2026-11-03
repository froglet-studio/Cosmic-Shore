using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Brittle Star's flight model (design: <c>R_VesselActions/BRITTLE_STAR.md</c> §3.1): the fleet's one-thumb
    /// <see cref="SingleStickVesselTransformer"/> with one addition — Speed Mode's multiplier on the throttle TARGET.
    ///
    /// <para><b>Why a subclass, not a write to a shared field.</b> The fleet has no runtime speed multiplier on the
    /// transformer, and the two ways to fake one are both forbidden: scaling the serialized <c>BoostMultiplier</c> in
    /// place ratchets (vessel rule 23), and raising <c>IsBoosting</c> hands the mode to every system that reacts to a
    /// boost. A multiplier the executor writes and this class alone reads has exactly one writer and one reader.</para>
    ///
    /// <para>The multiplier scales the TARGET, so the hull eases into and out of its new speed through the same
    /// <c>StepTowardTarget</c> every other change of speed goes through (vessel rule 16), and the turn rates — which
    /// read the live <c>speed</c> — tighten and loosen with it exactly as they do for a boost.</para>
    ///
    /// <para><b>Speed Roll</b> (the Time level-5 upgrade, Shombith 2026-11-03): a roll rate and a pull rate the executor
    /// writes from the right stick while Speed Mode is on, applied about the nose and the wing on top of the one-thumb
    /// steering, so the hull can roll and pull to change direction at Speed Mode's speed. Same single writer, single
    /// reader contract as the speed multiplier.</para>
    /// </summary>
    public class BrittleStarVesselTransformer : SingleStickVesselTransformer
    {
        /// <summary>Speed Mode's multiplier, eased by <see cref="BrittleStarExecutor"/> (its single writer). Runtime
        /// only, never serialized; 1 = cruise.</summary>
        public float SpeedModeMultiplier { get; set; } = 1f;

        /// <summary>Speed Roll's rates, degrees per second: x = roll about the nose, y = pull (pitch) about the wing.
        /// Written each frame by <see cref="BrittleStarExecutor"/> (its single writer); zero = no Speed Roll.</summary>
        public Vector2 SpeedRollDegreesPerSecond { get; set; }

        protected override float ComputeThrottleTarget() => base.ComputeThrottleTarget() * SpeedModeMultiplier;

        protected override void RotateShip()
        {
            Vector2 rates = SpeedRollDegreesPerSecond;
            if (rates.sqrMagnitude > 1e-6f)
            {
                float dt = Time.deltaTime;
                // Roll about the nose, then pull about the (rolled) wing: the pilot's roll-and-pull.
                accumulatedRotation = Quaternion.AngleAxis(-rates.x * dt, transform.forward) * accumulatedRotation;
                accumulatedRotation = Quaternion.AngleAxis(-rates.y * dt, transform.right) * accumulatedRotation;
            }
            base.RotateShip();
        }

        public override void ResetTransformer()
        {
            SpeedModeMultiplier = 1f;
            SpeedRollDegreesPerSecond = Vector2.zero;
            base.ResetTransformer();
        }
    }
}

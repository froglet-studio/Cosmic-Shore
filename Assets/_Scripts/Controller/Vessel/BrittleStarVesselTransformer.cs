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
    /// </summary>
    public class BrittleStarVesselTransformer : SingleStickVesselTransformer
    {
        /// <summary>Speed Mode's multiplier, eased by <see cref="BrittleStarExecutor"/> (its single writer). Runtime
        /// only, never serialized; 1 = cruise.</summary>
        public float SpeedModeMultiplier { get; set; } = 1f;

        protected override float ComputeThrottleTarget() => base.ComputeThrottleTarget() * SpeedModeMultiplier;

        public override void ResetTransformer()
        {
            SpeedModeMultiplier = 1f;
            base.ResetTransformer();
        }
    }
}

using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Angler's flight model (design: <c>R_VesselActions/ANGLER.md</c>): the fleet's two-stick
    /// <see cref="VesselTransformer"/> with one addition, the LURE BRAKE. While the lure is held the throttle TARGET is
    /// capped at the lab's <c>lureCruise</c>, because a hull at cruise outruns its own catch (the Angler Command Lab
    /// measured six creatures never tamed in 90 s at 40 u/s, and 14-19 s at 20 u/s or less).
    ///
    /// <para>The cap acts on the TARGET, so the hull eases down and back up through the same <c>StepTowardTarget</c>
    /// every other change of speed goes through (vessel rule 16), on both flight models. Boost overrides it: the cap
    /// applies to the cruise part only when the pilot is not boosting, as in the lab.</para>
    /// </summary>
    public class AnglerVesselTransformer : VesselTransformer
    {
        /// <summary>The lure brake's speed cap (u/s), written by <see cref="AnglerCommandExecutor"/> (its single writer).
        /// 0 = no cap. Runtime only, never serialized.</summary>
        public float LureSpeedCap { get; set; }

        protected override float ComputeThrottleTarget()
        {
            float target = base.ComputeThrottleTarget();
            if (LureSpeedCap > 0f && (VesselStatus == null || !VesselStatus.IsBoosting))
                target = Mathf.Min(target, LureSpeedCap);
            return target;
        }

        public override void ResetTransformer()
        {
            LureSpeedCap = 0f;
            base.ResetTransformer();
        }
    }
}

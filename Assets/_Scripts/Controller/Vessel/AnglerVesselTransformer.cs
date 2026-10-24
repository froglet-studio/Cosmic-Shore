using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Angler's flight model (design: <c>R_VesselActions/ANGLER.md</c>): a ONE-THUMB flier since round 4 (the designer,
    /// 2026-10-17: "he doesn't need to use the right thumbstick at all") - the fleet's
    /// <see cref="SingleStickVesselTransformer"/> (the left stick flies; the right stick is never read; the triggers and
    /// buttons are untouched), the Brittle Star's pattern - with one addition, the LURE BRAKE. While the lure is held the throttle TARGET is
    /// capped at the lab's <c>lureCruise</c> - 0 since round 4, so the lure STOPS the hull - because a hull at cruise outruns
    /// its own catch (the Angler Command Lab measured six creatures never tamed in 90 s at 40 u/s, and 14-19 s at 20 u/s
    /// or less).
    ///
    /// <para>The cap acts on the TARGET, so the hull eases down and back up through the same <c>StepTowardTarget</c>
    /// every other change of speed goes through (vessel rule 16), on both flight models. Boost overrides it: the cap
    /// applies to the cruise part only when the pilot is not boosting, as in the lab.</para>
    /// </summary>
    public class AnglerVesselTransformer : SingleStickVesselTransformer
    {
        /// <summary>No lure brake (<see cref="LureSpeedCap"/>). Negative, because 0 is a real cap: the lure STOPS the hull
        /// (the lab's <c>lureCruise</c> 0, the designer, 2026-10-17).</summary>
        public const float NoCap = -1f;

        /// <summary>The lure brake's speed cap (u/s), written by <see cref="AnglerCommandExecutor"/> (its single writer).
        /// <see cref="NoCap"/> (negative) = no cap; 0 = stop. Runtime only, never serialized.</summary>
        public float LureSpeedCap { get; set; } = NoCap;

        protected override float ComputeThrottleTarget()
        {
            float target = base.ComputeThrottleTarget();
            if (LureSpeedCap >= 0f && (VesselStatus == null || !VesselStatus.IsBoosting))
                target = Mathf.Min(target, LureSpeedCap);
            return target;
        }

        public override void ResetTransformer()
        {
            LureSpeedCap = NoCap;
            base.ResetTransformer();
        }
    }
}

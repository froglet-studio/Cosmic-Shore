using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A prism ram costs the vessel part of the BOOST it banked by drifting — the twin of
    /// <see cref="VesselChangeResourceByPrismEffectSO"/>, which does the same to its skim energy.
    ///
    /// Scaling the meter alone is not enough. <see cref="ChargeBoostActionExecutor"/> derives two
    /// live speed terms from that meter, and <see cref="VesselTransformer.CurrentBoostAmount"/>
    /// multiplies BOTH during a discharge — but only one of them ever re-reads the meter:
    ///
    ///   BoostMultiplier     recomputed from the meter on every discharge tick — self-correcting
    ///   ChargedBoostCharge  pinned at the value the CHARGE ended on — never re-read again
    ///
    /// So the meter write covers `BoostMultiplier` on its own (within one 0.1 s tick), and only
    /// the pinned snapshot needs correcting here — otherwise it keeps paying full price on half
    /// the product and a ram mid-boost is barely felt. It is scaled rather than recomputed
    /// because it has the form 1 + (maxBoostMultiplier − 1) × meter: scaling the meter by f is
    /// exactly scaling its distance above 1 by f, which needs no reference to the action SO.
    ///
    /// `BoostMultiplier` is deliberately NOT written. It is a SERIALIZED, authored field on
    /// VesselStatus (4 on the Dolphin) that boost sources which don't write it fall back to —
    /// `BoostActionSO` just flips IsBoosting, and `VesselResetBoostPrismEffectSO` restores it to
    /// an authored base. Scaling it in place would ratchet that authored value toward 1 a little
    /// more on every ram, permanently, with nothing to restore it.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselChangeBoostByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselChangeBoostByPrismEffectSO")]
    public class VesselChangeBoostByPrismEffectSO : VesselPrismEffectSO
    {
        [Tooltip("Resource slot holding the charged boost units. Must match the Boost Resource Index " +
                 "authored on the vessel's ChargeBoostActionSO (slot 1 on the Dolphin).")]
        [SerializeField] int boostResourceIndex = 1;

        [Tooltip("Fraction of the banked boost the vessel KEEPS on a prism ram. 0.5 = loses half.")]
        [SerializeField, Range(0f, 1f)] float retainedFraction = 0.5f;

        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            // Unity's fake-null operator, not C# ?. — a destroyed impactor is not C# null and
            // would walk straight through a null-propagating chain.
            if (!vesselImpactor) return;

            var status = vesselImpactor.Vessel?.VesselStatus;
            var rs = status?.ResourceSystem;
            if (rs == null) return;

            if (boostResourceIndex < 0 || boostResourceIndex >= rs.Resources.Count) return;

            rs.SetResourceAmount(boostResourceIndex,
                                 rs.Resources[boostResourceIndex].CurrentAmount * retainedFraction);

            // Only meaningful mid-discharge — that is the one state CurrentBoostAmount reads it in,
            // and outside it the value is stale bookkeeping the next BeginCharge overwrites.
            //
            // Ramming while STILL DRIFTING is a third case and is deliberately left alone: the
            // charge loop is running, so it refills the halved meter and re-derives this from it.
            // The ram costs drift-seconds rather than a permanent bank — the pilot is, after all,
            // still doing the thing that banks boost.
            if (status.IsChargedBoostDischarging)
                status.ChargedBoostCharge = 1f + (status.ChargedBoostCharge - 1f) * retainedFraction;
        }
    }
}

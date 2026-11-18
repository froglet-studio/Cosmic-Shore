using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A hull contact with a prism ADDS to a resource - the Rhino's ramp fuel, earned by ploughing (Garrett,
    /// 2026-11-18: "make their boosting resource tied to their slicing and plowing"). The twin of
    /// <see cref="SkimmerChangeResourceByPrismEffectSO"/>, which pays the Swordfish's bill for a slice.
    ///
    /// <para>Super-shielded mass pays nothing by default: the plough cannot move it (it deflects), so nothing was
    /// ploughed. Riding a trail pays nothing either, the same guard the plough and the ram keep.</para>
    ///
    /// <para>Prism contact is simulated per peer, so every peer refuels its own copy of the tank from its own
    /// contacts, as the plough itself runs per peer (RHINO_PLOUGH.md).</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselGainResourceByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselGainResourceByPrismEffectSO")]
    public sealed class VesselGainResourceByPrismEffectSO : VesselPrismEffectSO
    {
        [Tooltip("The resource slot and the amount one prism contact adds (add, not set).")]
        [SerializeField] ResourceChangeSpec change;

        [Tooltip("Pay nothing for a super-shielded prism (the plough deflects off it: nothing was ploughed).")]
        [SerializeField] bool skipSuperShielded = true;

        [Tooltip("Pay nothing while the hull rides a trail (the plough and the ram skip those contacts too).")]
        [SerializeField] bool skipWhileAttached = true;

        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            if (!impactor) return;
            var status = impactor.Vessel?.VesselStatus;
            if (status == null || (skipWhileAttached && status.IsAttached)) return;
            if (!PaysFor(prismImpactee, skipSuperShielded)) return;
            change.ApplyTo(status.ResourceSystem, this);
        }

        /// <summary>True when a contact with this prism earns the resource: a live prism, and not super-shielded when
        /// those are skipped. Shared with the skimmer twin.</summary>
        public static bool PaysFor(PrismImpactor prismImpactee, bool skipSuperShielded)
        {
            var prism = prismImpactee ? prismImpactee.Prism : null;
            if (!prism || prism.destroyed) return false;
            return !(skipSuperShielded && prism.prismProperties != null && prism.prismProperties.IsSuperShielded);
        }
    }
}

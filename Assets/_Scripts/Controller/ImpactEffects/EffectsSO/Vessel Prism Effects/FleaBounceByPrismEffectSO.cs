using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Flea's hull contact: the prism is a surface to bounce off, never a crash. Calls
    /// <see cref="FleaVesselTransformer.Bounce"/>, which reflects the momentum off the prism's axis and times the bounce
    /// against the RT press (FLEA.md §3). Wired into FleaImpactorDataContainer only, in place of the Stoat container's
    /// damage, boost reset and speed change (the platform's hull-contact cost a dull bounce replaces).
    /// </summary>
    [CreateAssetMenu(fileName = "FleaBounceByPrismEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/FleaBounceByPrismEffectSO")]
    public sealed class FleaBounceByPrismEffectSO : VesselPrismEffectSO
    {
        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            if (!impactor || !prismImpactee || !prismImpactee.Prism) return;
            var vessel = impactor.Vessel;
            var status = vessel != null ? vessel.VesselStatus : null;
            if (status != null && status.VesselTransformer is FleaVesselTransformer flea && flea)
            {
                flea.Bounce(prismImpactee.Prism.transform);
                return;
            }
            CSDebug.LogWarning($"[Flea] '{impactor.name}' hit a prism but its transformer is not a FleaVesselTransformer - " +
                               "is FleaBounceByPrismEffect wired into a non-Flea container?", impactor);
        }
    }
}

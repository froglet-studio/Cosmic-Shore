using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Manta's risk (MANTA_STING_KABLOOM.md §2): when its HULL hits a prism, every bomb it has planted goes off at
    /// once, small, and all the energy it has gathered is lost. Sits in <c>MantaImpactorDataContainer.vesselPrismEffects</c>,
    /// so the dispatcher's own guards already apply - the ribbon still coming out of the Manta's own ship and a prism
    /// whose shell owns the contact never reach it. All the state lives in the bay, which gates on simulation authority.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MantaDetonateByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/MantaDetonateByPrismEffectSO")]
    public class MantaDetonateByPrismEffectSO : VesselPrismEffectSO
    {
        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor?.Vessel?.VesselStatus;
            if (status == null) return;
            var prism = prismImpactee?.Prism;
            if (prism == null || prism.destroyed) return;
            if (!MantaStingActionExecutor.TryGetFor(status, out var bay)) return;
            bay.OnPrismImpact();
        }
    }
}

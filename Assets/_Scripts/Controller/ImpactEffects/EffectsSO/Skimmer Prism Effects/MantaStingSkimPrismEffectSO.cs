using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// STING's charging half, and its lifeform-planting half — one skimmer-prism effect
    /// because to the impact system both are the same contact: the Manta's skimmer touching a
    /// prism. Every skim tick pays bomb charge into the bay
    /// (<see cref="MantaStingActionExecutor.AddSkimCharge"/>, Charge-scaled, per-prism
    /// throttled); a skimmed prism that turns out to be a LIVING CREATURE'S BODY also plants a
    /// bomb on the creature — grazing wildlife is the joust the spec means for lifeforms, and
    /// making it automatic is the accessibility thesis ("a player who does nothing but hold
    /// both triggers... will arm bombs, plant bombs").
    ///
    /// Runs for every prism the dispatcher hands it (own-domain mass included — the Manta
    /// skims membranes and old ribbons alike; only the fresh-own-trail grace upstream is
    /// excluded). The bay itself gates on simulation authority, so remote replicas of this
    /// skimmer charge nothing.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MantaStingSkimPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/MantaStingSkimPrismEffectSO")]
    public class MantaStingSkimPrismEffectSO : SkimmerPrismEffectSO
    {
        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor?.Skimmer?.VesselStatus;
            if (status == null) return;
            if (!MantaStingActionExecutor.TryGetFor(status, out var bay)) return;

            var prism = prismImpactee?.Prism;
            if (prism == null || prism.destroyed) return;

            bay.AddSkimCharge(prism);

            if (prism is HealthPrism healthPrism)
            {
                var fauna = healthPrism.ResolveOwnerFauna();
                if (fauna != null)
                    bay.TryPlantOnFauna(fauna);
            }
        }
    }
}

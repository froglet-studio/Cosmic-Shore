using UnityEngine;

namespace CosmicShore.Gameplay
{
    public abstract class SkimmerCrystalEffectSO : ImpactEffectSO
    {
        [Tooltip("OFF (the default, and every shipped effect): this effect fires only on ELEMENTAL " +
                 "crystals, which is the only kind the skimmer dispatched before. ON: it also fires " +
                 "on OMNI crystals (including team crystals). Opt-in rather than automatic because " +
                 "widening the dispatch would otherwise change every existing skimmer effect — the " +
                 "Rhino's sword burst would start spending its energy on omni crystals it has never " +
                 "reacted to. The Scarab's ball conversion is the one effect that wants them.")]
        [SerializeField] bool alsoAppliesToOmniCrystals;

        /// <summary>True if this effect should also receive OMNI (non-elemental) crystals.</summary>
        public bool AlsoAppliesToOmniCrystals => alsoAppliesToOmniCrystals;

        public abstract void Execute(SkimmerImpactor impactor, CrystalImpactor  impactee);
    }
}

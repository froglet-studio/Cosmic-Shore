using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Tiger Moth's Dust-mode capsule sweeping an ALLY: their silence lifts and their temporary
    /// elemental debuffs end (<see cref="ElementalCleanse"/>). A rival is the bite's business
    /// (<see cref="VesselElementalDebuffBySkimmerEffectSO"/>), so this one does nothing to them. Runs on
    /// every peer, as the contact does: each peer cleanses its own copy.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselCleanseAllyBySkimmerEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/VesselCleanseAllyBySkimmerEffectSO")]
    public class VesselCleanseAllyBySkimmerEffectSO : VesselSkimmerEffectsSO
    {
        public override void Execute(VesselImpactor impactor, SkimmerImpactor impactee)
        {
            if (!impactor || impactor.Vessel == null) return;
            var ally = impactor.Vessel.VesselStatus;
            var pilot = impactee != null && impactee.Skimmer != null ? impactee.Skimmer.VesselStatus : null;
            if (!ElementalCleanse.IsAlly(pilot, ally)) return;
            ElementalCleanse.Cleanse(ally);
        }
    }
}

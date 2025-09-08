using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SkimmerStealPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer/SkimmerStealPrismEffectSO")]
    public class SkimmerStealPrismEffectSO : SkimmerPrismEffectSO
    {
        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Skimmer.ShipStatus;
            PrismEffectHelper.Steal(prismImpactee, status);
        }
    }
}
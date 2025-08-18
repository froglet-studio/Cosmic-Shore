using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SkimmerStealPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer/SkimmerStealPrismEffectSO")]
    public class SkimmerStealPrismEffectSO : StealPrismEffectBaseSO<SkimmerImpactor>
    {
        protected override IShipStatus GetShipStatus(SkimmerImpactor impactor)
            => impactor.Skimmer.ShipStatus;
    }
}
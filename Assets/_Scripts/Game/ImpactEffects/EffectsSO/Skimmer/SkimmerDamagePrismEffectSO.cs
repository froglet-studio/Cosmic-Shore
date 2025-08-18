using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SkimmerDamagePrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer/SkimmerDamagePrismEffectSO")]
    public class SkimmerDamagePrismEffectSO : DamagePrismEffectBase<SkimmerImpactor>
    {
        protected override IShipStatus GetAttackerStatus(SkimmerImpactor impactor)
            => impactor.Skimmer?.ShipStatus;
    }
}
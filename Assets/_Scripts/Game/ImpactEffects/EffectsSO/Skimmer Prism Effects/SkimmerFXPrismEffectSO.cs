using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SkimmerFXPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerFXPrismEffectSO")]
    public class SkimmerFXPrismEffectSO : SkimmerPrismEffectSO
    {
        [SerializeField] private float particleDurationAtSpeedOne = 300f;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var shipStatus = impactor?.Skimmer?.ShipStatus; // use the owning ship’s status
            var trailBlock = prismImpactee?.Prism?.TrailBlockProperties?.trailBlock;
            SkimFxRunner.RunAsync(shipStatus, trailBlock, particleDurationAtSpeedOne).Forget();
        }
    }
}

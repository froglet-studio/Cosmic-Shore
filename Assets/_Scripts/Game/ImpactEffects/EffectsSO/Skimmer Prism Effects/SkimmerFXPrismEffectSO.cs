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
            var shipStatus = impactor?.Skimmer?.VesselStatus; // use the owning vessel’s status
            var trailBlock = prismImpactee?.Prism?.prismProperties?.prism;
            SkimFxRunner.RunAsync(shipStatus, trailBlock, particleDurationAtSpeedOne).Forget();
        }
    }
}

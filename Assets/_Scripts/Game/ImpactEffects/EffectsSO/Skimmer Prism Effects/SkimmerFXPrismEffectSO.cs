using UnityEngine;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.EffectsSO.Helpers;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects.EffectsSO.SkimmerPrismEffects
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

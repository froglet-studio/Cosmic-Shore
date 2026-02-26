using UnityEngine;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.EffectsSO.Helpers;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects.EffectsSO.VesselPrismEffects
{
    [CreateAssetMenu(fileName = "VesselFXPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselFXPrismEffectSO")]
    public class VesselFXPrismEffectSO : VesselPrismEffectSO
    {
        [SerializeField] private float particleDurationAtSpeedOne = 300f;

        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var shipStatus = impactor?.Vessel?.VesselStatus;
            var trailBlock = prismImpactee?.Prism?.prismProperties?.prism;
            SkimFxRunner.RunAsync(shipStatus, trailBlock, particleDurationAtSpeedOne).Forget();
        }
    }
}

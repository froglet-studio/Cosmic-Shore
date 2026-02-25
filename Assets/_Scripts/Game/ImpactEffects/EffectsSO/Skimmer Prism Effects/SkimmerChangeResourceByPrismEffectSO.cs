using UnityEngine;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.EffectsSO.Helpers;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
using CosmicShore.Utility.Recording;

namespace CosmicShore.Game.ImpactEffects.EffectsSO.SkimmerPrismEffects
{
    [CreateAssetMenu(
        fileName = "SkimmerChangeResourceByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerChangeResourceByPrismEffectSO")]
    public class SkimmerChangeResourceByPrismEffectSO : SkimmerPrismEffectSO 
    {
        [SerializeField] ResourceChangeSpec _change;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var rs = impactor.Skimmer.VesselStatus.ResourceSystem;
            CSDebug.Log($"Applying Resource Value {_change}");
            _change.ApplyTo(rs, this);
        }
    }
}
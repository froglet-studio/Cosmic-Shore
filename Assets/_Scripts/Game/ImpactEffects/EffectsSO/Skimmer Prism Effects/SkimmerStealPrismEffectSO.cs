using System;
using UnityEngine;
using CosmicShore.Game.ImpactEffects;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects
{
    [CreateAssetMenu(fileName = "SkimmerStealPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerStealPrismEffectSO")]
    public class SkimmerStealPrismEffectSO : SkimmerPrismEffectSO
    {
        public static event Action<string> OnSkimmerStolenPrism;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Skimmer.VesselStatus;
            PrismEffectHelper.Steal(prismImpactee, status);

            OnSkimmerStolenPrism?.Invoke(status.PlayerName);
        }
    }
}
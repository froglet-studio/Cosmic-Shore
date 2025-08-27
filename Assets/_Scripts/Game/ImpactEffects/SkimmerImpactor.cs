using System;
using UnityEngine;

namespace CosmicShore.Game
{
    public class SkimmerImpactor : ImpactorBase
    {
        // [SerializeField, RequireInterface(typeof(IImpactEffect))] 
        // ScriptableObject[] skimmerShipEffectsSO;

        SkimmerShipEffectsSO[] skimmerShipEffects;
        SkimmerPrismEffectSO[] skimmerPrismEffects;
        SkimmerElementalCrystalEffects[]  skimmerElementalCrystalEffects;
        
        
        [SerializeField] private Skimmer skimmer;
        public Skimmer Skimmer => skimmer;
        
        // IImpactEffect[] skimmerShipEffects;
        // IImpactEffect[] skimmerPrismEffects;
        // IImpactEffect[] skimmerElementalCrystalEffects;

        private void Awake()
        {
            // skimmerShipEffects = Array.ConvertAll(skimmerShipEffectsSO, so =>  so as IImpactEffect);
            // skimmerPrismEffects = Array.ConvertAll(skimmerPrismEffectsSO, so =>  so as IImpactEffect);
            // skimmerElementalCrystalEffects = Array.ConvertAll(skimmerElementalCrystalEffectsSO, so =>  so as IImpactEffect);
        }

        protected override void AcceptImpactee(IImpactor impactee)
        {    
            switch (impactee)
            {
                case ShipImpactor shipImpactee:
                    // ExecuteEffect(impactee, skimmerShipEffects);
                    if(!DoesEffectExist(skimmerShipEffects)) return;
                    foreach (var effect in skimmerShipEffects)
                    {
                        effect.Execute(this, shipImpactee);
                    }
                    skimmer.ExecuteImpactOnShip(shipImpactee.Ship);
                    break;
                case PrismImpactor prismImpactee:
                    // ExecuteEffect(impactee, skimmerPrismEffects);
                    if(!DoesEffectExist(skimmerPrismEffects)) return;
                    foreach (var effect in skimmerPrismEffects)
                    {
                        effect.Execute(this, prismImpactee);
                    }
                    skimmer.ExecuteImpactOnPrism(prismImpactee.Prism);
                    break;
                case ElementalCrystalImpactor elementalCrystalImpactee:
                    // ExecuteEffect(impactee, skimmerElementalCrystalEffects);
                    if(!DoesEffectExist(skimmerElementalCrystalEffects)) return;
                    foreach (var effect in skimmerElementalCrystalEffects)
                    {
                        effect.Execute(this, elementalCrystalImpactee);
                    }
                    break;
            }
        }
    }
}
using System;
using UnityEngine;

namespace CosmicShore.Game
{
    public class SkimmerImpactor : ImpactorBase
    {
        [SerializeField, RequireInterface(typeof(IImpactEffect))] 
        ScriptableObject[] skimmerShipEffectsSO;
        
        [SerializeField, RequireInterface(typeof(IImpactEffect))] 
        ScriptableObject[] skimmerPrismEffectsSO;
        
        [SerializeField, RequireInterface(typeof(IImpactEffect))] 
        ScriptableObject[] skimmerElementalCrystalEffectsSO;
        
        [SerializeField] private Skimmer skimmer;
        public Skimmer Skimmer => skimmer;

        IImpactEffect[] skimmerShipEffects;
        IImpactEffect[] skimmerPrismEffects;
        IImpactEffect[] skimmerElementalCrystalEffects;

        private void Awake()
        {
            skimmerShipEffects = Array.ConvertAll(skimmerShipEffectsSO, so =>  so as IImpactEffect);
            skimmerPrismEffects = Array.ConvertAll(skimmerPrismEffectsSO, so =>  so as IImpactEffect);
            skimmerElementalCrystalEffects = Array.ConvertAll(skimmerElementalCrystalEffectsSO, so =>  so as IImpactEffect);
        }

        protected override void AcceptImpactee(IImpactor impactee)
        {    
            switch (impactee)
            {
                case ShipImpactor shipImpactor:
                    ExecuteEffect(impactee, skimmerShipEffects);
                    skimmer.ExecuteImpactOnShip(shipImpactor.Ship);
                    break;
                case PrismImpactor prismImpactor:
                    ExecuteEffect(impactee, skimmerPrismEffects);
                    skimmer.ExecuteImpactOnPrism(prismImpactor.Prism);
                    break;
                case ElementalCrystalImpactor elementalCrystalImpactor:
                    ExecuteEffect(impactee, skimmerElementalCrystalEffects);
                    break;
            }
        }
    }
}
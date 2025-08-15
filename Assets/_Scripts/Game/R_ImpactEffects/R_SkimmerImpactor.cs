using System;
using UnityEngine;

namespace CosmicShore.Game
{
    public class R_SkimmerImpactor : R_ImpactorBase
    {
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))] 
        ScriptableObject[] skimmerShipEffectsSO;
        
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))] 
        ScriptableObject[] skimmerPrismEffectsSO;
        
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))] 
        ScriptableObject[] skimmerElementalCrystalEffectsSO;
        
        [SerializeField] private Skimmer skimmer;
        public Skimmer Skimmer => skimmer;

        R_IImpactEffect[] skimmerShipEffects;
        R_IImpactEffect[] skimmerPrismEffects;
        R_IImpactEffect[] skimmerElementalCrystalEffects;

        private void Awake()
        {
            skimmerShipEffects = Array.ConvertAll(skimmerShipEffectsSO, so =>  so as R_IImpactEffect);
            skimmerPrismEffects = Array.ConvertAll(skimmerPrismEffectsSO, so =>  so as R_IImpactEffect);
            skimmerElementalCrystalEffects = Array.ConvertAll(skimmerElementalCrystalEffectsSO, so =>  so as R_IImpactEffect);
        }

        protected override void AcceptImpactee(R_IImpactor impactee)
        {    
            switch (impactee)
            {
                case R_ShipImpactor shipImpactor:
                    ExecuteEffect(impactee, skimmerShipEffects);
                    skimmer.ExecuteImpactOnShip(shipImpactor.Ship);
                    break;
                case R_PrismImpactor prismImpactor:
                    ExecuteEffect(impactee, skimmerPrismEffects);
                    skimmer.ExecuteImpactOnPrism(prismImpactor.Prism);
                    break;
                case R_ElementalCrystalImpactor elementalCrystalImpactor:
                    ExecuteEffect(impactee, skimmerElementalCrystalEffects);
                    break;
            }
        }
    }
}
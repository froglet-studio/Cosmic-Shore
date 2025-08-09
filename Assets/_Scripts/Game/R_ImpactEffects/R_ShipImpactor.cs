using System;
using UnityEngine;

namespace CosmicShore.Game
{
    public class R_ShipImpactor : R_ImpactorBase
    {
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))]
        ScriptableObject[] shipPrismEffectsSO;
        
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))]
        ScriptableObject[] shipOmniCrystalEffectsSO;
        
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))]
        ScriptableObject[] shipElementalCrystalEffectsSO;
        
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))]
        ScriptableObject[] shipFakeCrystalEffectsSO;
        
        R_IImpactEffect[] shipPrismEffects;
        R_IImpactEffect[] shipOmniCrystalEffects;
        R_IImpactEffect[] shipElementalCrystalEffects;
        R_IImpactEffect[] shipFakeCrystalEffects;

        private void Awake()
        {
            shipPrismEffects = Array.ConvertAll(shipPrismEffectsSO, so => (R_IImpactEffect)so);
            shipOmniCrystalEffects = Array.ConvertAll(shipOmniCrystalEffectsSO, so => (R_IImpactEffect)so);
            shipElementalCrystalEffects = Array.ConvertAll(shipElementalCrystalEffectsSO, so => (R_IImpactEffect)so);
            shipFakeCrystalEffects = Array.ConvertAll(shipFakeCrystalEffectsSO, so => (R_IImpactEffect)so);
        }

        protected override void AcceptImpactee(R_IImpactor impactee)
        {
            switch (impactee)
            {
                case R_PrismImpactor prismImpactor:
                    ExecuteEffect(impactee, shipPrismEffects);
                    break;
                case R_OmniCrystalImpactor omniCrystalImpactor:
                    ExecuteEffect(impactee, shipOmniCrystalEffects);
                    break;
                case R_ElementalCrystalImpactor elementalCrystalImpactor:
                    ExecuteEffect(impactee, shipElementalCrystalEffects);
                    break;
                case R_FakeCrystalImpactor fakeCrystalImpactor:
                    ExecuteEffect(impactee, shipFakeCrystalEffects);
                    break;
            }
        }
    }
}
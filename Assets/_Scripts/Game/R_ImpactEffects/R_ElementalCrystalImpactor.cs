using System;
using UnityEngine;

namespace CosmicShore.Game
{
    public class R_ElementalCrystalImpactor : R_CrystalImpactor
    {
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))]
        ScriptableObject[] elementalCrystalShipEffectsSO;
        
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))]
        ScriptableObject[] elementalCrystalSkimmerEffectsSO;
        
        R_IImpactEffect[] elementalCrystalShipEffects;
        R_IImpactEffect[] elementalCrystalSkimmerEffects;

        void Awake()
        {
            elementalCrystalShipEffects = Array.ConvertAll(elementalCrystalShipEffectsSO, so => so as R_IImpactEffect);
            elementalCrystalSkimmerEffects = Array.ConvertAll(elementalCrystalSkimmerEffectsSO, so => so as R_IImpactEffect);
        }
        
        protected override void AcceptImpactee(R_IImpactor impactee)
        {
            switch (impactee)
            {
                case R_ShipImpactor shipImpactor:
                    ExecuteEffect(impactee, elementalCrystalShipEffects);
                    break;
                case R_SkimmerImpactor skimmerImpactor:
                    ExecuteEffect(impactee, elementalCrystalSkimmerEffects);
                    break;
            }
        }
    }
}
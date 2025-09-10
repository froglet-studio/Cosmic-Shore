using System;
using UnityEngine;

namespace CosmicShore.Game
{
    public class ElementalCrystalImpactor : CrystalImpactor
    {
        // [SerializeField, RequireInterface(typeof(IImpactEffect))]
        // ScriptableObject[] elementalCrystalShipEffectsSO;
        
        // [SerializeField, RequireInterface(typeof(IImpactEffect))]
        // ScriptableObject[] elementalCrystalSkimmerEffectsSO;
        
        VesselCrystalEffectSO[] vesselCrystalEffects;
        SkimmerCrystalEffectSO[] skimmerCrystalEffects;
        
        // IImpactEffect[] elementalCrystalShipEffects;
        // IImpactEffect[] elementalCrystalSkimmerEffects;

        protected virtual void Awake()
        {
            base.Awake();
        }
        
        protected override void AcceptImpactee(IImpactor impactee)
        {
            switch (impactee)
            {
                case VesselImpactor shipImpactee:
                    // ExecuteEffect(impactee, elementalCrystalShipEffects);
                    if (!DoesEffectExist(vesselCrystalEffects)) return;
                    foreach (var effect in vesselCrystalEffects)
                    {
                        effect.Execute(shipImpactee,this);
                    }
                    break;
                case SkimmerImpactor skimmerImpactee:
                    // ExecuteEffect(impactee, elementalCrystalSkimmerEffects);
                    if (!DoesEffectExist(skimmerCrystalEffects)) return;
                    foreach (var effect in skimmerCrystalEffects)
                    {
                        effect.Execute(skimmerImpactee,this);
                    }
                    break;
            }
        }
    }
}
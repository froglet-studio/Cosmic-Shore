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
        
        ElementalCrystalShipEffectSO[] elementalCrystalShipEffects;
        ElementalCrystalSkimmerEffectSO[] elementalCrystalSkimmerEffects;
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
                    if (!DoesEffectExist(elementalCrystalShipEffects)) return;
                    foreach (var effect in elementalCrystalShipEffects)
                    {
                        effect.Execute(this, shipImpactee);
                    }
                    break;
                case SkimmerImpactor skimmerImpactee:
                    // ExecuteEffect(impactee, elementalCrystalSkimmerEffects);
                    if (!DoesEffectExist(elementalCrystalSkimmerEffects)) return;
                    foreach (var effect in elementalCrystalSkimmerEffects)
                    {
                        effect.Execute(this, skimmerImpactee);
                    }
                    break;
            }
        }
    }
}
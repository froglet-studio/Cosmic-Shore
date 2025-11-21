using System;
using UnityEngine;

namespace CosmicShore.Game
{
    public class ElementalCrystalImpactor : CrystalImpactor
    {
        VesselCrystalEffectSO[] vesselCrystalEffects;
        SkimmerCrystalEffectSO[] skimmerCrystalEffects;
        
        protected override void AcceptImpactee(IImpactor impactee)
        {
            switch (impactee)
            {
                case VesselImpactor shipImpactee:
                    if (!DoesEffectExist(vesselCrystalEffects)) return;
                    CrystalImpactData data = CrystalImpactData.FromCrystal(Crystal);
                    foreach (var effect in vesselCrystalEffects)
                    {
                        effect.Execute(shipImpactee, data);
                    }
                    break;
                case SkimmerImpactor skimmerImpactee:
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
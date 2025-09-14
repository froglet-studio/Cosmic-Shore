using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(IVessel))]
    public class VesselImpactor : ImpactorBase
    {
        [FormerlySerializedAs("shipPrismEffects")] [SerializeField]
        VesselPrismEffectSO[] vesselPrismEffects;
        
        [FormerlySerializedAs("vesselOmniCrystalEffects")] [FormerlySerializedAs("shipOmniCrystalEffects")] [SerializeField]
        VesselCrystalEffectSO[] vesselCrystalEffects;
        
        public IVessel Vessel { get; private set; }
        
        private void Awake()
        {
            Vessel ??= GetComponent<IVessel>();
        }

        protected override void AcceptImpactee(IImpactor impactee)
        {
            switch (impactee)
            {
                case PrismImpactor prismImpactee:
                   // ExecuteEffect(impactee, vesselPrismEffects);
                   if(!DoesEffectExist(vesselPrismEffects)) return;
                   foreach (var effect in vesselPrismEffects)
                   {
                       effect.Execute(this, prismImpactee);
                   }
                   break;
                case OmniCrystalImpactor omniCrystalImpactee:
                    //ExecuteEffect(impactee, vesselCrystalEffects);
                    if(!DoesEffectExist(vesselCrystalEffects)) return;
                    foreach (var effect in vesselCrystalEffects)
                    {
                        effect.Execute(this, omniCrystalImpactee);
                    }
                    break;
            }
        }

        private void Reset()
        { 
            Vessel ??= GetComponent<IVessel>();
        }
    }
}
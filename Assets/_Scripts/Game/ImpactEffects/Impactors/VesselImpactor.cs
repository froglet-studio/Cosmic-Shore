using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(IVessel))]
    public class VesselImpactor : ImpactorBase
    {
        [SerializeField] VesselImpactorDataContainerSO vesselImpactorDataContainerSO;
        
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
                   if(!DoesEffectExist(vesselImpactorDataContainerSO.VesselCrystalEffects)) return;
                   foreach (var effect in vesselImpactorDataContainerSO.VesselPrismEffects)
                   {
                       effect.Execute(this, prismImpactee);
                   }
                   break;
                case OmniCrystalImpactor omniCrystalImpactee:
                    if(!DoesEffectExist(vesselImpactorDataContainerSO.VesselCrystalEffects)) return;
                    foreach (var effect in vesselImpactorDataContainerSO.VesselCrystalEffects)
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
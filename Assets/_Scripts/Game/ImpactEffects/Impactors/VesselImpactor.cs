using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(IShip))]
    public class VesselImpactor : ImpactorBase
    {
        // [FormerlySerializedAs("shipPrismEffects")] [SerializeField]
        // VesselPrismEffectSO[] vesselPrismEffects;
        //
        // [FormerlySerializedAs("vesselOmniCrystalEffects")] [FormerlySerializedAs("shipOmniCrystalEffects")] [SerializeField]
        // VesselCrystalEffectSO[] vesselCrystalEffects;

        [SerializeField] VesselImpactorDataContainerSO vesselImpactorDataContainerSO;
        
        public IShip Ship { get; private set; }
        
        private void Awake()
        {
            Ship ??= GetComponent<IShip>();
        }

        protected override void AcceptImpactee(IImpactor impactee)
        {
            switch (impactee)
            {
                case PrismImpactor prismImpactee:
                   // ExecuteEffect(impactee, vesselPrismEffects);
                   if(!DoesEffectExist(vesselImpactorDataContainerSO.VesselCrystalEffects)) return;
                   foreach (var effect in vesselImpactorDataContainerSO.VesselPrismEffects)
                   {
                       effect.Execute(this, prismImpactee);
                   }
                   break;
                case OmniCrystalImpactor omniCrystalImpactee:
                    //ExecuteEffect(impactee, vesselCrystalEffects);
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
            Ship ??= GetComponent<IShip>();
        }
    }
}
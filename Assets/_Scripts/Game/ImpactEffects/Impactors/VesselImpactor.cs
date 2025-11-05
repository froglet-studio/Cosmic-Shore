using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(IVessel))]
    [RequireComponent(typeof(NetworkVesselImpactor))]
    public class VesselImpactor : ImpactorBase
    {
        [SerializeField] VesselImpactorDataContainerSO vesselImpactorDataContainerSO;
        [SerializeField] NetworkVesselImpactor networkVesselImpactor;
        
        public IVessel Vessel { get; private set; }
        
        private void Awake()
        {
            Vessel ??= GetComponent<IVessel>();
            networkVesselImpactor ??= GetComponent<NetworkVesselImpactor>();
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
                    if (networkVesselImpactor.IsSpawned && networkVesselImpactor.IsOwner)
                        networkVesselImpactor.ExecuteOnHitOmniCrystal();
                    else
                        ExecuteCrystalImpact_Old(omniCrystalImpactee);
                    break;
            }
        }

        internal void ExecuteCrystalImpact(CrystalImpactData data)
        {
            if(!DoesEffectExist(vesselImpactorDataContainerSO.VesselCrystalEffects)) return;
            foreach (var effect in vesselImpactorDataContainerSO.VesselCrystalEffects)
                effect.Execute(this, data);
        }

        void ExecuteCrystalImpact_Old(OmniCrystalImpactor impactee)
        {
            if(!DoesEffectExist(vesselImpactorDataContainerSO.VesselCrystalEffects)) return;
            foreach (var effect in vesselImpactorDataContainerSO.VesselCrystalEffects)
                effect.Execute(this, impactee);
        }

        void OnValidate()
        { 
            Vessel ??= GetComponent<IVessel>();
            networkVesselImpactor ??= GetComponent<NetworkVesselImpactor>();
        }
    }
}
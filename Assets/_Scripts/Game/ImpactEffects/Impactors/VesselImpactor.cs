using Unity.Netcode;
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
                    if (IsSpawned && IsOwner)
                    {
                        ExecuteCrystalImpact_ServerRpc(new CrystalImpactData());
                    }
                    else
                        ExecuteCrystalImpact_Old(omniCrystalImpactee);
                    break;
            }
        }

        [ServerRpc]
        void ExecuteCrystalImpact_ServerRpc(CrystalImpactData data) =>
            ExecuteCrystalImpact_ClientRpc(data);

        [ClientRpc]
        void ExecuteCrystalImpact_ClientRpc(CrystalImpactData data) =>
            ExecuteCrystalImpact(data);

        void ExecuteCrystalImpact(CrystalImpactData data)
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

        void Reset()
        { 
            Vessel ??= GetComponent<IVessel>();
        }
    }
}
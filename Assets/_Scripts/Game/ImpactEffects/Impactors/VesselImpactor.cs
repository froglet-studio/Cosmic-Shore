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
        public override Domains OwnDomain => Vessel.VesselStatus.Domain;

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
                    if (!DoesEffectExist(vesselImpactorDataContainerSO.VesselPrismEffects)) return;
                    foreach (var effect in vesselImpactorDataContainerSO.VesselPrismEffects)
                        effect.Execute(this, prismImpactee);
                    break;

                case OmniCrystalImpactor omniCrystalImpactee:
                {
                    var data = CrystalImpactData.FromCrystal(omniCrystalImpactee.Crystal);
                    if (networkVesselImpactor.IsSpawned && networkVesselImpactor.IsOwner)
                        networkVesselImpactor.ExecuteOnHitOmniCrystal(data);
                    else
                        ExecuteOmniCrystalImpact(data);
                    break;
                }

                // ✅ NEW: Elemental crystal path
                case ElementalCrystalImpactor elementalCrystalImpactee:
                {
                    var data = CrystalImpactData.FromCrystal(elementalCrystalImpactee.Crystal);
                    if (networkVesselImpactor.IsSpawned && networkVesselImpactor.IsOwner)
                        networkVesselImpactor.ExecuteOnHitElementalCrystal(data);
                    else
                        ExecuteElementalCrystalImpact(data);
                    break;
                }

                case SkimmerImpactor skimmerImpactee:
                    if (!DoesEffectExist(vesselImpactorDataContainerSO.VesselSkimmerEffects)) return;
                    foreach (var effect in vesselImpactorDataContainerSO.VesselSkimmerEffects)
                        effect.Execute(this, skimmerImpactee);
                    break;
            }
        }

        public void ExecuteOmniCrystalImpact(CrystalImpactData data)
        {
            if (!DoesEffectExist(vesselImpactorDataContainerSO.VesselCrystalEffects)) return;
            foreach (var effect in vesselImpactorDataContainerSO.VesselCrystalEffects)
                effect.Execute(this, data);
        }

        public void ExecuteElementalCrystalImpact(CrystalImpactData data)
        {
            VesselCrystalEffectSO[] effects = data.Element switch
            {
                Element.Mass   => vesselImpactorDataContainerSO.VesselMassCrystalEffects,
                Element.Charge => vesselImpactorDataContainerSO.VesselChargeCrystalEffects,
                Element.Space  => vesselImpactorDataContainerSO.VesselSpaceCrystalEffects,
                Element.Time   => vesselImpactorDataContainerSO.VesselTimeCrystalEffects,
                _ => null
            };

            if (!DoesEffectExist(effects)) return;

            foreach (var effect in effects)
                effect.Execute(this, data);
        }


        void OnValidate()
        {
            Vessel ??= GetComponent<IVessel>();
            networkVesselImpactor ??= GetComponent<NetworkVesselImpactor>();
        }
    }
}
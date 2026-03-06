using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;
using CosmicShore.Gameplay;
namespace CosmicShore.Gameplay
{
    [RequireComponent(typeof(IVessel))]
    [RequireComponent(typeof(NetworkVesselImpactor))]
    public class VesselImpactor : ImpactorBase
    {
        [SerializeField] ScriptableEventGameplaySFX gameplaySFXEvent;
        [SerializeField] VesselImpactorDataContainerSO vesselImpactorDataContainerSO;
        [SerializeField] NetworkVesselImpactor networkVesselImpactor;

        public IVessel Vessel { get; private set; }
        protected override bool isInitialized => Vessel?.VesselStatus?.Player != null;
        public override Domains OwnDomain => Vessel.VesselStatus.Domain;

        private void Awake()
        {
            Vessel ??= GetComponent<IVessel>();
            networkVesselImpactor ??= GetComponent<NetworkVesselImpactor>();
        }

        protected override void AcceptImpactee(IImpactor impactee)
        {
            if (vesselImpactorDataContainerSO == null)
                return;

            switch (impactee)
            {
                case PrismImpactor prismImpactee:
                    if (!DoesEffectExist(vesselImpactorDataContainerSO.VesselPrismEffects)) return;
                    gameplaySFXEvent.Raise(GameplaySFXCategory.VesselImpact);
                    foreach (var effect in vesselImpactorDataContainerSO.VesselPrismEffects)
                    {
                        if (effect == null) continue;
                        effect.Execute(this, prismImpactee);
                    }
                    break;

                case OmniCrystalImpactor omniCrystalImpactee:
                {
                    gameplaySFXEvent.Raise(GameplaySFXCategory.CrystalCollect);
                    var data = CrystalImpactData.FromCrystal(omniCrystalImpactee.Crystal);
                    if (networkVesselImpactor.IsSpawned && networkVesselImpactor.IsOwner)
                        networkVesselImpactor.ExecuteOnHitOmniCrystal(data);
                    else
                        ExecuteOmniCrystalImpact(data);
                    break;
                }

                case ElementalCrystalImpactor elementalCrystalImpactee:
                {
                    gameplaySFXEvent.Raise(GameplaySFXCategory.CrystalCollect);
                    var data = CrystalImpactData.FromCrystal(elementalCrystalImpactee.Crystal);
                    if (networkVesselImpactor.IsSpawned && networkVesselImpactor.IsOwner)
                        networkVesselImpactor.ExecuteOnHitElementalCrystal(data);
                    else
                        ExecuteElementalCrystalImpact(data);
                    break;
                }

                case SkimmerImpactor skimmerImpactee:
                    if (!DoesEffectExist(vesselImpactorDataContainerSO.VesselSkimmerEffects)) return;
                    gameplaySFXEvent.Raise(GameplaySFXCategory.VesselImpact);
                    foreach (var effect in vesselImpactorDataContainerSO.VesselSkimmerEffects)
                    {
                        if (effect == null) continue;
                        effect.Execute(this, skimmerImpactee);
                    }
                    break;
            }
        }

        public void ExecuteOmniCrystalImpact(CrystalImpactData data)
        {
            if (vesselImpactorDataContainerSO == null) return;
            if (!DoesEffectExist(vesselImpactorDataContainerSO.VesselCrystalEffects)) return;
            foreach (var effect in vesselImpactorDataContainerSO.VesselCrystalEffects)
            {
                if (effect == null) continue;
                effect.Execute(this, data);
            }
        }

        public void ExecuteElementalCrystalImpact(CrystalImpactData data)
        {
            if (vesselImpactorDataContainerSO == null) return;

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
            {
                if (effect == null) continue;
                effect.Execute(this, data);
            }
        }


        void OnValidate()
        {
            Vessel ??= GetComponent<IVessel>();
            networkVesselImpactor ??= GetComponent<NetworkVesselImpactor>();
        }
    }
}
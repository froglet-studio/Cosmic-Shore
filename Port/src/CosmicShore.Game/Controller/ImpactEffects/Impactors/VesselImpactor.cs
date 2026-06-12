using CosmicShore.Core;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
namespace CosmicShore.Gameplay
{
    [RequireComponent(typeof(IVessel))]
    [RequireComponent(typeof(NetworkVesselImpactor))]
    public class VesselImpactor : ImpactorBase
    {
        [Inject] AudioSystem audioSystem;
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
            switch (impactee)
            {
                case PrismImpactor prismImpactee:
                    if (!DoesEffectExist(vesselImpactorDataContainerSO.VesselPrismEffects)) return;
                    audioSystem?.PlayGameplaySFX(GameplaySFXCategory.VesselImpact, transform.position);
                    foreach (var effect in vesselImpactorDataContainerSO.VesselPrismEffects)
                        effect.Execute(this, prismImpactee);
                    break;

                // PORT Deviation (V19, restore when OmniCrystalImpactor ports — its body needs the full
                // Crystal (CrystalManager/Respawn/explode pipeline), still a V11 type shell):
                // case OmniCrystalImpactor omniCrystalImpactee:
                // {
                //     audioSystem?.PlayGameplaySFX(GameplaySFXCategory.CrystalCollect, transform.position);
                //     var data = CrystalImpactData.FromCrystal(omniCrystalImpactee.Crystal);
                //     if (networkVesselImpactor.IsSpawned && networkVesselImpactor.IsOwner)
                //         networkVesselImpactor.ExecuteOnHitOmniCrystal(data);
                //     else
                //         ExecuteOmniCrystalImpact(data);
                //     break;
                // }

                // PORT Deviation (V19, restore when ElementalCrystalImpactor ports — its body needs the full
                // Crystal (CrystalModels/DestroyCrystal/space-collect animation), still a V11 type shell):
                // case ElementalCrystalImpactor elementalCrystalImpactee:
                // {
                //     audioSystem?.PlayGameplaySFX(GameplaySFXCategory.CrystalCollect, transform.position);
                //     var data = CrystalImpactData.FromCrystal(elementalCrystalImpactee.Crystal);
                //     if (networkVesselImpactor.IsSpawned && networkVesselImpactor.IsOwner)
                //         networkVesselImpactor.ExecuteOnHitElementalCrystal(data);
                //     else
                //         ExecuteElementalCrystalImpact(data);
                //     break;
                // }

                case SkimmerImpactor skimmerImpactee:
                    if (!DoesEffectExist(vesselImpactorDataContainerSO.VesselSkimmerEffects)) return;
                    audioSystem?.PlayGameplaySFX(GameplaySFXCategory.VesselImpact, transform.position);
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

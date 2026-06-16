using CosmicShore.Core;
using Reflex.Attributes;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    [RequireComponent(typeof(IVessel))]
    [RequireComponent(typeof(NetworkVesselImpactor))]
    public class VesselImpactor : ImpactorBase
    {
        [Inject] AudioSystem audioSystem;
        [Inject] GameDataSO gameData;
        [SerializeField] VesselImpactorDataContainerSO vesselImpactorDataContainerSO;
        [SerializeField] NetworkVesselImpactor networkVesselImpactor;

        public IVessel Vessel { get; private set; }
        protected override bool isInitialized => Vessel?.VesselStatus?.Player != null;
        public override Domains OwnDomain => Vessel.VesselStatus.Domain;

        private void Awake()
        {
            Vessel ??= GetComponent<IVessel>();
            networkVesselImpactor ??= GetComponent<NetworkVesselImpactor>();
            audioSystem ??= AudioSystem.Instance != null
                ? AudioSystem.Instance
                : FindFirstObjectByType<AudioSystem>();
        }

        protected override void AcceptImpactee(IImpactor impactee)
        {
            switch (impactee)
            {
                case PrismImpactor prismImpactee:
                    if (!DoesEffectExist(vesselImpactorDataContainerSO.VesselPrismEffects)) return;
                    // HexRace's track is built from indestructible, environment-owned prisms
                    // (no player name) rather than destructible player trails. Hitting the
                    // track gets its own SFX in HexRace; every other prism collision (and all
                    // other modes) keeps the standard VesselImpact sound.
                    bool isTrackImpact =
                        gameData != null
                        && gameData.GameMode == GameModes.HexRace
                        && prismImpactee.Prism != null
                        && prismImpactee.Prism.IsEnvironmentOwned;
                    audioSystem?.PlayGameplaySFX(
                        isTrackImpact ? GameplaySFXCategory.TrackImpact : GameplaySFXCategory.VesselImpact,
                        transform.position);
                    foreach (var effect in vesselImpactorDataContainerSO.VesselPrismEffects)
                        effect.Execute(this, prismImpactee);
                    break;

                case OmniCrystalImpactor omniCrystalImpactee:
                {
                    audioSystem?.PlayGameplaySFX(GameplaySFXCategory.CrystalCollect, transform.position);
                    var data = CrystalImpactData.FromCrystal(omniCrystalImpactee.Crystal);
                    if (networkVesselImpactor.IsSpawned && networkVesselImpactor.IsOwner)
                        networkVesselImpactor.ExecuteOnHitOmniCrystal(data);
                    else
                        ExecuteOmniCrystalImpact(data);
                    break;
                }

                case ElementalCrystalImpactor elementalCrystalImpactee:
                {
                    audioSystem?.PlayGameplaySFX(GameplaySFXCategory.CrystalCollect, transform.position);
                    var data = CrystalImpactData.FromCrystal(elementalCrystalImpactee.Crystal);
                    if (networkVesselImpactor.IsSpawned && networkVesselImpactor.IsOwner)
                        networkVesselImpactor.ExecuteOnHitElementalCrystal(data);
                    else
                        ExecuteElementalCrystalImpact(data);
                    break;
                }

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

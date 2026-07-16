using System.Collections.Generic;
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
        // Vessels can carry more than one hull collider (the Squirrel has two
        // BoxColliders), so a single crystal pass raises OnTriggerEnter once per
        // collider pair and the whole crystal effect chain used to run twice.
        // Latch per crystal instance for the same window the crystal itself
        // holds IsExploding (Crystal.WaitForImpact) so the chain fires once.
        const float CrystalImpactLatchSeconds = 0.5f;

        [Inject] AudioSystem audioSystem;
        [Inject] GameDataSO gameData;
        [SerializeField] VesselImpactorDataContainerSO vesselImpactorDataContainerSO;
        [SerializeField] NetworkVesselImpactor networkVesselImpactor;

        readonly Dictionary<int, float> _lastCrystalImpactTime = new();

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
                    if (!TryLatchCrystalImpact(omniCrystalImpactee.Crystal)) break;
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
                    // A LIVING lifeform's embedded crystal (its heart) is a JOUST surface, not a
                    // pickup: skip the collect chain and run the lifeform-crystal effects instead
                    // (e.g. Squirrel Space-5 Crystal Joust). Local-only - the ecosystem sim is local.
                    if (elementalCrystalImpactee.Crystal && elementalCrystalImpactee.Crystal.IsEmbedded)
                    {
                        if (!TryLatchCrystalImpact(elementalCrystalImpactee.Crystal)) break;
                        if (!DoesEffectExist(vesselImpactorDataContainerSO.VesselLifeformCrystalEffects)) break;
                        foreach (var effect in vesselImpactorDataContainerSO.VesselLifeformCrystalEffects)
                            effect.Execute(this, elementalCrystalImpactee.Crystal);
                        break;
                    }

                    if (!TryLatchCrystalImpact(elementalCrystalImpactee.Crystal)) break;
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

        /// <summary>
        /// True exactly once per crystal per latch window — false for the
        /// duplicate hits a multi-collider hull generates on the same crystal.
        /// </summary>
        bool TryLatchCrystalImpact(Crystal crystal)
        {
            if (!crystal) return true; // no identity to latch on — let it through

            int id = crystal.GetInstanceID();
            float now = Time.time;
            if (_lastCrystalImpactTime.TryGetValue(id, out var last)
                && now - last < CrystalImpactLatchSeconds)
                return false;

            _lastCrystalImpactTime[id] = now;
            return true;
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
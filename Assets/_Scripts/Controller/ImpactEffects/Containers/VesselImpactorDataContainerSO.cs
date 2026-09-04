using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "VesselImpactorDataContainer",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Container/VesselImpactorDataContainerSO")]
    public class VesselImpactorDataContainerSO : ScriptableObject
    {
        public VesselPrismEffectSO[] VesselPrismEffects => vesselPrismEffects;
        public VesselCrystalEffectSO[] VesselCrystalEffects => vesselCrystalEffects;
        public VesselCrystalEffectSO[] VesselMassCrystalEffects => vesselMassCrystalEffects;
        public VesselCrystalEffectSO[] VesselChargeCrystalEffects => vesselChargeCrystalEffects;
        public VesselCrystalEffectSO[] VesselSpaceCrystalEffects => vesselSpaceCrystalEffects;
        public VesselCrystalEffectSO[] VesselTimeCrystalEffects => vesselTimeCrystalEffects;

        public VesselSkimmerEffectsSO[] VesselSkimmerEffects => vesselSkimmerEffects;

        /// <summary>
        /// Effects run when this vessel impacts a LIVING lifeform's embedded crystal (its heart)
        /// instead of the normal collect chain - e.g. the Squirrel's Space level-5 Crystal Joust.
        /// Empty on vessels with no lifeform-crystal interaction.
        /// </summary>
        public VesselLifeformCrystalEffectSO[] VesselLifeformCrystalEffects => vesselLifeformCrystalEffects;

        [FormerlySerializedAs("shipPrismEffects")]
        [SerializeField] VesselPrismEffectSO[] vesselPrismEffects;

        [FormerlySerializedAs("vesselOmniCrystalEffects")]
        [FormerlySerializedAs("shipOmniCrystalEffects")]
        [SerializeField] VesselCrystalEffectSO[] vesselCrystalEffects;
        
        [SerializeField] private VesselCrystalEffectSO[] vesselMassCrystalEffects;
        [SerializeField] private VesselCrystalEffectSO[] vesselChargeCrystalEffects;
        [SerializeField] private VesselCrystalEffectSO[] vesselSpaceCrystalEffects;
        [SerializeField] private VesselCrystalEffectSO[] vesselTimeCrystalEffects;

        [SerializeField] VesselSkimmerEffectsSO[] vesselSkimmerEffects;

        [SerializeField] VesselLifeformCrystalEffectSO[] vesselLifeformCrystalEffects;
    }
}
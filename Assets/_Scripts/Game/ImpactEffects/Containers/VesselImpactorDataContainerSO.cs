using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game
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
    }
}
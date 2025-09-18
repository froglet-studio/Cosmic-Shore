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
        
        [FormerlySerializedAs("shipPrismEffects")] [SerializeField]
        VesselPrismEffectSO[] vesselPrismEffects;
        
        [FormerlySerializedAs("vesselOmniCrystalEffects")] [FormerlySerializedAs("shipOmniCrystalEffects")] [SerializeField]
        VesselCrystalEffectSO[] vesselCrystalEffects;
    }
}
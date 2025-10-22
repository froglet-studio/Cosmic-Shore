using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SkimmerImpactorDataContainer",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Container/SkimmerImpactorDataContainerSO")]
    public class SkimmerImpactorDataContainerSO : ScriptableObject
    {
        public VesselSkimmerEffectsSO[] VesselSkimmerEffects => vesselSkimmerEffectsSO;
        public SkimmerPrismEffectSO[] SkimmerPrismEffects => skimmerPrismEffectsSO;
        public SkimmerCrystalEffectSO[] SkimmerCrystalEffects => skimmerCrystalEffectsSO;

        
        [SerializeField] VesselSkimmerEffectsSO[] vesselSkimmerEffectsSO;
        [SerializeField] SkimmerPrismEffectSO[] skimmerPrismEffectsSO;
        [SerializeField] SkimmerCrystalEffectSO[] skimmerCrystalEffectsSO;
    }
}
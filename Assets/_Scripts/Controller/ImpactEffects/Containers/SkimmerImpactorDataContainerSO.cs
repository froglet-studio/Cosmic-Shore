using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "SkimmerImpactorDataContainer",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Container/SkimmerImpactorDataContainerSO")]
    public class SkimmerImpactorDataContainerSO : ScriptableObject
    {
        public VesselSkimmerEffectsSO[] VesselSkimmerEffects => vesselSkimmerEffectsSO;
        public SkimmerPrismEffectSO[] SkimmerPrismEffects => skimmerPrismEffectsSO;
        public SkimmerCrystalEffectSO[] SkimmerCrystalEffects => skimmerCrystalEffectsSO;

        /// <summary>Effects run when this skimmer sweeps a LIVING lifeform's embedded heart.
        /// Deliberately a separate list from <see cref="SkimmerCrystalEffects"/> — a heart is not
        /// a pickup, and the crystal arm declines it on purpose. Empty on every vessel but the
        /// Butterfly, so the arm is a no-op fleet-wide.</summary>
        public SkimmerLifeformCrystalEffectSO[] SkimmerLifeformCrystalEffects => skimmerLifeformCrystalEffectsSO;

        
        [SerializeField] VesselSkimmerEffectsSO[] vesselSkimmerEffectsSO;
        [SerializeField] SkimmerPrismEffectSO[] skimmerPrismEffectsSO;
        [SerializeField] SkimmerCrystalEffectSO[] skimmerCrystalEffectsSO;
        [SerializeField] SkimmerLifeformCrystalEffectSO[] skimmerLifeformCrystalEffectsSO;
    }
}
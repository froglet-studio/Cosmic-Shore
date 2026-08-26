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
        /// This vessel's BESPOKE omni-crystal retirement — the animation that replaces the
        /// shared husk spray when this hull collects an omni crystal (the Squirrel's
        /// crystal-to-boost-ring morph). Empty on a vessel that has not been given one yet;
        /// that vessel keeps the shared explosion, so the fleet migrates one hull at a time.
        ///
        /// It is a single slot rather than an array because a crystal is retired ONCE: two
        /// retirements would both claim the crystal's body and draw over each other. Extra
        /// per-vessel flourishes belong in <see cref="VesselCrystalEffects"/> alongside it.
        /// </summary>
        public VesselOmniCrystalRetirementSO OmniCrystalRetirement => omniCrystalRetirement;

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

        [Tooltip("Bespoke omni-crystal retirement for THIS vessel - the animation that plays " +
                 "instead of the shared husk spray. Leave empty to keep the shared explosion.")]
        [SerializeField] VesselOmniCrystalRetirementSO omniCrystalRetirement;
    }
}
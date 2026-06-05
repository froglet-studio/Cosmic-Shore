using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Elemental crystal powerup: increases the collecting vessel's level for the
    /// crystal's own element (Charge / Mass / Space / Time), scaled by the crystal's
    /// world scale — bigger crystals grant bigger boosts.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SkimmerAdjustElementLevelByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Crystal/SkimmerAdjustElementLevelByCrystalEffectSO")]
    public class SkimmerAdjustElementLevelByCrystalEffectSO : SkimmerCrystalEffectSO
    {
        [Header("Scale → Level Mapping")]
        [Tooltip("Normalized element level gained per unit of crystal world scale. " +
                 "0.1 = one integer level (one petal tick) per unit of scale.")]
        [SerializeField] float levelPerUnitScale = 0.1f;

        [Tooltip("Upper bound on the normalized level gain from a single crystal, " +
                 "so runaway-grown flora crystals can't max an element in one pickup.")]
        [SerializeField] float maxLevelGainPerCrystal = 0.5f;

        public float LevelPerUnitScale => levelPerUnitScale;
        public float MaxLevelGainPerCrystal => maxLevelGainPerCrystal;

        public override void Execute(SkimmerImpactor impactor, CrystalImpactor crystalImpactee)
        {
            var resourceSystem = impactor?.Skimmer?.VesselStatus?.ResourceSystem;
            var crystal = crystalImpactee?.Crystal;
            if (!resourceSystem || !crystal) return;

            // Only the four elemental crystals grant element-specific powerups.
            if (!crystal.crystalProperties.IsElemental) return;

            float gain = ComputeLevelGain(
                crystal.transform.lossyScale.x, levelPerUnitScale, maxLevelGainPerCrystal);
            resourceSystem.AdjustLevel(crystal.crystalProperties.Element, gain);
        }

        /// <summary>
        /// Maps crystal world scale to a normalized element level gain.
        /// Pure so edit-mode tests can validate the mapping.
        /// </summary>
        public static float ComputeLevelGain(float crystalScale, float levelPerUnitScale, float maxLevelGain)
            => Mathf.Min(Mathf.Abs(crystalScale) * levelPerUnitScale, maxLevelGain);
    }
}

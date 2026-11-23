using CosmicShore.Core;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Elemental crystal powerup: raises the collecting vessel's level in the crystal's own
    /// element (Charge / Mass / Space / Time) by exactly ONE PETAL, whatever the crystal is.
    ///
    /// <para>Every elemental crystal is one size (<see cref="CosmicShore.ScriptableObjects.ElementalCrystalSetSO.CrystalWorldScale"/>)
    /// and one petal (Garrett, 2026-10-26), so the reward reads nothing off the crystal. It used to
    /// be <c>lossyScale x levelPerUnitScale</c>, capped: a heart paid what its lifeform's size said,
    /// and a mode's scatter paid 0.075-0.18 of a petal - a quantity no flower can show. A petal
    /// knocked out of a hull (<see cref="ElementalCrystalEjector"/>) is therefore returned whole,
    /// and the economy closes by construction rather than by three numbers agreeing.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "SkimmerAdjustElementLevelByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Crystal/SkimmerAdjustElementLevelByCrystalEffectSO")]
    public class SkimmerAdjustElementLevelByCrystalEffectSO : SkimmerCrystalEffectSO
    {
        /// <summary>The normalized level one elemental crystal grants: one petal, exactly.</summary>
        public const float LevelGainPerCrystal = ResourceSystem.PetalNormalized;

        public override void Execute(SkimmerImpactor impactor, CrystalImpactor crystalImpactee)
        {
            var resourceSystem = impactor?.Skimmer?.VesselStatus?.ResourceSystem;
            var crystal = crystalImpactee?.Crystal;
            if (!resourceSystem || !crystal) return;

            // Only the four elemental crystals grant element-specific powerups.
            if (!crystal.crystalProperties.IsElemental) return;

            resourceSystem.AdjustLevel(crystal.crystalProperties.Element, LevelGainPerCrystal);

            // Play per-element audio for the collecting vessel if it belongs to the local player.
            if (impactor.Skimmer.VesselStatus.IsLocalUser)
            {
                var category = crystal.crystalProperties.Element switch
                {
                    Element.Charge => GameplaySFXCategory.ElementChargeReceived,
                    Element.Mass   => GameplaySFXCategory.ElementMassReceived,
                    Element.Space  => GameplaySFXCategory.ElementSpaceReceived,
                    Element.Time   => GameplaySFXCategory.ElementTimeReceived,
                    _              => (GameplaySFXCategory)(-1),
                };
                if ((int)category != -1)
                    AudioSystem.Instance?.PlayGameplaySFX(category);
            }
        }
    }
}

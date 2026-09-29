using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(
        fileName = "SkimmerSFXByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Crystal/SkimmerSFXByCrystalEffectSO")]
    public class SkimmerSFXByCrystalEffectSO : SkimmerCrystalEffectSO
    {
        [Tooltip("Which gameplay SFX category to play when the skimmer touches a crystal.")]
        [SerializeField] private GameplaySFXCategory category = GameplaySFXCategory.CrystalSkim;

        public override void Execute(SkimmerImpactor impactor, CrystalImpactor crystalImpactee)
        {
            // Play at the skimmer's world position so FMOD 3D attenuation and
            // panning reflect where the skim actually happened - AI skims on
            // the far side of the map are quiet; nearby ones are loud.
            AudioSystem.Instance?.PlayGameplaySFX(category, impactor.transform.position);
        }
    }
}

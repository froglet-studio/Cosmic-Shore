using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(
        fileName = "SkimmerSFXByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Crystal/SkimmerSFXByCrystalEffectSO")]
    public class SkimmerSFXByCrystalEffectSO : SkimmerCrystalEffectSO
    {
        [Tooltip("SOAP gameplay SFX channel that AudioSystem listens on.")]
        [SerializeField] private ScriptableEventGameplaySFX gameplaySFXEvent;

        [Tooltip("Which gameplay SFX category to raise when the skimmer touches a crystal.")]
        [SerializeField] private GameplaySFXCategory category = GameplaySFXCategory.CrystalSkim;

        public override void Execute(SkimmerImpactor impactor, CrystalImpactor crystalImpactee)
        {
            gameplaySFXEvent.Raise(category);
        }
    }
}

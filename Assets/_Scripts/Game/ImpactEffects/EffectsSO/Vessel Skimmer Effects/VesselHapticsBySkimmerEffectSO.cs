using UnityEngine;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.EffectsSO.Helpers;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects.EffectsSO.VesselSkimmerEffects
{
    [CreateAssetMenu(fileName = "VesselHapticsBySkimmerEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/VesselHapticsBySkimmerEffectSO")]
    public class VesselHapticsBySkimmerEffectSO : VesselSkimmerEffectsSO
    {
        [SerializeField] HapticSpec _haptic;

        public override void Execute(VesselImpactor impactor, SkimmerImpactor impactee)
        {
            _haptic.PlayIfManual(impactor.Vessel.VesselStatus);
        }
    }
}
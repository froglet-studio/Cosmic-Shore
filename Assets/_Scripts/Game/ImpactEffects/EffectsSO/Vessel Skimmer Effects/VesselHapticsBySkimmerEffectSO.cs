using UnityEngine;
using CosmicShore.Game.ImpactEffects;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects
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
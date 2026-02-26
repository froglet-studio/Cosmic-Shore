using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
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
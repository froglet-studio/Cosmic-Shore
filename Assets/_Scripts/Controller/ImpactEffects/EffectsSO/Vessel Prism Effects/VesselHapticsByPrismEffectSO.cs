using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "VesselHapticsByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/VesselHapticsByPrismEffectSO")]
    public class VesselHapticsByPrismEffectSO : VesselPrismEffectSO
    {
        [SerializeField] HapticSpec _haptic;

        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            _haptic.PlayIfManual(vesselImpactor.Vessel.VesselStatus);
        }
    }
}
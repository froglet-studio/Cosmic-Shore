using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "VesselHapticsByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselHapticsByCrystalEffectSO")]
    public class VesselHapticsByCrystalEffectSO : VesselCrystalEffectSO
    {
        [SerializeField] HapticSpec _haptic;
        
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            _haptic.PlayIfManual(vesselImpactor.Vessel.VesselStatus);
        }
    }
}

using UnityEngine;
using CosmicShore.Game.Environment;
using CosmicShore.Game.ImpactEffects;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects
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

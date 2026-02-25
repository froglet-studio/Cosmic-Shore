using UnityEngine;
using CosmicShore.Game.Environment.FlowField;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.EffectsSO.Helpers;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects.EffectsSO.VesselCrystalEffects
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

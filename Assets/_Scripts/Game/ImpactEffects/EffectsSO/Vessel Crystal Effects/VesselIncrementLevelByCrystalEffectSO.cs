using UnityEngine;
using CosmicShore.Models.Enums;
using CosmicShore.Game.Environment.FlowField;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Game.Ship;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects.EffectsSO.VesselCrystalEffects
{
    [CreateAssetMenu(fileName = "VesselIncrementLevelByCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselIncrementLevelByCrystalEffectSO")]
    public class VesselIncrementLevelByCrystalEffectSO : VesselCrystalEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            vesselImpactor.Vessel.VesselStatus.ResourceSystem.IncrementLevel(data.Element);
        }
    }
}

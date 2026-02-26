using CosmicShore.Models.Enums;
using CosmicShore.Game.Environment;
using CosmicShore.Game.ImpactEffects;
using CosmicShore.Game.Ship;
using CosmicShore.Utility.Effects;
﻿using UnityEngine;

namespace CosmicShore.Game.ImpactEffects
{
    [CreateAssetMenu(fileName = "VesselAdjustLevelByCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselAdjustLevelByCrystalEffectSO")]
    public class VesselAdjustLevelByCrystalEffectSO : VesselCrystalEffectSO
    {
        [SerializeField] int LevelAdjustment;
        
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            vesselImpactor.Vessel.VesselStatus.ResourceSystem.AdjustLevel(data.Element, LevelAdjustment);
        }
    }
}

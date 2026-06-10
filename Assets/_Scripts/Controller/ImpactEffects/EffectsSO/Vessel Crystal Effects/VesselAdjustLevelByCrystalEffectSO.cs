using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
﻿using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "VesselAdjustLevelByCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselAdjustLevelByCrystalEffectSO")]
    public class VesselAdjustLevelByCrystalEffectSO : VesselCrystalEffectSO
    {
        [SerializeField] int LevelAdjustment;
        
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            vesselImpactor.Vessel.VesselStatus.ResourceSystem.AdjustLevel(data.Element, LevelAdjustment);

            if (vesselImpactor.Vessel.VesselStatus.IsLocalUser)
            {
                var category = data.Element switch
                {
                    Element.Charge => GameplaySFXCategory.ElementChargeReceived,
                    Element.Mass   => GameplaySFXCategory.ElementMassReceived,
                    Element.Space  => GameplaySFXCategory.ElementSpaceReceived,
                    Element.Time   => GameplaySFXCategory.ElementTimeReceived,
                    _              => (GameplaySFXCategory)(-1),
                };
                if ((int)category != -1)
                    AudioSystem.Instance?.PlayGameplaySFX(category);
            }
        }
    }
}

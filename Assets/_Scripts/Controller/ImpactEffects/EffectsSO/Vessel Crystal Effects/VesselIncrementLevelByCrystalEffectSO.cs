using CosmicShore.Core;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "VesselIncrementLevelByCrystalEffect", menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselIncrementLevelByCrystalEffectSO")]
    public class VesselIncrementLevelByCrystalEffectSO : VesselCrystalEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            vesselImpactor.Vessel.VesselStatus.ResourceSystem.IncrementLevel(data.Element);

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

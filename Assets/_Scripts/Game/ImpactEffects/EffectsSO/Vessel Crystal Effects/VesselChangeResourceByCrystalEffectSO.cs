using UnityEngine;
using CosmicShore.Game.Environment;
using CosmicShore.Game.ImpactEffects;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.Effects;
namespace CosmicShore.Game.ImpactEffects
{
    [CreateAssetMenu(
        fileName = "VesselChangeResourceByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselChangeResourceByCrystalEffectSO")]
    public class VesselChangeResourceByCrystalEffectSO : VesselCrystalEffectSO
    {
        [SerializeField] ResourceChangeSpec _change;
        
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var rs = vesselImpactor.Vessel.VesselStatus.ResourceSystem;
            _change.ApplyTo(rs, this);
        }
    }
}

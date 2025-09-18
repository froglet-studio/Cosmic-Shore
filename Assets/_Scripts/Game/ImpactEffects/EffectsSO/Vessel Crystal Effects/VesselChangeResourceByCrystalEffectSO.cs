using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "VesselChangeResourceByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselChangeResourceByCrystalEffectSO")]
    public class VesselChangeResourceByCrystalEffectSO : VesselCrystalEffectSO
    {
        [SerializeField] ResourceChangeSpec _change;

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactor crystalImpactee)
        {
            var rs = vesselImpactor.Vessel.VesselStatus.ResourceSystem;
            _change.ApplyTo(rs, this);
        }
    }
}

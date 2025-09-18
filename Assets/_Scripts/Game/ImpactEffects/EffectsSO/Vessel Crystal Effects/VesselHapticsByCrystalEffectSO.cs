using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselHapticsByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselHapticsByCrystalEffectSO")]
    public class VesselHapticsByCrystalEffectSO : VesselCrystalEffectSO
    {
        [SerializeField] HapticSpec _haptic;

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactor crystalImpactee)
        {
            _haptic.PlayIfManual(vesselImpactor.Vessel.VesselStatus);
        }
    }
}

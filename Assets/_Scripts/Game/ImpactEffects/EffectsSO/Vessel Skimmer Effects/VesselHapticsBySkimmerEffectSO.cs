using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselHapticsBySkimmerEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel/VesselHapticsBySkimmerEffectSO")]
    public class VesselHapticsBySkimmerEffectSO : VesselSkimmerEffectsSO
    {
        [SerializeField] HapticSpec _haptic;

        public override void Execute(VesselImpactor impactor, SkimmerImpactor impactee)
        {
            _haptic.PlayIfManual(impactor.Ship.ShipStatus);
        }
    }
}
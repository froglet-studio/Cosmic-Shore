using CosmicShore.Game.IO;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "VesselHapticsByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel/VesselHapticsByPrismEffectSO")]
    public class VesselHapticsByPrismEffectSO : VesselPrismEffectSO
    {
        [SerializeField] HapticSpec _haptic;

        public override void Execute(VesselImpactor vesselImpactor, PrismImpactor prismImpactee)
        {
            _haptic.PlayIfManual(vesselImpactor.Ship.ShipStatus);
        }
    }
}
using CosmicShore.Game.IO;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipHapticsByOtherEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipHapticsByOtherEffectSO")]
    public class ShipHapticsByOtherEffectSO : ShipOtherEffectSO
    {
        [SerializeField]
        HapticType _hapticType;

        public override void Execute(ShipImpactor shipImpactor, ImpactorBase impactee)
        {
            if (!shipImpactor.Ship.ShipStatus.AutoPilotEnabled)
                HapticController.PlayHaptic(_hapticType);
        }
    }
}

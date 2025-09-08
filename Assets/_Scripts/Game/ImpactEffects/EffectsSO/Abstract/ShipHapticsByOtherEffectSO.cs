using CosmicShore.Game.IO;
using UnityEngine;

namespace CosmicShore.Game
{
    // [CreateAssetMenu(fileName = "ShipHapticsByOtherEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/ShipHapticsByOtherEffectSO")]
    public abstract class ShipHapticsByOtherEffectSO : ImpactEffectSO
    {
        [SerializeField]
        HapticType _hapticType;

        public void Execute(ShipImpactor shipImpactor, ImpactorBase impactee)
        {
            if (!shipImpactor.Ship.ShipStatus.AutoPilotEnabled)
                HapticController.PlayHaptic(_hapticType);
        }
    }
}

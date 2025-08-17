using CosmicShore.Game.IO;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipHapticsByOtherEffect", menuName = "ScriptableObjects/Impact Effects/ShipHapticsByOtherEffectSO")]
    public class ShipHapticsByOtherEffectSO : ImpactEffectSO<ShipImpactor, ImpactorBase>
    {
        [SerializeField]
        HapticType _hapticType;

        protected override void ExecuteTyped(ShipImpactor shipImpactor, ImpactorBase impactee)
        {
            if (!shipImpactor.Ship.ShipStatus.AutoPilotEnabled)
                HapticController.PlayHaptic(_hapticType);
        }
    }
}

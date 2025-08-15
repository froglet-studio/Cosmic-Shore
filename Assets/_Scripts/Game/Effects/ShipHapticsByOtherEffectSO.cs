using CosmicShore.Game.IO;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipHapticsByOtherEffect", menuName = "ScriptableObjects/Impact Effects/ShipHapticsByOtherEffectSO")]
    public class ShipHapticsByOtherEffectSO : ImpactEffectSO<R_ShipImpactor, R_ImpactorBase>
    {
        [SerializeField]
        HapticType _hapticType;

        protected override void ExecuteTyped(R_ShipImpactor shipImpactor, R_ImpactorBase impactee)
        {
            if (!shipImpactor.Ship.ShipStatus.AutoPilotEnabled)
                HapticController.PlayHaptic(_hapticType);
        }
    }
}

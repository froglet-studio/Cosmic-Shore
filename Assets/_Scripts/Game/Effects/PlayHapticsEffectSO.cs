using CosmicShore.Game.IO;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "PlayHapticsImpactEffect", menuName = "ScriptableObjects/Impact Effects/PlayHapticsImpactEffectSO")]
    public class PlayHapticsEffectSO : ImpactEffectSO<R_ShipImpactor, R_ImpactorBase>
    {
        [SerializeField]
        HapticType _hapticType;

        protected override void ExecuteTyped(R_ShipImpactor shipImpactor, R_ImpactorBase impactee)
        {
            Debug.Log("Vessel encountered with the crystal");
            if (!shipImpactor.Ship.ShipStatus.AutoPilotEnabled)
                HapticController.PlayHaptic(_hapticType);
        }
    }
}

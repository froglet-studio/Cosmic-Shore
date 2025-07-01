using CosmicShore.Game.IO;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "PlayHapticsImpactEffect", menuName = "ScriptableObjects/Impact Effects/PlayHapticsImpactEffectSO")]
    public class PlayHapticsEffectSO : BaseImpactEffectSO
    {
        [SerializeField]
        HapticType _hapticType;

        public override void Execute(ImpactContext context)
        {
            if (!context.ShipStatus.AutoPilotEnabled)
                HapticController.PlayHaptic(_hapticType);
        }
    }
}

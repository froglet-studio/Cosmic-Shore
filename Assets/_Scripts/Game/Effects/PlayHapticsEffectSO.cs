using CosmicShore.Game.IO;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "PlayHapticsImpactEffect", menuName = "ScriptableObjects/Impact Effects/PlayHapticsImpactEffectSO")]
    public class PlayHapticsEffectSO : ImpactEffectSO
    {
        [SerializeField]
        HapticType _hapticType;

        /*public void Execute(ImpactEffectData data)
        {
            if (!data.ThisShipStatus.AutoPilotEnabled)
                HapticController.PlayHaptic(_hapticType);
        }*/
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}

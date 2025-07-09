using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "StopImpactEffect", menuName = "ScriptableObjects/Impact Effects/StopImpactEffectSO")]
    public class StopEffectSO : ImpactEffectSO, IStoppableImpactEffect
    {
        public void Execute(IStoppable stoppable)
        {
            if (stoppable == null)
            {
                Debug.LogWarning("StopEffectSO: Stoppable is null, cannot execute stop effect.");
                return;
            }

            stoppable.Stop();
        }
    }
}
